# AGENTS.md — OpenAgent 项目交接指南

> 本文档面向接手 OpenAgent 项目的 AI 智能体或人类开发者。它记录项目真实状态、架构约束、已踩的坑和推进路径，**优先于任何泛泛的猜测**。

---

## 1. 项目是什么

**OpenAgent** — Windows × Android 开源 AI 跨设备控制中心。
- **Windows 端**：.NET 10 / C# / WinUI 3（unpackaged），12 个产品工程 + 8 个测试工程
- **Android 端**：Kotlin / Jetpack Compose / Material 3，`android/` Gradle 项目（Gradle wrapper 已提交，APK 已可命令行构建）
- **当前版本**：v1.0.0（首个公开发布版，Windows MSI + EXE 安装包、Android APK 挂在 GitHub Release 上）
- **规格真源**：根目录 `OpenAgent 完整项目总提示词.md`（9581 行，366 节）

核心使命：让 Android 手机在同一局域网内发现 Windows 主机，并发送 AI 命令（如"打开记事本"）让 Windows 执行。

---

## 2. 架构铁律（违反必崩）

**分层单向依赖**，禁止反向或跳层：

```
UI → Agent Service → Tool Registry → Permission Manager → Tool Executor
```

关键规则：
- `OpenAgent.Windows.UI`（UI 库）**只依赖 Core + Shared**，不直接引用 Transport / Providers / Tools
- 设备数据经 `IAgentHost.DevicesAsync` + `DeviceSummary` 暴露，UI 不直接读 `DeviceRecord`
- Provider 的 DI 扩展 (`AddOpenAgentProviders`) 放在 `OpenAgent.Providers` **自身**，由 App 顶层调用，避免 Agent→Providers 循环依赖
- Windows 端是 **unpackaged WinUI 3**（无 Visual Studio / 无 MSIX）：`WindowsPackageType=None` + `WindowsAppSDKSelfContained=true`

---

## 3. 工程与构建

### Windows（.NET）
```bash
# 全量构建（必须带 -p:Platform=x64）
dotnet build OpenAgent.sln -c Release -p:Platform=x64

# 测试（当前 275 个，全绿）
dotnet test OpenAgent.sln -c Release -p:Platform=x64

# 可分发产物（self-contained，供安装包打包）
dotnet publish src/apps/windows/OpenAgent.Windows/OpenAgent.Windows.csproj \
  -c Release -p:Platform=x64 -r win-x64 --self-contained true -o artifacts/windows/win-x64

# 安装包（MSI + 包裹它的 EXE bundle）
dotnet tool restore          # 拉取 WiX 7（清单在根目录 dotnet-tools.json）
./scripts/build-installer.ps1
```
> `OpenAgent.pri`（XAML 资源索引）必须出现在 publish 输出里，否则应用启动即崩（见 §8.9）。
> `build-installer.ps1` 会在打包前检查该文件是否存在，缺失即失败退出。

### Android（Kotlin）
```bash
cd android
JAVA_HOME="<AndroidStudio>/jbr" ./gradlew assembleDebug assembleRelease
```
- 需要 JDK **17–21**（Gradle 8.14.3 不支持 PATH 上的 JDK 25），直接用 Android Studio 自带 JBR。
- Release 签名读 `android/keystore.properties`（已 gitignore）；缺失时 release 退回 debug 签名，干净 checkout 仍可构建。
- `android/local.properties` 提供 `sdk.dir`（`ANDROID_HOME` 未设置）。
- 两个文件里的 Windows 路径必须用**正斜杠**（见 §8.10）。

