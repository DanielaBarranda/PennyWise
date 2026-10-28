#!/usr/bin/env bash
# Runs ON EC2 #1 (frontend) as root. deploy.sh uploads the React build and the
# Nginx site config to /tmp and then pipes this script over SSH.
set -euo pipefail

WEB=/var/www/pennywise
CONF=/etc/nginx/conf.d/pennywise.conf

echo "Unpacking new website version..."
rm -rf "$WEB.new"
mkdir -p "$WEB.new"
tar -xzf /tmp/pennywise-web.tar.gz -C "$WEB.new" --no-same-owner
chmod -R a+rX "$WEB.new"

# Install the Nginx config, but roll back if Nginx says it is invalid.
if [ -f "$CONF" ]; then cp "$CONF" "$CONF.bak"; fi
install -m 644 /tmp/pennywise.conf "$CONF"
if ! nginx -t 2>/tmp/nginx-test.log; then
  cat /tmp/nginx-test.log
  if [ -f "$CONF.bak" ]; then mv "$CONF.bak" "$CONF"; else rm -f "$CONF"; fi
  echo "ERROR: invalid Nginx config; the previous config was kept."
  exit 1
fi
rm -f "$CONF.bak"

# Swap in the new site and keep the previous one for a quick manual rollback.
rm -rf "$WEB.old"
if [ -d "$WEB" ]; then mv "$WEB" "$WEB.old"; fi
mv "$WEB.new" "$WEB"

systemctl enable --quiet nginx
systemctl reload nginx || systemctl start nginx
rm -f /tmp/pennywise-web.tar.gz /tmp/pennywise.conf
sleep 1 # "reload" returns before the new Nginx workers take over

echo -n "Website (local):          "; curl -fs -o /dev/null -w "HTTP %{http_code}\n" http://localhost/
echo -n "API via Nginx -> EC2 #2:  "
if ! curl -fs http://localhost/api/health; then
  echo "FAILED - Nginx could not reach the API on EC2 #2"
  exit 1
fi
echo
echo "Frontend OK"
