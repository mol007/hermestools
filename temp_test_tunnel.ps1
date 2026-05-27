param(
  [Parameter(Mandatory=$true)] [string]$Server,
  [Parameter(Mandatory=$true)] [string]$User,
  [Parameter(Mandatory=$true)] [string]$KeyPath,
  [int]$RemotePort = 2222,
  [int]$LocalPort = 22
)

$ErrorActionPreference = 'Stop'

if (!(Test-Path $KeyPath)) {
  throw "Key file not found: $KeyPath"
}

Write-Host "Starting temporary SSH reverse tunnel..."
Write-Host "Server: $User@$Server"
Write-Host "Remote port: $RemotePort -> localhost:$LocalPort"
Write-Host "Close this window (or Ctrl+C) to drop the connection."

$sshArgs = @(
  '-N',
  '-o','ExitOnForwardFailure=yes',
  '-o','ServerAliveInterval=30',
  '-o','ServerAliveCountMax=2',
  '-i', $KeyPath,
  '-R', "${RemotePort}:localhost:${LocalPort}",
  "${User}@${Server}"
)

& ssh @sshArgs

Write-Host "Tunnel closed. Connection dropped."
