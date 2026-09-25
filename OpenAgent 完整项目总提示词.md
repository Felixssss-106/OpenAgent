# OpenAgent

## Windows × Android 开源 AI 跨设备控制中心

## 完整项目总提示词 / Product Specification / Technical Architecture / Development Contract

---

# 零、强制前置要求（最高优先级，任何阶段不得绕过）

> 本章优先级高于本文件其余全部章节。若本章与后文任何条款冲突，以本章为准。

## 0.1 进行任何 UI 开发前，必须先阅读 Apple Human Interface Guidelines

在**任何 UI 界面开发**之前——包括但不限于新增页面、修改布局、调整配色、
设计或改动组件、编写 WinUI 3 / XAML 或 Jetpack Compose 界面代码——
必须执行以下动作：

1. **先访问并详细阅读 https://developer.apple.com/cn/design/human-interface-guidelines/**

   至少覆盖与本次任务直接相关的章节，例如：

   ```text
   设计基础：Color / Layout / Typography / Materials / Accessibility / App icons
   组件：    Lists and tables / Tab bars / Toolbars / Sidebars / Buttons
             Menus / Search fields / Split views / Disclosure controls
   输入：    Keyboards / Pointing devices / Gestures
   技术：    Liquid Glass / Dark Mode
   ```

2. **在实现说明中引用所依据的具体条款**（章节名 + 要点），不得凭印象或"惯例"设计。

3. **该章节有配图时，需结合配图理解**。例如：
   - 标签页栏的选中态（Liquid Glass 胶囊高亮）
   - 工具栏的返回按钮形态（圆形 + 细描边）
   - 列表的内嵌分组样式（分组标签 + 圆角容器 + 内缩分隔线）

4. 若线上文档与本机已有实现不一致，**以线上 HIG 为准**，并在
   `docs/dev-log.md` 记录差异与决策。

**这条要求优先于任何"我觉得更好看""业界都这么做"的个人判断。**

## 0.2 UI 设计标准以本仓库 `design/` 目录为唯一准绳

**`design/` 目录是 OpenAgent 全部 UI 开发的唯一设计标准与参考依据。**

```text
D:\WorkSpace\OpenAgent\
├── .impeccable.md                  设计上下文（受众 / 使用场景 / 品牌人格 / 硬约束）
├── Apple\                          Apple 风格设计系统（Pinguo，设计 token 的来源）
└── design\
    ├── tokens.css                  设计 token：3 套 OKLCH 色板 × 明暗双主题
    │                               + 字号阶梯 / 间距 / 圆角 / 动效 / 层级
    ├── directions.html             5 个 UI 方向对比稿（含最终方向选定依据）
    └── pixso-final\
        ├── index.html          ★  30 张设计稿总览（动手前先看这里）
        ├── manifest.json           画板清单 + Pixso 画板 ID 映射 + 关键设计决策
        └── 01~30-*.png             30 张原生尺寸设计稿
```

**强制规则**：

- 新增或修改任何界面，**必须先打开 `design/pixso-final/index.html`**，
  找到对应画板并逐项对照，保持布局、圆角、间距、字重、配色完全一致。
- **颜色 / 字号 / 圆角 / 间距只能取自 `design/tokens.css`**，不得硬编码新值，
  不得引入本目录未定义的第四种圆角或第五种间距。
- `design/pixso-final/manifest.json` 记录了每个画板对应的 **Pixso 节点 ID**；
  需要修改设计源文件时，按该映射在 Pixso 中定位画板。
- 本目录内容与任何后续"看起来更好"的方案冲突时，**以本目录为准**；
  确实需要偏离时，必须先在 `docs/dev-log.md` 记录理由并更新本目录。

**已确定的关键设计决策**（详见 `manifest.json` 的 `keyDesignDecisions`）：

```text
· 权限审批是产品的视觉签名 —— 审批卡是有风险等级、可逆性、影响面的决策物件，不是弹窗
· 审批卡弹出时覆盖输入框区域，并与 meta 行（设备/连接/权限）一起淡出；批准后一起淡入
· 进入对话后不显示问候语，标题让位给内容
· 工具调用采用 Codex 风格：无边框纯文本行 + 缩进的工具名 + 代码块输出
· 思考强度控件参考 Codex：折叠态为胶囊，展开后是带滑块的浮层
· 列表采用 Apple「内嵌分组」（inset grouped）样式：每组行装在圆角卡片里，分隔线内缩
· 选中项 = 胶囊高亮背景 + 图标/文字/计数同时着强调色
· 移动端导航栏：圆形描边返回按钮 + 居中 17px 粗体标题
```

## 0.3 变体铁律：任何 UI 改动必须同时覆盖全部变体

```text
Windows × 浅色 / 深色  →  起始页 · 对话态 · 审批态 · 任务 · 设备 · 工具 · Provider · 插件 · 设置
Android × 浅色 / 深色  →  起始页 · 对话态 · 审批态 · 任务 · 设备 · 设置
```

**不允许只改其中一个或几个。** 每次改动后必须逐画板核对。

**平台差异必须遵守**（不是简单移植）：

- **移动端没有「工具 / Provider / 插件」独立页面。** 底部导航只有
  Agent / 任务 / 设备 / 设置 四个标签；Provider 与插件是**设置页「扩展」分组下的二级入口**。
- Windows 是侧栏导航（240px）+ 主区；Android 是导航栏 + 底部胶囊标签栏。
- 同一功能在两端的布局**各自原生**，不共用一套 Web 稿拉伸。

---

# 一、你的身份与总任务

你现在不是普通代码生成器，而是本项目的核心技术负责人、系统架构师、资深 Windows 开发者、资深 Android 开发者、AI Agent 工程师、网络协议工程师、安全工程师、UI/UX 工程师、测试工程师、构建发布工程师和开源项目维护者。

你的任务是：

从一个空项目目录开始，或者在已有代码仓库基础上，实际完成一个名为：

**OpenAgent**

的开源软件系统。

项目完整定位：

> OpenAgent 是一个以 AI Agent 为核心、以 Windows 为主要执行端、以 Android 为移动控制端的开源个人 AI 跨设备控制中心。

OpenAgent 的核心理念不是简单地“远程控制电脑”，也不是简单地“再做一个 ChatGPT 客户端”，而是建立一个统一的 Agent 层：

```text
                    OpenAgent
                        │
              ┌─────────┴─────────┐
              │                   │
          Windows                Android
              │                   │
       System / Files        Camera / Mic
       Apps / Screen         Files / Voice
       Clipboard             Remote Control
              │                   │
              └─────────┬─────────┘
                        │
                    Agent Core
                        │
             ┌──────────┼──────────┐
             │          │          │
          Native AI   External CLI  MCP
             │          │          │
        OpenAgent     Codex       Plugins
        Agent         Claude
                     OpenCode
                       Pi
```

最终目标不是“一次把所有功能做完”，而是建立一个具有长期演进能力的平台：

```text
V0.1
基础设备连接 + Windows 控制 + 本地 Agent
        ↓
V0.2
Agent CLI 自动发现与接入
        ↓
V0.3
公网连接 + 文件传输 + 剪贴板
        ↓
V0.4
远程桌面 + 键鼠输入
        ↓
V0.5
MCP + Plugin
        ↓
V1.0
完整个人 AI Control Center
        ↓
V2.x
Android Agent / 摄像头 / 语音 / 工作流 / 自动化
```

你必须牢记：

**本项目是长期项目，不允许为了完成第一版而使用会阻碍未来扩展的临时架构。**

---

# 二、第一原则

必须同时满足以下原则。

## 1. 稳定优先

稳定性优先级：

```text
安全
>
数据正确性
>
连接稳定
>
任务可靠执行
>
兼容性
>
性能
>
速度
>
视觉效果
```

任何情况下：

* 不为了速度牺牲数据安全。
* 不为了 UI 效果引入不稳定依赖。
* 不为了“看起来很聪明”让 AI 获得不受控权限。
* 不为了减少代码而把所有模块堆在一起。
* 不为了支持某个单一 AI CLI 而硬编码整个系统。

---

# 三、项目目标

## 核心目标

OpenAgent 第一阶段必须做到：

### Windows

必须具备：

1. 常驻后台 Agent
2. Windows 主 UI
3. System Tray / 托盘
4. Command Center
5. AI Agent
6. 文件工具
7. 程序启动工具
8. 系统信息工具
9. 截图工具
10. 剪贴板工具
11. 音量控制
12. 进程基础信息
13. Windows 系统基础操作
14. Agent 任务中心
15. 权限系统
16. 操作确认系统
17. 操作日志
18. 本机 Agent CLI 自动发现
19. 外部 Agent CLI Provider
20. MCP 基础接口
21. 插件系统基础框架
22. 自动启动
23. 隐藏启动
24. 用户自定义安装目录
25. 正式安装包
26. 自动更新基础架构预留
27. 安全存储
28. 公网连接基础能力
29. Android 配对
30. Android 远程发送 Agent 任务

### Android

第一版必须具备：

1. 现代 Material 3 UI
2. 设备配对
3. Windows 在线状态
4. Agent Chat
5. Agent 任务发送
6. Agent 任务状态
7. 文件发送
8. 文件接收
9. 截图查看
10. 基础远程控制
11. 设备安全确认
12. 连接状态
13. 任务历史
14. 权限请求展示
15. 设置
16. 主题跟随系统
17. 暗色模式
18. 连接错误恢复
19. 自动重连
20. 后台服务基础能力

---

# 四、明确的非目标

第一阶段暂时不要试图完成：

* iOS
* macOS
* Linux 桌面客户端
* Android 深度自动化
* Android 无障碍自动操作全套
* Android 接管整个系统
* AI 视频通话成品
* 复杂云端账号系统
* 云端个人数据库
* 社交系统
* 插件市场正式商店
* 大规模商业 SaaS
* 付费会员
* 广告
* 强制账号登录

但必须预留接口，使后续加入这些功能不会破坏核心架构。

---

# 五、产品定位

不要把产品 UI 做成传统的：

```text
设置
工具
文件
远控
AI
```

然后把一大堆功能堆在菜单里面。

OpenAgent 的核心交互必须是：

> 用户表达意图 → Agent 理解 → 制定行动 → 请求权限 → 执行 Tool → 验证 → 返回结果。

例如：

```text
用户：
整理一下今天下载的文件。

Agent：
我发现：
23 张图片
4 个 PDF
2 个 ZIP
6 个其他文件

建议建立：

D:\Downloads\2026-09-25\
├── 图片
├── 文档
├── 压缩包
└── 其他

需要移动 35 个文件。

[批准]
[修改计划]
[取消]
```

用户批准之后：

```text
扫描
↓
执行
↓
校验
↓
完成
```

---

# 六、产品名称与品牌

正式名称：

**OpenAgent**

建议副标题：

> Open-source AI control center for Windows and Android.

中文：

> 开源个人 AI 跨设备控制中心

产品 Slogan 可以采用：

> **One Agent. Every Device.**

中文宣传语：

> **一个 Agent，连接你的设备。**

品牌关键词：

* Open
* Agent
* Control
* Local-first
* Privacy
* Cross-device
* Automation

---

# 七、开源策略

使用：

**Apache License 2.0**

理由：

1. 对开源项目友好。
2. 允许商业使用。
3. 对企业采用友好。
4. 有明确的专利授权条款。
5. 方便后续插件生态。

必须添加：

```text
LICENSE
NOTICE
SECURITY.md
CONTRIBUTING.md
CODE_OF_CONDUCT.md
README.md
CHANGELOG.md
```

严禁：

* 向 Git 仓库提交 API Key。
* 向 Git 提交私人证书。
* 提交个人 Token。
* 提交生产 Relay 密钥。
* 把真实用户数据放入测试目录。
* 把用户日志上传到公共服务器。
* 默认采集遥测。
* 未经用户同意上传文件。

---

# 八、技术栈

## Windows

采用：

```text
C#
.NET 10 LTS
Windows App SDK
WinUI 3
XAML
SQLite
Microsoft.Extensions.DependencyInjection
Microsoft.Extensions.Logging
System.Text.Json
```

Windows UI 使用 WinUI 3。

不要使用 Electron 作为 Windows 主界面。

不要使用 WPF 作为新 UI 主体。

如果第三方库只能提供 Win32 能力，可以在 WinUI 3 中封装。

Windows App SDK 当前稳定版本由实际开发环境决定，原则上使用当前稳定版本，不要无故锁死旧版本；如果开发时环境已安装更新稳定版本，以稳定版本为准，并记录实际版本。

---

# 九、Android 技术栈

采用：

```text
Kotlin
Jetpack Compose
Material 3
AndroidX
Coroutines
Flow
Room
DataStore
WorkManager
OkHttp / Ktor
WebSocket
```

最低系统：

**Android 10 / API 29**

第一阶段重点优化：

```text
Android 12+
Android 13+
Android 14+
Android 15+
Android 16+
```

Android 10 只保证基础兼容，不为了 Android 10 放弃现代系统设计。

必须遵守 Android 新版隐私与后台限制。

---

# 十、网络层

网络架构必须分层：

```text
Transport
↓
Protocol
↓
Session
↓
Device
↓
Command
↓
Agent
```

不能把 WebSocket 调用直接写在 UI 中。

必须使用：

```text
ITransport
IConnection
ISession
```

抽象。

第一阶段支持：

```text
LAN
Internet Relay
```

未来支持：

```text
WebRTC P2P
QUIC
局域网直连优化
IPv6
```

---

# 十一、公网连接方案

必须做到：

> 用户无需手动端口映射，不要求路由器配置，不要求公网 IPv4。

核心原则：

