@echo off
chcp 65001 >nul
rem ============================================================
rem  AutoDisplayPower - one-click build script
rem  Usage:
rem     build.cmd          -> framework-dependent (~0.2MB, needs .NET 8 Desktop Runtime)
rem     build.cmd full     -> self-contained single file (~68MB, needs nothing)
rem ============================================================
setlocal
cd /d "%~dp0"

where dotnet >nul 2>nul
if errorlevel 1 (
  echo.
  echo [ERROR] .NET SDK not found.
  echo Please install .NET 8.0 SDK first:
  echo     https://dotnet.microsoft.com/download/dotnet/8.0
  echo.
  pause
  exit /b 1
)

echo.
echo Detected .NET SDK version:
dotnet --version
echo.

if /i "%~1"=="full" goto full

echo == Building framework-dependent version (about 0.2MB) ==
echo    Target machine must have .NET 8 Desktop Runtime installed.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Small
if errorlevel 1 goto fail
echo.
echo DONE. Output file:
echo    %~dp0publish-small\AutoDisplayPower.exe
echo.
pause
exit /b 0

:full
echo == Building self-contained single-file version (about 68MB) ==
echo    Runs on any Windows 11 x64 without installing .NET.
echo.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1"
if errorlevel 1 goto fail
echo.
echo DONE. Output file:
echo    %~dp0publish\AutoDisplayPower.exe
echo.
pause
exit /b 0

:fail
echo.
echo [ERROR] Build failed. Please copy the messages above and report.
echo.
pause
exit /b 1