### 项目结构
```
OpenAgent/
├── src/
│   ├── apps/windows/          # OpenAgent.Windows (exe) + OpenAgent.Windows.UI (库)
│   ├── core/                  # OpenAgent.Core / Agent / Transport / Storage / Security
│   ├── tools/                 # OpenAgent.Tools (11 个内置工具)
│   ├── providers/             # OpenAgent.Providers (IAgentProvider + Native + CLI 适配器)
│   ├── plugins/               # OpenAgent.Plugins (仅契约)
│   ├── mcp/                   # OpenAgent.Mcp (仅契约)
│   └── shared/                # OpenAgent.Shared
├── tests/                     # 8 个测试工程（Core / Agent / Storage / Security / Providers / Transport / Tools / UI）
├── android/                   # Kotlin + Jetpack Compose 客户端
├── installer/                 # WiX 7 源码（OpenAgent.wxs=MSI, Bundle.wxs=EXE, app.ico）
├── docs/
│   ├── protocol.md            # 跨设备线格式真源（beacon + JSON envelope）
│   ├── dev-log.md             # 开发日志（按提交记录）
│   └── motion.md              # 动效规格（时长/缓动/reduce-motion）
├── design/
│   ├── tokens.css             # 设计 token 真源（OKLCH）
│   └── motion.md              # 动效真源
└── scripts/
    ├── gen-tokens.py          # CSS → Themes/Tokens.xaml（不要手改 xaml）
    ├── oa_mark.py             # 图形 mark 的唯一来源（形状+配色）
    ├── gen-tray-icon.py       # 托盘 .ico 生成（经典 32bpp DIB，PNG 走不通）
    ├── gen-installer-icon.py  # 多尺寸 app.ico（安装包用）
    ├── gen-android-icons.py   # 安卓 mipmap + 自适应图标（复用 oa_mark）
    ├── ui-shot.ps1            # Windows：build → 运行 → 按效果图尺寸截客户区
    ├── capture-window.ps1     # Windows：唤醒显示器 + PrintWindow + 裁剪 + 空帧门禁
    ├── ui-shot-android.sh     # Android：gradle → 装 → 点 tab → screencap → 390x844
    ├── android-tab.py         # Android：从 uiautomator dump 里取某个 tab 的中心点
    └── build-installer.ps1    # publish → MSI → ICE 校验 → EXE bundle
```

---

## 4. 命名空间陷阱（编译杀手）

项目自身有 `OpenAgent.Windows` 命名空间。**引用系统 API 时必须加 `global::` 前缀**：

```csharp
// ❌ 错误：解析成 OpenAgent.Windows.Storage
using Windows.Storage;

// ✅ 正确
using global::Windows.Storage.ApplicationData;
using global::Windows.System.VirtualKey;
```

同理：
- `Microsoft.UI.Xaml.Shapes.Path` 与 `System.IO.Path` 冲突 → `using Path = Microsoft.UI.Xaml.Shapes.Path;`
- `Color` 冲突 → `using global::Windows.UI.Color;`

---

## 5. 已落地的核心模块

### 5.1 Provider 层（Phase 4+，提交 `ae309dd`）
- `IAgentProvider` 抽象：Id / DisplayName / Capabilities / CreateSession / SendPrompt / Stop / Resume / HealthCheck
- `NativeAgentProvider`（`openagent.native`）：确定性关键词路由，始终可用
- `CliDiscovery`：扫描 PATH 发现 Codex / Claude Code / OpenCode / Pi / Gemini
- `CliInvocationProfile(s)`：per-CLI 调用数据化（codex=`exec`、claude=`-p`、opencode=`run`、pi/gemini=无）
- `CliAgentProvider`：驱动已发现 CLI，发 Thought→ToolCall→ToolResult 步骤
- `IProcessRunner`：进程派生抽象，`RealProcessRunner` 实现
- `AddOpenAgentProviders`：遍历 `CliDiscovery.Scan()` 自动注册发现的 CLI

### 5.2 Transport 层（Phase 6，提交 `bd80f73` + `133e9ff`）
- `ITransport` + `DeviceRecord` + `LocalLoopbackTransport`（本机恒在）
- `LanBeaconFrame`：UTF-8 线格式 `OPENAGENT-BEACON v1|id|name|platform|version|port|ticks`
- `LanMessageEnvelope`：JSON 命令/结果信封 `{"type":"command",...}`
- `UdpLanTransport`：UDP 广播 beacon + 监听，发现对端为 `ConnectionType="lan"`
  - socket 绑不上 → **静默降级为仅 loopback**（不抛异常）
  - `SendAsync`：已知对端单播，否则尽力广播，绝不抛异常
- `InboundMessage` 事件：收到非 beacon 的 JSON envelope 时触发

### 5.3 UI 层（Phase 2-UI）
- `MainWindow`：无边框（`ExtendsContentIntoTitleBar`），关闭即隐藏（托盘存活）
- `AgentPage`：起始页 / 对话态 / 审批态同一个面；Alt+Space 从托盘唤起并落到这里
  （`CommandCenterWindow` 已删除，真实链路 plan→execute→审批卡都在 AgentPage 内）
- `TasksPage` / `ToolsPage` / `DevicesPage`：读真实数据（非 sample data）
- 托盘：右键菜单（开主窗口 / 开 Command Center / 退出）
- 动效：`Motion.cs`（时长阶梯 + `UISettings.AnimationsEnabled` 开关）
  - M-02 进/出、M-14 审批展开、M-17 长按确认（1200ms）、M-24 描绘打勾