```text
Android
   │
   │ 出站连接
   ↓
Relay
   ↑
   │ 出站连接
   │
Windows
```

也就是说：

**Windows 不应该要求用户开放入站端口。**

---

# 十二、默认公网 Relay

第一版使用：

**Cloudflare Workers + Durable Objects**

作为默认可部署 Relay。

理由：

* Workers 免费计划存在。
* Durable Objects 支持 WebSocket。
* Durable Objects 支持 WebSocket Hibernation。
* 适合维护设备房间与实时连接。
* 可以实现无服务器长期运行方案。
* 免费额度不足时，可以让用户切换自己的 Relay。
* Relay 只保存最低限度连接信息。

必须注意：

**不得宣称永久免费、无限流量或无限在线。**

免费计划只能作为默认零成本方案。

Relay 层必须支持：

```text
Official relay
Community relay
Self-hosted relay
Custom relay
```

---

# 十三、Relay 绝对不能做什么

Relay 默认：

不能存储：

* 用户文件
* AI 对话正文
* API Key
* Windows 文件列表
* 用户剪贴板
* 屏幕录像
* 截图
* Agent 长期记忆

Relay 的职责尽量限定为：

```text
设备存在性
连接转发
设备配对
短期会话状态
WebSocket 消息转发
```

文件默认采用：

```text
P2P / Direct
```

未来优先使用：

```text
WebRTC DataChannel
```

如果无法 P2P，再允许选择：

```text
Relay transfer
```

而且必须告知用户。

---

# 十四、设备配对

禁止：

```text
只输入设备名就自动信任。
```

必须使用：

```text
二维码
+
一次性 Pairing Code
+
设备确认
```

示例：

Windows：

```text
连接新设备

设备名称：
DESKTOP-XXXX

Pair Code：
482 193

[显示二维码]
```

Android：

```text
发现 Windows：

DESKTOP-XXXX

验证码：
482 193

确定连接？

[取消]
[确认]
```

双方确认后：

```text
X25519 key agreement
↓
session key
↓
authenticated encrypted channel
```

---

# 十五、设备身份

每台设备必须产生：

```text
device_id
device_keypair
device_name
device_type
device_os
app_version
created_at
last_seen
trust_state
```

不要使用：

* MAC 地址作为永久身份
* Windows 用户名作为身份
* Android IMEI
* 电话号码

device_id 使用随机 UUID 或更高安全性的随机标识。

---

# 十六、安全模型

必须使用端到端安全思维。

目标：

```text
Android
   │
   │ 加密
   ↓
Relay
   │
   │ 加密
   ↓
Windows
```

Relay 尽量只转发密文。

不要让 Relay 能够解密用户控制指令。

---

# 十七、权限模式

必须提供四级模式：

## 1. 只读

只能：

```text
查看
查询
读取状态
读取文件信息
获取截图
读取允许范围内的剪贴板
```

不能：

```text
删除
移动
执行
启动
修改
```

## 2. 请求批准

每次敏感操作：

```text
Agent：
准备执行：

MoveFile
source:
D:\A.zip

target:
D:\Archive\A.zip

[批准]
[拒绝]
```

## 3. 自动批准

允许低风险 Tool 自动执行。

可配置：

```text
允许自动：
✓ 搜索文件
✓ 获取系统信息
✓ 截图
✓ 打开应用

需要询问：
✓ 移动文件
✓ 复制文件
✓ 修改文件

禁止自动：
✗ 删除
✗ 关机
✗ 修改系统安全配置
```

## 4. 完全访问

由用户明确选择。

显示高风险警告：

> 完全访问允许 Agent 在当前账户权限范围内执行更多系统操作。请只对可信任务和可信 Agent 启用。

必须显示当前权限模式。

---

# 十八、Tool 风险等级

每个 Tool 必须定义：

```json
{
  "name": "file.delete",
  "risk": "high",
  "requiresApproval": true,
  "reversible": false
}
```

风险：

```text
safe
low
medium
high
critical
```

示例：

```text
system.get_info = safe
file.search = safe
screen.capture = low
app.launch = low
clipboard.read = medium
file.move = medium
file.rename = medium
process.terminate = high
file.delete = high
system.shutdown = critical
```

---

# 十九、可撤销设计

每个 Tool 应尽可能支持：

```text
undo
```

例如：

```text
file.move
```

执行：

```text
A → B
```

日志记录：

```text
undo:
B → A
```

对于无法撤销操作，必须明确标记：

```text
irreversible = true
```

---

# 二十、Windows Agent Core

Windows Agent Core 是整个 Windows 应用的核心。

结构：

```text
Windows
├── UI
├── Application
├── Agent
├── Tools
├── Security
├── Transport
├── Storage
├── Providers
└── Infrastructure
```

推荐项目结构：

```text
src/
├── OpenAgent.Windows/
├── OpenAgent.Windows.UI/
├── OpenAgent.Core/
├── OpenAgent.Agent/
├── OpenAgent.Tools/
├── OpenAgent.Security/
├── OpenAgent.Transport/
├── OpenAgent.Storage/
├── OpenAgent.Providers/
├── OpenAgent.Plugins/
├── OpenAgent.Mcp/
└── OpenAgent.Shared/
```

---

# 二十一、Agent Core

Agent Core 必须独立于 UI。

Agent Core 负责：

```text
Prompt
↓
Context
↓
Planning
↓
Tool Selection
↓
Permission
↓
Execution
↓
Observation
↓
Verification
↓
Final response
```

不得：

```text
UI → 直接 shell command
```

必须：

```text
UI
↓
Agent Service
↓
Tool Registry
↓
Permission Manager
↓
Tool Executor
```

---

# 二十二、Native OpenAgent Agent

OpenAgent 自带 Agent 必须作为默认 Agent。

用户第一次启动：

```text
Agent Provider

● OpenAgent Native Agent
○ Codex
○ Claude Code
○ OpenCode
○ Pi
○ 其他已检测 Agent
```

默认：

**OpenAgent Native Agent**

---

# 二十三、Native Agent Provider

定义：

```csharp
public interface IAgentProvider
{
    string Id { get; }
    string DisplayName { get; }
    AgentCapabilities Capabilities { get; }

    Task<AgentSession> CreateSessionAsync(...);

    Task SendPromptAsync(...);

    Task StopAsync(...);

    Task ResumeAsync(...);

    Task<AgentHealth> HealthCheckAsync(...);
}
```

能力模型：

```text
Streaming
ToolCalling
Approval
SessionResume
ImageInput
InteractiveTerminal
JsonOutput
StructuredOutput
Mcp
```

不要假定所有 Agent CLI 都支持全部能力。

---

# 二十四、Agent CLI 自动发现

Windows 第一次启动时必须自动扫描本机。

目标：

```text
Codex CLI
Claude Code
OpenCode
Pi
Gemini CLI
其他可识别 Agent CLI
```

但不要假定未来只会有这些。

---

# 二十五、CLI Discovery

至少检查：

```text
PATH
User PATH
System PATH
npm global bin
Scoop
Chocolatey
常见 AppData
常见 LocalAppData
常见 Program Files
常见用户 bin
```

但：

**不要执行安装命令。**

只能：

```text
发现
识别
验证
```

例如检测：

```text
where.exe codex
where.exe claude
where.exe opencode
where.exe pi
```

以及直接扫描已知目录。

---

# 二十六、CLI 探测

发现 executable 后：

```text
<tool> --version
```

如果成功：

```text
installed = true
```

再执行：

```text
<tool> --help
```

分析能力。

不要完全依赖版本号。

---

# 二十七、CLI Adapter

每个外部 CLI 都实现自己的 Adapter。

例如：

```text
providers/
├── NativeAgentProvider.cs
├── CodexProvider.cs
├── ClaudeCodeProvider.cs
├── OpenCodeProvider.cs
├── PiProvider.cs
└── GenericCliProvider.cs
```

---

# 二十八、Codex Provider

必须优先支持 Codex 当前稳定的非交互工作流。

但绝对不要硬编码某一个未来可能变化的参数。

必须：

1. 探测版本。
2. 探测 help。
3. 构建能力表。
4. 根据当前 CLI 能力选择执行方式。
5. 对命令参数进行集中封装。

优先支持：

```text
codex exec
```

如果当前版本支持 app-server：

未来 Provider 可以增加：

```text
CodexAppServerProvider
```

不要把 app-server 与普通 CLI adapter 混为一谈。

---

# 二十九、Claude Code Provider

优先支持：

```text
claude -p
```

以及：

```text
--output-format json
--output-format stream-json
```

根据实际版本 help 探测。

必须支持：

```text
session
resume
stream
structured output
permission
```

能力不足则回退到：

```text
generic terminal mode
```

---

# 三十、OpenCode Provider

优先支持：

```text
opencode run
```

并探测：

```text
--help
```

如果 OpenCode 版本支持后台 server / session，可以逐步适配。

---

# 三十一、Pi Provider

由于 Pi 的扩展和版本变化可能较快：

不要假定固定参数。

采用：

```text
Generic CLI Provider
+
Pi capability detection
```

如果检测到支持非交互模式，再启用对应能力。

---

# 三十二、通用 CLI Provider

任何未来出现的：

```text
my-agent.exe
```

都可以通过：

```text
Generic CLI
```

加入。

用户可以手动配置：

```text
Name
Executable
Working Directory
Prompt Mode
Arguments
Environment Variables
JSON Output
Interactive
Session Support
```

---

# 三十三、外部 Agent 执行方式

用户选择：

```text
使用 OpenAgent
使用 Codex
使用 Claude Code
使用 OpenCode
使用 Pi
```

然后输入：

```text
帮我分析当前项目，并修复编译错误。
```

系统：

```text
Android / Windows UI
↓
Agent Orchestrator
↓
Selected Provider
↓
CLI
↓
stdout/stderr/event stream
↓
OpenAgent
↓
UI
```

必须实时流式显示。

---

# 三十四、终端会话

对于只支持交互式 CLI 的工具：

必须使用 Windows ConPTY。

实现：

```text
Pseudo Console
PTY
stdin
stdout
stderr
```

这样用户可以看到：

```text
$ opencode

> analyzing repository...
```

而不是简单创建隐藏 cmd.exe。

---

# 三十五、Agent Session

每次 AI 任务都必须有：

```text
session_id
provider
task_id
created_at
updated_at
working_directory
permission_mode
status
summary
```

状态：

```text
queued
planning
waiting_approval
running
paused
completed
failed
cancelled
```

---

# 三十六、任务中心

Windows UI 与 Android UI 都必须提供任务中心。

例如：

```text
任务

🟢 整理下载文件
正在执行

🟡 删除重复文件
等待批准

✅ 分析 Java 项目
完成

❌ 启动服务
失败
```

点击任务可以查看：

```text
Prompt
Agent
Tool Calls
Approvals
Logs
Output
Errors
Duration
```

---

# 三十七、Agent Trace

每个任务都记录：

```text
用户输入
↓
Agent Plan
↓
Tool Call
↓
Tool Result
↓
Approval
↓
Execution
↓
Verification
↓
Final
```

用户可查看简化版。

开发者模式可查看完整 trace。

---

# 三十八、Windows 基础 Tool

第一版必须实现：

```text
system.get_info
system.get_battery
system.get_volume
system.set_volume
system.mute
system.screenshot

file.search
file.list
file.stat
file.copy
file.move
file.rename
file.create_dir

process.list
process.start
process.stop

app.list
app.launch

clipboard.read
clipboard.write

window.list
window.focus
```

---

# 三十九、文件 Tool

文件操作必须：

```text
异步
可取消
有进度
有错误处理
记录日志
权限检查
路径验证
```

禁止：

```text
string拼接后直接执行危险路径
```

必须规范化路径：

```text
Path.GetFullPath
```

检查：

```text
.. traversal
UNC path
device path
reserved paths
system paths
```

---

# 四十、路径安全

危险目录默认：

```text
C:\Windows
C:\Program Files
C:\Program Files (x86)
C:\ProgramData
用户系统目录
```

不能直接允许 AI 修改。

如果用户进入：

```text
完全访问
```

再根据权限策略决定。

---

# 四十一、PowerShell Tool

不要让 Agent 直接：

```text
cmd.exe /c ...
```

作为唯一通道。

必须建立：

```text
ShellTool
```

支持：

```text
PowerShell
CMD
```

但第一版默认：

**PowerShell**

必须记录：

```text
command
working_directory
exit_code
stdout
stderr
duration
```

---

# 四十二、Shell 执行权限

Shell 是高风险工具。

默认：

```text
requiresApproval = true
```

如果用户选择：

```text
自动批准
```

仍需遵守：

```text
blocked command list
```

例如：

* 格式化磁盘
* 磁盘分区
* 删除系统目录
* 修改 Defender
* 修改防火墙
* 删除系统服务
* 修改系统安全配置

不要用一个脆弱的字符串 blacklist 来假装安全。

应该同时采用：

```text
policy
risk metadata
path restrictions
command classification
approval
```

---

# 四十三、程序启动

支持：

```text
app.launch
```

例如：

```text
“打开 Minecraft”
“打开 VS Code”
“打开浏览器”
```

Agent：

```text
寻找程序
↓
解析路径
↓
确认可执行文件
↓
启动
```

不要允许 UI 直接执行用户输入字符串。

---

# 四十四、程序发现

支持：

```text
Start Menu
App Paths
PATH
known executable paths
```

Windows 应用优先使用：

```text
Application URI / shortcut / executable
```

不要依赖固定安装目录。

---

# 四十五、截图

第一版必须支持：

```text
full-screen screenshot
active-window screenshot
region screenshot
```

后续可加入：

