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

# 测试（当前 280 个，全绿）
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
| `OpenAgent.Windows.UI.Tests` | 75 | CommandPlanner、ViewMapper、LongPressCounter、PlanViewMapper |
| `OpenAgent.Agent.Tests` | 15 | 代理编排 |
| `OpenAgent.Storage.Tests` | 9 | SQLite 存储 |
| `OpenAgent.Security.Tests` | 25 | 安全策略 |
| **总计** | **280** | |

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

还有一类更隐蔽的：退出码**来自一个不传递测试结论的命令**。`adb shell am instrument`
无论用例过没过都返回 0，所以 `android-shot-test.sh` 早期版本在断言真的失败
（`Tests run: 1,  Failures: 1`）时照样退 0，还把失败帧缩放成交付物。
现在判定改读 `artifacts/instrument.log`（`FAILURES!!!` / `Failures: N` / `Errors: N` /
`INSTRUMENTATION_FAILED` 一律退 1，缺 `OK (` 也退 1），外加"本次真的重写了 PNG"的 mtime 断言；
正反两支都验过（通过日志放行、逐字复制的失败日志触发、陈旧 mtime 触发）。

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

### 8.26 内容列的扫描窗口有三条"看不见的墙"
把同一套逐行扫描从侧栏搬到内容列（页头：标题/副标题/分隔线/分组标题/卡片上沿）时，
三连误报全出在仪器上，逐条量完才知道：
1. **滚动条**：它的轨道每一行都有墨迹，窗口右边界不避开就把整个页头并成一条带
   （`x1 = w - 160`）；
2. **卡片左右描边**：跨着描边扫会把"卡片上沿"和"第一行文字"并成一条（改成在描边内侧扫）；
3. **两边画的不是同一张卡**：效果图 17 有一张已安装插件卡，成品一个插件都没装、画的是空态卡——
   于是"缺一条带"比的根本不是同一个东西。窗口停在 `y=205`：过卡片上沿，不过卡片内容。
另外，旧代码把 head 窗口的下沿写成 `h - 200`（=700）而不是 `200`，一直多扫了整段导航区。
窗口现在统一写成 `(label, x0, x1, refx0, refx1, y0, y1)`，`x1` 负数=距右边界偏移，别再靠减法。

### 8.27 XAML 里声明了却没人赋值的 TextBlock = 永久空白行
`DevicesPage.xaml` 有 `x:Name="StatsText"`（`PageSubtitle` 样式），code-behind 从头到尾没提过它，
于是页头少一行、还占着位。查法：把 views 里的 `x:Name` 与同名 `.xaml.cs` 里的引用对一遍
（`grep -c` 求和），0 引用的就是要找的。补文案时只用页面真的知道的事实——效果图那行写
"2 台已配对"，配对还不存在，所以写 `N 台设备 · M 台在线`。

### 8.28 per-machine MSI 静默安装拿不到提权：1603 只是表象
`msiexec /i xxx.msi /qn` 在非管理员 shell 里返回 **1603**（"fatal error during installation"），
什么信息都不给。加 `/L*v log.txt` 才看得到真正那条：
`MSI_LUA: Installation UI level is silent, no credential elevation is possible` →
`Error 1925. You do not have sufficient privileges to complete this installation for all users of the machine.`
静默模式**不能**弹凭据框，所以这一步要么由用户批准一次交互式 UAC，要么就如实登记为未验，
不能因为"MSI 构建成功"就声称装过。日志是 **UTF-16LE**，Git Bash 里 `grep` 读不出东西，
用 `open(path, encoding="utf-16")` 读。回滚是干净的（安装目录与卸载注册表都不留残迹），
但每次跑完都要自己确认一遍。

### 8.29 应用还在跑就 `dotnet publish`，会被文件锁打死
截图/驱动 UI 的实例没关，publish 复制 `OpenAgent.dll` 时撞 `MSB3026` 重试，
最终 `error MSB3027`（"文件被 OpenAgent (pid) 锁定"）。日志前 10 行都是 warning，
`tail` 一眼看不到错误，容易误判成"编译过了只是没打出来"。
所以 publish 前先 `Stop-Process -Name OpenAgent -Force`，publish 的退出码单独取
（别接管道），再 `ls -la` 看产物 mtime 确认真的重出了。