### 5.4 工具层（Phase 3）
11 个内置工具：`file.list/read/write/move/copy/delete`、`process.list/terminate`、`app.launch/close`、`screen.capture`、`system.get_info`
- `screen.capture` 用 GDI（`BitBlt` + `GetDIBits`），零新依赖
- 路径走 `ToolPath` → `PathPolicy`，拒绝 `..`（不 normalize）

### 5.5 Android 客户端（Kotlin + Compose，已对齐效果图 07–12、25–30）
- `LanClient`：UDP beacon + 监听 + `MessageEnvelope` 命令收发，Wire format 与 .NET **字节级兼容**
- 四个 tab 一个悬浮胶囊底栏（`OaTabBar`）：Agent / 任务 / 设备 / 设置，Agent 是启动页
- `AgentScreen`：起始页（问候语 + 状态胶囊 + 输入胶囊）与对话态同一个面
- `TasksScreen` / `DevicesScreen` / `SettingsScreen`：44px 导航条 + 20dp 圆角分组卡
- 配色/字号/圆角全部来自 `design/tokens.css`（`ui/theme/Color.kt`、`Type.kt`），
  **必须关掉 Material You 动态取色**，否则手机按壁纸取色，与效果图无关
- 图标是 `ui/Glyphs.kt` 里手绘的 24 单位网格：不引 `material-icons-extended`
  （release 没开混淆，会把整套图标全打进包）
- 效果图里的 审批态 / 思考强度 在手机上**没有数据源**：envelope 只有 `command`/`result`/`hello`
  三种文本消息，权限门在 Windows 侧，所以这两块没有画成假控件

---

## 6. 测试约定

### 测试项目清单
| 项目 | 数量 | 覆盖 |
|------|------|------|
| `OpenAgent.Core.Tests` | 36 | 事件总线、任务状态机、权限策略 |
| `OpenAgent.Providers.Tests` | 40 | Native 路由、CLI 发现、CLI 适配器 |
| `OpenAgent.Transport.Tests` | 24 | Beacon codec、Envelope codec、UDP 发现、载荷投递、inbound 事件 |
| `OpenAgent.Tools.Tests` | 56 | 11 个工具 + 路径策略 |
| `OpenAgent.Windows.UI.Tests` | 70 | CommandPlanner、ViewMapper、LongPressCounter、PlanViewMapper |
| `OpenAgent.Agent.Tests` | 15 | 代理编排 |
| `OpenAgent.Storage.Tests` | 9 | SQLite 存储 |
| `OpenAgent.Security.Tests` | 25 | 安全策略 |
| **总计** | **275** | |

### WinUI 测试陷阱
**测试项目不能带 `UseWinUI` 或 `Microsoft.WindowsAppSDK` 包引用**。否则 testhost 因 `Microsoft.TestPlatform.CoreUtilities` 加载冲突崩溃。
- 正确做法：`net10.0-windows10.0.26100.0` TFM + `win-x64` RID，**无** `UseWinUI`
- UI 库通过项目引用 transitively 到达

### 异步测试陷阱
- 不要在测试方法里用 `.Result`（xUnit1031 报错）→ 改用 `await`
- `UdpClient.ReceiveAsync` 无 `CancellationToken` 重载 → 用 `Task.WhenAny(receiveTask, delay)` 轮询

---

## 7. 设计系统

### Token 真源
`design/tokens.css`（OKLCH 格式）是唯一真源。**不要手改 `Themes/Tokens.xaml`**：
```bash
python scripts/gen-tokens.py --palette graphite
```

### 动效真源
`design/motion.md`：
- 时长阶梯：Instant 120 / State 200 / Exit 240 / Layout 320 / Enter 480 / LongPress 1200
- `Motion.Enabled` = `UISettings.AnimationsEnabled`，reduce-motion 时直接赋终值
- 只动 `transform` 和 `opacity`
- `DoubleAnimation.EasingFunction` 的参数类型是 `EasingFunctionBase`（不是 `IEasingFunction`）

**与效果图的有意偏差**：Pixso 的稿子是静帧，从不画焦点态。真机启动时 WinUI 会把焦点
放在第一个可聚焦元素上，于是设置页首行会有焦点框——这是 WCAG「焦点可见」要求的，
不为了对稿而关掉。截图对比时看到这一处差异属正常。

---

## 8. 已知坑（按出现频率排序）

### 8.1 `string.Split` CS1503（高频）
```csharp
// ❌ 编译失败
output.Split(' ', '\t', '\r', '\n', StringSplitOptions.RemoveEmptyEntries);

// ✅ 正确：显式 char 数组
output.Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
```

