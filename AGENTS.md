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

# 测试（当前 266 个，全绿）
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

### 5.5 Android 客户端（Phase 8 seed，提交 `133e9ff`）
- `LanClient`：UDP beacon + 监听 + `MessageEnvelope` 命令收发
- `DevicesScreen`：发现列表 → 点进聊天
- `AgentChatScreen`：命令/结果气泡
- `SettingsScreen`：设备标识 / 端口 / 系统信息
- Wire format 与 .NET **字节级兼容**

---

## 6. 测试约定

### 测试项目清单
| 项目 | 数量 | 覆盖 |
|------|------|------|
| `OpenAgent.Core.Tests` | 36 | 事件总线、任务状态机、权限策略 |
| `OpenAgent.Providers.Tests` | 40 | Native 路由、CLI 发现、CLI 适配器 |
| `OpenAgent.Transport.Tests` | 24 | Beacon codec、Envelope codec、UDP 发现、载荷投递、inbound 事件 |
| `OpenAgent.Tools.Tests` | 56 | 11 个工具 + 路径策略 |
| `OpenAgent.Windows.UI.Tests` | 61 | CommandPlanner、ViewMapper、LongPressCounter |
| `OpenAgent.Agent.Tests` | 15 | 代理编排 |
| `OpenAgent.Storage.Tests` | 9 | SQLite 存储 |
| `OpenAgent.Security.Tests` | 25 | 安全策略 |
| **总计** | **266** | |

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
dotnet test  OpenAgent.sln -c Release -p:Platform=x64     # 266

# 2. Windows 安装包
dotnet publish src/apps/windows/OpenAgent.Windows/OpenAgent.Windows.csproj \
  -c Release -p:Platform=x64 -r win-x64 --self-contained true -o artifacts/windows/win-x64
#    → 校验 artifacts/windows/win-x64/OpenAgent.pri 存在，且 exe 能起窗口
dotnet tool restore && ./scripts/build-installer.ps1 -SkipPublish
#    → artifacts/installer/OpenAgent-<ver>-x64.{msi,exe}
#    → 装一遍再卸一遍：msiexec /i ... /qn，起窗口，msiexec /x ... /qn

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

*本文档最后更新：2026-09-26，对应 v1.0.0 首个发布版。文中所有数字（266 测试、8 测试工程、12 产品工程）与工具链结论均为本机实测，不是转抄。*
