# PennyWise deployment (AWS)

```
Browser ──HTTP :80──▶ EC2 #1 pennywise-frontend   13.229.192.174 (Elastic IP)
                        Nginx: React files + forwards /api/* ──┐
                                                               │ private network, port 5000
                      EC2 #2 pennywise-backend    172.31.32.247 (no public IP) ◀┘
                        systemd service pennywise-api (ASP.NET Core, .NET 10)
                        SQLite: /var/lib/pennywise/pennywise.db (on the EBS disk)
```

Region `ap-southeast-1` (Singapore). Both instances are `t4g.micro`, Amazon Linux 2023
(ARM64), 8 GB encrypted gp3 disks, and tagged `Project=PennyWise`.

| Security group | Inbound rules |
|---|---|
| `pennywise-frontend-sg` | TCP 80 from anywhere; TCP 22 from the owner's IP only |
| `pennywise-backend-sg` | TCP 5000 and TCP 22 from `pennywise-frontend-sg` only |

## Files in this folder
| File | Goes to | Purpose |
|---|---|---|
| `deploy.sh` | runs on your PC | Builds both apps, uploads them, installs them, checks health |
| `nginx/pennywise.conf` | EC2 #1 `/etc/nginx/conf.d/` | Serves React; proxies `/api/` to EC2 #2's private IP |
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
ssh -F deploy/ssh_config pennywise-frontend 'curl -s localhost/api/health'   # EC2 #1 -> EC2 #2
```

## Save credits: stop the servers when not in use
Stopped instances cost about $5/month (disks + Elastic IP) instead of about $21/month.
The data and the website address stay the same.
```bash
aws ec2 stop-instances  --region ap-southeast-1 --instance-ids i-0b48c4651b306a9be i-0abf58fa6ba45b074
aws ec2 start-instances --region ap-southeast-1 --instance-ids i-0b48c4651b306a9be i-0abf58fa6ba45b074
```
After starting, wait about a minute, then open http://13.229.192.174.

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