### 8.30 驱动真实窗口截图：抢前台与传中文都有坑
`scripts/ui-state-verify.sh` 要在无人工点击下把成品驱动到对话态/审批态再截图，两条硬坑：
1. **`SetForegroundWindow` 会被拒**。Windows 只允许"已经持有前台"的进程改前台，杀掉上一个
   同名实例就足以失去这个资格——第一次循环成功、第二次就失败。解法：先 `keybd_event(VK_MENU,
   KEYEVENTF_KEYUP)` 敲一下 ALT（官方认可的"我收到输入了"信号），再 `SetForegroundWindow`，
   失败就退到 `Microsoft.VisualBasic.Interaction.AppActivate(pid)`，并**用
   `GetForegroundWindow` 复核**；不复核就是把字打进了 IDE，而截图脚本照样产出一张新图。
2. **中文别走命令行**。`.ps1` 无 BOM 时 PowerShell 5.1 按 ANSI 读，Git Bash 的 heredoc 也会
   先把 CJK 打烂。提示词以**十进制码点**列表传进脚本，脚本里 `[char]` 还原。
   （`-File` 调用时所有参数都是**单个字符串**：形参不能声明成 `[int[]]`，要在脚本内 `Split(',')`。）
3. 驱动态的截图必须断言"真的进入那个态了"：与同主题的起始页比内容列像素差（本例 1.9–2.2%），
   审批态再断言橙色卡框存在。否则驱动失败 = 拍到起始页，mtime 却是新的，门禁照样放行。

### 8.31 卡片检测两条铁律：带宽 < 页宽、描边要成"线"
`ui-android-audit.py` 靠"离卡片填充色 ≤6 的像素带"找卡片，两个坑都会让门禁**静默跳过**（跳过还打印 ok，最坏的那种绿）：
1. **整页宽的同色带会被当成卡片**。浅色主题里系统栏正好落在 `(247,247,250)±6`，于是"最左填充像素"
   =0，`min(lefts) >= 8` 的守卫判定"这页没卡片"，边距和描边两项检查一起跳过。规则：**卡片是比页窄的带**
   （`run.max()-run.min() < 0.95 * width`）。
2. **一个描边像素不是一条边**。旧实现只要"某行填充最左点 ±4px 内有一个发丝色像素"就返回 true——
   把卡片左边框整条擦干净仍然通过，因为每行都还能命中它自己的**上边框**。规则：在某一列上数行数，
   要求 ≥40 行同色（真边框是一条竖线）；报告的颜色取该列匹配像素的**众数**，不是中间那行的像素
   （中间行常落在抗锯齿上，会报成 `(241,241,244)` 而真值是 `(229,229,234)`）。
两条都用"故意做错"的图验过：抹掉左框 → `FAIL card hairline: None`、exit 1；还原 → exit 0。

### 8.32 WinUI 的焦点框：`IsTabStop=false` 挡不住它，`FocusVisual` 又改不了
`SettingRow` 是 `UserControl` 包一个 `Button`，所以**每一行都是可聚焦控件**——包括那五行根本没有 `Click`
处理器的。窗口激活时若没有任何元素持有焦点，WinUI 把焦点给第一个可聚焦元素并**画上 2px 焦点框**
（浅色 `(26,26,26)`、深色近白），效果图 18/24 那里什么都没有。三个坑：
1. `RootButton.IsTabStop = false` 只挡 Tab 遍历，**不挡激活焦点**——框照样在。
2. WinUI 3 的 `Control.FocusVisual` 不是可写属性（`CS1061: "Button"未包含"FocusVisual"的定义`），
   没法从控件上把 adorner 摘掉。
