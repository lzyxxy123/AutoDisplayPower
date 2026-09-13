# AutoDisplayPower · 智能显示器电源管理助手

> 常驻系统托盘的 Windows 11 小工具：按**当前实际活动的显示器**自动调整“合盖”电源策略，
> 并在外接屏插拔时**自动切换显示模式**。全程普通权限运行、**无 UAC 弹窗**。

- **平台**：Windows 11 x64
- **框架**：.NET 8.0 WinForms（**无任何第三方 NuGet 依赖**，可完全离线构建）
- **产物**：单文件 EXE，双击即用（自包含版约 68MB / 框架依赖版约 240KB）

---

## 🚀 快速开始（下载 → 编译 → 运行）

### 1. 安装 .NET 8.0 SDK（仅编译需要）
下载安装：<https://dotnet.microsoft.com/download/dotnet/8.0>
> 已验证版本：SDK `8.0.424`。运行**框架依赖版**还需 .NET 8 **Desktop** Runtime（装了 SDK 就自带）。

### 2. 获取源码
```powershell
git clone https://github.com/lzyxxy123/AutoDisplayPower.git
cd AutoDisplayPower
```
或直接在 GitHub 页面 **Code → Download ZIP** 后解压。

### 3. 编译 —— 双击 `build.cmd` 即可
```
build.cmd            一次生成【两个版本】（推荐）
build.cmd small      只生成小体积版
build.cmd full       只生成自包含单文件版
```
> 因为双击无法传参数，**不带参数时默认两个版本都编**。

### 4. 运行
双击生成的 EXE，托盘出现图标后**右键**即可使用：
- `publish-small\AutoDisplayPower.exe` —— **约 240KB**（本机已装运行时就用这个）
- `publish\AutoDisplayPower.exe` —— **约 68MB**（自包含，拷到任何 Win11 都能跑）

---

## 📦 两个版本怎么选

| | 框架依赖版（`-Small`） | 自包含单文件版 |
|---|---|---|
| 体积 | **约 240 KB** | 约 68 MB |
| 依赖 | 需已装 .NET 8 **Desktop** Runtime | **不需要**任何运行时 |
| 适用 | 自己用 / 目标机已装运行时 | 发给别人 / 换机器免装环境 |

**体积大不是环境问题**，而是“是否把整套 .NET 运行时一起打包”的区别。

---

## ✨ 功能一览

| 功能 | 说明 |
|---|---|
| **合盖电源策略自动切换** | 按当前屏幕状态自动改“合盖操作”（AC/DC 同时改） |
| **盖子开合状态判定** | 用 WMI 判定，**零切换、不闪屏、不黑屏** |
| **插拔自动切换显示模式** | 插外接 / 拔外接自动切换，可配置策略 |
| **切换结果校验** | 切换后重新检测，切不过去就提示失败（不假报成功） |
| **显示器型号可配置** | 换外接屏免配置（默认“任意外接屏”） |
| **4 套界面主题** | 菜单实时切换并记忆，彩色矢量图标 |
| **开机自启动** | 任务计划程序，受限时自动回退 Run 键 |
| **单条汇总通知** | 切换时只弹一条：`已切换为X屏幕，电源策略为Y` |

---

## 🧠 核心逻辑

### 状态机（由“活动屏幕集合”决定合盖操作）

| 检测到的活动屏幕 | 合盖操作（AC/DC 同时设置） |
|---|---|
| 仅外接屏 | **不操作** (0) |
| 仅笔记本内屏 | **睡眠 S3** (1) |
| 内屏 + 外接同时亮 | **不操作** (0) |
| 空 / 无法识别 | **保持现状**，仅记日志 |

### 盖子开合状态（关键设计）

`EnumDisplayDevices` 只能看到**正在点亮**的屏幕，内屏被关掉时就“看不见”了（`QueryDisplayConfig`
本机固定返回 `ERROR_INVALID_PARAMETER`，不可用）。实测发现 **WMI `root\wmi\WmiMonitorID`
列出的是“已连接”显示器（含未激活的内屏）**：

- 开盖时内屏在列表中（即使当前是“仅外接”模式）
- **关盖后内屏从列表消失**

→ 因此：**内屏在列表 = 开盖；不在 = 关盖；查询失败 = 未知**。不切换、不闪屏、不黑屏。

> 由于本机无法访问 nuget.org（装不了 `System.Management`），WMI 改用 Windows 自带的
> **晚绑定 COM**（`WbemScripting.SWbemLocator`）查询；取条目用 `_NewEnum` 顺序枚举
> —— 实测 `ExecQuery` 返回的集合不支持 `Item(index)` 随机访问。

### 显示模式自动切换

