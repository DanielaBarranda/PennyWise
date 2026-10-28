#!/usr/bin/env bash
# Runs ON EC2 #1 (frontend) as root. deploy.sh uploads the React build and the
# Nginx site config to /tmp and then pipes this script over SSH.
set -euo pipefail

WEB=/var/www/pennywise
CONF=/etc/nginx/conf.d/pennywise.conf
CERT=/etc/letsencrypt/live/penny-wise.app/fullchain.pem

# The HTTPS config needs the Let's Encrypt certificate. It is created once by hand
# (see deploy/README.md, "HTTPS certificate"), then renewed automatically.
if [ ! -f "$CERT" ]; then
  echo "ERROR: no certificate at $CERT - follow 'HTTPS certificate' in deploy/README.md first."
  exit 1
fi
install -d -m 755 /var/www/letsencrypt

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

# --resolve sends the request to this server while still using the real name,
# so the certificate is checked exactly as a browser would check it.
LOCAL=(--resolve penny-wise.app:443:127.0.0.1)
echo -n "Website (local HTTPS):    "; curl -fs "${LOCAL[@]}" -o /dev/null -w "HTTP %{http_code}\n" https://penny-wise.app/
echo -n "API via Nginx -> EC2 #2:  "
if ! curl -fs "${LOCAL[@]}" https://penny-wise.app/api/health; then
  echo "FAILED - Nginx could not reach the API on EC2 #2"
  exit 1
fi
echo
echo "Frontend OK"
