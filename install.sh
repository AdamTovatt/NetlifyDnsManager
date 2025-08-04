#!/bin/bash
set -e

# Determine architecture
ARCH=$(uname -m)
if [[ "$ARCH" == "aarch64" ]]; then
    ZIP_NAME="linux-arm64.zip"
elif [[ "$ARCH" == "armv7l" ]]; then
    ZIP_NAME="linux-arm.zip"
else
    echo "Unsupported architecture: $ARCH"
    exit 1
fi

# Variables
URL="https://github.com/AdamTovatt/NetlifyDnsManager/releases/download/v1.0.0/$ZIP_NAME"
DEST_DIR="/opt/netlify-dns-manager"
SERVICE_FILE="/etc/systemd/system/netlify-dns-manager.service"
ZIP_FILE="/tmp/netlify-dns-manager.zip"
USER_NAME=$(whoami)

# Download and unzip
curl -L "$URL" -o "$ZIP_FILE"
mkdir -p "$DEST_DIR"
unzip -o "$ZIP_FILE" -d "$DEST_DIR"
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

echo ""
echo "Installation completed."
echo "To configure the service, edit the environment variables in:"
echo "   $SERVICE_FILE"
echo "Then run:"
echo "   sudo systemctl daemon-reload"
echo "   sudo systemctl enable --now netlify-dns-manager.service"
