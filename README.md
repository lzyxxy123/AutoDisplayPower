# AutoDisplayPower（智能显示器电源管理助手）

常驻系统托盘的 Windows 11 小工具：**根据当前活动的显示器型号自动调整“合盖”电源策略**，
并在外接屏插拔时**自动切换显示模式**（仅外接 / 仅笔记本）。

- 目标平台：Windows 11 x64（.NET 8.0 WinForms）
- 运行方式：双击即运行（发布版为自包含单 EXE，无需安装 .NET 运行时、无 UAC）

## 核心逻辑

| 检测到的屏幕集合 | 合盖操作（AC/DC 同时设置） |
|---|---|
| 仅 KG257S PLUS（外接） | 不操作 (0) |
| 仅 ATNA40HQ01-0（内屏） | 睡眠 S3 (1) |
| KG257S PLUS + ATNA40HQ01-0 | 不操作 (0) |
| 其他（空 / 未知型号） | 保持现状，仅记日志 |

**盖子开合状态**由 WMI `root\wmi\WmiMonitorID` 判定（**零切换、不闪屏、不黑屏**）：
该 WMI 类列出的是**"已连接"**的显示器（含未激活的内屏）——实测开盖时内屏在列表中（即使当前为
"仅外接"模式），**关盖后内屏从列表消失**。因此：内屏在列表 → 开盖；不在列表 → 关盖；查询失败 → 未知。
（曾尝试 `QueryDisplayConfig`，但它在本机固定返回 `ERROR_INVALID_PARAMETER`，故弃用。）
为绕开本机无法访问 nuget.org 的限制，WMI 通过 Windows 自带的**晚绑定 COM**（`WbemScripting.SWbemLocator`）查询，
取条目使用 `_NewEnum` 顺序枚举（`ExecQuery` 返回的集合不支持 `Item(index)` 随机访问）。

显示模式自动切换（插拔时触发，会覆盖手动设置的扩展/复制）：

- 插入外接屏 → `DisplaySwitch.exe /external`（仅外接）
- 拔掉外接屏 → `DisplaySwitch.exe /internal`（仅笔记本）

程序以普通权限运行（无 UAC）：修改当前用户活动电源方案的合盖值**无需提权**；
开机自启动优先通过**任务计划程序**（At Logon，当前用户）实现，若受限则回退写入 **HKCU Run 键**保证生效。

## 项目结构

```
AutoDisplayPower/
├── AutoDisplayPower.csproj     # .NET 8.0 WinForms
├── Program.cs                  # 入口（含 --check 自检参数）
├── MainForm.cs                 # 托盘图标 + 主循环（ApplicationContext）
├── ConfigForm.cs               # 显示器型号配置对话框
├── CheckMode.cs                # 只读自检模式
├── Services/
│   ├── DisplayDetector.cs      # EDID 型号识别（可配置型号）+ 状态机规则
│   ├── PowerManager.cs         # powercfg 修改 LIDACTION（AC/DC）
│   ├── DisplaySwitcher.cs      # DisplaySwitch.exe 切换显示模式
│   ├── PlugPolicy.cs           # “插上外接屏时”的行为（固定/记住上次，注册表持久化）
│   └── StartupManager.cs       # 开机自启动：任务计划程序 + Run 键兜底
├── Utils/
│   ├── UiTheme.cs              # Fluent 浅色语义色板（状态→颜色映射）
│   ├── ModernMenuRenderer.cs   # 现代浅色菜单渲染器（彩色对钩/只读行彩色文字）
│   ├── Win32Display.cs         # P/Invoke：EnumDisplayDevices 枚举“活动”显示器
│   ├── WmiQuery.cs             # 晚绑定 COM 查询 WMI（无需 System.Management）
│   ├── DisplayTopology.cs      # P/Invoke：QueryDisplayConfig（本机不可用，仅保留调试）
│   ├── EdidHelper.cs           # 读取注册表 EDID，解析真实型号名(0xFC 描述符)
│   ├── Logger.cs               # %LOCALAPPDATA%\AutoDisplayPower\app.log
│   ├── AppSettings.cs          # HKCU\Software\AutoDisplayPower（含显示器型号配置）
│   └── TrayIconFactory.cs      # 运行时绘制托盘图标
└── publish.ps1                 # 发布脚本（自包含 / -Small 小体积）
```

## 构建与发布

```powershell
# 编译（Debug）
dotnet build

# 自包含单文件发布（约 68MB，无需运行时，适合分发）
.\publish.ps1

# 小体积发布（约 0.2MB，需目标机已装 .NET 8 Desktop Runtime，本机已装）
.\publish.ps1 -Small
```
*发布说明*：项目**无任何第三方 NuGet 依赖**（显示器枚举走纯 Win32 API），可完全离线构建。
- **自包含单文件**约 68MB：打包了整套 .NET 8 运行时，任何 Win11 都能跑，无需安装运行时。
- **框架依赖版**仅约 **0.2MB**：体积大幅减小，但目标机需已安装 .NET 8 Desktop Runtime（本机已装 8.0.30）。
  体积大**不是环境问题**，而是"是否把运行时一起打包"的区别；目标机有运行时就用小体积版。

## 显示器型号配置

程序按"内屏型号"与"外接屏型号"归类，可在托盘菜单 **「显示器型号配置…」** 里随时修改：

