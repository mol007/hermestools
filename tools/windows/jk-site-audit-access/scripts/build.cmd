@echo off
setlocal

set ROOT=%~dp0..\
set SRC=%ROOT%src
set DIST=%ROOT%dist

if not exist "%DIST%" mkdir "%DIST%"

cd /d "%SRC%"
if errorlevel 1 goto :err

echo [1/3] Restoring...
dotnet restore .\JKSiteAudit.csproj
if errorlevel 1 goto :err
dotnet restore .\JKSiteAuditRunner.csproj
if errorlevel 1 goto :err

echo [2/3] Building JKSiteAudit.exe...
dotnet publish .\JKSiteAudit.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if errorlevel 1 goto :err

copy /Y .\bin\Release\net8.0\win-x64\publish\JKSiteAudit.exe "%DIST%\JKSiteAudit.exe" >nul
if errorlevel 1 goto :err

echo [3/3] Building wrapper JKSiteAuditRunner.exe...
dotnet publish .\JKSiteAuditRunner.csproj -c Release -r win-x64 --self-contained true /p:PublishSingleFile=true
if errorlevel 1 goto :err

copy /Y .\bin\Release\net8.0\win-x64\publish\JKSiteAuditRunner.exe "%DIST%\JKSiteAuditRunner.exe" >nul
if errorlevel 1 goto :err

echo Build complete:
echo   %DIST%\JKSiteAudit.exe
echo   %DIST%\JKSiteAuditRunner.exe
exit /b 0

:err
echo Build failed.
exit /b 1
