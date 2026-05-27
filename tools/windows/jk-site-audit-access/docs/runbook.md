# Runbook: JKSiteAudit

1) Build on a Windows build machine
- Install .NET 8 SDK
- Open PowerShell
- cd tools/windows/jk-site-audit-access
- ./scripts/build.ps1

2) Transfer dist/JKSiteAudit.exe to target endpoint.

3) Run as Administrator.

4) Collect output from:
- %PROGRAMDATA%\JKCyber\SiteAudit\

5) Upload JSON/TXT outputs back to ticket.

## What to look for
- open_ports: unexpected listening services
- smb_shares: admin shares plus any broad-access shares
- dns_client_servers: wrong DNS servers or public DNS on domain devices
- firewall_profiles: disabled inbound protections
- network_profiles: Public profile on domain-joined assets

## Scope and safety
- read-only
- no changes
- no credential harvest
- no persistence