### 8.2 `Process.WaitForExit` CS1739
```csharp
// ❌ 无 millisecondsTimeout 命名参数
process.WaitForExit(millisecondsTimeout: 2000);

// ✅ 正确：位置参数
process.WaitForExit(2000);
```

### 8.3 CA1416 `[SupportedOSPlatform]`
调用 `[SupportedOSPlatform("windows")]` 标记的方法（如 `CliDiscovery.Scan()`）时，**测试方法也必须加 `[SupportedOSPlatform("windows")]`**。

### 8.4 `ExtractTarget` 交替 Trim bug
 `"spotify" 的` 剥掉 `"` 和 `的` 后中间空格暴露，`"` 变"内部"无法移除。
 ```csharp
 // 解法：循环 Trim（剥标点 → 剥空白 → 再剥标点直到长度不变）
 while (true) {
     var prev = rest;
     rest = rest.Trim('。', '.', '，', ',', ...).Trim();
     if (rest.Length == prev.Length) break;
 }
 ```

### 8.5 WinUI 缓动类型
`DoubleAnimation.EasingFunction` 的参数类型是 `EasingFunctionBase`（`Microsoft.UI.Xaml.Media.Animation`），**不是** `IEasingFunction`（UWP 遗留）。

### 8.6 表达式体方法后接孤儿大括号
```csharp
// ❌ CS1519
public Task ToolsAsync() => ...;
}

// ✅ 删除多余的 }
```

### 8.7 `byte[] + "\n"` 不是拼接字节
C# 中 `byte[] + string` 调用 `byte[].ToString()` 再拼接，不是 append 字节。
```csharp
// ❌ 得到 "System.Byte[]\n"
var line = LanBeaconFrame.Encode(frame) + "\n";

// ✅ 先转 string
var line = Encoding.UTF8.GetString(LanBeaconFrame.Encode(frame)) + "\n";
```

### 8.8 `app.manifest` 里的 `dpiHosting` 让 exe 根本无法启动
`<dpiHosting>` 取值只接受布尔（`true`/`false`），写成 `PerMonitorV2` 会让 SxS 清单激活失败：
```
SideBySide 事件 79：命名空间 http://schemas.microsoft.com/SMI/2020/WindowsSettings^dpiHosting 未注册
```
构建期对应 warning `81010002: Unrecognized Element "dpiHosting"`。unpackaged WinUI 3 只需要
`<dpiAwareness>PerMonitorV2</dpiAwareness>`，删掉 `dpiHosting` 即可。
**只要出现这条 warning 就当错误处理**——它不是风格提示，是产物不可运行。

### 8.9 `dotnet publish` 会丢掉 `OpenAgent.pri`
应用自己的资源索引由 Appx/Pri 目标写进 `$(TargetDir)$(ProjectPriFileName)`，不在 publish 的
`ResolvedFileToPublish` 集合里，于是发布目录缺 `.pri`，XAML 初始化即崩：
```
Application Error 1000：故障模块 Microsoft.UI.Xaml.dll，异常码 0xc000027b
```
修法是在 csproj 里把 `$(ProjectPriFullPath)` 挂进 `ComputeFilesToPublish`（已落地）。
**冒烟测试必须针对 publish 目录，而不是 `bin`**——两者文件集合不同。

