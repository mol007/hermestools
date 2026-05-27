$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $PSScriptRoot
$src = Join-Path $root "src"
$dist = Join-Path $root "dist"

if (!(Test-Path $dist)) { New-Item -ItemType Directory -Path $dist | Out-Null }

Push-Location $src
try {
    dotnet restore
    dotnet publish .\JKSiteAudit.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true

    $published = Join-Path $src "bin\Release\net8.0\win-x64\publish\JKSiteAudit.exe"
    if (!(Test-Path $published)) { throw "Build succeeded but EXE not found: $published" }

    Copy-Item $published (Join-Path $dist "JKSiteAudit.exe") -Force
    Write-Host "Build complete: $dist\JKSiteAudit.exe"
}
finally {
    Pop-Location
}