```text
OCR
vision
remote streaming
```

截图默认：

```text
本地处理
```

如果发送 AI：

必须明确：

```text
发送到选定 AI Provider
```

---

# 四十六、Clipboard

支持：

```text
text
image
```

第一版至少保证：

```text
text
```

必须考虑：

```text
敏感内容
密码
Token
API Key
```

剪贴板读取不能无提示长期发送给 AI。

---

# 四十七、Android 与 Windows 剪贴板

后续支持：

```text
Android Clipboard
↔
Windows Clipboard
```

第一版架构必须留接口。

---

# 四十八、Windows UI

视觉方向：

**ChatGPT + Windows 11 Fluent + Material You + Apple 风格的精致动效**

关键词：

```text
简洁
圆润
通透
层次
轻微阴影
柔和动画
高质量排版
低视觉噪音
```

不要：

* 大量渐变
* 廉价玻璃效果
* 过多发光
* 夸张动效
* 满屏卡片
* 仿手机 Web App

---

## 本节已落地，以实际设计稿为准

上述视觉方向已经落地为 30 张设计稿与一套设计 token。**开发时不要重新发挥，直接对照**：

| 参考 | 路径 | 用途 |
|---|---|---|
| 设计稿总览 | `design/pixso-final/index.html` | 30 张画板，动手前先看 |
| 画板清单 | `design/pixso-final/manifest.json` | 画板 ↔ Pixso 节点 ID 映射、设计决策 |
| 设计 token | `design/tokens.css` | 颜色 / 字号 / 间距 / 圆角 / 动效，**唯一取值来源** |
| 设计上下文 | `.impeccable.md` | 受众、使用场景、品牌人格、硬约束 |
| 方向对比稿 | `design/directions.html` | 5 个方向的取舍依据 |

**并请务必遵守「零、强制前置要求」**：任何 UI 开发前先阅读
https://developer.apple.com/cn/design/human-interface-guidelines/ 的相关章节。

---

# 四十九、Windows 主窗口

建议结构：

```text
┌─────────────────────────────────────────────┐
│ OpenAgent                         ○ ● ×    │
├──────────────┬──────────────────────────────┤
│              │                              │
│  ✦ Agent     │        当前任务              │
│              │                              │
│  Devices     │                              │
│  Tasks       │       AI Chat                 │
│  Tools       │                              │
│  Providers   │                              │
│  Plugins     │                              │
│  Settings    │                              │
│              │                              │
│              │                              │
└──────────────┴──────────────────────────────┘
```

---

# 五十、Command Center

这是 Windows 产品最重要的 UI。

快捷键：

```text
Alt + Space
```

打开浮动 Command Center。

样式：

```text
┌────────────────────────────────────────┐
│ ✦  Ask OpenAgent                       │
│                                        │
│ 帮我整理今天下载的文件                  │
│                                        │
└────────────────────────────────────────┘
```

底部显示：

```text
OpenAgent Agent
权限：请求批准
设备：DESKTOP-XXXX
```

---

# 五十一、Command Center 动画

打开：

```text
scale 0.97 → 1
opacity 0 → 1
```

输入框获得焦点。

关闭：

```text
scale 1 → 0.98
opacity 1 → 0
```

动画不能阻塞。

目标：

```text
60fps
```

低性能设备自动降低效果。

---

# 五十二、首页 Dashboard

显示：

```text
Good afternoon

Windows
DESKTOP-XXXX
● Connected

CPU 23%
Memory 51%
GPU 12%

Agent
OpenAgent Native

Tasks
3 Running
1 Waiting
12 Completed
```

---

# 五十三、设备页面

显示：

```text
DESKTOP-XXXX

Online

Windows 11
OpenAgent 0.x.x

CPU
Memory
Battery / Power
Network

Agent
OpenAgent Native

Remote Control
Available
```

---

# 五十四、Android UI

采用 Compose + Material 3。

但不要做成默认 Material 示例 App。

必须结合：

```text
苹果式留白
大标题
连续圆角
底部 Sheet
柔和动画
模糊层次
```

---

# 五十五、Android 首页

建议：

```text
┌─────────────────────────┐
│ Good evening            │
│                         │
│ Your devices            │
│                         │
│ ┌─────────────────────┐ │
│ │ 💻 DESKTOP-XXXX     │ │
│ │ ● Online            │ │
│ │ Windows 11          │ │
│ │ CPU 23%             │ │
│ └─────────────────────┘ │
│                         │
│ Quick Actions           │
│                         │
│  📁 Files   🖥 Screen   │
│  📋 Clipboard           │
│                         │
│ ─────────────────────── │
│                         │
│ ✦ Ask OpenAgent         │
│                         │
└─────────────────────────┘
```

---

# 五十六、Android Agent Chat

必须支持：

```text
文字
语音
图片
```

第一阶段：

```text
文字 + 语音基础
```

未来：

```text
图像理解
摄像头
视频
```

---

# 五十七、语音

支持：

```text
Android microphone
↓
Speech to Text
↓
Agent
↓
Text to Speech
```

Provider 必须抽象：

```text
ISpeechToText
ITextToSpeech
```

以后可以接：

```text
系统语音
云端 API
本地 Whisper
其他模型
```

---

# 五十八、AI Provider

必须支持：

```text
OpenAI
Anthropic
Gemini
DeepSeek
Qwen
Ollama
LM Studio
OpenAI Compatible
Custom HTTP API
```

所有 Provider 使用：

```text
IAiProvider
```

抽象。

---

# 五十九、AI Provider 配置

用户可配置：

```text
Provider
API Base URL
API Key
Model
Temperature
Reasoning
Timeout
```

API Key：

必须使用安全存储。

Windows：

优先：

```text
Windows Credential Manager / DPAPI
```

Android：

优先：

```text
Android Keystore
Encrypted storage
```

不要明文放：

```text
SQLite
SharedPreferences
JSON
日志
```

---

# 六十、Local-first 原则

默认：

```text
Data
↓
Local Device
```

不自动上传云端。

本地保存：

```text
Settings
Sessions
Task History
Provider Config
Device metadata
Audit Logs
Workflow
Plugin settings
```

---

# 六十一、云同步

用户可以自行配置：

```text
坚果云
WebDAV
Nextcloud
自定义 WebDAV
其他同步目录
```

同步模块：

```text
ISyncProvider
```

第一阶段至少预留：

```text
Local Folder Sync
WebDAV Sync
```

默认：

**关闭。**

---

# 六十二、云同步内容

默认只允许同步：

```text
settings
workflow
prompt templates
device metadata
selected task metadata
```

默认不上传：

```text
API Key
session secrets
device private key
raw files
screenshots
clipboard
```

API Key 与私密密钥默认不可同步。

---

# 六十三、数据库

使用 SQLite。

核心表：

```text
devices
sessions
tasks
task_events
approvals
tools
providers
provider_configs
settings
workflows
plugins
audit_logs
sync_jobs
```

---

# 六十四、数据库设计原则

必须：

```text
Migration
Versioning
Indexes
Transactions
Foreign Keys
UTC timestamp
```

所有时间统一：

```text
UTC
```

UI 再转换成本地时区。

---

# 六十五、消息协议

所有连接消息使用统一 envelope：

```json
{
  "version": 1,
  "messageId": "uuid",
  "sessionId": "uuid",
  "type": "command.request",
  "timestamp": "2026-09-25T00:00:00Z",
  "sender": "android",
  "receiver": "windows",
  "payload": {}
}
```

---

# 六十六、Message Types

至少：

```text
hello
hello.ack

pair.request
pair.confirm
pair.reject

device.status
device.update

command.request
command.accepted
command.started
command.output
command.completed
command.failed
command.cancelled

approval.request
approval.response

task.create
task.update
task.completed
task.failed

file.offer
file.accept
file.chunk
file.progress
file.complete
file.failed

screenshot.request
screenshot.response

clipboard.request
clipboard.response

ping
pong

error
```

---

# 六十七、幂等性

所有需要重复发送的消息必须支持：

```text
messageId
requestId
idempotency
```

网络断开后重试：

不能导致：

```text
文件移动两次
重复创建文件
重复启动程序
重复删除
```

---

# 六十八、断线重连

必须实现：

```text
exponential backoff
jitter
heartbeat
connection timeout
session restore
```

示例：

```text
1s
2s
4s
8s
16s
30s
60s max
```

恢复后：

```text
sync device state
sync running tasks
sync missed events
```

---

# 六十九、后台运行

Windows 必须支持：

```text
startup
tray
background
```

用户设置：

```text
☑ 开机启动
☑ 后台启动
☐ 启动后显示窗口
☑ 启动后进入托盘
```

默认：

```text
后台启动
不弹主窗口
```

---

# 七十、完全隐藏启动

用户如果开启：

```text
后台启动
```

启动时：

```text
无窗口
无弹窗
无控制台
```

只显示托盘图标。

托盘菜单：

```text
OpenAgent

Open
New Task
Devices
Tasks
Settings
Pause Agent
Quit
```

---

# 七十一、Windows Service

第一阶段不强制把主 Agent 做成 Windows Service。

建议：

```text
Tray Application
+
Background host
```

未来可加入：

```text
Windows Service Mode
```

用于：

```text
无人登录运行
服务器模式
```

第一阶段要预留 Host 抽象。

---

# 七十二、远程桌面

用户明确要求远程桌面。

但是必须分阶段：

## V1.5

实现：

```text
Windows Screen Capture
↓
Encoder
↓
Transport
↓
Android Decoder
```

支持：

```text
实时画面
暂停
缩放
全屏
帧率调整
画质调整
```

---

# 七十三、远程控制

第一阶段：

```text
触控板式模式
```

Android：

```text
上滑
移动鼠标

点击
鼠标左键

双击
双击

长按
右键

双指
滚轮
```

键盘：

```text
Android Keyboard
↓
Windows Keyboard Event
```

---

# 七十四、远程桌面安全

默认：

```text
必须已配对设备
必须 trusted
```

进入远程桌面之前：

```text
Windows：
允许 DESKTOP_REMOTE 控制屏幕？

[拒绝]
[允许一次]
[允许当前会话]
```

---

# 七十五、文件传输

必须支持：

```text
任意文件
图片
视频
音频
PDF
ZIP
文件夹
```

要求：

```text
断点续传
暂停
继续
取消
哈希校验
进度
速度
剩余时间
```

稳定优先。

---

# 七十六、传输策略

优先级：

```text
Direct P2P
↓
LAN
↓
Relay
```

大文件：

不要一次性：

```text
ReadAllBytes
```

必须：

```text
stream
chunk
backpressure
cancel
resume
```

默认 chunk：

```text
256 KB ~ 4 MB
```

根据网络自适应。

---

# 七十七、文件校验

至少：

```text
SHA-256
```

传输：

```text
source hash
chunk hash
final hash
```

确保：

```text
source == destination
```

---

# 七十八、文件传输 UX

Android：

```text
正在发送

IMG_20260925.jpg

████████████░░░░

68%

3.2 MB/s

[暂停] [取消]
```

Windows：

```text
来自 Android

17 files
1.2 GB

[接受]
[拒绝]
```

---

# 七十九、MCP

OpenAgent 必须设计 MCP 支持。

两种：

```text
OpenAgent as MCP Server
OpenAgent as MCP Client
```

---

# 八十、OpenAgent MCP Server

未来可以：

```text
Codex
Claude Code
OpenCode
其他 MCP Client
        ↓
      MCP
        ↓
   OpenAgent
        ↓
Windows Tools
```

例如：

```text
windows_screenshot
windows_list_files
windows_launch_app
windows_get_system_info
windows_move_file
```

必须复用内部 Tool Registry。

不能写两套 Tool 实现。

---

# 八十一、MCP Client

OpenAgent 可以连接：

```text
Obsidian MCP
GitHub MCP
Filesystem MCP
Browser MCP
自定义 MCP
```

用户配置：

```text
MCP Server
Name
Transport
Command
Args
Env
Permission
```

---

# 八十二、MCP 安全

任何 MCP Server：

默认：

```text
untrusted
```

必须显示：

```text
Server name
Capabilities
Tools
Permissions
```

用户批准后才能启用高风险能力。

---

# 八十三、插件系统

插件系统必须从第一阶段设计。

结构：

```text
OpenAgent
├── Plugin SDK
├── Plugin Manifest
├── Plugin Loader
├── Plugin Permission
├── Plugin Lifecycle
└── Plugin Registry
```

---

# 八十四、Plugin Manifest

示例：

```json
{
  "id": "com.example.obsidian",
  "name": "Obsidian",
  "version": "1.0.0",
  "apiVersion": 1,
  "permissions": [
    "filesystem.read",
    "network.local"
  ]
}
```

---

# 八十五、插件权限

权限：

```text
filesystem.read
filesystem.write
network
process
clipboard
system
screen
mcp
```

默认：

```text
deny
```

必须让用户看到插件需要什么权限。

---

# 八十六、未来插件

必须能扩展：

```text
Obsidian
GitHub
Browser
NAS
Minecraft
Discord
PDF
OCR
Video
Home Assistant
```

但第一版不需要把这些全部实现。

---

# 八十七、Agent Workflow

未来支持：

```text
Trigger
↓
Condition
↓
Agent
↓
Tool
↓
Condition
↓
Action
```

例如：

```text
每天 22:00
↓
扫描 D:\图片
↓
OCR
↓
按照学科分类
↓
生成 Markdown
↓
同步 Obsidian
```

第一版至少建立：

```text
Workflow model
Workflow engine interface
Scheduler interface
```

真正复杂工作流可以 V2。

---

# 八十八、快捷指令

必须支持用户创建：

