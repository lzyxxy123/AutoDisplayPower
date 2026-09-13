# AutoDisplayPower 发布脚本
# 用法：
#   .\publish.ps1            自包含单文件（约 68MB，无需目标机安装 .NET 运行时，适合分发）
#   .\publish.ps1 -Small     框架依赖单文件（约 0.2MB，目标机需已装 .NET 8 Desktop 运行时；本机已装 8.0.30）
param(
    [switch]$Small
)
$ErrorActionPreference = 'Stop'

# 程序正在运行会锁定输出 exe，先结束它
$running = Get-Process -Name 'AutoDisplayPower' -ErrorAction SilentlyContinue
if ($running) {
    Write-Host "检测到 AutoDisplayPower 正在运行，先结束它（否则无法覆盖 exe）……" -ForegroundColor Yellow
    $running | Stop-Process -Force -ErrorAction SilentlyContinue
    Start-Sleep -Milliseconds 600
}

if ($Small) {
    Write-Host "== 发布【框架依赖版】(体积极小，约0.2MB) =="
    dotnet publish .\AutoDisplayPower.csproj -c Release -r win-x64 --self-contained false `
      -p:PublishSingleFile=true -p:DebugType=None -o .\publish-small
    $out = '.\publish-small\AutoDisplayPower.exe'
} else {
    Write-Host "== 发布【自包含单文件】(约68MB，无需运行时) =="
    dotnet publish .\AutoDisplayPower.csproj -c Release -r win-x64 --self-contained true `
      -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None `
      -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
    $out = '.\publish\AutoDisplayPower.exe'
}

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "发布失败（dotnet 退出码 $LASTEXITCODE）" -ForegroundColor Red
    exit 1
}

if (-not (Test-Path $out)) {
    Write-Host ""
    Write-Host "发布失败：未生成 $out" -ForegroundColor Red
    exit 1
}

$size = [math]::Round((Get-Item $out).Length / 1KB, 1)
Write-Host ""
Write-Host "发布成功：" -NoNewline -ForegroundColor Green
Write-Host (Resolve-Path $out).Path
Write-Host "文件大小：$size KB"
if ($Small) {
    Write-Host "注意：目标机需已安装 .NET 8 Desktop Runtime（本机已装 Microsoft.WindowsDesktop.App 8.0.30）。"
}
exit 0
