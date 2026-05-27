$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src"
$dist = Join-Path $root "dist"

if (!(Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }

Push-Location $src
try {
    dotnet restore .\JKSiteAudit.csproj
    dotnet restore .\JKSiteAuditRunner.csproj

    dotnet publish .\JKSiteAudit.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
    $auditExe = Join-Path $src "bin\Release\net8.0\win-x64\publish\JKSiteAudit.exe"
    if (!(Test-Path $auditExe)) { throw "Build succeeded but EXE not found: $auditExe" }
    Copy-Item $auditExe (Join-Path $dist "JKSiteAudit.exe") -Force

    dotnet publish .\JKSiteAuditRunner.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
    $runnerExe = Join-Path $src "bin\Release\net8.0\win-x64\publish\JKSiteAuditRunner.exe"
    if (!(Test-Path $runnerExe)) { throw "Build succeeded but EXE not found: $runnerExe" }
    Copy-Item $runnerExe (Join-Path $dist "JKSiteAuditRunner.exe") -Force

    Write-Host "Build complete:"
    Write-Host "  $dist\JKSiteAudit.exe"
    Write-Host "  $dist\JKSiteAuditRunner.exe"
}
finally {
    Pop-Location
}