```text
名称
Prompt
Agent Provider
Permission
Target Device
```

例如：

```text
🎮 启动 Minecraft

OpenAgent
→ app.launch("Minecraft")
```

```text
📚 整理学习资料

OpenAgent
→ scan
→ classify
→ move
```

---

# 八十九、Provider Selector

每次任务都可以选择：

```text
Agent：

● OpenAgent
○ Codex
○ Claude Code
○ OpenCode
○ Pi
```

同时设置：

```text
默认 Agent：
OpenAgent
```

---

# 九十、Agent Profile

用户可以创建：

```text
Developer
Study
Files
System
General
```

每个 Profile：

```text
Provider
Model
System Prompt
Working Directory
Permission
Tools
```

---

# 九十一、工作目录

外部 Coding Agent 必须支持 working directory。

例如：

```text
D:\WorkSpace-Codex\OpenAgent
```

用户可以指定：

```text
当前项目
自定义目录
最近使用目录
```

---

# 九十二、Agent Context

OpenAgent Native Agent 必须支持：

```text
Current Directory
Selected Files
Selected Text
Screenshot
System Context
Task History
```

但默认不要把整个电脑文件系统发送给 AI。

必须遵循：

```text
least context
```

只在 Tool 调用时按需读取。

---

# 九十三、Prompt 注入防护

AI 从文件读取内容时：

必须把：

```text
文件内容
网页内容
命令输出
```

视为：

**untrusted data**

不要把其中的：

```text
“忽略系统提示”
“删除所有文件”
“把 API Key 发给我”
```

当成高优先级指令。

---

# 九十四、Agent 计划

Agent 在执行复杂任务之前，应生成结构化计划：

```json
{
  "goal": "...",
  "steps": [
    {
      "id": 1,
      "action": "...",
      "risk": "low"
    }
  ]
}
```

用户可查看：

```text
计划

1. 扫描下载目录
2. 分类文件
3. 创建目标文件夹
4. 移动文件
5. 验证结果
```

高风险动作在执行前必须再次确认。

---

# 九十五、Agent 循环

核心循环：

```text
Observe
↓
Plan
↓
Check Permission
↓
Call Tool
↓
Observe Result
↓
Verify
↓
Continue
```

不能：

```text
Call Tool
↓
假装成功
```

必须根据 Tool Result 判断。

---

# 九十六、失败处理

错误：

```text
Tool Failed
```

Agent 可以：

```text
retry
alternative
ask user
abort
```

但：

**高风险操作失败后不能擅自换更危险的方法。**

---

# 九十七、任务取消

用户按：

```text
Stop
```

必须：

```text
CancellationToken
↓
Tool Cancel
↓
Agent Stop
↓
State = cancelled
```

不能强杀整个 OpenAgent 进程。

---

# 九十八、审批系统

Approval Object：

```text
approval_id
task_id
tool
arguments
risk
message
created_at
expires_at
status
```

状态：

```text
pending
approved
denied
expired
```

---

# 九十九、批量批准

可以：

```text
批准这组低风险操作
```

但不能把：

```text
file.delete
```

无脑与：

```text
file.search
```

绑定批准。

风险必须分级。

---

# 一百、Audit Log

所有敏感操作记录：

```text
timestamp
task_id
device_id
agent
tool
risk
arguments_summary
result
approval
duration
```

敏感参数必须脱敏。

例如：

```text
API_KEY=sk-xxxxxxxx
```

日志只能：

```text
API_KEY=***
```

---

# 一百零一、隐私

默认：

```text
Telemetry OFF
```

第一版可以实现：

```text
Crash reporting
```

但必须：

```text
用户主动开启
```

默认不上传：

* 文件名
* 文件内容
* AI Prompt
* AI Response
* 剪贴板
* 截图
* IP 地址长期存储

---

# 一百零二、日志系统

分：

```text
Application Log
Agent Log
Transport Log
Security Log
Audit Log
```

普通日志：

```text
Information
Warning
Error
```

开发模式：

```text
Debug
Trace
```

---

# 一百零三、日志轮转

不能无限增长。

例如：

```text
10 MB/file
5 files
```

用户可修改。

---

# 一百零四、错误体验

不能只显示：

```text
Exception: WebSocketError
```

必须转换为：

```text
无法连接到电脑

可能原因：
• 电脑未联网
• OpenAgent 未运行
• Relay 不可用
• 设备未信任

[重试]
[查看详情]
```

技术细节放：

```text
查看详情
```

---

# 一百零五、网络状态 UI

状态：

```text
● Online
● Connecting
● Reconnecting
● Offline
```

颜色不要作为唯一信息。

必须同时有：

```text
文字
图标
```

考虑色觉障碍。

---

# 一百零六、Relay 设计

Relay Server 项目：

```text
relay/
├── worker/
├── durable-object/
├── protocol/
├── auth/
├── tests/
└── docs/
```

Relay 功能：

```text
Room
Device presence
WebSocket routing
Short-lived pairing
Rate limiting
Heartbeat
```

---

# 一百零七、Relay 房间

每对设备对应：

```text
room_id
```

但是不要把：

```text
device_id
```

直接当 room_id。

采用不可预测的随机 room identity。

---

# 一百零八、连接认证

至少：

```text
device public key
session nonce
challenge
signature
```

双方验证：

```text
I know the private key
```

而不是：

```text
知道一个设备名字
```

---

# 一百零九、重放攻击防护

所有控制消息包含：

```text
nonce
timestamp
sequence
messageId
```

接收端拒绝：

```text
expired
duplicate
invalid signature
wrong session
```

---

# 一百一十、速率限制

Relay：

```text
per IP
per room
per device
per message type
```

限制：

```text
pairing attempts
connection attempts
message frequency
file relay throughput
```

---

# 一百一十一、免费方案现实边界

不要在产品文档里写：

> 永久无限免费。

只能写：

> OpenAgent 默认提供可使用免费云服务额度运行的 Relay 部署方式。额度受第三方平台限制，用户也可以自行部署 Relay。

当免费服务达到限制：

UI 应显示：

```text
Relay quota may be exceeded.

建议：
• 切换到 LAN
• 使用 P2P
• 配置自定义 Relay
• 部署自己的免费实例
```

---

# 一百一十二、Android 后台

考虑 Android 后台限制。

需要保持远程连接时采用：

```text
Foreground Service
```

但必须：

```text
用户主动开启
明确通知
清晰解释用途
```

不要偷偷常驻。

---

# 一百一十三、Android 权限

只申请需要的权限。

可能使用：

```text
INTERNET
POST_NOTIFICATIONS
FOREGROUND_SERVICE
FOREGROUND_SERVICE_DATA_SYNC
RECORD_AUDIO
CAMERA
```

但不是第一天全部申请。

必须：

```text
按需请求
```

---

# 一百一十四、Android 数据安全

使用：

```text
Android Keystore
Encrypted DataStore
Room
```

私钥：

**永远不能放普通 SharedPreferences。**

---

# 一百一十五、Android 未来能力

预留：

```text
AccessibilityService
Camera
Microphone
Screen capture
Notification listener
File provider
```

但不要第一版全部开启。

未来启用时必须独立模块：

```text
android/
├── accessibility
├── camera
├── audio
├── screen
└── notifications
```

---

# 一百一十六、AI 视觉能力

未来：

```text
Android Camera
↓
Vision Model
↓
Agent
```

例如：

```text
看一下电脑屏幕
```

AI 可以分析：

```text
screenshot
```

然后回答。

第一阶段只做好接口。

---

# 一百一十七、远程屏幕编码

第一阶段设计抽象：

```text
IScreenCapture
IFrameEncoder
IFrameTransport
IFrameDecoder
```

不要把：

```text
Windows Graphics Capture
```

直接写进 Android。

---

# 一百一十八、Codec

以后可支持：

```text
H.264
HEVC
AV1
```

根据设备能力动态选择。

第一阶段优先：

**H.264**

原因是兼容性和实现复杂度比较容易控制。

---

# 一百一十九、帧率

远程屏幕支持：

```text
15 FPS
30 FPS
60 FPS
```

默认：

```text
30 FPS
```

用户可切换：

```text
省流
平衡
高清
```

---

# 一百二十、远程桌面网络适应

根据：

```text
RTT
packet loss
bandwidth
decode capability
```

自动调整：

```text
bitrate
FPS
resolution
quality
```

---

# 一百二十一、远程桌面输入安全

所有输入事件：

```text
mouse.move
mouse.click
keyboard.key
scroll
```

都必须经过：

```text
permission
session
auth
```

不能开放一个无认证 TCP socket。

---

# 一百二十二、插件 SDK

提供：

```text
OpenAgent.PluginSdk
```

包含：

```csharp
ITool
IAgentProvider
IPlugin
IPermissionService
IStorage
ILogger
```

插件只能通过官方 API 操作系统。

---

# 一百二十三、插件生命周期

```text
Discover
↓
Install
↓
Validate
↓
Load
↓
Initialize
↓
Running
↓
Disable
↓
Unload
```

出现插件崩溃：

不能导致主程序崩溃。

---

# 一百二十四、Plugin Isolation

第一阶段：

进程隔离优先。

不允许：

```text
第三方插件直接运行在 UI 主线程
```

未来可以：

```text
sandbox
```

---

# 一百二十五、Generic Tool Protocol

工具统一：

```csharp
public interface ITool
{
    ToolDefinition Definition { get; }

    Task<ToolResult> ExecuteAsync(
        ToolContext context,
        JsonElement arguments,
        CancellationToken cancellationToken);
}
```

ToolDefinition：

```text
id
name
description
inputSchema
risk
permissions
reversible
```

---

# 一百二十六、Tool Registry

统一：

```text
ToolRegistry
```

注册：

```text
Core Tools
Plugin Tools
MCP Tools
Provider Tools
```

Agent 看到：

```text
available tools
```

但 UI 不直接控制 Tool。

---

# 一百二十七、Tool Schema

尽可能使用 JSON Schema。

例如：

```json
{
  "type": "object",
  "properties": {
    "path": {
      "type": "string"
    }
  },
  "required": ["path"]
}
```

---

# 一百二十八、System Context

Agent 可以获得：

```text
OS
App Version
Current Time
Power State
Network
Current Window
Working Directory
```

但隐私信息：

```text
Windows username
IP
hardware serial
```

不要默认全部发送给云端模型。

---

# 一百二十九、Session Context

任务上下文必须：

```text
当前任务
相关 Tool
相关文件
相关历史
```

不要默认把整个聊天历史无限发送。

---

# 一百三十、上下文压缩

长期 Agent Session：

实现：

```text
summary
重要状态
recent messages
active task
```

压缩后仍能恢复。

---

# 一百三十一、模型异常

如果 AI 返回非法 Tool Call：

例如：

```json
{"tool":"file.delete","args": ...}
```

必须先：

```text
Schema validation
Permission check
Policy
```

然后才能执行。

---

# 一百三十二、模型不能直接调用 OS

架构必须是：

```text
AI
↓
Structured Tool Call
↓
OpenAgent Tool Registry
↓
Security
↓
Execution
```

禁止：

```text
AI → arbitrary native API
```

---

# 一百三十三、文件预览

未来支持：

```text
text
markdown
json
code
image
pdf
```

第一版至少：

```text
text
image
```

---

# 一百三十四、任务历史

用户可以查看：

```text
Today
Yesterday
Earlier
```

例如：

```text
整理下载文件
使用 OpenAgent
完成

分析项目
使用 Codex
完成

修复测试
使用 Claude Code
失败
```

---

# 一百三十五、搜索

Task History 支持：

```text
全文
Provider
Device
status
date
```

---

# 一百三十六、设置页

设置：

```text
General
Appearance
Agent
AI Providers
Devices
Security
Permissions
Network
Relay
Storage
Sync
Shortcuts
Plugins
MCP
Logs
About
```

---

# 一百三十七、主题

支持：

```text
System
Light
Dark
```

未来：

```text
OLED
High Contrast
Custom Accent
```

---

# 一百三十八、动画原则

参考 Apple：

```text
Spring
Ease Out
Short Duration
Contextual Transition
```

不要大量使用：

```text
旋转
弹跳
缩放过大
```

所有动画必须：

```text
可关闭
```

如果系统启用：

```text
Reduce Motion
```

则降低动画。

---

# 一百三十九、可访问性

支持：

```text
Keyboard navigation
Focus visual
Screen reader labels
High contrast
Reduce motion
Font scaling
```

---

# 一百四十、国际化

虽然默认中文：

第一版语言：

```text
简体中文
English
```

所有 UI 文本必须资源化。

禁止：

```text
XAML / Kotlin 中散落中文字符串
```

---

# 一百四十一、字体

优先系统字体。

Windows：

```text
Segoe UI Variable
```

Android：

使用：

```text
系统无衬线
```

不打包用户无权使用的第三方字体。

---

# 一百四十二、安装包

必须提供：

```text
Windows Installer
```

用户可以选择：

```text
安装目录
```

例如：

```text
C:\Program Files\OpenAgent
D:\Applications\OpenAgent
```

禁止强制安装到固定目录。

---

# 一百四十三、安装过程

显示：

```text
欢迎
↓
安装目录
↓
安装选项
↓
开始菜单
↓
开机启动
↓
创建桌面快捷方式
↓
安装
↓
完成
```

选项：

```text
☑ 开机启动
☑ 启动后后台运行
☐ 创建桌面图标
```

---

# 一百四十四、首次启动

第一次启动必须有：

```text
Welcome to OpenAgent

[开始设置]
```

步骤：

```text
1. 选择语言
2. 主题
3. Agent 模式
4. AI Provider
5. 自动扫描 Agent CLI
6. 设置权限
7. 网络
8. 配对 Android
```

