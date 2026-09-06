# 独立发布：单文件、自包含（无需目标机安装 .NET 运行时）
# IncludeNativeLibrariesForSelfExtract=true 把 WinForms 原生 DLL 一并嵌入，最终只有一个 EXE
# 输出目录：.\publish\AutoDisplayPower.exe
$ErrorActionPreference = 'Stop'
dotnet publish .\AutoDisplayPower.csproj -c Release -r win-x64 --self-contained true `
  -p:PublishSingleFile=true -p:EnableCompressionInSingleFile=true -p:DebugType=None `
  -p:IncludeNativeLibrariesForSelfExtract=true `
  -o .\publish
Write-Host ""
Write-Host "发布完成：" -NoNewline
Write-Host (Resolve-Path .\publish\AutoDisplayPower.exe).Path
