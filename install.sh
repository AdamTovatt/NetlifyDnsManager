#!/bin/bash

# Variables
URL="https://github.com/AdamTovatt/NetlifyDnsManager/releases/download/v1.0.0/linux-arm64.zip"
DEST_DIR="/opt/netlify-dns-manager"
SERVICE_FILE="/etc/systemd/system/netlify-dns-manager.service"
ZIP_FILE="/tmp/netlify-dns-manager.zip"
USER_NAME=$(whoami)

# Download and unzip
curl -L "$URL" -o "$ZIP_FILE"
mkdir -p "$DEST_DIR"
unzip -o "$ZIP_FILE" -d "$DEST_DIR"
mv "$DEST_DIR/linux-arm64/NetlifyDnsManager" "$DEST_DIR/"
rm -r "$DEST_DIR/linux-arm64"
chmod +x "$DEST_DIR/NetlifyDnsManager"

# Create systemd service file
cat <<EOF > "$SERVICE_FILE"
[Unit]
Description=Netlify Dns Manager Background Service

[Service]
ExecStart=$DEST_DIR/NetlifyDnsManager
User=$USER_NAME
Restart=always
RestartSec=4

Environment=NETLIFY_ACCESS_TOKEN=[your-token]
Environment=DOMAIN_01=[your-first-domain]
Environment=CHECK_INTERVAL=[your-desired-check-interval-in-seconds]
Environment=ENABLE_LOGGING=[true/false-to-determine-if-it-should-log-all-information]

[Install]
WantedBy=multi-user.target
EOF