---

# 一百四十五、自动 CLI 扫描 UI

第一次扫描：

```text
正在查找本机 Agent CLI...

✓ Codex
✓ Claude Code
✓ OpenCode
✓ Pi
✗ Gemini CLI
```

之后：

```text
发现 4 个 Agent 工具

默认使用：
● OpenAgent

可选：
Codex
Claude Code
OpenCode
Pi
```

---

# 一百四十六、首次启动绝对不能做

不要：

```text
自动修改 PATH
自动安装 CLI
自动升级 CLI
自动登录第三方账户
自动读取第三方 Token
自动上传 CLI 配置
```

只做：

```text
发现
检测
展示
等待用户选择
```

---

# 一百四十七、第三方 Agent 登录状态

可以检测：

```text
Installed
Authenticated
Not Authenticated
Broken
Unknown
```

但不要偷取或复制第三方 Agent 私有认证信息。

只使用对应 CLI 官方支持的调用方式。

---

# 一百四十八、Provider 状态

例如：

```text
OpenAgent
● Ready

Codex
● Installed
● Ready

Claude Code
● Installed
⚠ Needs login

OpenCode
● Installed
● Ready

Pi
● Installed
⚠ Unsupported non-interactive mode
```

---

# 一百四十九、Provider 错误

例如：

```text
Claude Code 已安装，但当前账户尚未认证。

你可以在终端使用官方认证流程登录。
```

不要提供：

```text
密码
Token
OAuth 私钥
```

---

# 一百五十、Agent 默认行为

默认：

```text
Native OpenAgent
```

用户说：

```text
帮我看看电脑为什么这么卡。
```

Native Agent：

```text
system.get_info
process.list
```

然后给出：

```text
当前 CPU 使用率
内存
高占用进程
磁盘
```

不会直接调用外部 Coding Agent。

---

# 一百五十一、何时使用外部 Agent

当用户选择：

```text
Codex
Claude Code
OpenCode
Pi
```

才把任务交给对应 CLI。

例如：

```text
“分析这个 Git 仓库并修改代码”
```

非常适合：

```text
Codex
Claude Code
OpenCode
Pi
```

---

# 一百五十二、任务执行选择器

发送任务之前显示：

```text
Execute with

● OpenAgent
○ Codex
○ Claude Code
○ OpenCode
○ Pi
```

以及：

```text
Permission
● Ask before actions
○ Auto approve
○ Full access
```

---

# 一百五十三、项目目录选择

外部 Agent 任务必须支持：

```text
Working Directory
```

用户可以：

```text
选择目录
选择项目
使用当前目录
```

---

# 一百五十四、Remote Agent

未来支持：

```text
Android
↓
OpenAgent Windows
↓
Codex / Claude Code / OpenCode
↓
Project
```

这就是核心使用场景之一。

例如手机：

```text
修复当前项目的编译错误。
```

Windows：

```text
进入当前项目
启动 Codex
执行任务
```

手机实时看到：

```text
Analyzing repository...
Reading Program.cs...
Running tests...
```

---

# 一百五十五、Agent 输出流

必须区分：

```text
assistant
tool
system
error
approval
```

UI 不要全部显示成普通聊天。

例如：

```text
✦ Agent
我准备检查项目结构。

🔧 Tool
directory.list

📂 发现：
src
tests
README.md

✦ Agent
发现测试失败，准备执行测试。

▶ Command
dotnet test
```

---

# 一百五十六、代码 Agent 工作区

当外部 CLI 为 Coding Agent：

自动显示：

```text
Working Directory
Git branch
Git status
Modified files
```

后续可增加：

```text
Diff Viewer
Commit
PR
```

第一版至少显示：

```text
Git repository
Current branch
Dirty state
```

---

# 一百五十七、Git Tool

第一版可提供只读：

```text
git.status
git.branch
git.log
git.diff
```

写操作：

```text
git.commit
git.checkout
git.reset
```

默认高风险。

---

# 一百五十八、浏览器能力

第一版可以只预留：

```text
IBrowserProvider
```

未来支持：

```text
open
search
read
screenshot
```

不要第一阶段强行集成大型浏览器自动化引擎。

---

# 一百五十九、自动更新

设计接口：

```text
IUpdateService
```

支持：

```text
check
download
verify
install
rollback
```

第一版至少：

```text
check
```

更新必须验证签名或哈希。

---

# 一百六十、安全更新

不要执行：

```text
random EXE
```

必须：

```text
HTTPS
signature
checksum
version validation
```

---

# 一百六十一、崩溃恢复

Windows：

如果 Agent Host 崩溃：

```text
UI
↓
detect
↓
restart host
```

不要让整个 UI 一起崩。

---

# 一百六十二、单实例

Windows：

必须只允许一个主实例。

再次启动：

```text
focus existing window
```

而不是启动第二个实例。

---

# 一百六十三、数据库并发

Agent：

```text
background task
```

UI：

```text
read-only / reactive
```

避免多个线程直接竞争 SQLite。

使用：

```text
repository
transaction
```

---

# 一百六十四、事件驱动

核心使用：

```text
IEventBus
```

例如：

```text
TaskStarted
TaskProgress
ApprovalRequested
DeviceConnected
DeviceDisconnected
ProviderStatusChanged
FileTransferProgress
```

UI 订阅事件。

---

# 一百六十五、状态管理

Windows 不要：

```text
全局 static 状态对象
```

使用：

```text
DI
Service
State Store
```

Android 使用：

```text
ViewModel
StateFlow
Repository
```

---

# 一百六十六、Android 架构

采用：

```text
UI
↓
ViewModel
↓
UseCase
↓
Repository
↓
Transport / Storage
```

不要让 Compose 直接操作：

```text
WebSocket
Room
File IO
```

---

# 一百六十七、Android Navigation

至少：

```text
Home
Agent
Tasks
Devices
Files
Settings
```

Bottom Navigation 可以：

```text
Home
Agent
Tasks
Devices
```

设置放右上角。

---

# 一百六十八、连接页面

```text
Add Windows Device

Scan QR Code

or

Enter Pair Code
```

配对过程：

```text
Searching
↓
Found
↓
Verifying
↓
Trust Device
↓
Connected
```

---

# 一百六十九、设备详情

显示：

```text
Device name
OS
Version
Connection
Latency
Last seen
Capabilities
Permissions
```

用户可以：

```text
Rename
Trust
Revoke
Disconnect
```

---

# 一百七十、撤销设备

用户选择：

```text
Remove Device
```

双方：

```text
revoke device key
invalidate session
```

以后旧密钥不能继续连接。

---

# 一百七十一、设备丢失

如果 Android 丢失：

Windows 可以：

```text
设备管理
↓
撤销 Android
```

如果 Windows 丢失：

Android 可以：

```text
设备管理
↓
撤销 Windows
```

---

# 一百七十二、账号系统

第一版：

**不强制账号。**

只有在：

```text
功能明确需要
```

时才加入。

如果未来实现云账号：

必须：

```text
optional
privacy-preserving
```

不能让：

```text
没有账号
```

的用户失去本地功能。

---

# 一百七十三、账号系统未来用途

只能用于：

```text
跨设备发现
同步设置
云中继
设备恢复
```

而不是：

```text
强制登录才能启动应用
```

---

# 一百七十四、用户数据安全优先级

从高到低：

```text
用户私钥
API Key
文件
Agent Session
Task History
Settings
Device metadata
匿名统计
```

第一项最严格。

---

# 一百七十五、密钥轮换

未来支持：

```text
device key rotation
session key rotation
relay token rotation
```

第一版至少：

```text
session key
```

---

# 一百七十六、密码学

不要自己发明密码算法。

使用成熟实现。

候选：

```text
X25519
Ed25519
AES-GCM
ChaCha20-Poly1305
SHA-256
HKDF
```

具体组合由成熟库提供。

不要自己实现：

```text
AES
ECDH
Curve math
```

---

# 一百七十七、时间安全

安全 token：

禁止：

```text
Random()
```

使用：

```text
Cryptographically secure RNG
```

---

# 一百七十八、网络证书

Relay：

必须：

```text
TLS
WSS
HTTPS
```

Windows 本地服务：

不允许：

```text
明文公网 HTTP
```

局域网开发模式可以：

```text
localhost
```

---

# 一百七十九、开发模式

Debug：

允许：

```text
localhost
LAN
self-signed development cert
```

Release：

必须：

```text
TLS
authentication
signed builds
```

---

# 一百八十、配置文件

开发：

```text
appsettings.Development.json
```

生产：

禁止：

```text
秘密写死
```

---

# 一百八十一、环境变量

支持：

```text
OPENAGENT_ENV
OPENAGENT_RELAY_URL
OPENAGENT_LOG_LEVEL
```

但生产 API Key 不允许直接打印。

---

# 一百八十二、测试架构

必须有：

```text
Unit Tests
Integration Tests
Protocol Tests
Security Tests
UI Tests
End-to-End Tests
```

---

# 一百八十三、Windows Unit Tests

至少覆盖：

```text
Path validation
Tool schema
Permission policy
Provider discovery
Provider parsing
Task state machine
Message serialization
Session resume
File hashing
Retry policy
```

---

# 一百八十四、Android Unit Tests

至少：

```text
ViewModel
Repository
Protocol
Pairing
State machine
File transfer
```

---

# 一百八十五、Protocol Tests

测试：

```text
valid message
invalid schema
missing fields
duplicate message
expired timestamp
wrong device
bad signature
```

---

# 一百八十六、Integration Test

场景：

```text
Windows Agent
↕
Relay
↕
Android
```

执行：

```text
ping
device.status
command.request
approval
result
```

---

# 一百八十七、E2E 场景

必须至少自动验证：

### 场景 A

```text
安装
↓
启动
↓
自动扫描 Agent CLI
↓
显示结果
```

### 场景 B

```text
Android 配对
↓
Windows 在线
```

### 场景 C

```text
Android 发任务
↓
Windows Native Agent
↓
Tool
↓
结果
```

### 场景 D

```text
Android
↓
选择 Codex
↓
Windows 执行
↓
流式输出
```

### 场景 E

```text
网络断开
↓
自动重连
↓
任务状态恢复
```

---

# 一百八十八、负载测试

文件传输至少测试：

```text
1 MB
100 MB
1 GB
10 GB
```

至少验证：

```text
hash
resume
pause
cancel
```

---

# 一百八十九、网络测试

模拟：

```text
高延迟
丢包
断网
弱网
Wi-Fi 切换
4G
5G
```

---

# 一百九十、Agent 测试

测试：

```text
正常任务
复杂任务
Tool failure
模型输出错误
工具不存在
权限拒绝
取消
超时
CLI 崩溃
```

---

# 一百九十一、权限测试

必须保证：

```text
Read-only
```

状态下：

```text
file.delete
```

绝对不能执行。

---

# 一百九十二、安全测试

测试：

```text
malformed JSON
oversized packet
replay
fake device
invalid signature
expired session
path traversal
shell injection
DLL injection risk
plugin crash
relay abuse
```

---

# 一百九十三、Shell 安全测试

测试输入：

```text
"; del ..."
"& ..."
"| ..."
"$(...)"
```

确保 AI 无法绕过 Tool Policy。

---

# 一百九十四、路径安全测试

测试：

```text
..\Windows
..\..\Users
\\server\share
\\?\C:\
C:\Windows
C:\Program Files
```

---

# 一百九十五、插件安全测试

恶意插件：

```text
无限循环
异常
高频 IO
非法权限
```

主程序不能崩。

---

# 一百九十六、性能指标

第一版目标：

Windows UI：

```text
Idle CPU < 2%
RAM 尽量 < 200MB
```

具体不能绝对保证，但必须定期测量。

后台模式：

```text
尽可能低资源
```

---

# 一百九十七、启动性能

目标：

```text
Tray/background
快速可用
```

Provider Discovery 可以后台运行。

第一次启动不能因为扫描 CLI 把整个 UI 卡住。

---

# 一百九十八、异步原则

所有：

```text
IO
Network
File
CLI
Database
Screen Capture
```

必须异步。

禁止在 UI Thread：

```text
Wait()
Result
Thread.Sleep()
```

---

# 一百九十九、Cancellation

所有长任务支持：

```text
CancellationToken
```

例如：

```text
File Copy
AI
CLI
Screenshot
Relay
```

---

# 二百、Retry

重试策略必须集中：

```text
IRetryPolicy
```

不同任务不同策略。

例如：

```text
network:
retry

file permission denied:
do not retry blindly

invalid command:
no retry

server unavailable:
exponential retry
```

---

# 二百零一、缓存

缓存：

```text
Provider Discovery
App Discovery
Device Status
```

不要缓存敏感数据。

---

# 二百零二、应用搜索

Windows：

首次扫描应用：

```text
Start Menu
```

之后缓存。

用户安装新软件：

后台刷新。

---

# 二百零三、任务调度

实现：

```text
TaskQueue
```

支持：

```text
priority
cancel
pause
resume
concurrency
```

默认并发：

```text
低
```

不要让多个 Agent 同时随意操作同一个文件。

---

# 二百零四、资源锁

如果：

```text
Task A
```

正在修改：

```text
D:\Project
```

Task B：

不应该同时写。

实现：

```text
Resource Lock
```

---

# 二百零五、任务冲突

例如：

```text
Agent A
移动 file.txt

Agent B
删除 file.txt
```

系统：

```text
Conflict detected
```

用户确认或自动决策。

---

# 二百零六、Agent 工作区锁

Coding Agent：

```text
Project Path
```

允许：

```text
one writer
multiple readers
```