### 8.10 `.properties` 文件里的单反斜杠路径会被吃掉
`java.util.Properties` 会反转义 `\`，`C:\Users\…` 变成 `C:Users…`：
- `local.properties` → AGP SDK 定位 `IOException: Invalid file path`
- `keystore.properties` → `Keystore file 'D:\...\android\app\D:WorkSpace…jks' not found`

统一写正斜杠：`sdk.dir=C:/Users/.../Sdk`。

### 8.11 Compose：`Modifier.padding` 是扩展函数
`padding` 定义在 `androidx.compose.foundation.layout`，不是 `Modifier` 的成员。全限定接收者
`androidx.compose.ui.Modifier.padding(...)` 仍会 `Unresolved reference 'padding'`——必须 import 扩展本身。

### 8.12 WiX v7 与 v4/v5 教程不兼容（写安装包必看）
网上绝大多数 WiX 示例是 v3/v4 的，v7 下会报 `unexpected child element` / `unexpected attribute`：

| v3/v4/v5 写法 | v7 写法 |
|---|---|
| `<Directory Id="TARGETDIR">` + `ProgramFiles64Folder` 手工嵌套 | `<StandardDirectory Id="ProgramFiles64Folder">`（手写 TARGETDIR 会撞 `WIX7009` 虚拟符号冲突） |
| heat.exe 收集目录 | `<Files Include="$(var.Dir)\**" />` 直接 harvest |
| `<BootstrapperApplicationRef Id="WixStandardBootstrapperApplication.HyperlinkLicense"/>` | `<BootstrapperApplication><bal:WixStandardBootstrapperApplication xmlns:bal=".../wxs/bal" Theme="hyperlinkLicense" LicenseUrl="..."/></BootstrapperApplication>` |
| `<Bundle>` 下 `<MajorUpgrade/>` | **已移除**；产品级升级交给 MSI 的 `MajorUpgrade`，Bundle 侧无对应元素 |
| `<Chain DisableModify>` / `<MsiPackage DisplayInternalUI>` | v7 不接受这两个属性 |

两个实用逃生口：
- `dotnet wix convert <v3文件>` 会把 v3 源码翻译成当前写法，是拿权威语法最快的办法。
- WiX 的 XSD 不落地，但元素词表编在 `~/.wix/extensions/**.dll` 与
  `~/.nuget/packages/wix/*/tools/net8.0/any/WixToolset.Core.Burn.dll` 里，可以用扫字符串的方式查。

扩展要先 `dotnet wix extension add -g WixToolset.BootstrapperApplications.wixext`（注意 `-g`，
`list` 不带 `-g` 是空的）；EULA：`dotnet wix eula accept wix7`。

### 8.13 显示器断电后，所有截图都是同一张死帧
显示器一断电 DWM 就停止合成：新开的窗口从来没画过（纯黑/纯白），已经开着的窗口
则停在断电那一刻——**包括 IDE 自己的窗口**，所以"别的应用还能截到内容"不能证明合成是活的。
`PrintWindow(PW_RENDERFULLCONTENT)`、WGC、桌面拷贝三条路全中，UIA 树却完全正常
（元素齐全、矩形正确），因此看起来像"应用没渲染"。
查 `Microsoft-Windows-Kernel-Power` 事件（id 566 是会话状态转换）能定位到断电时刻。
修法：截图前 `SetThreadExecutionState(ES_DISPLAY_REQUIRED)` +
`SendMessage(HWND_BROADCAST, WM_SYSCOMMAND, SC_MONITORPOWER, 1)` + 一次 VK_SHIFT 键抬起，
`scripts/capture-window.ps1` 已经内置。

### 8.14 WinUI 3 的 `Application.RequestedTheme` 改不得
运行时改（甚至在创建第一个窗口之前改）都会 fail-fast：
`0xc000027b` / WER 签名 `combase.dll` + `80040111 RPC_E_CALL_REJECTED`。
只能主题化窗口根元素，于是**代码里 `Application.Current.Resources[key]` 取到的画刷
仍然是系统配色的**——浅色外壳里选中的导航胶囊是深色，就是这么来的。
`UiBrushes` 的解法：按请求方元素的 `ActualTheme` 走一遍合并字典，
读 `{Name}Color` 字面量而不是 `{Name}Brush` 实例——非活动主题字典里的 Brush 会把它的
`{StaticResource}` 重新解析到当前活动配色上，字面量 Color 不会。

### 8.15 `TaskbarIcon.IconSource` 喂 PNG 必抛
H.NotifyIcon 把 `ImageSource` 经 GDI 转成 `Icon`，PNG 转不了：
`ArgumentException: Argument 'picture' must be a picture that can be used as a Icon`
（`StreamExtensions.ToSmallIcon` → `new Icon(stream, size)`），结果是托盘里根本没有图标。
给 `TaskbarIcon.Icon` 一个真 .ico（经典 32bpp DIB 条目，别用 PNG-in-ICO），
并且把建托盘图标整段包在 try 里——托盘失败不该赔上主窗口。

### 8.16 `dotnet test` 不编译外壳工程，绿了也可能跑着旧二进制
`dotnet test OpenAgent.sln` 只构建测试工程及其依赖，**`OpenAgent.Windows`（exe 那个工程）不在其中**。
改了 `AgentPage.xaml` 之类再跑测试，测试全绿、退出码 0，但 `bin\...\OpenAgent.dll`
还是几小时前的——于是"我已经改了"的截图对比会得出完全相反的结论。
改过外壳就必须：先 `Get-Process OpenAgent | Stop-Process -Force`（否则 MSB3027 复制失败），
再 `dotnet build OpenAgent.sln -c Release -p:Platform=x64`，然后才截图。

### 8.17 固定高度的行会把文字的下伸部剪掉
`Height="60"` 的行里放一个 56px 的 `TextBlock`（自然行高约 77），WinUI 会把元素的 arrange
高度夹到行高，而 **TextBlock 会按自己的边界裁剪**——于是 "Good night" 的 `g` 在下伸处被切平，
屏幕上读作 "Good niaht"。把 `Height` 调大没用：行（Panel row）先把子元素夹回 60。
CSS 的 `line-height` 比字形矮时是溢出绘制，WinUI 是裁剪，所以效果图上"行距 60"的写法搬不过来。
修法：用 `Canvas`（不裁剪、按子元素自身尺寸 arrange）配 `Canvas.Top` 保持 60 的行距。
效果图里所有示例文案都不带下伸部（`Good afternoon`），所以这个 bug 只在 22:00–05:00 出现，
截图验收必须覆盖到夜间分支。

### 8.18 unpackaged 应用没有 `ApplicationData`，`LocalSettings` 会静默吞掉设置
`Windows.Storage.ApplicationData.Current` 需要包身份；本工程是 `WindowsPackageType=None`，
所以每次访问都抛异常。如果像最初那样把它包在 `try { … } catch { }` 里，症状是：
设置页改主题 → 当场生效 → **重启后回到默认**，日志里一个字都没有。
（`HKCU\Software\Classes\Local Settings\Software` 下没有键、`%LOCALAPPDATA%\Packages` 下
没有本应用目录，就是这条的证据。）
现在走 `UiSettings`（`%LOCALAPPDATA%\OpenAgent\ui-settings.json`，与数据库同一目录）。
另外 `App.ApplyTheme` 只负责上色、不写设置——`--theme=` 截图开关也走它，
否则截一轮图就把用户的主题改掉了。

### 8.19 Flyout / 弹出层不能用 PrintWindow 截，也不能先挪窗口
WinUI 的 `Flyout` 挂在独立的 popup HWND 上：`list_windows` 列不出它，
对主窗口 `PrintWindow(PW_RENDERFULLCONTENT)` 截出来那块位置是一片纯色（看起来像"没打开"）。
UIA 树里同样找不到 `Slider` 节点。
截这类状态只能：真鼠标点击 → **立刻** `CopyFromScreen` 抓屏幕矩形。
任何一次 `MoveWindow`/`ShowWindow`（`capture-window.ps1` 每次都做）都会触发 light dismiss
把它关掉——先用 `-e`/UIA Invoke 试的时候，很可能它开过又被截前的挪窗关了。

### 8.20 `cmd | tail` 会把失败读成成功
`./gradlew assembleRelease -q 2>&1 | tail -8` 的退出码来自 `tail`，不是 gradle。
一次编译失败（重复 import）因此被报成 exit 0，紧接着 `adb install -r` 装的是**上一版 APK**，
后面所有测量都在量旧产物——而旧产物看起来完全正常，所以没人起疑。
构建/测试要么不接管道直接取退出码，要么 `echo "exit=${PIPESTATUS[0]}"`；
判断产物是否真的更新，看 mtime 与 sha256，不看构建器的措辞。

### 8.21 Android 没有 JVM 单测，`testReleaseUnitTest` 是空跑
`android/app/src` 下只有 `main` 与 `androidTest`，**没有 `src/test`**。
`./gradlew testReleaseUnitTest` 3 秒 `BUILD SUCCESSFUL` 是零用例的绿，别当门禁。
Android 真实的自动化覆盖只有 `androidTest` 里那两个类（AgentScreenTest / SecondaryScreensTest），
必须连设备/模拟器跑：
`adb install -r app-debug.apk && adb install -r -g app-debug-androidTest.apk && adb shell am instrument -w com.openagent.android.test/androidx.test.runner.AndroidJUnitRunner`。

### 8.22 门禁必须能失败，否则它就是假绿
写校验脚本时最容易犯的错：只 `print` 差异，`main()` 忘了 `return`，
`sys.exit(None)` → 永远 0；调用方拿这个 0 去判断"通过"，于是门禁形同不存在，
还比没有更糟——它看起来像证据。
**每个新门禁都要跑一次"故意错"的输入确认它返回非 0**，并且正反两次都不要经过管道。
本项目现例：`scripts/ui-colour-audit.py` 正确配对退 0（最大 ΔE 0.0），
把浅色效果图配深色截图就退 1（最大 ΔE 422.1）。
另注：`scripts/ui-semantic-colour-audit.py` 是**报告**不是门禁——抗锯齿色调会让
正确配对也留下约 4 条未匹配，真错也退 1，所以不能用它的退出码做判断。

### 8.23 给 PowerShell 传 MSYS 路径会静默失败
`bash` 里的 `$ROOT` 长成 `/d/WorkSpace/OpenAgent`，PowerShell **认不得**这种路径：
`Start-Process -FilePath '/d/...'` 直接失败，而脚本若把它的输出丢进 `/dev/null`，
表现就是"什么都没发生但流程继续"。跨界传路径一律先 `cygpath -w`，
`-File` 参数同理（要写 `D:\...\scripts\capture-window.ps1`）。

### 8.24 截图类脚本必须断言"这次真的写出了文件"
`ui-verify.sh` 第一版把启动与截图的输出都吞掉了，上面那条路径错误导致**一张都没拍**，
但它照样打印 "captures regenerated" 并退 0——审计读的是上一轮留下的旧图。
现在每拍一张都比对 `stat -c %Y` 与本次起始时间，没被重写就 `FAILED` 并退 1；
反向也验过（把 exe 路径改坏 → `FAILED: ... was not rewritten by this run`，exit 1）。

### 8.25 逐行扫描只能断言"上边界"，不能断言"下边界"
`ui-band-sweep.py` 把侧栏按行切成墨迹带（band），第一版对每条带同时断言起点和终点，
12/14 张直接失败——差的是 4px 的**带高**。逐像素打样才看清：同一行标签在效果图里墨到
`y=215`，在成品里墨到 `y=211`，而**相邻两行的间距两边都是 29px**（319/348 对 317/346）。
带高由字形下伸部决定，是字体光栅化的产物，任何边距常量都挪不动它；带上边界才是布局。
现在只断言起点（容差 4px，实测最差 3px），终点只报数不断言。
同一原因也让一条效果图带在成品里被切成两条（行内 1px 空隙），所以"多出来的带"只记 note。

另一条只能靠打样发现的：**30 张效果图自己就不一致**——账号行上方那条分隔线在 01 里画在
`y=759`，在 13/14/15/16/17/18 里根本没画，而外壳每张都画。这种差异绝不许去"对齐某一张"，
门禁的口径因此是"画了的都在不在该在的位置"，而不是"两边逐条相等"。

门禁有效性两向验过：把 `cur-agent-light.png` 侧栏头部整体下移 8px → exit 1 并点名 5 条带；
拿外壳对齐前的旧图（`now-dashboard.png`、`dbg-settings.png`）当成品喂进去 → 15/17 条 FAIL。

### 8.26 per-machine MSI 静默安装拿不到提权：1603 只是表象
`msiexec /i xxx.msi /qn` 在非管理员 shell 里返回 **1603**（"fatal error during installation"），
什么信息都不给。加 `/L*v log.txt` 才看得到真正那条：
`MSI_LUA: Installation UI level is silent, no credential elevation is possible` →
`Error 1925. You do not have sufficient privileges to complete this installation for all users of the machine.`
静默模式**不能**弹凭据框，所以这一步要么由用户批准一次交互式 UAC，要么就如实登记为未验，
不能因为"MSI 构建成功"就声称装过。日志是 **UTF-16LE**，Git Bash 里 `grep` 读不出东西，
用 `open(path, encoding="utf-16")` 读。回滚是干净的（安装目录与卸载注册表都不留残迹），
但每次跑完都要自己确认一遍。

### 8.27 应用还在跑就 `dotnet publish`，会被文件锁打死
截图/驱动 UI 的实例没关，publish 复制 `OpenAgent.dll` 时撞 `MSB3026` 重试，
最终 `error MSB3027`（"文件被 OpenAgent (pid) 锁定"）。日志前 10 行都是 warning，
`tail` 一眼看不到错误，容易误判成"编译过了只是没打出来"。
所以 publish 前先 `Stop-Process -Name OpenAgent -Force`，publish 的退出码单独取
（别接管道），再 `ls -la` 看产物 mtime 确认真的重出了。

---

## 9. 数据与安全

- 数据目录：`%LOCALAPPDATA%\OpenAgent`
- API Key：**走 OS 安全存储**，绝不写入 SQLite
- 当前 LAN beacon 和消息信封是**明文**，无配对/信任/加密（Phase 6–7 解决）

---

## 10. 未实现清单（真实 `NOT IMPLEMENTED`）

| 领域 | 缺失项 | 计划阶段 |
|------|--------|----------|
| Transport | mDNS 发现、Cloudflare Relay、LAN 配对/信任/加密 | Phase 6–7 |
| Providers | CLI 流式/多轮会话 | Phase 4+ |
| Agent | Native 真模型循环（当前是确定性关键词路由） | Phase 3 |
| Plugins | 加载器、manifest、隔离 | Phase 12 |
| MCP | 客户端/服务端桥接 | Phase 11 |
| File Transfer | 流式/分片/暂停/恢复 | Phase 9 |
| Remote Screen | 屏幕捕获/H264/触摸/键盘 | Phase 10 |

---

## 11. 推进建议（优先级）

v1.0.0 已作为首个发布版上线（Windows zip + Android APK）。接下来的优先级：

1. **LAN 配对 + 加密**（让"跨设备"真正安全可信）
2. **Cloudflare Relay**（让不在同一局域网的两端互通）
3. **Agent 真模型循环**（把 Native 关键词路由换成真实模型推理）
4. **真机互操作验证**（APK 已构建、签名，并在 API 36 模拟器上跑通界面与导航；模拟器在 NAT 后收不到局域网广播，所以 beacon↔envelope 仍需一台同 Wi-Fi 的真机确认）

> 注：原第 4 条"Android APK 构建验证"已完成——工具链齐备，且构建暴露出一个真实编译错误（§8.11）。

---

## 11.1 发布流程（v1.0.0 起）

```bash
# 1. 全绿门禁
dotnet build OpenAgent.sln -c Release -p:Platform=x64
dotnet test  OpenAgent.sln -c Release -p:Platform=x64     # 275
bash scripts/ui-verify.sh        # 从 publish 目录重拍 7 页 × 2 色，核对地标色 + 断言侧栏外壳几何
#                                （能失败才算门禁，见 §8.22；几何口径见 §8.25）
#    → Android 侧另跑：python scripts/ui-android-audit.py
#      需要 artifacts/shots/raw-<page>-<theme>.png（adb exec-out screencap，**不要缩放**）；
#      它查内容边距、卡片发丝描边、主色存在性，薄色只在原始分辨率上可判。