插拔时触发（会覆盖手动设置的扩展/复制），具体切到哪个模式由
**「插上外接屏时」**设置决定（见下）。拔掉外接屏固定切回“仅笔记本”。

---

## 🎨 界面与主题

### 4 套主题（托盘菜单「界面主题」，切换后持久化，默认 **C 深色**）

| 主题 | 风格 |
|---|---|
| **A 浅色 · 蓝色强调** | Fluent 浅色，蓝色高亮 |
| **B 纯白 · 橙色强调** | 纯白底 + 橙色强调（类火绒），图标保留语义色 |
| **C 深色主题**（默认） | 深灰底 + 亮字 + 亮蓝强调 |
| **D 极简 · 左侧色条** | 不用色块，用左侧细色条 + 加粗标记当前项 |

### 图标语义（代码绘制的彩色矢量图标，无外部资源）

| 分类 | 颜色/造型 |
|---|---|
| 屏幕 | 仅外接=蓝显示器 / 仅笔记本=青笔记本 / 扩展=紫双屏 |
| 盖子 | 打开=绿 / 闭合=琥珀蚌壳 / 未知=灰 |
| 策略 | 不操作=蓝暂停 / 睡眠=紫月亮 / 关机=琥珀电源 / 写入失败=红警告 / 未知=灰问号 |

### 交互细节
- **选中状态显示在右侧**：用主题主色**对钩** + 文字加粗（左侧只放彩色图标，排版紧凑）
- **「开机自启动」是滑动开关**（轨道+圆钮），点击即切换
- **电源策略变化时**：该行 3 秒内加粗 + 浅色高亮 + 显示“（已更新）”
- **托盘图标随屏幕状态变色**；**鼠标悬停**显示单行摘要：
  `仅外接 · 盖子打开 · 合盖不操作`

---

## ⚙️ 配置项（均存于 `HKCU\Software\AutoDisplayPower`，重启保留）

| 配置 | 位置 | 说明 |
|---|---|---|
| **显示器型号** | 托盘 →「显示器型号配置…」 | 内屏型号（默认 `ATNA40HQ01-0`）；外接屏型号可多个；默认勾选**「任意外接屏」**，换屏免配置 |
| **插上外接屏时** | 托盘 →「插上外接屏时」 | `始终「仅外接」`（默认）/ `始终「扩展」` / `记住上次选择` |
| **界面主题** | 托盘 →「界面主题」 | A/B/C/D 四选一 |
| **开机自启动** | 托盘 →「开机自启动」 | 默认开启；勾选=创建计划任务 `AutoDisplayPower` |

> **「记住上次选择」**只记录**外接屏在场时**选择的「仅外接 / 扩展」（拔外接屏触发的自动切换不计入），
> 下次插上按它执行；选中的那项会显示当前记住的是哪一种。

---

## 🔧 自检与诊断

### 命令行参数（不改动任何设置）

```powershell
AutoDisplayPower.exe --check                  # 打印显示器/状态/策略（含“盖子判据”）
AutoDisplayPower.exe --check --selftest-power # 额外以当前值回写，验证写权限
AutoDisplayPower.exe --topology               # 打印显示拓扑（调试）
AutoDisplayPower.exe --icons                  # 导出全部图标 PNG 预览
AutoDisplayPower.exe --mockup                 # 导出 4 套主题的菜单样张
AutoDisplayPower.exe --uipreview              # 离屏渲染真实菜单为 PNG（验证实际绘制效果）
```

### 便捷脚本（双击即可）

| 脚本 | 用途 |
|---|---|
| `check.cmd` | 运行 `--check` 并显示报告 |
| `diag.cmd` | WMI / 盖子检测通路诊断（对比晚绑定 COM 与 `Get-CimInstance`） |
| `lidtest.cmd open` / `lidtest.cmd closed` | 开盖/合盖两种状态下采集系统信号并对比 |

日志：`%LOCALAPPDATA%\AutoDisplayPower\app.log`（超过 2MB 自动滚动）

---

## 📁 项目结构

