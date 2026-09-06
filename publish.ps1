# AutoDisplayPower 发布脚本
# 用法：
#   .\publish.ps1            自包含单文件（约 68MB，无需目标机安装 .NET 运行时，适合分发）
#   .\publish.ps1 -Small     框架依赖单文件（约 0.2MB，目标机需已装 .NET 8 Desktop 运行时；本机已装 8.0.30）
param(
    [switch]$Small
)
$ErrorActionPreference = 'Stop'

if ($Small) {
    Write-Host "== 发布【框架依赖版】(体积极小，约0.2MB) =="
    dotnet publish .\AutoDisplayPower.csproj -c Release -r win-x64 --self-contained false `
      -p:PublishSingleFile=true -p:DebugType=None -o .\publish-small
    Write-Host ""
    Write-Host "输出：" -NoNewline
    Write-Host (Resolve-Path .\publish-small\AutoDisplayPower.exe).Path
    Write-Host "注意：目标机需已安装 .NET 8 Desktop Runtime（本机已装 Microsoft.WindowsDesktop.App 8.0.30）。"
} else {
    Write-Host "== 发布【自包含单文件】(约68MB，无需运行时) =="
    dotnet publish .\AutoDisplayPower.csproj -c Release -r win-x64 --self-contained true `
      -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None `
      -p:IncludeNativeLibrariesForSelfExtract=true -o .\publish
    Write-Host ""
    Write-Host "输出：" -NoNewline
    Write-Host (Resolve-Path .\publish\AutoDisplayPower.exe).Path
}
