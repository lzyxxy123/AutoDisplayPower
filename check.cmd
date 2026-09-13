@echo off
chcp 65001 >nul
rem AutoDisplayPower 自检：运行 --check 并显示报告内容
set EXE=%~dp0publish-small\AutoDisplayPower.exe
if not exist "%EXE%" set EXE=%~dp0publish\AutoDisplayPower.exe
if not exist "%EXE%" set EXE=%~dp0bin\Debug\net8.0-windows\AutoDisplayPower.exe
if not exist "%EXE%" (
  echo [ERROR] 找不到 AutoDisplayPower.exe，请先运行 publish.ps1 或 publish.ps1 -Small
  pause
  exit /b 1
)
echo 使用: %EXE%
"%EXE%" --check
timeout /t 2 >nul
set REPORT=%LOCALAPPDATA%\AutoDisplayPower\check-report.txt
if exist "%REPORT%" (
  echo.
  echo ================= 自检报告 =================
  type "%REPORT%"
) else (
  echo.
  echo [WARN] 未找到报告文件: %REPORT%
)
echo ============================================
pause
