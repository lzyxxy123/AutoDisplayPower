@echo off
chcp 65001 >nul
rem === AutoDisplayPower self-check launcher (ASCII only on purpose) ===
set "EXE=%~dp0publish-small\AutoDisplayPower.exe"
if not exist "%EXE%" set "EXE=%~dp0publish\AutoDisplayPower.exe"
if not exist "%EXE%" set "EXE=%~dp0bin\Debug\net8.0-windows\AutoDisplayPower.exe"
if not exist "%EXE%" (
  echo [ERROR] AutoDisplayPower.exe not found. Run publish.ps1 first.
  pause
  exit /b 1
)
echo Using: %EXE%
"%EXE%" --check
timeout /t 2 >nul
set "REPORT=%LOCALAPPDATA%\AutoDisplayPower\check-report.txt"
if exist "%REPORT%" (
  echo.
  echo ============== CHECK REPORT ==============
  type "%REPORT%"
) else (
  echo.
  echo [WARN] report not found: %REPORT%
)
echo =========================================
pause
