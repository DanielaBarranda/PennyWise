# PennyWise: Deployment Verification (Milestone 7)

**Live site:** https://penny-wise.app (Elastic IP 13.229.192.174)
**Verified:** 28–29 October 2026, against the deployed AWS instances, not a local copy.
Sections 2–7 were first run over HTTP; section 8 repeats the key checks after HTTPS was added.
Every result below is real command output from those checks. The commands are included so
they can be run again during the demo.

## 1. AWS resources used

All resources are in `ap-southeast-1` (Singapore) and tagged `Project=PennyWise`. None are shared
with other projects.

| Resource | Name | Details |
|---|---|---|
| EC2 #1 (frontend) | `pennywise-frontend` | t4g.micro, Amazon Linux 2023 ARM64, Elastic IP 13.229.192.174, private IP 172.31.45.21 |
| EC2 #2 (backend) | `pennywise-backend` | t4g.micro, Amazon Linux 2023 ARM64, private IP 172.31.32.247, **no public IP** |
| Elastic IP | `pennywise-frontend-ip` | 13.229.192.174 |
| Security group | `pennywise-frontend-sg` | TCP 443 and 80 from anywhere (80 only redirects); TCP 22 from the owner's IP only |
| Security group | `pennywise-backend-sg` | TCP 5000 and 22 from `pennywise-frontend-sg` only |
| EBS volumes | `pennywise-frontend-disk`, `pennywise-backend-disk` | 8 GB gp3 each, encrypted |
| Key pair | `pennywise-key` | Private key kept only on the owner's PC |
| Domain (outside AWS) | `penny-wise.app` | Name.com, free via the GitHub Student Developer Pack; A records → 13.229.192.174 |
| TLS certificate (outside AWS) | Let's Encrypt | Free, auto-renewed on EC2 #1 |

Not used, on purpose: RDS, load balancers, NAT gateways. SQLite on EC2 #2 is enough for a
single-user app and keeps costs low.

## 2. The website and API work from the internet

```text
$ curl -s -o /dev/null -w "HTTP %{http_code}\n" http://13.229.192.174/
HTTP 200
$ curl -s http://13.229.192.174/api/health
{"status":"ok","database":"ok"}
```

The served page is the PennyWise React build (`<title>PennyWise — Budget Tracker</title>`),
not the Nginx default page.

## 3. EC2 #1 can talk to EC2 #2 over the private network

Run on EC2 #1. Nginx forwards `/api/` to `172.31.32.247:5000`:

```text
$ ssh -F deploy/ssh_config pennywise-frontend 'curl -s -w " [HTTP %{http_code}]\n" http://localhost/api/health'
{"status":"ok","database":"ok"} [HTTP 200]
```

Before the API was installed, EC2 #1 got `Connection refused` on 172.31.32.247:5000. "Refused"
(rather than a timeout) proves the security group allowed the traffic and only the program was
missing.

## 4. The backend is not reachable from the internet

```text
backend public IP: None
$ curl -m 5 http://13.229.192.174:5000/api/health
(timed out - port 5000 is not open on the frontend's public address)
```

The only ways in are through Nginx on EC2 #1 (port 5000) or SSH through EC2 #1 (port 22).
The backend also has no outbound internet access.

## 5. The real app works end to end (Browser → EC2 #1 → EC2 #2 → SQLite)

Run from a PC against the public address with temporary test records, which were deleted afterwards:

| Action | Request | Result |
|---|---|---|
| Create expense | `POST /api/expenses` | **201**, saved with id 1 |
| Invalid input (−5, "food", blank description) | `POST /api/expenses` | **400** with one message per field |
| Edit expense | `PUT /api/expenses/1` | **200**, amount 123.45 → 200.10 |
| Filter by month and category | `GET /api/expenses?month=2099-12&category=Others` | **200**, only the matching record |
| Dashboard totals | `GET /api/dashboard/summary?month=2099-12` | `totalSpent: 200.1`, `expenseCount: 1` |
| Delete | `DELETE /api/expenses/1` | **204**, then `GET` returns **404** |

