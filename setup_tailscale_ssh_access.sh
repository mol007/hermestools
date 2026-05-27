#!/usr/bin/env bash
set -euo pipefail

# Safe setup: adds YOUR public key for jkadmin so you can SSH over Tailscale.
# Usage:
#   sudo ./setup_tailscale_ssh_access.sh "ssh-ed25519 AAAA... your_laptop"

if [[ ${EUID:-$(id -u)} -ne 0 ]]; then
  echo "Run as root: sudo $0 '<public-key>'"
  exit 1
fi

if [[ $# -ne 1 ]]; then
  echo "Usage: sudo $0 '<ssh-public-key>'"
  exit 1
fi

PUBKEY="$1"
USER_NAME="jkadmin"
USER_HOME="/home/${USER_NAME}"
SSH_DIR="${USER_HOME}/.ssh"
AUTH_KEYS="${SSH_DIR}/authorized_keys"

# Basic key format guard
if ! grep -Eq '^(ssh-ed25519|ssh-rsa|ecdsa-sha2-nistp256)\s+[A-Za-z0-9+/=]+(\s+.*)?$' <<<"$PUBKEY"; then
  echo "Invalid public key format."
  exit 1
fi

# Ensure user exists
id "$USER_NAME" >/dev/null 2>&1 || { echo "User $USER_NAME not found"; exit 1; }

install -d -m 700 -o "$USER_NAME" -g "$USER_NAME" "$SSH_DIR"
touch "$AUTH_KEYS"
chown "$USER_NAME":"$USER_NAME" "$AUTH_KEYS"
chmod 600 "$AUTH_KEYS"

if grep -Fqx "$PUBKEY" "$AUTH_KEYS"; then
  echo "Key already present in $AUTH_KEYS"
else
  echo "$PUBKEY" >> "$AUTH_KEYS"
  echo "Key added to $AUTH_KEYS"
fi

# Keep SSH password auth enabled for now (safe rollout). You can disable later after test.
if systemctl is-active --quiet ssh; then
  echo "ssh service is active"
else
  systemctl enable --now ssh
  echo "ssh service started"
fi

TS_IP=$(tailscale ip -4 2>/dev/null || true)
if [[ -n "$TS_IP" ]]; then
  echo "Tailscale IP: $TS_IP"
  echo "Test from your laptop: ssh ${USER_NAME}@${TS_IP}"
else
  echo "Tailscale IP not found. Ensure: sudo tailscale up --ssh"
fi

echo "Done."