第一版至少警告并阻止明显冲突。

---

# 二百零七、Git Dirty State

执行 Coding Agent 前：

如果存在未提交修改：

```text
Working tree has uncommitted changes.
```

用户可：

```text
Continue
Cancel
```

默认：

**继续但提醒。**

不要擅自 stash。

---

# 二百零八、Agent 输出持久化

不要把所有输出永远存数据库。

策略：

```text
Task metadata
+
Compressed output
```

大日志保存在：

```text
local log file
```

数据库只保存：

```text
path / pointer
```

---

# 二百零九、日志清理

提供：

```text
Clear Logs
Clear Task History
Clear Agent Sessions
Clear Device Trust
Reset App
```

“Reset App” 必须明确：

```text
会删除什么
```

---

# 二百一十、数据目录

Windows：

建议：

```text
%LOCALAPPDATA%\OpenAgent\
```

例如：

```text
Config
Database
Logs
Cache
Sessions
Plugins
Downloads
```

Android：

使用：

```text
app internal storage
```

用户选择的文件使用 Storage Access Framework。

---

# 二百一十一、用户可导出

支持：

```text
Export Settings
Export Diagnostics
Export Audit Logs
```

绝对不能把：

```text
private keys
API keys
```

默认包含在 diagnostics 中。

---

# 二百一十二、诊断包

生成：

```text
OpenAgent-Diagnostics.zip
```

内容：

```text
version
OS version
provider status
network status
non-sensitive logs
```

自动脱敏。

---

# 二百一十三、README

README 必须包含：

```text
OpenAgent

What is it?
Features
Architecture
Screenshots
Getting started
Development
Security
Privacy
Agent CLI integration
MCP
Plugin
Relay
Build
Release
FAQ
Contributing
License
```

---

# 二百一十四、README 首页必须突出

```text
AI Control Center
Windows + Android
Local-first
Open Source
External Agent CLI support
```

---

# 二百一十五、安装文档

文档必须说明：

```text
Windows prerequisites
Android prerequisites
.NET
Visual Studio
Android Studio
SDK
Node/npm if needed
Cloudflare Wrangler
```

不要要求用户安装一堆不需要的软件。

---

# 二百一十六、开发环境

Windows：

推荐：

```text
Visual Studio
.NET SDK
Windows App SDK tooling
Windows 11 SDK
Git
```

Android：

```text
Android Studio
JDK
Android SDK
```

---

# 二百一十七、构建脚本

根目录：

```text
build.ps1
build.cmd
```

至少：

```text
build
test
package
```

---

# 二百一十八、开发脚本

提供：

```text
scripts/
├── bootstrap.ps1
├── build.ps1
├── test.ps1
├── package.ps1
├── lint.ps1
└── release.ps1
```

---

# 二百一十九、CI

GitHub Actions：

```text
build
test
lint
security scan
package
```

Windows：

```text
x64
```

未来：

```text
ARM64
```

---

# 二百二十、Android CI

至少：

```text
assembleDebug
test
lint
```

未来：

```text
instrumentation test
```

---

# 二百二十一、版本号

采用：

```text
Semantic Versioning
```

格式：

```text
MAJOR.MINOR.PATCH
```

例如：

```text
0.1.0
0.2.0
0.9.0
1.0.0
```

---

# 二百二十二、Release Channel

支持：

```text
Nightly
Canary
Beta
Stable
```

第一阶段：

```text
Nightly
Stable
```

---

# 二百二十三、Crash Recovery

如果数据库迁移失败：

不能启动即删数据库。

必须：

```text
backup
migration
verify
commit
```

失败：

```text
rollback
```

---

# 二百二十四、数据库备份

升级数据库前：

```text
backup.sqlite
```

保留最近：

```text
3
```

份。

---

# 二百二十五、配置迁移

版本变化：

```text
v0 → v1
```

必须有 migration。

不能：

```text
delete config
```

---

# 二百二十六、协议版本

消息：

```text
version
```

必须兼容：

```text
N
N-1
```

至少预留。

---

# 二百二十七、能力协商

连接后：

```json
{
  "capabilities": [
    "file-transfer",
    "screenshot",
    "agent",
    "clipboard"
  ]
}
```

Android 可以据此决定显示哪些按钮。

---

# 二百二十八、功能降级

如果 Windows 老版本不支持：

```text
remote-screen
```

Android UI：

```text
Remote screen unavailable
```

而不是整个设备 offline。

---

# 二百二十九、CLI 能力降级

例如 Claude Code 支持：

```text
stream-json
```

则启用流式事件。

如果只支持文本：

```text
fallback = plain text
```

如果完全无法自动化：

```text
interactive terminal
```

---

# 二百三十、不要为了 Provider 写死业务逻辑

错误：

```csharp
if (provider == "codex")
{
    ...
}
```

大规模散落整个项目。

正确：

```text
Provider adapter
```

业务层只知道：

```text
IAgentProvider
```

---

# 二百三十一、Windows Native Agent 与外部 CLI 的关系

二者必须平级：

```text
IAgentProvider
├── NativeAgent
├── Codex
├── Claude
├── OpenCode
├── Pi
└── Generic CLI
```

---

# 二百三十二、用户体验

用户不应该理解：

```text
Provider
Tool
MCP
Relay
Transport
Session
```

普通用户看到：

```text
Agent
设备
任务
权限
设置
```

开发者模式才展示：

```text
Provider
Tool Call
Protocol
Relay
Trace
```

---

# 二百三十三、开发者模式

设置：

```text
Developer Mode
```

开启后：

```text
Protocol Inspector
Agent Trace
Raw CLI Output
Tool Registry
MCP Inspector
Relay Diagnostics
```

---

# 二百三十四、普通模式

隐藏：

```text
raw JSON
stack trace
WebSocket frame
private path
```

只显示人类可理解的信息。

---

# 二百三十五、错误码

定义：

```text
OA-1000 Network
OA-2000 Auth
OA-3000 Permission
OA-4000 Agent
OA-5000 Tool
OA-6000 File
OA-7000 Provider
OA-8000 Plugin
```

---

# 二百三十六、支持页面

应用内：

```text
Help
Diagnostics
Open GitHub
Report Bug
Security
Privacy
```

安全漏洞不能要求公开 Issue。

应指向：

```text
SECURITY.md
```

---

# 二百三十七、国际化错误

所有错误同时支持：

```text
中文
English
```

内部 error code 不翻译。

---

# 二百三十八、通知

Windows：

```text
任务完成
任务需要批准
设备上线
设备离线
文件传输完成
```

Android：

```text
Windows 在线
任务完成
等待确认
文件传输
```

通知不能泄露敏感内容。

例如：

不要：

```text
“Agent 即将删除 D:\Users\xxx\password.txt”
```

可以：

```text
“Agent 请求执行高风险文件操作”
```

点击后查看详细内容。

---

# 二百三十九、默认权限策略

首次安装默认：

```text
Read-only / Ask
```

不要默认：

```text
Full Access
```

---

# 二百四十、用户选择权限

设置：

```text
Agent Access

● Ask before actions
○ Auto approve
○ Full access
```

高级设置：

```text
Per-tool permissions
Per-provider permissions
Per-device permissions
Per-folder permissions
```

---

# 二百四十一、目录沙盒

用户可以配置：

```text
Allowed Folders
```

例如：

```text
D:\Projects
D:\Documents
D:\Downloads
```

Agent 只能在这些目录执行文件写操作。

系统目录默认：

```text
Blocked
```

---

# 二百四十二、外部 Agent 权限

外部 Codex / Claude / OpenCode / Pi：

必须继承 OpenAgent 权限。

也就是说：

```text
Android
↓
OpenAgent Permission
↓
External CLI
```

不能出现：

```text
OpenAgent：
Ask

但 Codex：
Full system shell
```

绕过权限。

---

# 二百四十三、CLI sandbox

如果外部 Agent 本身支持 sandbox：

优先使用它。

如果 OpenAgent 权限低：

不能通过：

```text
--dangerously-skip-permissions
```

之类的参数自动绕过用户设置。

除非：

**用户明确选择完全访问。**

---

# 二百四十四、Agent 命令记录

外部 CLI：

记录：

```text
executable
arguments
working directory
provider
session
```

敏感环境变量：

必须脱敏。

---

# 二百四十五、Environment Variables

用户可以配置：

```text
ENV_NAME
ENV_VALUE
```

但 UI：

默认隐藏：

```text
API_KEY
TOKEN
SECRET
PASSWORD
```

显示：

```text
••••••••
```

---

# 二百四十六、Generic CLI Security

Generic CLI 是危险能力。

必须：

```text
默认关闭自动执行
必须用户添加
明确可执行文件路径
验证签名/路径
允许用户设置参数模板
```

---

# 二百四十七、Provider 删除

用户可以：

```text
Disable
Delete configuration
Rescan
```

不能卸载第三方 Agent。

OpenAgent 只管理：

```text
integration
```

---

# 二百四十八、刷新 CLI

设置：

```text
Rescan Agent CLI
```

重新：

```text
PATH
known directories
version
help
```

---

# 二百四十九、Provider 更新

不要主动更新外部 CLI。

只提供：

```text
Open in terminal
Check docs
```

以后才考虑：

```text
Update Provider
```

---

# 二百五十、Agent Prompt 模板

用户可以保存：

```text
Prompt Templates
```

例如：

```text
代码审查
Bug 修复
项目分析
重构
测试
文档
```

---

# 二百五十一、快捷键

Windows：

```text
Alt + Space
```

未来：

```text
Ctrl + Shift + A
```

Android：

```text
长按 Agent
```

---

# 二百五十二、Command Palette

Windows：

支持：

```text
搜索应用
搜索设备
搜索任务
搜索设置
启动 Agent
```

统一入口。

---

# 二百五十三、Agent 交互示例

用户：

```text
把桌面上今天的图片整理一下。
```

系统：

```text
发现 18 张图片。

分类：
截图 7
照片 8
其他 3

准备移动到：

D:\Desktop\整理\2026-09-25

需要你的批准。
```

用户：

```text
批准。
```

然后：

```text
[1/18] moving ...
[2/18] moving ...
...
```

最后：

```text
完成。

18 个文件
18 个成功
0 个失败
```

---

# 二百五十四、Agent 与手机

Android：

```text
把电脑 Downloads 中今天的 PDF 找出来。
```

Windows：

```text
扫描 D:\Downloads
```

返回：

```text
找到 4 个 PDF。

[全部发送到手机]
```

用户点击。

开始传输。

---

# 二百五十五、跨设备工作流

必须考虑：

```text
Phone → Windows
Windows → Phone
Agent → Windows
Agent → Android
Android → Agent → Windows
```

未来：

```text
Windows → Agent → Cloud
```

---

# 二百五十六、未来 AI Video Call 架构预留

不要第一版实现完整视频通话。

但必须预留：

```text
ICameraSource
IAudioSource
IVisionProvider
ISpeechToText
ITextToSpeech
IRealtimeSession
```

未来可以：

```text
Camera
↓
Realtime AI
↓
Voice
↓
Screen
```

---

# 二百五十七、未来 Android Screen Control

预留：

```text
IAccessibilityController
IScreenCaptureController
INotificationController
```

第一版不强制。

---

# 二百五十八、未来 Android Agent

最终 Android 也可以拥有：

```text
Native Agent
```

例如：

```text
拍照
↓
OCR
↓
整理
↓
上传电脑
```

当前先让 Android 作为：

**移动控制端**

---

# 二百五十九、未来云端同步

Sync Provider：

```text
ISyncProvider
```

实现：

```text
LocalFolderSync
WebDavSync
```

未来：

```text
Nextcloud
Nutstore
OneDrive
Google Drive
```

第一版无需绑定具体云平台。

---

# 二百六十、文件冲突

同步：

```text
Conflict detected
```

显示：

```text
Local
Remote
Newest
Keep both
```

禁止自动覆盖敏感数据。

---

# 二百六十一、迁移

项目以后可能从：

```text
0.1
```

升级到：

```text
1.0
2.0
```

所以：

```text
数据库
配置
协议
插件
```

都必须版本化。

---

# 二百六十二、代码规范

C#：

```text
nullable enable
implicit usings
analyzers
async suffix
CancellationToken
```

Kotlin：

```text
coroutines
sealed interface
StateFlow
immutable UI state
```

---

# 二百六十三、禁止的坏代码

禁止：

```text
God Class
God Service
Global static mutable state
Magic strings
UI business logic
sync IO in UI
unhandled exceptions
```

---

# 二百六十四、异常处理

每一层处理：

```text
Domain
Application
Infrastructure
UI
```

不要：

```text
catch(Exception) {}
```

吞异常。

必须：

```text
log
translate
recover
```

---

# 二百六十五、依赖原则

优先：

```text
成熟
活跃
有许可证
可审计
跨平台
```

不要：

```text
随意添加 50 个 NuGet
```

每个依赖都要回答：

```text
为什么需要？
替代方案是什么？
许可证是什么？
维护状态怎么样？
```

---

# 二百六十六、依赖许可证检查

CI：

检查：

```text
MIT
Apache-2.0
BSD
```

对：

```text
GPL
AGPL
商业限制
```

必须人工标记。

---

# 二百六十七、供应链安全

依赖：

```text
lock version
hash
Dependabot/Renovate
security audit
```

不要随意：

```text
curl | bash
```

作为应用安装流程。

---

# 二百六十八、Release 安全

发布：

```text
SHA-256
signature
release notes
```

未来：

```text
Sigstore
```

---

# 二百六十九、GitHub 仓库结构

最终：

