# Free hosting: Vercel + Azure App Service (F1)

A second home for PennyWise that costs nothing, for when the AWS servers are stopped.
It is completely separate from the AWS deployment (`deploy/deploy.sh`, Nginx, the EC2
instances): nothing here changes them, and penny-wise.app still points to AWS.

| Part | Where | Address |
|---|---|---|
| Website (React) | Vercel, Hobby plan (free) | https://penny-wise-app.vercel.app |
| API + SQLite database | Azure App Service, F1 Linux plan (free), East Asia | https://pennywise-api-ph.azurewebsites.net |

Vercel forwards every `/api/*` request to Azure (`frontend/vercel.json`), so the browser
sees a single site: the session cookie, `SameSite=Strict` and the CSRF header work as they
do behind Nginx. `vercel.json` also sets the same security headers and caching as the
Nginx config.

## The two databases

The Azure copy of the database started as a copy of the AWS one (8 Nov 2026), together
with the AWS Data Protection key, so existing accounts and two-step verification keep
working. From then on the two databases are **separate**: changes made
on one site do not appear on the other. Use one at a time, and copy the database across
when switching back (stop the API first, copy `pennywise.db` and the `keys` folder).

On Azure they live in persistent storage:

- `/home/data/pennywise.db`
- `/home/data/keys/` (Data Protection keys: these are secrets, never commit them)

## Azure settings (App Service → Configuration)

| Setting | Value | Why |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Production behaviour (secure cookies, etc.) |
| `ConnectionStrings__PennyWise` | `Data Source=/home/data/pennywise.db` | Database in persistent storage |
| `PennyWise__KeysPath` | `/home/data/keys` | Keeps sign-ins valid across restarts |
| `PennyWise__TrustedProxies` | *(empty)* | Azure's front end, not Nginx, adds X-Forwarded-* |
| `PennyWise__ForwardLimit` | `2` | Visitor IP is two proxies back (Vercel, then Azure) |
| `PennyWise__GoogleCertsUrl` | `https://www.googleapis.com/oauth2/v3/certs` | Azure can reach Google directly |
| `ASPNETCORE_FORWARDEDHEADERS_ENABLED` | `false` | Only the app's own forwarded-headers setup runs |

Startup command: `dotnet PennyWise.Api.dll`.

## Deploying changes

```bash
bash deploy/azure-vercel/deploy.sh            # API, then website
bash deploy/azure-vercel/deploy.sh frontend   # website only
```

Needs the Vercel CLI (`vercel login`) and the Azure CLI signed in. The Azure for Students
subscription requires multi-factor sign-in for changes; if the CLI says so, run the
`az login ... --claims-challenge ...` command it prints.

## Limits of the free tiers

- F1: 60 CPU minutes a day and 1 GB of storage. The app sleeps when idle, so the first
  request after a while takes 10–20 seconds.
- Vercel Hobby: personal, non-commercial use.
- Google sign-in needs `https://penny-wise-app.vercel.app` in the OAuth client's
  "Authorized JavaScript origins" (Google Cloud Console → APIs & Services → Credentials).