# 2. Windows 安装包
dotnet publish src/apps/windows/OpenAgent.Windows/OpenAgent.Windows.csproj \
  -c Release -p:Platform=x64 -r win-x64 --self-contained true -o artifacts/windows/win-x64
#    → 校验 artifacts/windows/win-x64/OpenAgent.pri 存在，且 exe 能起窗口
dotnet tool restore && ./scripts/build-installer.ps1 -SkipPublish
#    → artifacts/installer/OpenAgent-<ver>-x64.{msi,exe}
#    → 装一遍再卸一遍：msiexec /i ... /qn，起窗口，msiexec /x ... /qn
python scripts/verify-installer-payload.py
#    → 用 msiexec /a 解出管理镜像，与验收过的 publish 目录逐文件比 SHA-256
#      （以前只比"581 个文件"这种计数，改名/陈旧载荷能在计数相同的情况下蒙混过关）

# 3. Android 产物
cd android && JAVA_HOME="<AndroidStudio>/jbr" ./gradlew assembleDebug assembleRelease
#    → apksigner verify --print-certs app/build/outputs/apk/release/app-release.apk

# 4. 发布
gh release create v1.0.0 <win-msi> <win-exe> <release-apk> --title ... --notes ...
```

**发布前必须做的三件事**：冒烟测试跑 publish 目录（不是 `bin`）；安装包**真的装一次再卸一次**；
`git grep` 扫一遍 token / 私钥 / 本机绝对路径。

---

## 12. 关键文件速查

| 文件 | 作用 |
|------|------|
| `OpenAgent 完整项目总提示词.md` | 规格真源（366 节） |
| `docs/protocol.md` | 跨设备线格式（beacon + envelope） |
| `docs/dev-log.md` | 开发日志（按提交记录） |
| `Directory.Build.props` | 全局版本/分析器（当前 Version=1.0.0） |
| `src/apps/windows/OpenAgent.Windows/App.xaml.cs` | DI 组合根 |
| `src/core/OpenAgent.Transport/UdpLanTransport.cs` | LAN 传输 |
| `src/providers/OpenAgent.Providers/ProviderServiceRegistration.cs` | Provider DI |
| `design/tokens.css` | 设计 token 真源 |
| `design/motion.md` | 动效真源 |
| `android/app/src/.../data/LanClient.kt` | Android 网络核心 |

---

*本文档最后更新：2026-09-27，对应 v1.0.0 首个发布版。文中所有数字（275 测试、8 测试工程、12 产品工程）与工具链结论均为本机实测，不是转抄。*