3. 所以截图口径改成：`capture-window.ps1` 在真正抓图前**点一下惰性区域**，把这份瞬时焦点挪走，
   再把指针停到窗口外（原有的悬停规避）。**点位必须对七个页面全都惰性**：最早用 client
   `(700,100)`，任务列表一旦有真实数据，第一张卡片就顶到 y=69，这一点击中了任务行、
   弹出它的详情卡，并且把列表滚动到被聚焦的那一项——于是门禁报的是"页头没画"，
   而截图里根本是一个弹窗。现在点 `(120,500)`：侧栏最后一个导航项（插件 ~423）与
   设置（~788）之间的空档，哪一页都不会触发任何东西，顺带把上一轮遗留的 light-dismiss
   浮层关掉。
新增 `scripts/ui-focus-ring-audit.py` 当门禁：任何一张截图带 adorner 就退 1。它自己也被"故意做错"验过
——第一次验证明显失败（numpy 广播报错，图根本没改），**报 0 不等于能看见**，重画一个矩形才确认它能报
`RING settings/light`。

### 8.33 WinUI 3 里根本没有"把阴影挂上去"这一步，且深底会把叠加吃掉
效果图 01/02 的输入条外面有一圈 `--shadow-float` 软光晕（全应用只有它有：设置卡、审批卡
实测都是平的）。发布版以前完全不画，逐行采样：白底上边框下 20/255、侧面 10、上方 4，
分别在 16/12/7px 内衰减到 0；深色底（10）下是 3/2/1。

四条"正统"路子全部**由编译器**判死（不是文档、不是猜测）：
1. `ThemeShadow` 在 WinUI 3 没有 `Receiver` / `SetReceiver`（UWP 才有），找不到投影面就等于没有阴影。
2. `UIElement.Shadow` 的声明类型是 `Microsoft.UI.Xaml.Media.Shadow`（`ThemeShadow` 的基类），
   把 `Compositor.CreateDropShadow()` 的结果赋给它 → `CS0029`。注意 **`CreateDropShadow()` 本身是存在的**。
3. `Compositor.CreateShadowCollection()`、`Visual.Shadows`、`Visual.Shadow`、`ContainerVisual.Shadow`
   四个挂载点全部 `CS1061`。
4. XAML 里也没有 `DropShadow` 这个可实例化类型。

改用 10 个同心胶囊 `Border` 叠在输入条下面（`AgentPage.xaml` 的 `ComposerHalo`）：每层一份
`ComposerHaloBrush`，**能盖到某条边的层数就是那条边的浓度**——10 层压到下边、5 层压到侧面、
2 层压到上边，正好复现三条边的峰值。层的几何按效果图逐行采样反量化得到。

再一个坑：**每层的 alpha 不能两个主题共用**。WinUI 每合一层就把 8bit 通道四舍五入一次，
深色底 `round(10 × (1-0.031)) = round(9.69) = 10`，十层叠完一层推进都没有；浅色底
`255 × (1-0.0078) = 253` 不受这一步影响。所以 `--composer-halo` 在明暗两块里分别是
`0.0078`（→ `#02000000`）和 `0.0667`（→ `#11000000`），各自沿本主题的取整链反推。
代价是深色侧面只能是 3 而不是效果图的 1——10 级底上只有 3 个量化级，叠不出更细的台阶。

门禁 `scripts/ui-halo-gate.py`（已并入 `ui-verify.sh`，共五条）把明暗 × 三条边共 6 条衰减曲线
逐像素比一遍，容差 2 级；把光晕抹平重跑会报 3 条 FAIL 并退 1，确认它看得见"没有阴影"这件事。

顺带两条踩过的：XAML 注释里不能出现 `--`（`<!-- --shadow-float` 直接是畸形 XML，注释开头写
token 名就会中）；而 C# 编译报错时 XAML 编译器会连带吐一条
`WMC9999 未将对象引用设置到对象的实例`，它是噪声不是第二个故障，只认 `error CS` 那几行。

### 8.34 手机端同一个光晕：`Modifier.shadow` 能用，但只有 4 个参数可调，且有一条边归系统
效果图 07/08 上，发布版 APK 缺的不止光晕：输入条描边用错了 token（`borderSubtle` #E5E5EA，
设计是 `borderDefault` #D1D1D6），底部标签栏设计里有 1dp `borderSubtle` 描边而构建里完全没有。
描边是一行改动；光晕要 4 轮"改参数→装 APK→截图→逐 dp 采样"才收敛，因为 Compose 只暴露
elevation 和一个颜色，elevation 到衰减曲线的映射没有任何文档：