```text
OpenAgent/
│
├── apps/
│   ├── windows/
│   └── android/
│
├── core/
│   ├── agent/
│   ├── protocol/
│   ├── security/
│   ├── storage/
│   └── transport/
│
├── providers/
│   ├── native/
│   ├── codex/
│   ├── claude/
│   ├── opencode/
│   ├── pi/
│   └── generic/
│
├── tools/
│   ├── filesystem/
│   ├── shell/
│   ├── process/
│   ├── app/
│   ├── system/
│   ├── clipboard/
│   └── screen/
│
├── plugins/
│   └── sdk/
│
├── mcp/
│
├── relay/
│   └── cloudflare/
│
├── docs/
│
├── tests/
│
├── scripts/
│
├── .github/
│   └── workflows/
│
├── LICENSE
├── README.md
├── SECURITY.md
├── CONTRIBUTING.md
└── CHANGELOG.md
```

---

# 二百七十、代码仓库原则

不要把：

```text
Windows
Android
Relay
```

混成一个超大的工程文件。

模块边界必须清晰。

---

# 二百七十一、共享协议

Android 和 Windows 共享的数据结构可以：

```text
JSON Schema
```

作为协议源。

生成：

```text
C#
Kotlin
```

模型。

不要手动维护两份不同定义。

---

# 二百七十二、Schema Version

每个协议：

```text
version: 1
```

所有修改：

```text
backward compatibility
```

---

# 二百七十三、Remote Command Schema

例如：

```json
{
  "command": "system.get_info",
  "requestId": "uuid",
  "arguments": {}
}
```

响应：

```json
{
  "requestId": "uuid",
  "status": "success",
  "result": {}
}
```

---

# 二百七十四、Tool Result

标准化：

```json
{
  "success": true,
  "data": {},
  "error": null
}
```

失败：

```json
{
  "success": false,
  "data": null,
  "error": {
    "code": "OA-5001",
    "message": "Permission denied"
  }
}
```

---

# 二百七十五、错误信息原则

用户信息：

```text
简洁
有帮助
可操作
```

开发信息：

```text
完整
可诊断
```

---

# 二百七十六、开发日志

Agent 在开发本项目时必须维护：

```text
docs/dev-log.md
```

记录：

```text
日期
改动
原因
测试
已知问题
```

---

# 二百七十七、开发 Checklist

每完成模块：

```text
[ ] Code
[ ] Unit Test
[ ] Integration Test
[ ] Error Handling
[ ] Logging
[ ] Security
[ ] Documentation
```

---

# 二百七十八、不能伪造完成

禁止：

```text
“已经实现”
```

但实际上：

```text
只有 interface
TODO
mock
假数据
空实现
```

如果功能没有完成：

明确：

```text
NOT IMPLEMENTED
```

并记录原因。

---

# 二百七十九、Mock 的使用

开发早期允许：

```text
Mock Relay
Mock Agent
Mock Tool
Mock Device
```

但：

必须明确区分：

```text
Mock
Production
```

---

# 二百八十、第一阶段开发顺序

严格按：

```text
Phase 0
基础工程
↓
Phase 1
Windows Core
↓
Phase 2
Windows UI
↓
Phase 3
Native Agent
↓
Phase 4
Agent CLI Discovery
↓
Phase 5
Permission
↓
Phase 6
Protocol
↓
Phase 7
Relay
↓
Phase 8
Android
↓
Phase 9
File Transfer
↓
Phase 10
Remote Screen
↓
Phase 11
MCP
↓
Phase 12
Plugin
↓
Phase 13
Testing
↓
Phase 14
Packaging
```

---

# 二百八十一、Phase 0

创建：

```text
solution
projects
directory
CI
README
LICENSE
```

让整个项目：

```text
build
```

成功。

---

# 二百八十二、Phase 1 Windows Core

实现：

```text
DI
logging
storage
tool registry
security
task
event bus
```

---

# 二百八十三、Phase 2 Windows UI

实现：

```text
MainWindow
Sidebar
Dashboard
CommandCenter
Tasks
Settings
Tray
```

---

# 二百八十四、Phase 3 Native Agent

实现：

```text
IAgentProvider
NativeAgent
Agent loop
Tool calling
Approval
Task
Trace
```

---

# 二百八十五、Phase 4 CLI Discovery

实现：

```text
CliDiscoveryService
CapabilityDetector
ProviderAdapter
```

至少检测：

```text
codex
claude
opencode
pi
```

并提供：

```text
Generic CLI
```

---

# 二百八十六、Phase 5 Permission

实现：

```text
PermissionService
PolicyEngine
ApprovalService
AuditLog
```

---

# 二百八十七、Phase 6 Protocol

实现：

```text
MessageEnvelope
Serialization
Session
Heartbeat
Reconnect
Capability negotiation
```

---

# 二百八十八、Phase 7 Relay

实现：

```text
Cloudflare Worker
Durable Object
WebSocket
Room
Pairing
Forwarding
Rate limit
```

---

# 二百八十九、Phase 8 Android

实现：

```text
Compose
Home
Agent
Devices
Tasks
Settings
Pairing
WebSocket
```

---

# 二百九十、Phase 9 File Transfer

实现：

```text
stream
chunk
pause
resume
cancel
hash
```

---

# 二百九十一、Phase 10 Remote Screen

实现：

```text
screen capture
H264
stream
decode
touch
keyboard
```

---

# 二百九十二、Phase 11 MCP

实现：

```text
MCP Server
MCP Client
Tool bridge
Permission
```

---

# 二百九十三、Phase 12 Plugin

实现：

```text
SDK
manifest
loader
permissions
process isolation
```

---

# 二百九十四、Phase 13 Testing

必须补齐：

```text
unit
integration
security
e2e
```

---

# 二百九十五、Phase 14 Packaging

生成：

```text
Windows installer
Android APK
Relay deployment
```

---

# 二百九十六、MVP 验收标准

MVP 不算：

```text
“UI 看起来可以”
```

必须实际完成：

```text
Windows 启动
↓
自动发现 Agent CLI
↓
Native Agent Ready
↓
Android 安装
↓
扫码配对
↓
连接
↓
Android 发消息
↓
Windows Agent 收到
↓
调用 Tool
↓
权限确认
↓
执行
↓
结果返回 Android
```

这条链必须真实跑通。

---

# 二百九十七、首个 Demo

必须实现：

用户在 Android：

```text
帮我打开记事本。
```

Android：

```text
→ command.request
```

Windows：

```text
→ Agent
→ app.launch
```

权限：

```text
允许启动应用？
```

用户允许。

Windows：

```text
Notepad started
```

Android：

```text
✅ 记事本已打开
```

---

# 二百九十八、第二个 Demo

Android：

```text
截图给我看看。
```

Windows：

```text
screen.capture
```

返回：

```text
image
```

Android 显示。

---

# 二百九十九、第三个 Demo

Android：

```text
看看 Downloads 里今天有哪些 PDF。
```

Windows：

```text
file.search
```

返回。

Android：

```text
Found 5 PDFs
```

---

# 三百、第四个 Demo

Android：

```text
用 Codex 分析这个项目的编译问题。
```

Windows：

```text
Provider = Codex
Working Directory = selected project
```

执行：

```text
codex exec ...
```

输出实时回传。

---

# 三百零一、第五个 Demo

Android：

```text
把这个文件传到电脑。
```

启动：

```text
file transfer
```

完成：

```text
hash verified
```

---

# 三百零二、第六个 Demo

公网：

```text
Android on mobile network
        ↓
Cloudflare Relay
        ↓
Windows on home Wi-Fi
```

无需端口映射。

正常连接。

---

# 三百零三、第一版结束条件

只有同时满足：

```text
Build = Pass
Unit Tests = Pass
Integration = Pass
E2E = Pass
Security baseline = Pass
Installer = Pass
Android APK = Pass
Relay Deployment = Pass
Documentation = Pass
```

才能宣称：

```text
MVP Complete
```

---

# 三百零四、开发 Agent 工作方式

你作为 Coding Agent，必须遵守：

## 先检查

```text
目录
Git
SDK
dotnet
Android SDK
Java
Node
Cloudflare tooling
```

然后：

```text
确认环境
```

---

# 三百零五、不要重复询问已经明确的信息

用户已经明确：

```text
项目名称：OpenAgent
Android >= 10
Windows 主端
公网连接
免费优先
不开强制账号
开源
Native Agent 默认
CLI 可选
CLI 自动发现
权限四级
远程桌面
MCP
插件
本地优先
云同步可选
```

不得再次要求用户确认这些。

---

# 三百零六、遇到技术问题

先：

```text
阅读现有代码
↓
定位问题
↓
提出最小修改
↓
实现
↓
测试
```

不要：

```text
发现一个小问题
↓
重写整个项目
```

---

# 三百零七、遇到依赖版本变化

不要因为在线文档和本机版本不同就停止。

采用：

```text
当前稳定 API
```

并把版本写进：

```text
docs/compatibility.md
```

---

# 三百零八、遇到外部 Agent CLI 版本变化

使用：

```text
capability detection
```

而不是：

```text
version == xxx
```

除非真的需要。

---

# 三百零九、遇到免费 Relay 限制

不能偷偷：

```text
切换收费服务
```

必须：

```text
告诉用户
切换自托管
切换 P2P
降低 relay
```

---

# 三百一十、免费原则

OpenAgent 本身：

```text
开源
免费
无强制账号
无广告
```

用户可自己提供：

```text
AI API
Relay
WebDAV
```

---

# 三百一十一、AI 成本

OpenAgent 不承诺：

```text
AI API 永久免费
```

用户使用的 AI Provider：

```text
由用户自行承担其 API/订阅成本
```

如果使用本地：

```text
Ollama
LM Studio
```

可以完全不产生 API 费用。

---

# 三百一十二、本地模型优先

Native Agent 必须允许未来支持：

```text
Local LLM
```

不要强耦合 OpenAI。

---

# 三百一十三、AI 抽象

至少：

```text
IChatModel
IStreamingModel
IToolCallingModel
IVisionModel
IEmbeddingModel
```

第一阶段至少实现：

```text
IChatModel
IToolCallingModel
IStreamingModel
```

---

# 三百一十四、模型上下文

每次调用：

```text
system
developer
user
tool
```

角色必须清晰。

Tool Result：

不要伪装成 user。

---

# 三百一十五、Prompt 管理

系统 Prompt：

放：

```text
resources/prompts/
```

版本控制。

未来可以：

```text
prompt version
```

---

# 三百一十六、Agent Persona

默认：

```text
professional
concise
transparent
safe
```

不要让 Agent：

```text
假装它执行了没有执行的操作。
```

---

# 三百一十七、Tool 透明度

Agent 必须告诉用户：

```text
我将执行：
file.move
```

如果用户在高级设置中开启：

```text
compact mode
```

可以隐藏部分细节。

---

# 三百一十八、最终回答原则

Agent 完成任务后：

必须回答：

```text
做了什么
结果
失败什么
是否需要用户继续操作
```

例如：

```text
已完成。

18 个文件已整理。
2 个文件由于权限不足未移动。

建议：
以管理员权限重试，或者手动处理这 2 个文件。
```

不要：

```text
Done.
```

---

# 三百一十九、管理员权限

不要默认要求：

```text
Run as Administrator
```

尽量普通用户运行。

需要管理员权限时：

```text
明确说明
```

并只在对应动作上申请。

---

# 三百二十、UAC

如果必须：

```text
elevated helper
```

使用最小权限。

主程序不应该整个以管理员身份运行。

---

# 三百二十一、Windows Defender / SmartScreen

安装器：

必须签名能力预留。

不要：

```text
关闭 Defender
```

作为安装前提。

---

# 三百二十二、网络防火墙

Relay 模式：

不要求：

```text
开放公网入站端口
```

LAN 模式：

尽可能使用：

```text
Windows Firewall rule
```

且必须在用户确认后添加。

---

# 三百二十三、局域网发现

未来支持：

```text
mDNS
DNS-SD
UDP discovery
```

但第一版优先：

```text
QR pairing
```

---

# 三百二十四、LAN 优化

在同一 Wi-Fi：

```text
优先直连
```

不要通过公网 Relay 传：

```text
10 GB 文件
```

---

# 三百二十五、网络选择策略

```text
same LAN
→ LAN direct

otherwise
→ P2P if available

otherwise
→ Relay
```

---

# 三百二十六、Relay 状态

用户能看到：

```text
Connection:
Direct LAN
P2P
Relay
```

不必展示复杂技术名词。

可以显示：

```text
Direct
Relay
```

高级模式再显示：

```text
LAN / P2P / Relay
```

---

# 三百二十七、远程控制延迟

显示：

```text
Latency 32 ms
```

但不要把它作为绝对准确指标。

---

# 三百二十八、文件传输速度

显示：

```text
12.5 MB/s
```

来自：

```text
实际 bytes / time
```

不能假造。

---

# 三百二十九、测试设备

至少：

Windows：

```text
Windows 10/11
x64
```

重点：

```text
Windows 11
```

Android：

```text
Android 10
Android 12
Android 14
Android 15+
```

---

# 三百三十、Windows API 兼容

如果 API 在某些 Windows 版本不存在：

必须：

```text
Capability detection
```

而不是：

```text
OS version string only
```

---

# 三百三十一、架构原则总结

任何未来需求都必须问：

```text
这个功能应该属于哪个层？
```

如果答案是：

```text
UI
```

不能把：

```text
Security
Network
Database
Agent
```

塞进 UI。

---

# 三百三十二、最终逻辑

OpenAgent 的核心：

