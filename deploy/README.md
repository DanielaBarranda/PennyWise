# PennyWise deployment (AWS)

```
Browser ──HTTPS :443──▶ EC2 #1 pennywise-frontend   https://penny-wise.app → 13.229.192.174 (Elastic IP)
                        Nginx: TLS, React files + forwards /api/* ──┐
                                                               │ private network, port 5000
                      EC2 #2 pennywise-backend    172.31.32.247 (no public IP) ◀┘
                        systemd service pennywise-api (ASP.NET Core, .NET 10)
                        SQLite: /var/lib/pennywise/pennywise.db (on the EBS disk)
```

Region `ap-southeast-1` (Singapore). Both instances are `t4g.micro`, Amazon Linux 2023
(ARM64), 8 GB encrypted gp3 disks, and tagged `Project=PennyWise`.

| Security group | Inbound rules |
|---|---|
| `pennywise-frontend-sg` | TCP 443 and 80 from anywhere (80 only redirects to HTTPS); TCP 22 from the owner's IP only |
| `pennywise-backend-sg` | TCP 5000 and TCP 22 from `pennywise-frontend-sg` only |

## Files in this folder
| File | Goes to | Purpose |
|---|---|---|
| `deploy.sh` | runs on your PC | Builds both apps, uploads them, installs them, checks health |
| `nginx/pennywise.conf` | EC2 #1 `/etc/nginx/conf.d/` | HTTPS for penny-wise.app, HTTP→HTTPS redirect, serves React, proxies `/api/` to EC2 #2's private IP |
| `systemd/pennywise-api.service` | EC2 #2 `/etc/systemd/system/` | Runs the API as user `pennywise`, restarts it on crash and at boot |
| `remote/install-backend.sh` | runs on EC2 #2 | Backs up the DB, swaps in the new API, waits for `/api/health` |
| `remote/install-frontend.sh` | runs on EC2 #1 | Validates the Nginx config, swaps in the new site, reloads Nginx |
| `ssh_config` | your PC | SSH host names; the backend is reached *through* the frontend |

The private key lives only at `~/.ssh/pennywise-key.pem` on your PC. It is never committed.

## Deploy (from Git Bash, in the repository root)
```bash
bash deploy/deploy.sh            # API, then website
bash deploy/deploy.sh frontend   # website only
bash deploy/deploy.sh backend    # API only (backs up the database first)
```

## Everyday commands
```bash
ssh -F deploy/ssh_config pennywise-frontend                    # log in to EC2 #1
ssh -F deploy/ssh_config pennywise-backend                     # log in to EC2 #2 (via EC2 #1)

ssh -F deploy/ssh_config pennywise-backend 'systemctl status pennywise-api'
ssh -F deploy/ssh_config pennywise-backend 'sudo journalctl -u pennywise-api -n 50'
ssh -F deploy/ssh_config pennywise-backend 'sudo systemctl restart pennywise-api'
ssh -F deploy/ssh_config pennywise-frontend 'curl -s http://172.31.32.247:5000/api/health'   # EC2 #1 -> EC2 #2
```

## Save credits: stop the servers when not in use
Stopped instances cost about $5/month (disks + Elastic IP) instead of about $21/month.
The data and the website address stay the same.
```bash
aws ec2 stop-instances  --region ap-southeast-1 --instance-ids i-0b48c4651b306a9be i-0abf58fa6ba45b074
aws ec2 start-instances --region ap-southeast-1 --instance-ids i-0b48c4651b306a9be i-0abf58fa6ba45b074
```
After starting, wait about a minute, then open https://penny-wise.app.

## Domain and HTTPS certificate
- **Domain:** `penny-wise.app`, registered free for one year at Name.com through the GitHub
  Student Developer Pack (expires 28 Oct 2027, auto-renew **off**). Its DNS has two A records,
  `penny-wise.app` and `www.penny-wise.app`, both pointing to the Elastic IP 13.229.192.174.
- **Certificate:** free from Let's Encrypt, covering `penny-wise.app` and `www.penny-wise.app`.
  It is valid for 90 days and renewed automatically by `certbot-renew.timer` (twice a day it
  renews anything within 30 days of expiry, then reloads Nginx).
- `.app` domains only work over HTTPS in browsers, so the site cannot be served without the
  certificate.

Check renewal:
```bash
ssh -F deploy/ssh_config pennywise-frontend 'systemctl list-timers certbot-renew.timer; sudo certbot certificates'
ssh -F deploy/ssh_config pennywise-frontend 'sudo certbot renew --dry-run'   # waits up to 8 min on purpose
```

The certificate was created once on EC2 #1 (Nginx was already serving `/var/www/pennywise`,
with `.well-known` linked to `/var/www/letsencrypt` for the first check):
```bash
sudo dnf -y install certbot
sudo install -d -m 755 /var/www/letsencrypt/.well-known/acme-challenge
sudo ln -sfn /var/www/letsencrypt/.well-known /var/www/pennywise/.well-known
sudo certbot certonly --webroot -w /var/www/letsencrypt --cert-name penny-wise.app \
  -d penny-wise.app -d www.penny-wise.app --agree-tos --register-unsafely-without-email \
  --deploy-hook "systemctl reload nginx"
sudo systemctl enable --now certbot-renew.timer
```
After that, `bash deploy/deploy.sh frontend` installs the HTTPS Nginx config.

## Accounts and sign-in (production settings)
- **Session cookie** `pennywise_session`: HttpOnly, SameSite=Strict, Secure (HTTPS only in
  production). Nginx and the API share one origin (`https://penny-wise.app`), so no CORS is
  configured, on purpose.
- **Cookie keys** are stored in `/var/lib/pennywise/keys/` on EC2 #2 (set by
  `appsettings.Production.json`), so restarts and deploys do not sign everyone out.
  They are part of the data folder; keep them private.
- **Client IP for rate limiting:** the API trusts `X-Forwarded-For` only from the frontend's
  private IP (`TrustedProxies` in `appsettings.Production.json`).
- **Private settings** go in `/etc/pennywise/api.env` on EC2 #2 (never in Git), one
  `KEY=value` per line, read by the systemd service. Currently used for:
  ```
  PennyWise__LegacyOwnerEmail=owner@example.com
  ```
  Budgets and expenses created before accounts existed have no owner and are hidden from
  everyone. The first time an account with this email registers or logs in, they are
  assigned to it. Without the setting nothing is assigned; nothing is ever deleted.
- **Password reset by email is not available**: the servers cannot send email (that would
  need Amazon SES or another mail service).

## Backups
Every backend deploy saves the database to `/var/lib/pennywise/backups/` (5 newest kept).
To copy the live database to your PC:
```bash
ssh -F deploy/ssh_config pennywise-backend 'sudo tar -czf - -C /var/lib/pennywise pennywise.db' > pennywise-backup.tar.gz
```

## If your SSH stops working
The frontend only accepts SSH from the IP address the owner had when it was set up.
If your internet provider gives you a new IP, the website keeps working but SSH times out;
the `pennywise-frontend-sg` rule for port 22 must be updated to the new IP.
