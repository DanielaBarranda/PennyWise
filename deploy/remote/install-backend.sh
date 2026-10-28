#!/usr/bin/env bash
# Runs ON EC2 #2 (backend) as root. deploy.sh uploads the API package and the
# systemd unit to /tmp and then pipes this script over SSH.
set -euo pipefail

APP=/opt/pennywise/api
DATA=/var/lib/pennywise
BACKUPS=$DATA/backups

# A dedicated system user with no login shell runs the API.
id pennywise &>/dev/null || useradd --system --no-create-home --shell /sbin/nologin pennywise
# The database folder lives outside the app folder, so redeploys never touch the data.
install -d -o pennywise -g pennywise -m 700 "$DATA" "$BACKUPS"

echo "Unpacking new API version..."
rm -rf "$APP.new"
mkdir -p "$APP.new"
tar -xzf /tmp/pennywise-api.tar.gz -C "$APP.new" --no-same-owner
chmod 755 "$APP.new/PennyWise.Api"

systemctl stop pennywise-api 2>/dev/null || true

# Back up the database while the API is stopped, keeping the 5 newest backups.
if [ -f "$DATA/pennywise.db" ]; then
  STAMP=$(date -u +%Y%m%d-%H%M%S)
  tar -czf "$BACKUPS/pennywise-$STAMP.tar.gz" -C "$DATA" $(cd "$DATA" && ls pennywise.db*)
  chown pennywise:pennywise "$BACKUPS/pennywise-$STAMP.tar.gz"
  ls -1t "$BACKUPS"/pennywise-*.tar.gz | tail -n +6 | xargs -r rm -f
  echo "Database backed up to $BACKUPS/pennywise-$STAMP.tar.gz"
fi

# Swap in the new version and keep the previous one for a quick manual rollback.
rm -rf "$APP.old"
if [ -d "$APP" ]; then mv "$APP" "$APP.old"; fi
mv "$APP.new" "$APP"

install -m 644 /tmp/pennywise-api.service /etc/systemd/system/pennywise-api.service
systemctl daemon-reload
systemctl enable --quiet pennywise-api
systemctl start pennywise-api
rm -f /tmp/pennywise-api.tar.gz /tmp/pennywise-api.service

echo "Waiting for the API to report healthy..."
for _ in $(seq 1 30); do
  if curl -fs http://localhost:5000/api/health; then
    echo
    echo "Backend OK: pennywise-api is $(systemctl is-active pennywise-api)"
    exit 0
  fi
  sleep 1
done

echo "ERROR: the API did not become healthy. Recent log:"
journalctl -u pennywise-api -n 40 --no-pager
exit 1
