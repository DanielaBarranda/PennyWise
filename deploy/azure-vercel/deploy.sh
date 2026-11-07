#!/usr/bin/env bash
# Deploys PennyWise to the free hosting: the API to Azure App Service (F1) and the
# website to Vercel. This is separate from deploy/deploy.sh (AWS), which it never touches.
#
# Run from Git Bash in the repository root:
#   bash deploy/azure-vercel/deploy.sh            # backend, then frontend
#   bash deploy/azure-vercel/deploy.sh backend    # only the API     (Azure)
#   bash deploy/azure-vercel/deploy.sh frontend   # only the website (Vercel)
#
# Needs: the .NET 10 SDK, Python, the Vercel CLI (signed in) and the Azure CLI
# (signed in; set AZ if it is not on PATH). See deploy/azure-vercel/README.md.
set -euo pipefail
cd "$(dirname "$0")/../.."
TARGET="${1:-all}"
AZ="${AZ:-az}"
APP=pennywise-api-ph
GROUP=pennywise-rg
SITE=https://pennywise-one-opal.vercel.app
BUILD=build/azure

step() { printf '\n==> %s\n' "$1"; }

deploy_backend() {
  step "Publishing the API (framework-dependent: Azure provides .NET 10)"
  rm -rf "$BUILD"
  dotnet publish backend/PennyWise.Api -c Release -o "$BUILD/api" --nologo -v quiet
  python -I -c "
import os, sys, zipfile
src, out = sys.argv[1], sys.argv[2]
with zipfile.ZipFile(out, 'w', zipfile.ZIP_DEFLATED) as z:
    for root, _, files in os.walk(src):
        for f in files:
            p = os.path.join(root, f)
            z.write(p, os.path.relpath(p, src))
" "$BUILD/api" "$BUILD/api.zip"

  step "Uploading to Azure ($APP); the database in /home/data is not touched"
  # Kudu's zip deploy replaces only the app files in /home/site/wwwroot.
  local token
  token=$($AZ account get-access-token --resource https://management.azure.com/ --query accessToken -o tsv)
  curl -sf -X POST -H "Authorization: Bearer $token" --data-binary @"$BUILD/api.zip" \
    "https://$APP.scm.azurewebsites.net/api/zipdeploy?isAsync=false" -o /dev/null -w "Upload: HTTP %{http_code}\n"
  $AZ webapp restart -g "$GROUP" -n "$APP" --only-show-errors

  step "Waiting for the API to answer"
  for _ in $(seq 1 20); do
    if curl -sf -m 30 "https://$APP.azurewebsites.net/api/health" | grep -q '"ok"'; then echo "API health: ok"; return; fi
    sleep 10
  done
  echo "The API did not answer in time; check the Azure portal (Log stream)." >&2
  exit 1
}

deploy_frontend() {
  step "Building and deploying the website to Vercel"
  (cd frontend && vercel deploy --prod --yes >/dev/null)
  curl -sf -o /dev/null -w "Website: HTTP %{http_code}\n" "$SITE/"
  curl -sf -m 60 "$SITE/api/health" | grep -q '"ok"' && echo "API through Vercel: ok"
}

case "$TARGET" in
  backend) deploy_backend ;;
  frontend) deploy_frontend ;;
  all) deploy_backend; deploy_frontend ;;
  *) echo "Usage: bash deploy/azure-vercel/deploy.sh [backend|frontend]" >&2; exit 1 ;;
esac