| elevation · alpha | 峰值 | 长度 |
|---|---|---|
| 8 · 0.078 | 3 | 17dp（糊成一片） |
| 6 · 0.30 | 16 | 14dp |
| 4.2 · 0.33 | 17 | 11dp（头对、尾巴短） |
| 5.5 · 0.42 | 20 | 12dp ✓ |

两个天花板要认，别装作能修：
1. **深色只能到 4 级里的 2 级**——`shadowAlpha` 已经是 1.0。单位 alpha 的高斯要摊到 12dp，
   边缘就不可能留 40% 覆盖率；压低 elevation 能换回峰值但尾巴立刻缩掉。Windows 那边是叠加
   胶囊、不是高斯，所以 3 级能拿满。
2. **标签栏那条光晕根本画不出来**：它的衰减落在导航栏 inset 里，而应用整体在
   `windowInsetsPadding(navigationBars)` 内绘制，那一带由系统合成在上面。实测标签栏下边缘
   之外是 `(255,255,255)` / 深色 `(17,18,22)`——都不是应用画布。要画就得让标签栏浮到系统导航
   栏底下，那会动其他所有门禁钉住的位置。

门禁 `scripts/ui-halo-gate.py` 现在两端共用（Windows 6 条曲线 + Android 4 条 + 两处描边），
容差按平台分别是 2 和 3，都是量出来的不是取的整数。它第一版**一直在量标签栏而不是输入条**：
定位函数取"最长的一段表面色"，而标签栏 60dp 比输入条 52dp 长，于是它对着一个从没看过的控件
报了 ok。改成取最靠上的一段；采样也封顶 12dp——第 13 个样本会撞到标签栏自己的描边。

### 8.35 每一行的"高度"要把分隔线那 1px 算进去，否则整页会累积漂移
效果图量的行距是**含分隔线**的：工具 42、Provider 46、任务 48、设置 42。
构建里模板把内容声明成这些数字，再另外加 1px 分隔 → 实际 43/47/49/44。
设置页最严重（`SettingRow(43)` + 独立分隔元素 = 44），四张卡片到第四张已经比效果图低 12px。

改法是把那 1px 从布局里拿掉，而不是把数字改成 41/45/47 之外的"好看的数"：
- `ToolRow` 42→41、Provider 行 46→45、任务行 48→47（`RowList` 的 item 自带 1px 上边框）；
- `SettingRow` 43→42，并且 `RowDivider` 加 `Margin="0,0,0,-1"`——分隔线画在两行的公共像素上，
  不再额外占一行；
- `SectionHeading` 上边距 24→23（卡片下边框到下一个小标题的墨迹是 28 行，不是 29）。

**看到 41/45/47 别改回去**：设计里的 42/46/48 是"行距"，不是"内容高度"。

同一条规则在安卓端错得更狠：`ValueRow` 声明 46dp 再另加 1dp 分隔 → 实测行距 47.7，
而效果图 26/27 的卡片两行 border-to-border 是 77，即 38.5。现在 `Shape.rowHeight = 38.dp`，
实测 39.9。

改完后设置页（效果图 18/24）在两端主题下**结构差异为 0**，是全应用唯一做到的页面。

顺带记一个只报不判的工具：`scripts/ui-hotspot-sweep.py`。它把整帧差异过滤成"两边都局部平坦
却仍然不同"的像素——把字形抗锯齿和效果图里的假数据全部洗掉，只剩表面色、描边、间距、范围在
 disagree。输入条光晕和这个 44px 行距都是它找出来的。它**永远退 0**：没有任何阈值能把
"效果图有、我们故意不实现的数据"和"表面真的漂了"分开（设备页那 76,810px 就是效果图里那台
已配对设备 vs 构建里的空态），所以它是诊断不是门禁。

它也在设置页报出 437px 的起始页残差——那是问候语本身：效果图写 "afternoon"，构建在 01:47
截图时写 "morning"，字体、字重、位置全一致。这种"差异"是对的。

