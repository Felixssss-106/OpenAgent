# AGENTS.md — OpenAgent 项目交接指南

> 本文档面向接手 OpenAgent 项目的 AI 智能体或人类开发者。它记录项目真实状态、架构约束、已踩的坑和推进路径，**优先于任何泛泛的猜测**。

---

## 1. 项目是什么

**OpenAgent** — Windows × Android 开源 AI 跨设备控制中心。
- **Windows 端**：.NET 10 / C# / WinUI 3（unpackaged），12 个产品工程 + 5 个测试工程
- **Android 端**：Kotlin / Jetpack Compose / Material 3，`android/` Gradle 项目
- **当前版本**：v1.0.0（tag `v1.0.0`，提交 `133e9ff`）
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

# 测试（当前 259 个，全绿）
dotnet test OpenAgent.sln -c Release -p:Platform=x64
```

### Android（Kotlin）
```bash
cd android
./gradlew :app:assembleDebug
```
> ⚠️ 当前沙盒**无 Android SDK**，APK 需在 Android Studio 中构建。

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
├── tests/                     # 5 个测试工程（Core / Providers / Transport / Tools / UI）
├── android/                   # Kotlin + Jetpack Compose 客户端
├── docs/
│   ├── protocol.md            # 跨设备线格式真源（beacon + JSON envelope）
│   ├── dev-log.md             # 开发日志（按提交记录）
│   └── motion.md              # 动效规格（时长/缓动/reduce-motion）
├── design/
│   ├── tokens.css             # 设计 token 真源（OKLCH）
│   └── motion.md              # 动效真源
└── scripts/
    ├── gen-tokens.py          # CSS → Themes/Tokens.xaml（不要手改 xaml）
    └── gen-tray-icon.py       # 托盘图标生成
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
- `CommandCenterWindow`：Alt+Space 唤起，真实链路 plan→execute→审批卡
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
| `OpenAgent.Core.Tests` | 25 | 事件总线、任务状态机、权限策略 |
| `OpenAgent.Providers.Tests` | 40 | Native 路由、CLI 发现、CLI 适配器 |
| `OpenAgent.Transport.Tests` | 24 | Beacon codec、Envelope codec、UDP 发现、载荷投递、inbound 事件 |
| `OpenAgent.Tools.Tests` | 56 | 11 个工具 + 路径策略 |
| `OpenAgent.Windows.UI.Tests` | 61 | CommandPlanner、ViewMapper、LongPressCounter |
| `OpenAgent.Agent.Tests` | 15 | 代理编排 |
| `OpenAgent.Storage.Tests` | 9 | SQLite 存储 |
| `OpenAgent.Security.Tests` | 25 | 安全策略 |
| **总计** | **259** | |

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

1. **LAN 配对 + 加密**（让"跨设备"真正安全可信）
2. **Cloudflare Relay**（让不在同一局域网的两端互通）
3. **Agent 真模型循环**（把 Native 关键词路由换成真实模型推理）
4. **Android APK 构建验证**（在 Android Studio 中构建并测试与 Windows 的互操作）

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

*本文档最后更新：2026-09-26，对应提交 `133e9ff`（v1.0.0）*
