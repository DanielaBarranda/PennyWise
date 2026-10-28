#!/usr/bin/env bash
# Builds PennyWise on your PC and deploys it to the two EC2 instances.
#
# Run from Git Bash in the repository root:
#   bash deploy/deploy.sh            # backend, then frontend
#   bash deploy/deploy.sh backend    # only the ASP.NET Core API  (EC2 #2)
#   bash deploy/deploy.sh frontend   # only the React website      (EC2 #1)
#
# Needs: Node.js, the .NET 10 SDK, and the private key at ~/.ssh/pennywise-key.pem.
set -euo pipefail
cd "$(dirname "$0")/.."

TARGET="${1:-all}"
BUILD=build
PUBLIC_URL="https://penny-wise.app"

# Prefer Windows' built-in OpenSSH (it respects the key file's Windows permissions).
if [ -x /c/Windows/System32/OpenSSH/ssh.exe ]; then
  SSH=/c/Windows/System32/OpenSSH/ssh.exe
  SCP=/c/Windows/System32/OpenSSH/scp.exe
  CFG="$(cygpath -m "$PWD/deploy/ssh_config")"
else
  SSH=ssh
  SCP=scp
  CFG="deploy/ssh_config"
fi
ssh_() { "$SSH" -F "$CFG" "$@"; }
scp_() { "$SCP" -F "$CFG" -q "$@"; }
step() { printf '\n==> %s\n' "$1"; }

mkdir -p "$BUILD"

deploy_backend() {
  step "Publishing the API for Linux ARM64 (self-contained: the server needs no .NET install)"
  rm -rf "$BUILD/api"
  dotnet publish backend/PennyWise.Api -c Release -r linux-arm64 --self-contained true \
    -o "$BUILD/api" --nologo -v quiet
  tar -czf "$BUILD/pennywise-api.tar.gz" -C "$BUILD/api" .
  echo "Package: $(du -h "$BUILD/pennywise-api.tar.gz" | cut -f1)"

  step "Uploading to EC2 #2 (through EC2 #1) and installing"
  scp_ "$BUILD/pennywise-api.tar.gz" deploy/systemd/pennywise-api.service pennywise-backend:/tmp/
  ssh_ pennywise-backend 'sudo bash -s' < deploy/remote/install-backend.sh
}

deploy_frontend() {
  step "Building the React website"
  if [ ! -d frontend/node_modules ]; then (cd frontend && npm ci --no-audit --no-fund); fi
  (cd frontend && npm run build)
  tar -czf "$BUILD/pennywise-web.tar.gz" -C frontend/dist .

  step "Uploading to EC2 #1 and installing"
  scp_ "$BUILD/pennywise-web.tar.gz" deploy/nginx/pennywise.conf pennywise-frontend:/tmp/
  ssh_ pennywise-frontend 'sudo bash -s' < deploy/remote/install-frontend.sh
}

case "$TARGET" in
  backend) deploy_backend ;;
  frontend) deploy_frontend ;;
  all) deploy_backend; deploy_frontend ;;
  *) echo "Usage: bash deploy/deploy.sh [all|backend|frontend]"; exit 1 ;;
esac

step "Checking from this PC over the internet"
echo -n "Website:      "; curl -s -o /dev/null -w "HTTP %{http_code}\n" "$PUBLIC_URL/"
echo -n "API health:   "; curl -s "$PUBLIC_URL/api/health"; echo
echo
echo "Done. Open $PUBLIC_URL"