```
AutoDisplayPower/
├── build.cmd                   # ★ 一键编译（默认生成两个版本）
├── publish.ps1                 # 发布脚本（自包含 / -Small）
├── AutoDisplayPower.csproj     # .NET 8.0 WinForms
├── Program.cs                  # 入口（含 --check/--icons/--mockup/--uipreview）
├── MainForm.cs                 # 托盘图标 + 主循环 + 菜单（ApplicationContext）
├── ConfigForm.cs               # 显示器型号配置对话框
├── CheckMode.cs                # 自检/预览模式
├── Services/
│   ├── DisplayDetector.cs      # 型号识别（可配置）+ 状态机 + WMI 盖子判定
│   ├── PowerManager.cs         # powercfg 修改 LIDACTION（AC/DC）
│   ├── DisplaySwitcher.cs      # DisplaySwitch.exe 切换显示模式
│   ├── PlugPolicy.cs           # “插上外接屏时”的行为（持久化）
│   └── StartupManager.cs       # 开机自启动：计划任务 + Run 键兜底
└── Utils/
    ├── Theme.cs / UiTheme.cs   # 4 套主题 + 主题转发
    ├── ModernMenuRenderer.cs   # 菜单渲染器（右侧对钩/滑动开关/只读行彩色）
    ├── MenuIconFactory.cs      # 彩色矢量菜单图标
    ├── TrayIconFactory.cs      # 托盘图标（随状态变色）
    ├── Win32Display.cs         # P/Invoke：枚举活动显示器
    ├── WmiQuery.cs             # 晚绑定 COM 查询 WMI（免 System.Management）
    ├── EdidHelper.cs           # 注册表 EDID → 型号名（0xFC 描述符）
    ├── DisplayTopology.cs      # QueryDisplayConfig（本机不可用，仅调试）
    ├── Logger.cs / AppSettings.cs
    └── MockupRenderer.cs       # 主题样张渲染
```

---

## ❓ 常见问题

**Q：仓库里为什么没有 exe？**
A：`.gitignore` 排除了编译产物（`*.exe`、`bin/`、`obj/`、`publish*/`）—— 源码仓库只放源码，
避免臃肿和过期。请按上面的**快速开始**用 `build.cmd` 自行编译。

**Q：托盘提示“电源策略：写入失败，需权限”？**
A：说明当前会话**读不到也改不了**电源方案的合盖项（受限/虚拟会话，或本机方案确实没有合盖项）。
用 `check.cmd` 看“盖子判据”确认。程序会显示“期望策略（已下发/写入失败）”而不是“查询失败”。

**Q：盖子状态显示“未知”？**
A：WMI 查询失败（受限会话/权限）。用 `diag.cmd` 诊断；正常桌面会话通常可用。

**Q：切换时提示“切换失败：未检测到XX屏幕”？**
A：这是**正常校验**——目标屏幕不在场（例如关盖时切「仅笔记本」、没接外接时切「仅外接/扩展」）。
「扩展」要求双屏同亮才算成功。

**Q：勾选“开机自启动”失败？**
A：任务计划程序受限时会自动回退写 `HKCU\...\Run`；若两者都被拒绝，请用管理员检查系统策略。

**Q：为什么 EXE 有 68MB？**
A：自包含版把整套 .NET 8 运行时打包进去了（换机器免装环境）。要小就用框架依赖版（约 240KB）。

---

## ⚠️ 已知边界（v1.0）

- **型号识别**基于“活动显示器”枚举 + 注册表 EDID 名称（0xFC）；显示器断电导致 EDID 不可读时会按
  “未知/已拔出”处理，属系统限制。
- 外接屏 EDID 实际名为 **`KG257S PLUS`**（字母 S），与早期规格书文字 `KG2575 PLUS`（数字 5）不同，
  程序**两者兼容**；内屏为 `ATNA40HQ01-0`。
- 合盖睡眠的**触发**由系统完成：程序在状态变化后立即改写策略；若长期处于“仅内屏→睡眠”策略下直接
  合盖，会按既有策略立即睡眠（符合预期）。
- 合盖状态下拔掉外接屏不会立刻睡眠（需再次合盖或系统超时），为状态机的自然结果。
- `DisplayTopology.cs`（`QueryDisplayConfig`）在部分机器返回 `ERROR_INVALID_PARAMETER`，已不参与判定，
  仅保留调试。

---

## 🛠 开发说明

- **分支**：`dev` 开发 → 稳定后合并到 `main`
- **提交规范**：`feat:` / `fix:` / `docs:` / `style:` / `test:` 前缀
- **完全离线构建**：无 NuGet 依赖；`<NuGetAudit>false</NuGetAudit>` 已关闭联网审计
- **调试验证技巧**：改动菜单/图标外观后，用 `--uipreview` 导出真实菜单渲染图对照，比肉眼猜更可靠
  （本项目就是靠它定位到“禁用项图标被灰度化”“`OnRenderMenuItemBackground` 对未选中项不被调用”两个坑）

---

## 卸载

删除 EXE 即可。如需彻底清理：

1. 托盘 →「开机自启动」取消勾选（或命令行 `schtasks /Delete /TN AutoDisplayPower /F`）
2. 删除 `HKCU\Software\AutoDisplayPower`（保存的主题/型号配置）
3. 删除 `%LOCALAPPDATA%\AutoDisplayPower`（日志/报告）
