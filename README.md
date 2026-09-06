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

**盖子开合状态**另由 `QueryDisplayConfig` 判定：内屏目标"物理可用(available)"→开盖；"已断开"→关盖。
该 API 能区分"内屏物理断开（关盖）"与"内屏物理连接但未激活（开盖+仅外接）"，因此
即使程序强制"仅外接"（切断内屏显示），仍能正确显示开/合盖；受限会话读取失败时保守显示"未知"。

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
│   └── StartupManager.cs       # 开机自启动：任务计划程序 + Run 键兜底
├── Utils/
│   ├── Win32Display.cs         # P/Invoke：EnumDisplayDevices 枚举“活动”显示器
│   ├── DisplayTopology.cs      # P/Invoke：QueryDisplayConfig 判定开/合盖
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
- **盖子开合**：依赖 `QueryDisplayConfig` 判断内屏“物理可用”。在受限/虚拟显示会话（如远程桌面、
  某些沙箱）该 API 可能返回 `ERROR_INVALID_PARAMETER`，此时保守显示“未知”，而非错误的“闭合”；
  在普通 Windows 交互登录会话中可正确区分开/合。
- 合盖瞬间的睡眠触发由系统完成：本程序在**状态变化后**立即改写策略，若机器长时间处于
  “仅内屏→合盖睡眠”策略下直接合盖，会按既有策略立即睡眠（符合预期）；接外接后再合盖则不会睡眠。
- 合盖状态下拔掉外接屏不会立刻睡眠（需要再次合盖或系统超时），此为规格书状态机的自然结果。