- **内屏型号**：笔记本内置屏的 EDID 型号（默认 `ATNA40HQ01-0`）。
- **外接屏型号**：多个用逗号分隔；默认勾选 **「任意外接屏」**——任意非内屏显示器都算外接屏，
  这样**换外接屏无需改配置**即可继续用。若不勾选，则只把列表中列出的型号当外接屏。
- 配置保存在 `HKCU\Software\AutoDisplayPower`，保存后立即生效。

## 自检模式（不改动任何设置）

```powershell
.\publish\AutoDisplayPower.exe --check                 # 打印检测到的显示器与推断状态
.\publish\AutoDisplayPower.exe --check --selftest-power # 额外以当前值原样回写，验证写权限
.\publish\AutoDisplayPower.exe --topology              # 打印显示拓扑（调试用）
```

便捷脚本（双击即可）：

- **`check.cmd`** — 运行 `--check` 并显示报告（含"盖子判据"）
- **`diag.cmd`** — WMI/盖子检测通路诊断（对比晚绑定 COM 与 `Get-CimInstance`）
- **`lidtest.cmd open|closed`** — 开盖/合盖两种状态下的系统信号对比（用于排查盖子检测）

报告同时写入 `%LOCALAPPDATA%\AutoDisplayPower\check-report.txt`（UTF-8 带 BOM）。

## 使用说明

- **界面风格**（4 套主题，托盘菜单「界面主题」可切换并持久化；默认 **C 深色**）：
  - **A 浅色 · 蓝色强调**：Fluent 浅色，当前模式 = 整行淡蓝高亮
  - **B 纯白 · 橙色强调**（类火绒）：白底 + 橙色高亮/开关，图标保留语义色
  - **C 深色主题**（默认）：深灰底 + 亮字 + 亮蓝强调
  - **D 极简 · 左侧色条**：不用色块，当前模式只用左侧细色条 + 加粗
  - 文字统一跟随主题；**语义全部交给彩色矢量图标**（代码绘制，无需外部资源）：
    屏幕（仅外接=蓝显示器 / 仅笔记本=青笔记本 / 扩展=双显示器）、
    盖子（打开=绿 / 闭合=琥珀 / 未知=灰）、
    策略（不操作=蓝暂停 / 睡眠=紫月亮 / 关机=琥珀电源 / 写入失败=红警告 / 未知=灰问号）
  - **「开机自启动」为滑动开关**（轨道+圆钮），点击即切换
  - **电源策略变化时**：该行 3 秒内加粗 + 浅色高亮，并显示"（已更新）"
  - **托盘图标彩色并随屏幕状态变色**，颜色同样跟随主题
  - **鼠标悬停托盘图标**显示单行摘要：`仅外接 · 盖子打开 · 合盖不操作`
  - 调试：`AutoDisplayPower.exe --icons` 导出全部图标；`--mockup` 导出 4 套主题的菜单样张
- 右键托盘图标可查看实时状态、手动切换「仅外接 / 仅笔记本 / 扩展」。
  **三个切换项会按“当前实际显示状态”打对钩**（未知状态时都不打勾）。
- **「插上外接屏时」子菜单**（三选一，持久化保存）：
  - `始终「仅外接」`（默认，与规格书 3.3 一致）
  - `始终「扩展」`
  - `记住上次选择` —— 记住你在**外接屏在场时**选的「仅外接 / 扩展」，下次插上按它来
    （拔外接屏触发的自动切换不计入记录；选中的那项会显示当前记住的是哪一种）
- **切换会校验真实结果**：执行切换后约 1 秒重新检测屏幕，达到目标模式才算成功，并提示
  `已切换为XX屏幕，电源策略为YYY`；若目标屏幕不在场（如关盖时切「仅笔记本」、无外接时切
  「仅外接 / 扩展」），则提示 `切换失败：未检测到XX屏幕（当前检测到：…）`。「扩展」要求双屏同亮。
  自动切换（插拔外接屏触发）同样校验。
- 「开机自启动」默认开启：勾选/取消即创建/删除计划任务 `AutoDisplayPower`。
- 日志：`%LOCALAPPDATA%\AutoDisplayPower\app.log`（超过 2MB 自动滚动）。

## 已知边界（v1.0）

- 显示器**型号识别**基于系统“活动显示器”枚举（EnumDisplayDevices）+ 注册表 EDID 名称描述符（0xFC）；
  若某台显示器供电关闭导致 EDID 不可读，会被当作“未知/已拔出”处理，属系统行为限制。
- **指纹匹配说明**：程序按 EDID 实际型号名匹配。外接屏 EDID 名记为 `KG257S PLUS`（字母 S），
  与规格书文字 `KG2575 PLUS`（数字 5）不同，程序对两者均兼容；内屏为 `ATNA40HQ01-0`。
- **盖子开合**：由 WMI `WmiMonitorID`（"已连接"显示器列表）判定内屏是否在位，**零切换、不闪屏**。
  若 WMI 查询失败（受限会话/权限），保守显示"未知"，而非错误的"闭合"。
  可用 `diag.cmd` 诊断 WMI 通路，`check.cmd` 查看含"盖子判据"的完整自检报告。
- 合盖瞬间的睡眠触发由系统完成：本程序在**状态变化后**立即改写策略，若机器长时间处于
  “仅内屏→合盖睡眠”策略下直接合盖，会按既有策略立即睡眠（符合预期）；接外接后再合盖则不会睡眠。
- 合盖状态下拔掉外接屏不会立刻睡眠（需要再次合盖或系统超时），此为规格书状态机的自然结果。
