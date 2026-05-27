# jk-site-audit-access

Purpose: safe Windows endpoint audit launcher for JK Cyber Consultants.

What it does:
- Runs read-only checks for:
  - open listening ports
  - SMB share exposure
  - DNS configuration and DNS resolution
  - firewall profile state
  - basic host/network identity
- Exports results to JSON and TXT under `%PROGRAMDATA%\JKCyber\SiteAudit\`.
- Does NOT change system settings.
- Does NOT open backdoors, add users, or disable security.

Folder layout:
- `src/` C# source for a small console EXE.
- `scripts/` build helper scripts.
- `dist/` output EXE location (after build).
- `docs/` runbook and interpretation notes.

## Build on Windows (recommended)

1) Install .NET SDK 8.0+ on a Windows build machine.
2) Open PowerShell in this folder.
3) Run:

```powershell
./scripts/build.ps1
```

4) EXE output:

`dist\JKSiteAudit.exe`

## Run on target PC

Run as Administrator (recommended for full visibility):

```powershell
.\JKSiteAudit.exe
```

Output files:
- `%PROGRAMDATA%\JKCyber\SiteAudit\audit-<hostname>-<timestamp>.json`
- `%PROGRAMDATA%\JKCyber\SiteAudit\audit-<hostname>-<timestamp>.txt`

## Security notes
- Read-only assessment only.
- No credential collection.
- No remote command execution.
- Designed for "No ticket = no work" traceability.