The browser check of the live site (headless Edge loading the Dashboard and Expenses pages and
opening the add-expense form) recorded:

```text
200 /                                   200 /api/dashboard/summary?month=2026-10
200 /assets/index-CJpd13w9.css          200 /api/expenses?month=2026-10
200 /assets/index-CNhE-ZgR.js           200 /favicon.svg
add-expense validation messages: Amount is required. | Description is required.
No console errors, exceptions or failed requests.
```

Budget calculations (80% and 100% thresholds, category limits, exact decimal totals) are covered
by the 36 automated backend tests in `backend/PennyWise.Api.Tests` (36 passed).

## 6. Data survives restarts

| Test | Result |
|---|---|
| `sudo systemctl restart pennywise-api` on EC2 #2 | The record was still there after the restart |
| **Full reboot of both EC2 instances** (`aws ec2 reboot-instances`) | Both booted at 16:26:02 UTC, Nginx and the API **started automatically**, and the record was still there |

The database is stored at `/var/lib/pennywise/pennywise.db` on EC2 #2's EBS disk, outside the
application folder, so redeploying never replaces it. Each backend deploy first saves a backup
to `/var/lib/pennywise/backups/`.

## 7. Services and security settings

```text
EC2 #1 listening:  :80 nginx, :22 sshd
EC2 #2 listening:  :5000 PennyWise.Api, :22 sshd
nginx:          active / enabled at boot, config test successful
pennywise-api:  active / enabled at boot, runs as user "pennywise" (not root)
DB folder:      /var/lib/pennywise owned by pennywise, mode 700
SSH:            password login disabled, root login with password disabled
IMDSv2:         required on both instances
CPU credits:    standard on both (no surprise burst charges)
EBS:            encrypted on both
```

## 8. HTTPS with the penny-wise.app domain

```text
https://penny-wise.app/             -> HTTP 200, certificate trusted
https://penny-wise.app/api/health   -> HTTP 200 {"status":"ok","database":"ok"}

http://penny-wise.app/              -> 301 https://penny-wise.app/
http://www.penny-wise.app/          -> 301 https://penny-wise.app/
https://www.penny-wise.app/         -> 301 https://penny-wise.app/
http://13.229.192.174/              -> 301 https://penny-wise.app/
https://13.229.192.174/             -> refused during the TLS handshake (no certificate for a bare IP)

certificate:  CN=penny-wise.app, SAN penny-wise.app + www.penny-wise.app
issuer:       Let's Encrypt (YE1), expires 26 Jan 2027, renewed automatically
TLS 1.1: rejected   TLS 1.2: accepted   TLS 1.3: accepted
headers:      Strict-Transport-Security, X-Content-Type-Options, X-Frame-Options, Referrer-Policy

$ sudo certbot renew --dry-run
Congratulations, all simulated renewals succeeded
certbot-renew.timer: enabled / active
```

The browser check over HTTPS (Dashboard, Expenses and the add-expense form) recorded every
request as 200 and **no console errors, exceptions or failed requests**. EC2 #1 → EC2 #2 still
works both directly (`http://172.31.32.247:5000/api/health`) and through Nginx over HTTPS.

## 9. Known limitations

- **The domain is free for one year** (until 28 Oct 2027). Auto-renew is off, so the site would
  only be reachable by IP after that unless the domain is renewed (about $27/year).
- **No login.** PennyWise is a single-user app, so anyone with the address can see and change
  the data.
- **One server, one database file.** Fine for one user. Many users or several API servers would
  call for a managed database such as RDS.

## Run these checks again

```bash
curl https://penny-wise.app/api/health                                             # internet → API
ssh -F deploy/ssh_config pennywise-frontend 'curl -s http://172.31.32.247:5000/api/health'   # EC2 #1 → EC2 #2
ssh -F deploy/ssh_config pennywise-backend 'systemctl status pennywise-api --no-pager'
ssh -F deploy/ssh_config pennywise-backend 'sudo ls -la /var/lib/pennywise'
```
