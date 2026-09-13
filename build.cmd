@echo off
chcp 65001 >nul
rem ============================================================
rem  AutoDisplayPower - one-click build script
rem
rem  Just double-click this file  ->  builds BOTH versions
rem
rem  Optional arguments:
rem     build.cmd small    only framework-dependent  (~240 KB)
rem     build.cmd full     only self-contained       (~68 MB)
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
echo ============================================================
echo   AutoDisplayPower  build
echo ============================================================
echo   .NET SDK version:
dotnet --version
echo.

set "MODE=%~1"
if /i "%MODE%"=="small" goto onlysmall
if /i "%MODE%"=="full"  goto onlyfull

set "DO_SMALL=1"
set "DO_FULL=1"
goto run

:onlysmall
set "DO_SMALL=1"
goto run

:onlyfull
set "DO_FULL=1"
goto run

:run
if not defined DO_SMALL goto skipSmall
echo ------------------------------------------------------------
echo   [1] framework-dependent  ^(about 240 KB^)
echo       - tiny, but target machine needs .NET 8 Desktop Runtime
echo ------------------------------------------------------------
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Small
if errorlevel 1 goto fail
set "OK_SMALL=1"
:skipSmall

if not defined DO_FULL goto skipFull
echo.
echo ------------------------------------------------------------
echo   [2] self-contained single file  ^(about 68 MB^)
echo       - runs on any Windows 11 x64, nothing else to install
echo ------------------------------------------------------------
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1"
if errorlevel 1 goto fail
set "OK_FULL=1"
:skipFull
goto done

:done
echo.
echo ============================================================
echo   BUILD SUCCEEDED
echo ============================================================
if defined OK_SMALL (
  for %%F in ("%~dp0publish-small\AutoDisplayPower.exe") do echo   [small] %%~fF   (%%~zF bytes^)
  echo           ^-^> use this on a machine that has .NET 8 Desktop Runtime
)
if defined OK_FULL (
  for %%F in ("%~dp0publish\AutoDisplayPower.exe") do echo   [full ] %%~fF   (%%~zF bytes^)
  echo           ^-^> self-contained: copy anywhere and double-click
)
echo.
echo   Tip: run it by double-clicking AutoDisplayPower.exe
echo        (right-click the tray icon for settings)
echo ============================================================
echo.
pause
exit /b 0

:fail
echo.
echo ============================================================
echo   BUILD FAILED - please copy the messages above and report
echo ============================================================
echo.
pause
exit /b 1
