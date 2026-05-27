# Runbook: JKSiteAudit

1) Build on a Windows build machine
- Install .NET 8 SDK
- Open PowerShell
- cd tools/windows/jk-site-audit-access
- If blocked by execution policy:
  - powershell -ExecutionPolicy Bypass -File .\scripts\build.ps1
- Or use normal command wrapper (no PowerShell script policy issue):
  - .\scripts\build.cmd
- Or:
  - Set-ExecutionPolicy -Scope Process -ExecutionPolicy Bypass
  - .\scripts\build.ps1

2) Transfer dist/JKSiteAudit.exe to target endpoint.

3) Run as Administrator.
- Basic run:
  - .\JKSiteAudit.exe
- Domain expected check:
  - .\JKSiteAudit.exe --domain yourdomain.local

4) Collect output from:
- %PROGRAMDATA%\JKCyber\SiteAudit\

5) Upload JSON/TXT outputs back to ticket.

## What to look for
- RDP listening (3389)
- non-default SMB shares
- DNS servers that are not internal/private
- firewall profile disabled
- firewall default inbound set to ALLOW
- public network profile on managed endpoint
- domain mismatch (if --domain provided)

## Scope and safety
- read-only
- no changes
- no credential harvest
- no persistence
- high-impact remediation still requires human approval
