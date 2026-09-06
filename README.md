# AutoDisplayPower（智能显示器电源管理助手）

常驻系统托盘的 Windows 11 小工具：**根据当前活动的显示器型号自动调整“合盖”电源策略**，
并在外接屏插拔时**自动切换显示模式**（仅外接 / 仅笔记本）。

- 目标平台：Windows 11 x64（.NET 8.0 WinForms）
- 运行方式：双击即运行（发布版为自包含单 EXE，无需安装 .NET 运行时、无 UAC）

## 核心逻辑

| 检测到的屏幕集合 | 推断物理状态 | 合盖操作（AC/DC 同时设置） |
|---|---|---|
| 仅 KG257S PLUS（外接） | 合盖（内屏已断开） | 不操作 (0) |
| 仅 ATNA40HQ01-0（内屏） | 开盖单屏 | 睡眠 S3 (1) |
| KG257S PLUS + ATNA40HQ01-0 | 开盖扩展双屏 | 不操作 (0) |
| 其他（空 / 未知型号） | 未知（安全模式） | 保持现状，仅记日志 |

显示模式自动切换（插拔时触发，会覆盖手动设置的扩展/复制）：

- 插入外接屏 → `DisplaySwitch.exe /external`（仅外接）
- 拔掉外接屏 → `DisplaySwitch.exe /internal`（仅笔记本）

程序以普通权限运行（无 UAC）：修改当前用户活动电源方案的合盖值**无需提权**；
开机自启动通过**任务计划程序**（At Logon，当前用户）实现，不写注册表 Run 键。

## 项目结构

```
AutoDisplayPower/
├── AutoDisplayPower.csproj     # .NET 8.0 WinForms
├── Program.cs                  # 入口（含 --check 自检参数）
├── MainForm.cs                 # 托盘图标 + 主循环（ApplicationContext）
├── CheckMode.cs                # 只读自检模式
├── Services/
│   ├── DisplayDetector.cs      # EDID 型号识别（读取 0xFC 描述符）+ 状态机规则
│   ├── PowerManager.cs         # powercfg 修改 LIDACTION（AC/DC）
│   ├── DisplaySwitcher.cs      # DisplaySwitch.exe 切换显示模式
│   └── StartupManager.cs       # schtasks 管理开机自启动
├── Utils/
│   ├── Win32Display.cs         # P/Invoke：EnumDisplayDevices 枚举“活动”显示器
│   ├── EdidHelper.cs           # 读取注册表 EDID，解析真实型号名(0xFC 描述符)
│   ├── Logger.cs               # %LOCALAPPDATA%\AutoDisplayPower\app.log
│   ├── AppSettings.cs          # HKCU\Software\AutoDisplayPower
│   └── TrayIconFactory.cs      # 运行时绘制托盘图标
└── publish.ps1                 # 单文件自包含发布脚本
```

## 构建与发布

```powershell
# 编译（Debug）
dotnet build

# 独立发布（单文件自包含，输出 .\publish\AutoDisplayPower.exe）
.\publish.ps1
```
*发布说明*：自包含单文件约 68MB（WinForms 自包含的合理体积，规格书预估 20~40MB 偏乐观）。
项目**无任何第三方 NuGet 依赖**（显示器枚举走纯 Win32 API），可完全离线构建。
若目标机已安装 .NET 8 Desktop Runtime（本机已装 8.0.30），可改用框架依赖发布把体积降到 1MB 以内：

```powershell
dotnet publish .\AutoDisplayPower.csproj -c Release -p:PublishSingleFile=true -o .\publish-fd
```

## 自检模式（不改动任何设置）

```powershell
.\publish\AutoDisplayPower.exe --check                 # 打印检测到的显示器与推断状态
.\publish\AutoDisplayPower.exe --check --selftest-power # 额外以当前值原样回写，验证写权限
```

报告同时写入 `%LOCALAPPDATA%\AutoDisplayPower\check-report.txt`。

## 使用说明

- 右键托盘图标可查看实时状态、手动切换「仅外接 / 仅笔记本 / 扩展」。
- 「开机自启动」默认开启：勾选/取消即创建/删除计划任务 `AutoDisplayPower`。
- 日志：`%LOCALAPPDATA%\AutoDisplayPower\app.log`（超过 2MB 自动滚动）。

## 已知边界（v1.0）

- 显示器**型号识别**基于系统“活动显示器”枚举（EnumDisplayDevices）+ 注册表 EDID 名称描述符（0xFC）；
  若某台显示器供电关闭导致 EDID 不可读，会被当作“未知/已拔出”处理，属系统行为限制。
- **指纹匹配说明**：程序按 EDID 实际型号名匹配。外接屏 EDID 名记为 `KG257S PLUS`（字母 S），
  与规格书文字 `KG2575 PLUS`（数字 5）不同，程序对两者均兼容；内屏为 `ATNA40HQ01-0`。
- 合盖瞬间的睡眠触发由系统完成：本程序在**状态变化后**立即改写策略，若机器长时间处于
  “仅内屏→合盖睡眠”策略下直接合盖，会按既有策略立即睡眠（符合预期）；接外接后再合盖则不会睡眠。
- 合盖状态下拔掉外接屏不会立刻睡眠（需要再次合盖或系统超时），此为规格书状态机的自然结果。
