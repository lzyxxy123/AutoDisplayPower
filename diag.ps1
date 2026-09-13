$ErrorActionPreference = 'Continue'
$f  = [System.Reflection.BindingFlags]::InvokeMethod
$gp = [System.Reflection.BindingFlags]::GetProperty

function Show-Inner($ex) { $e = $ex; while ($e.InnerException) { $e = $e.InnerException }; return $e.Message }

Write-Host "===================================================" -ForegroundColor Cyan
Write-Host " AutoDisplayPower WMI / 盖子检测诊断" -ForegroundColor Cyan
Write-Host "===================================================" -ForegroundColor Cyan

# ---------- 1) 晚绑定 COM（与程序使用的完全相同的方式）----------
Write-Host ""
Write-Host "--- [1] WMI COM (WbemScripting.SWbemLocator) 与程序相同的方式 ---" -ForegroundColor Yellow
try {
    $t = [Type]::GetTypeFromProgID('WbemScripting.SWbemLocator')
    if (-not $t) { Write-Host "  找不到 COM 类型 WbemScripting.SWbemLocator" }
    else {
        $l = [Activator]::CreateInstance($t)
        Write-Host "  Locator 创建 OK"
        $s = $t.InvokeMember('ConnectServer', $f, $null, $l, [object[]]@('.', 'root\wmi', [Type]::Missing, [Type]::Missing, [Type]::Missing, [Type]::Missing, [Type]::Missing))
        Write-Host "  ConnectServer(root\wmi) OK"
        try {
            $r = $s.GetType().InvokeMember('ExecQuery', $f, $null, $s, [object[]]@('SELECT InstanceName FROM WmiMonitorID'))
            $c = $r.GetType().InvokeMember('Count', $gp, $null, $r, $null)
            Write-Host "  ExecQuery OK  Count=$c" -ForegroundColor Green
            if ($c -gt 0) {
                foreach ($i in 1..$c) {
                    $item = $r.GetType().InvokeMember('Item', $f, $null, $r, [object[]]@($i))
                    $n = $item.GetType().InvokeMember('InstanceName', $gp, $null, $item, $null)
                    Write-Host "      $n"
                }
            }
        } catch { Write-Host "  ExecQuery 失败: $(Show-Inner $_.Exception)" -ForegroundColor Red }
    }
} catch { Write-Host "  COM 失败: $(Show-Inner $_.Exception)" -ForegroundColor Red }

# ---------- 2) Get-CimInstance 对照 ----------
Write-Host ""
Write-Host "--- [2] Get-CimInstance  root\wmi\WmiMonitorID ---" -ForegroundColor Yellow
try {
    $mi = @(Get-CimInstance -Namespace root\wmi -ClassName WmiMonitorID -ErrorAction Stop)
    Write-Host "  成功，共 $($mi.Count) 条"
    foreach ($x in $mi) { Write-Host "      $($x.InstanceName)" }
} catch { Write-Host "  失败: $(Show-Inner $_.Exception)" -ForegroundColor Red }

# ---------- 3) Win32_PnPEntity 内屏(监视器类) ----------
Write-Host ""
Write-Host "--- [3] Get-CimInstance  root\cimv2\Win32_PnPEntity (Monitor 类) ---" -ForegroundColor Yellow
try {
    $pe = @(Get-CimInstance -Namespace root\cimv2 -ClassName Win32_PnPEntity -Filter "PNPClass='Monitor'" -ErrorAction Stop)
    Write-Host "  成功，共 $($pe.Count) 条"
    foreach ($p in $pe) { Write-Host "      $($p.PNPDeviceID)  Status=$($p.Status)  ConfigManagerErrorCode=$($p.ConfigManagerErrorCode)" }
} catch { Write-Host "  失败: $(Show-Inner $_.Exception)" -ForegroundColor Red }

# ---------- 4) 程序自身 --check 报告 ----------
Write-Host ""
Write-Host "--- [4] 程序 --check 报告（含'盖子判据'）---" -ForegroundColor Yellow
$exe = Join-Path $PSScriptRoot 'publish-small\AutoDisplayPower.exe'
if (-not (Test-Path $exe)) { $exe = Join-Path $PSScriptRoot 'publish\AutoDisplayPower.exe' }
if (Test-Path $exe) {
    Write-Host "  使用: $exe"
    Start-Process -FilePath $exe -ArgumentList '--check' -Wait | Out-Null
    Start-Sleep -Seconds 1
    $rep = Join-Path $env:LOCALAPPDATA 'AutoDisplayPower\check-report.txt'
    if (Test-Path $rep) { Get-Content $rep | ForEach-Object { Write-Host "  $_" } }
    else { Write-Host "  找不到报告文件: $rep" -ForegroundColor Red }
} else { Write-Host "  找不到 AutoDisplayPower.exe（请先发布）" -ForegroundColor Red }

Write-Host ""
Write-Host "---------------------------------------------------" -ForegroundColor Cyan
Write-Host "请把以上全部内容截图或复制发回。" -ForegroundColor Green