### 8.36 截图脚本会写真实用户状态，于是它自己把自己变得不可复现
`ui-verify.sh` / `ui-state-verify.sh` 启动的是**真实安装包**，读的是
`%LOCALAPPDATA%\OpenAgent`。跑久了那个库里堆着 50 条任务、25 轮对话——全是这些验证自己攒出来的。
后果有两个，都伪装成 UI 故障：

1. 页面不再从效果图那个状态打开（列表滚动过、卡片顶到 y=69），于是 §8.32 里那个"每页都惰性"的
   `(700,100)` 点击点中了任务行，弹出详情卡并把列表滚到聚焦项，门禁报"页头没画"。
2. `approval/dark` 单独跑必过、跟在整条流水线后面就不过——因为驱动拿到的已经不是干净会话。

修法是在源头隔离：`OpenAgent.App` 新增 `--data=<dir>`（映射到 `OpenAgentOptions.DatabaseRoot`，
为空则仍用默认目录），`ui-state-verify.sh` 每次驱动都用
`artifacts/state-sandbox/<state>-<theme>` 这个一次性目录并在结束后删掉。
现在连跑两遍 `ui-verify.sh` 判定完全一致（审批卡描边 22 / 15 行，五项审计全绿）。

**规则：任何会启动真实应用并留下持久状态的脚本，都必须自带一次性数据目录**；
否则它的失败看起来永远像 UI 回归。同理，"安全"的点击点位要对**有真实数据的应用**安全，
不是对你开发时那个空库安全。

手机端另两处：
- `--data` 之外，输入条到标签栏的 12dp 被拆在两个地方（`AgentScreenContent` 的 `Spacer(10)`
  与 `MainActivity` NavHost 全局的 `padding(bottom=8)`），合起来 18。现在 `Shape.barGap=4`
  显式表达这 12，门禁按两端主题各断言一次。
- 找标签栏描边那条线的容差只能是 2 不能是 6：输入条自己的光晕在边框外第一像素就是 235，
  而浅色描边是 229 —— 放宽就会匹配到光晕上，报出 0dp 的间距还一路 ok。

未改的一项：二级页的页面标题墨迹比效果图高 2-3px，块内间距是对的（标题→细线 56 vs 55），
差的是 `Segoe UI Variable Display` 30px 的行盒 leading。用 padding 去补等于把字体度量的
偶然结果写进间距 token，所以和 DM Sans、CJK 替换归到一类，只记录不动。

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
dotnet test  OpenAgent.sln -c Release -p:Platform=x64     # 280
bash scripts/ui-verify.sh        # 从 publish 目录重拍 7 页 × 2 色，核对地标色 + 断言侧栏/页头几何
#                                + 驱动并核对对话态/审批态（效果图 03–06，见 §8.30）
#                                （能失败才算门禁，见 §8.22；几何口径见 §8.25/§8.26）
#   SKIP_STATES=1 跳过驱动态（要抢前台）；SKIP_SHOTS=1 只审计已有截图
#    → Android 侧另跑：bash scripts/ui-verify-android.sh
#      装的是 **release APK**（发布产物，签名 `fdb10835…`，`isMinifyEnabled=false`），
#      主题用模拟器的 `cmd uimode night yes|no` 切（release 不可调试，写不了 SharedPreferences）。
#      每个路由写两张图：`raw-<route>-<theme>.png`（原始 1080x2400，审计读它）和
#      `android-<route>-<theme>.png`（390x844，人眼看）。**不要就地缩放**——1px 描边会被抹掉，
#      审计会"通过"在一条糊掉的红线上（见 §8.31）。
#      模拟器到不了的状态（无主机 → 无设备卡/任务行/对话）由 Compose 测试 hold 住拍帧，
#      `scripts/android-shot-test.sh <out> <class#method>`，raw 与缩放两张同样都留。

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

*本文档最后更新：2026-09-27，对应 v1.0.0 首个发布版。文中所有数字（280 测试、8 测试工程、12 产品工程）与工具链结论均为本机实测，不是转抄。*