```text
                User
                 │
                 ▼
          Command Center
                 │
                 ▼
           Agent Core
                 │
       ┌─────────┼─────────┐
       │         │         │
   Provider     Plan      Context
       │         │         │
       └─────────┼─────────┘
                 ▼
            Tool Registry
                 │
                 ▼
          Permission Engine
                 │
                 ▼
            Tool Executor
                 │
       ┌─────────┼──────────┐
       ▼         ▼          ▼
    Windows    Android     MCP
     Tools     Tools      Tools
                 │
                 ▼
               Result
                 │
                 ▼
             Agent Core
                 │
                 ▼
               User
```

---

# 三百三十三、未来最终形态

最终项目可以发展为：

```text
                         OpenAgent
                              │
        ┌─────────────────────┼─────────────────────┐
        │                     │                     │
     Windows                Android              Cloud
        │                     │                     │
     Agent OS              Mobile Agent        Relay
        │                     │                     │
  ┌─────┼─────┐        ┌─────┼─────┐        ┌─────┼─────┐
  │     │     │        │     │     │        │     │     │
 Files Apps Screen   Camera Voice Files    Sync Relay Auth
  │     │     │        │     │     │
  └─────┴─────┴────────┴─────┴─────┘
                    │
                 AI Agent
                    │
          ┌─────────┼─────────┐
          │         │         │
       Native      MCP      External
       Agent               Agent CLI
                               │
                  ┌────────────┼────────────┐
                  │            │            │
                Codex        Claude       OpenCode
                               │
                               Pi
```

---

# 三百三十四、最终产品体验

理想状态下，用户不需要理解：

```text
Windows
Android
Relay
WebSocket
MCP
Provider
Tool
Agent
```

用户只需要说：

> “帮我完成这件事。”

OpenAgent 负责：

```text
理解
→
规划
→
请求权限
→
执行
→
验证
→
同步
→
反馈
```

---

# 三百三十五、开发执行要求

现在开始真正实施。

你的工作顺序必须是：

```text
1. 检查现有仓库
2. 建立项目结构
3. 创建基础 solution
4. 创建共享协议
5. 创建 Core
6. 创建 Windows Host
7. 创建 Tool Registry
8. 创建 Permission
9. 创建 Native Agent
10. 创建 UI
11. CLI Discovery
12. Provider Adapter
13. Protocol
14. Relay
15. Android
16. 配对
17. Remote command
18. File transfer
19. Remote screen
20. MCP
21. Plugin
22. Tests
23. Installer
24. Docs
```

---

# 三百三十六、开发节奏

不要一次生成几万个文件。

采用：

```text
小模块
→
编译
→
测试
→
再继续
```

每完成一个阶段：

```text
build
test
inspect errors
fix
```

---

# 三百三十七、严禁“伪完成”

下面都视为未完成：

```text
TODO
NotImplementedException
fake delay
mock response
hardcoded success
假在线
假连接
假文件传输
伪 CLI
静态 Demo
```

UI 可以先使用 Mock，但真正验收时必须切换真实实现。

---

# 三百三十八、功能优先级

如果时间或资源不足，按照以下顺序：

```text
P0
安全
连接
Agent
权限
Tool
Windows
Android 配对

P1
CLI
任务
文件
截图
日志

P2
公网
断线恢复
文件传输
远程桌面

P3
MCP
插件
工作流

P4
Camera
Vision
Android automation
高级云同步
```

---

# 三百三十九、P0 不能缺失

即使界面不完美：

P0 必须完整工作。

---

# 三百四十、UI 优先级

视觉优先级：

```text
信息层次
>
操作清晰
>
响应速度
>
动效
>
装饰
```

---

# 三百四十一、Agent 优先级

Agent 优先级：

```text
正确性
>
安全
>
透明
>
可恢复
>
速度
>
聪明程度
```

---

# 三百四十二、远程控制优先级

远程控制：

```text
安全
>
稳定
>
可恢复
>
兼容
>
延迟
>
画质
```

---

# 三百四十三、文件传输优先级

```text
数据正确
>
断点恢复
>
稳定
>
校验
>
速度
```

---

# 三百四十四、公网连接优先级

```text
安全
>
稳定
>
自动重连
>
简单
>
低成本
>
低延迟
```

---

# 三百四十五、文档优先级

每个复杂模块必须至少有：

```text
README
Architecture
Configuration
Troubleshooting
Security
```

---

# 三百四十六、架构文档

建立：

```text
docs/
├── architecture.md
├── protocol.md
├── security.md
├── agent.md
├── providers.md
├── plugins.md
├── mcp.md
├── relay.md
├── android.md
├── windows.md
├── remote-control.md
└── development.md
```

---

# 三百四十七、开发者文档

必须说明：

```text
如何添加 Tool
如何添加 Provider
如何添加 MCP Server
如何添加 Plugin
如何添加 UI Page
如何添加协议消息
如何写测试
```

---

# 三百四十八、第三方 Provider 文档

例如：

```text
docs/providers/codex.md
docs/providers/claude.md
docs/providers/opencode.md
docs/providers/pi.md
docs/providers/generic-cli.md
```

说明：

```text
Detection
Authentication
Capabilities
Command strategy
Limitations
Troubleshooting
```

---

# 三百四十九、CLI Provider 不确定性

因为第三方 CLI 随时可能变化：

任何 Provider：

必须包含：

```text
version detection
help detection
capability detection
fallback
error diagnosis
```

---

# 三百五十、API 设计原则

公共 API：

```text
small
stable
versioned
documented
```

不要让内部实现泄漏到插件 SDK。

---

# 三百五十一、未来插件兼容

Plugin API：

```text
v1
```

以后：

```text
v2
```

旧插件仍尽量：

```text
compatibility shim
```

---

# 三百五十二、数据库兼容

Plugin 自己可以拥有：

```text
plugin namespace
```

不能随意修改核心表。

---

# 三百五十三、用户删除插件

必须保留：

```text
plugin data
```

直到用户主动选择：

```text
Remove plugin and data
```

---

# 三百五十四、AI 数据删除

支持：

```text
Delete session
Delete task
Delete history
```

立即从数据库删除对应记录。

缓存文件也要清理。

---

# 三百五十五、临时文件

所有临时文件：

```text
temp
```

任务完成后：

```text
cleanup
```

失败也要：

```text
cleanup
```

---

# 三百五十六、Crash 时的数据

不要把：

```text
AI prompt
files
screenshots
```

写进 crash dump。

---

# 三百五十七、截图隐私

如果未来截图自动发送 AI：

必须允许：

```text
Never
Ask
Always for current task
```

---

# 三百五十八、麦克风隐私

Android：

录音时：

必须：

```text
系统麦克风指示
```

用户停止就立即关闭。

不能后台偷偷持续录音。

---

# 三百五十九、Camera 隐私

同样：

```text
显式授权
显式状态
可随时停止
```

---

# 三百六十、项目最终质量

目标不是：

```text
Hackathon Demo
```

而是：

```text
可长期维护的开源项目
```

所以代码：

```text
Readable
Testable
Secure
Modular
Documented
```

---

# 三百六十一、最终验收 Demo 流程

完整演示必须做到：

## 电脑

启动 Windows。

后台运行：

```text
OpenAgent
```

## 手机

打开 Android。

看到：

```text
DESKTOP-XXXX
Online
```

## 手机发送：

```text
帮我查看电脑当前运行的主要程序。
```

Windows：

```text
process.list
```

返回。

## 然后：

```text
帮我打开记事本。
```

请求批准。

用户批准。

## 再：

```text
截图给我。
```

Windows 截图。

Android 显示。

## 再：

```text
用 Codex 检查这个项目有没有编译错误。
```

显示：

```text
Codex
Working Directory
```

流式输出。

## 最后：

断开 Wi-Fi。

重新连接。

OpenAgent：

```text
Reconnecting...
```

恢复：

```text
Connected
```

任务状态不丢。

---

# 三百六十二、最终目标

项目最终应该让用户产生这样的体验：

> 我的 Windows 电脑上运行着一个真正的 AI Agent，而我的 Android 手机就是这个 Agent 的移动入口。

不仅可以：

```text
聊天
```

还可以：

```text
控制
执行
查看
传输
自动化
编程
管理
```

更重要的是：

```text
用户拥有控制权
```

用户自己选择：

```text
AI Provider
Agent CLI
权限
Relay
同步
设备
插件
MCP
```

OpenAgent 不应该成为用户数据的中心。

它应该成为：

> **用户自己的 AI 基础设施。**

---

# 三百六十三、最终执行指令

现在开始执行本项目。

不要只输出：

```text
架构说明
```

不要只输出：

```text
代码片段
```

不要只创建：

```text
Demo UI
```

必须：

```text
创建真实项目
↓
写真实代码
↓
建立真实模块
↓
编译
↓
测试
↓
修复
↓
再次编译
↓
继续下一阶段
```

如果现有代码存在问题：

```text
分析
→
最小修改
→
测试
```

如果某项功能因为第三方 API 或平台限制暂时不能完整实现：

必须：

```text
实现真实可运行的基础版本
+
建立抽象接口
+
记录限制
+
提供后续实现路径
```

不要用假数据冒充真实功能。

---

# 三百六十四、最终输出格式

每次完成一个开发阶段后，向用户汇报：

```text
## 完成内容

...

## 已实现

...

## 测试

...

## 构建结果

...

## 已知问题

...

## 下一阶段

...
```

不要长篇解释已经写进代码的内容。

重点报告：

```text
真实完成了什么
测试是否通过
还有什么问题
```

---

# 三百六十五、最高级原则

始终遵循：

> **Do not optimize for impressive demos. Optimize for trustworthy software.**

中文：

> **不要为了看起来厉害而开发，要为了真正可信、真正可用、真正可维护而开发。**

OpenAgent 第一目标：

```text
安全
```

第二目标：

```text
稳定
```

第三目标：

```text
真正能让用户用 AI 控制自己的 Windows
```

第四目标：

```text
让 Android 成为可靠的移动控制端
```

第五目标：

```text
让 Codex / Claude Code / OpenCode / Pi 等已有 Agent 成为可插拔的执行能力
```

最终形成：

```text
                 OpenAgent
                     │
          ┌──────────┴──────────┐
          │                     │
       Windows                Android
          │                     │
      Execute                Control
          │                     │
          └──────────┬──────────┘
                     │
                  AI Agent
                     │
         ┌───────────┼───────────┐
         │           │           │
      Native       MCP        External
       Agent                   Agents
                                 │
                  ┌──────────────┼──────────────┐
                  │              │              │
                Codex         Claude         OpenCode
                                                │
                                                Pi
```

这就是整个项目的最终产品方向。

现在开始从 **Phase 0** 执行，而不是继续停留在设计阶段。

在任何阶段都优先保证：

```text
安全 > 稳定 > 正确 > 可恢复 > 性能 > UI 装饰
```

并且所有未来功能都必须在不破坏核心架构的前提下加入。

---

# 三百六十六、UI 开发检查清单（每次动手前后各过一遍）

> 本章是「零、强制前置要求」的执行细则。任何 UI 相关改动，提交前必须逐项确认。

## 366.1 动手前

```text
[ ] 已访问并阅读 https://developer.apple.com/cn/design/human-interface-guidelines/
    的相关章节（列出章节名与要点，写进实现说明）
[ ] 已打开 design/pixso-final/index.html，找到对应的画板并对照
[ ] 已确认本次改动涉及哪些变体（平台 × 主题 × 状态），列全
[ ] 若涉及新页面，已确认该平台是否存在此导航入口
    （例：移动端没有「工具 / Provider / 插件」独立页面）
```

## 366.2 设计一致性

```text
[ ] 颜色全部取自 design/tokens.css，无硬编码色值
[ ] 字号 / 字重取自 token 阶梯（DM Sans：Regular / Medium / Bold）
[ ] 圆角只用两档：28（内容容器）/ 999（胶囊）
[ ] 嵌套铁律：内圆角 ≤ 外圆角
[ ] 间距取自 token 阶梯；Windows 水平外边距 96、Android 20
[ ] 左边缘基准线对齐：Windows 内容左边缘 = 336；Android = 20
[ ] 无新增阴影层级（仅底部悬浮元素使用 DROP_SHADOW）
```

## 366.3 状态与反馈

```text
[ ] 选中项：胶囊高亮背景 + 图标/文字/计数同时着强调色，其余项全灰
[ ] 列表行：内嵌分组卡片（圆角 28 + 内缩分隔线）
[ ] 会进入子视图的行带显示指示符（chevron-right）；
    展示详情用信息按钮（ⓘ），两者不可混用
[ ] 权限审批卡：有风险等级、可逆性、影响面，且覆盖输入框区域
[ ] 审批卡弹出时，meta 行与输入框一起淡出；批准后一起淡入
[ ] 状态不得只用颜色表达（必须同时有文字或图标）
```

## 366.4 可访问性（对应 HIG Accessibility）

```text
[ ] 键盘可完整导航，焦点可见
[ ] 对比度：正文 ≥ 4.5:1，大字与控件 ≥ 3:1
[ ] 触控目标 ≥ 44×44 pt
[ ] 尊重系统「减弱动效」设置
[ ] 字体缩放 200% 时布局不破裂
```

## 366.5 收尾

```text
[ ] 全部变体逐一核对（不允许只改一端或只改浅色）
[ ] 设计稿如有变更，已同步更新 Pixso 并重新导出到 design/pixso-final/
[ ] 已在 docs/dev-log.md 记录改动、理由、所依据的 HIG 条款
[ ] 如需偏离 design/ 标准，已记录理由并更新 design/ 目录
```

---

项目必须最终成为一个真实、可构建、可安装、可运行、可测试、可扩展、可开源维护的：

# OpenAgent

## Windows × Android Open-source AI Control Center

。
