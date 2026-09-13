param([string]$State = "")

# ============================================================
#  AutoDisplayPower 盖子检测对比测试脚本（只读，不修改任何设置）
#  用法：
#     .\lidtest.cmd open      （开盖时运行）
#     .\lidtest.cmd closed    （合盖时运行）
#   两次输出会保存为 lidtest-open.txt / lidtest-closed.txt，发我即可
# ============================================================
$ErrorActionPreference = 'SilentlyContinue'

if ([string]::IsNullOrWhiteSpace($State)) { $State = Read-Host "请输入当前盖子状态（open=开盖 / closed=合盖）" }
$State = ([string]$State).Trim().ToLower()
if ([string]::IsNullOrWhiteSpace($State)) { $State = "unknown" }

$out = New-Object System.Collections.Generic.List[string]
function Add-Line([string]$t) { $out.Add($t); Write-Host $t }

Write-Host "===================================================" -ForegroundColor Cyan
Add-Line "===== 盖子状态: $State ====="
Add-Line ("时间: " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
Add-Line ""

# ---------- 1) 注册表枚举的显示设备 ----------
Add-Line "--- 1) DISPLAY 枚举设备（注册表）---"
$dispRoot = 'HKLM:\SYSTEM\CurrentControlSet\Enum\DISPLAY'
$models = @()
try { $models = Get-ChildItem $dispRoot -ErrorAction Stop | Select-Object -ExpandProperty PSChildName } catch { }
if ($models.Count -eq 0) {
    Add-Line "  (读取失败或无权限)"
} else {
    Add-Line ("  设备型号列表: " + ($models -join ', '))
    Add-Line ("  内屏 SDC4203 键存在? " + (Test-Path "$dispRoot\SDC4203"))
    foreach ($m in $models) {
        $insts = @(Get-ChildItem "$dispRoot\$m" -ErrorAction SilentlyContinue)
        Add-Line "  [$m]  实例数=$($insts.Count)"
        $inst = $insts | Select-Object -First 1
        if ($inst) {
            $p = Get-ItemProperty $inst.PSPath -ErrorAction SilentlyContinue
            Add-Line "      FriendlyName = $($p.FriendlyName)"
            Add-Line "      ConfigFlags  = $($p.ConfigFlags)   Capabilities = $($p.Capabilities)   Address = $($p.Address)"
        }
    }
}
Add-Line ""

# ---------- 2) WMI: Win32_DesktopMonitor ----------
Add-Line "--- 2) Win32_DesktopMonitor（WMI，含电源状态 Availability）---"
try {
    $dm = @(Get-CimInstance -ClassName Win32_DesktopMonitor -ErrorAction Stop)
    if ($dm.Count -eq 0) { Add-Line "  (无结果)" }
    foreach ($d in $dm) {
        Add-Line "  Name=$($d.Name) | DeviceID=$($d.DeviceID) | Availability=$($d.Availability) | Status=$($d.Status)"
    }
    Add-Line "  说明: Availability 3=运行中 7=已断电 8=脱机"
} catch { Add-Line "  WMI 不可用: $($_.Exception.Message)" }
Add-Line ""

# ---------- 3) root\wmi: WmiMonitorID ----------
Add-Line "--- 3) WmiMonitorID（活动显示器实例）---"
try {
    $mi = @(Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorID -ErrorAction Stop)
    if ($mi.Count -eq 0) { Add-Line "  (无结果)" }
    foreach ($x in $mi) { Add-Line "  $($x.InstanceName)" }
} catch { Add-Line "  不可用: $($_.Exception.Message)" }
Add-Line ""

# ---------- 4) 显卡 ----------
Add-Line "--- 4) 显卡 ---"
try {
    $vc = @(Get-CimInstance -ClassName Win32_VideoController -ErrorAction Stop)
    if ($vc.Count -eq 0) { Add-Line "  (无结果)" }
    foreach ($v in $vc) { Add-Line "  GPU: $($v.Name)  分辨率=$($v.CurrentHorizontalResolution)x$($v.CurrentVerticalResolution)" }
} catch { Add-Line "  不可用: $($_.Exception.Message)" }
Add-Line ""

# ---------- 5) GraphicsDrivers 相关键（值 + 子键）----------
Add-Line "--- 5) GraphicsDrivers 内部键（是否随关盖变化）---"
$gd = 'HKLM:\SYSTEM\CurrentControlSet\Control\GraphicsDrivers'
foreach ($k in 'InternalMonEdid','MonitorDataStore','Configuration','Connectivity') {
    $kp = "$gd\$k"
    if (Test-Path $kp) {
        $vals = @((Get-Item $kp).GetValueNames())
        $subs = @(Get-ChildItem $kp -ErrorAction SilentlyContinue | Select-Object -ExpandProperty PSChildName)
        Add-Line "  $k : 值数=$($vals.Count) 子键数=$($subs.Count)"
        if ($subs.Count -gt 0) { Add-Line "      子键: $(($subs | Select-Object -First 8) -join ', ')" }
    } else { Add-Line "  $k : (不存在)" }
}

# ---------- 输出 & 保存 ----------
$text = ($out -join "`r`n")
$dir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$file = Join-Path $dir "lidtest-$State.txt"
try { $text | Out-File -FilePath $file -Encoding UTF8; Write-Host ""; Write-Host "已保存: $file" -ForegroundColor Green } catch { Write-Host "保存失败: $($_.Exception.Message)" -ForegroundColor Yellow }
Write-Host ""
Write-Host "请再换成另一种盖子状态，重新运行一次。两次结果都发我。" -ForegroundColor Cyan
