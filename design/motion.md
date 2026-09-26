# OpenAgent — UI 动效提示词手册

> 本文件是 OpenAgent 的**动效唯一取值来源**（Motion Spec）。
> 所有效果均从 [MotionVault](https://xiyu519.github.io/MotionVault/) 的 202 个效果中挑选，
> 并按本项目「克制、可信、精确、有掌控感」的品牌气质重新定参。
> 与 [`tokens.css`](./tokens.css) 的动效 token、以及规格文档
> [第 138 节 · 动画原则](../OpenAgent%20完整项目总提示词.md) 冲突时，以本文件为准。

---

## 0. 怎么用这份文档

1. **先读 §1 全局规范** —— 所有动效共享同一套时长与缓动，任何单条动效都不得自造缓动曲线。
2. **按场景查 §2 索引表** 定位到具体条目（M-01 … M-29）。
3. **复制该条的 AI Prompt**（英文，React + Tailwind + Framer Motion）交给 AI 工具生成组件。
4. **落地前过一遍 §4 检查清单** —— 无障碍、性能预算、主题适配。

**已确认的 3 条基线动效**：`M-01`（开场问候语）、`M-05`（工具调用 shimmer）、
`M-11`（电脑版全部卡片），标记为 **【已确认】**，提示词为最终版，不得改动参数。

**动效预览**：[`motion-demo.html`](./motion-demo.html) —— 29 条动效的可交互演示页
（纯 CSS + 原生 JS，双击即可打开，含明暗主题切换与「减弱动效」开关）。
本文件负责「怎么写」，演示页负责「长什么样」，两者参数一致。

**技术栈说明**：本套 Prompt 以 React + Tailwind + Framer Motion 表达（便于直接粘给
AI 编程工具）。Windows 与 Android 各自的原生落地映射见 §5。

---

## 1. 全局动效规范

### 1.1 时长阶梯

| Token | 值 | 用途 |
|---|---|---|
| `--dur-instant` | **120ms** | 按压、开关、变色、悬停底色 |
| `--dur-state` | **200ms** | 菜单、悬浮、tooltip、焦点环 |
| `--dur-exit` | **240ms** | 退出（= 进入时长的 75%） |
| `--dur-layout` | **320ms** | 抽屉、模态、折叠、卡片让位 |
| `--dur-enter` | **480ms** | 页面进入、首屏内容块入场 |
| `--dur-ambient` | **2400–2600ms** | 循环类（进度、shimmer），仅限等待态 |

> Apple 设计系统的 `150 / 250 / 350ms` 三档是同一阶梯的近似值；实现时统一用上表。

### 1.2 缓动曲线

| Token | 值 | 语义 | Framer Motion |
|---|---|---|---|
| `--ease-out-expo` | `cubic-bezier(0.16, 1, 0.30, 1)` | **默认**入场、浮现、揭示 | `[0.16, 1, 0.3, 1]` |
| `--ease-out-quart` | `cubic-bezier(0.25, 1, 0.50, 1)` | 位移、让位、滑换 | `[0.25, 1, 0.5, 1]` |
| `--ease-in-out` | `cubic-bezier(0.65, 0, 0.35, 1)` | 循环、呼吸、往返 | `[0.65, 0, 0.35, 1]` |
| `--ease-in` | `cubic-bezier(0.70, 0, 0.84, 0)` | **退出**、收起、淡出 | `[0.7, 0, 0.84, 0]` |
| `--ease-apple` | `cubic-bezier(0.32, 0.72, 0, 1)` | Apple 体系交互（按钮/链接） | `[0.32, 0.72, 0, 1]` |
| `--ease-spring` | — | 仅模态、开关、气泡，**必须无可见过冲** | `{ type: 'spring', stiffness: 380, damping: 40, mass: 1 }` |

> 项目铁律：**无回弹、无弹性、无夸张缩放**。MotionVault 里的 bouncy 弹簧一律替换为
> 上面的 `--ease-spring`（阻尼比 ζ≈1.03，几乎不过冲）。

### 1.3 共享动效常量（建议落库位置）

```ts
// src/web/motion.ts —— 所有组件只从这里取参数
export const ease = {
  out:    [0.16, 1, 0.3, 1],
  outQ:   [0.25, 1, 0.5, 1],
  inOut:  [0.65, 0, 0.35, 1],
  in:     [0.7, 0, 0.84, 0],
  apple:  [0.32, 0.72, 0, 1],
} as const;

export const dur = { instant: .12, state: .2, exit: .24, layout: .32, enter: .48 } as const;

export const spring = { type: 'spring', stiffness: 380, damping: 40, mass: 1 } as const;

export const stagger = { unit: .12, line: .12, card: .06, fast: .04 } as const;
```

### 1.4 三条硬约束

1. **常驻动画禁止**。Idle CPU < 2% / RAM < 200MB 是硬指标 —— 所有循环动效只允许
   存在于「加载中 / 思考中 / 传输中」这类有明确终点的状态里，且状态结束后立刻卸载。
2. **必须可关闭**。全站尊重 `prefers-reduced-motion: reduce`（`tokens.css` 已内置降级）。
   在 reduce 模式下：循环动效 → 静态；位移/缩放 → 仅保留 opacity；stagger → 0。
3. **只动 `transform` 与 `opacity`**（`filter: blur()` 仅用于文字入场与 shimmer，
   且必须 `will-change` 后及时清除）。禁止动画化 `width/height/top/left/box-shadow`。

### 1.5 主题与配色适配（重要）

MotionVault 的效果默认基于 **zinc** 深色系。落到 OpenAgent 时必须做 token 映射，
并在浅色主题下单独定参，禁止硬编码色值：

| MotionVault | OpenAgent Token | 浅色主题处理 |
|---|---|---|
| `zinc-950` / `#09090B` | `--bg-canvas` | 保持亮底，改用中性文字色 |
| `zinc-900` | `--bg-surface` | — |
| 1px 边框 | `--border-subtle` | — |
| 白色高光 / 光斑 | `--accent`（`#007aff`，8–12% 透明） | 降低不透明度至 6–8% |
| `zinc-400` 次文字 | `--text-secondary` | — |

---

## 2. 场景 → 动效快速索引

| 场景 | 编号 | 动效（MotionVault 出处） | 触发方式 | 时长 |
|---|---|---|---|---|
| 开场问候语 | **M-01** | 模糊浮现 Blur Fade In（文字 01）【已确认】 | 自动播放 / 循环 | 700ms + 120ms×n |
| Command Center 唤出 | **M-02** | 模糊浮现（克制变体）（文字 01） | 快捷键 `Alt+Space` | 320ms |
| 页面切换 | **M-03** | 逐行揭示 Line Reveal（文字 09） | 路由切换 | 480ms + 120ms×n |
| 首屏内容块入场 | **M-04** | 模糊浮现 交错版（文字 01） | 进入视口 | 700ms + 60ms×n |
| Agent 思考 / 工具调用中 | **M-05** | 流光文字 Gradient Shine（文字 05）【已确认】 | 状态驱动 / 悬停重播 | 2500ms + 5s 间隔 |
| 流式输出前缓冲 | **M-06** | 三点跳跃 Dots Bounce（加载 02） | 状态驱动 | 1200ms 循环 |
| 首屏骨架屏 | **M-07** | 骨架屏 Skeleton（加载 08） | 数据未就绪 | 1600ms 循环 |
| 多步任务进度 | **M-08** | 多步加载 Multi-Step（加载 09） | 状态推进 | 900ms / 步 |
| 任务完成度环 | **M-09** | 环形进度 Ring Progress（SVG 05） | 数值变化 | 900ms |
| 文件传输进度 | **M-10** | 流光进度 Loading Bar（加载 14） | 进度事件 | 2400ms |
| **电脑版全部卡片** | **M-11** | 聚光灯卡片 Spotlight（卡片 02）【已确认】 | 移动鼠标 | 跟随指针 |
| 卡片网格聚焦 | **M-12** | 聚焦卡组 Focus Cards（卡片 25） | 悬停 | 320ms |
| 横排卡片组展开 | **M-13** | 悬停扩展卡片组 Hover Expand（卡片 28） | 悬停 | 420ms |
| 审批卡展开详情 | **M-14** | 弹性展开卡 Expandable Card（卡片 39） | 点击 | 380ms |
| 任务时间线堆叠 | **M-15** | 滚动堆叠卡 Scroll Stack（卡片 06） | 滚动 | 跟随滚动 |
| 主按钮悬停 | **M-16** | 微光按钮 Shimmer Button（按钮 06） | 悬停 / 自动 | 2000ms + 4s 间隔 |
| 高风险操作确认 | **M-17** | 长按确认 Hold to Confirm（按钮 12） | 长按 1200ms | 1200ms |
| 次级按钮悬停 | **M-18** | 文字滑换 Text Swap（按钮 07） | 悬停 | 260ms |
| 指针按下反馈 | **M-19** | 点击波纹 Ripple（按钮 03） | 点击 | 600ms |
| 传输 / 下载按钮 | **M-20** | 下载变形 Download Morph（按钮 11） | 点击 | 2000ms |
| 模态弹出 | **M-21** | 弹簧弹窗 Spring Modal（弹簧 02） | 点击 / 事件 | 320ms |
| 气泡提示 | **M-22** | 弹性气泡 Spring Tooltip（弹簧 04） | 悬停 / 聚焦 | 200ms |
| 设置开关 | **M-23** | 弹性开关 Elastic Toggle（弹簧 06） | 点击 | 260ms |
| 审批通过 | **M-24** | 描绘打勾 Draw Check（SVG 06） | 状态变化 | 800ms |
| 统计数字 | **M-25** | 滚动数字 Rolling Counter（文字 13） | 进入视口 | 1200ms |
| 输入校验失败 | **M-26** | 抖动输入框 Wobble Input（弹簧 05） | 校验失败 | 400ms |
| 通知 / Toast | **M-27** | 通知卡叠展开 Notification Stack（卡片 31） | 事件推送 | 320ms |
| 侧边导航切换 | **M-28** | 融合导航 Gooey Nav（弹簧 11） | 点击 | 300ms |
| 起始页背景氛围 | **M-29** | 鼠标点亮网格 Spotlight Grid（背景 06） | 移动鼠标 | 跟随指针 |

分类页链接：`text` · `cards` · `buttons` · `loaders` · `spring` · `svg` · `scroll` · `backgrounds`
（完整地址：`https://xiyu519.github.io/MotionVault/<分类>`）

---

## 3. 动效明细

### 组 1 · 启动与进入

---

#### M-01 · 开场问候语 · 模糊浮现 【已确认】

| 字段 | 值 |
|---|---|
| **使用场景** | 起始页首次打开时的问候语（「你好，今天想做什么？」），以及新会话空白页的引导文案 |
| **动效类型** | 文字入场：逐词/逐字 opacity + blur + 位移，带 stagger |
| **时长** | 每单元 700ms；单元间隔 120ms |
| **缓动** | `[0.16, 1, 0.3, 1]`（`--ease-out-expo`） |
| **触发方式** | 自动播放；`loop` 每 4200ms 重播一次 |
| **适用组件** | `GreetingHero`、`EmptyState` 标题、会话首条 Agent 消息 |
| **MotionVault** | [文字动效 01 · 模糊浮现](https://xiyu519.github.io/MotionVault/text) · 自动播放 |

**AI Prompt**

```text
Create a React component called BlurFadeIn using React + Tailwind CSS + Framer Motion.
It receives a `text` prop and renders it split into units: split by words when the text
contains spaces, otherwise split per character (good for Chinese). Each unit is an
inline-block motion.span animated from { opacity: 0, filter: 'blur(8px)', y: 10 } to
{ opacity: 1, filter: 'blur(0px)', y: 0 } with duration 0.7s, ease [0.16, 1, 0.3, 1],
and a 0.12s stagger between units (configurable via `stagger` prop, plus a `delay` prop
for the first unit). Support a `loop` prop that remounts the units on an interval
(default ~4.2s) so the effect replays continuously. Keep the text selectable/accessible
via an aria-label on the wrapper.
```

**落地备注** · 中英混排时按 `\s` 切词优先（中文无空格 → 逐字），单元用 `inline-block` +
`whitespace-pre` 保留原始空格；reduce 模式下合并为单次 200ms opacity 淡入。

---

#### M-02 · Command Center 唤出

| 字段 | 值 |
|---|---|
| **使用场景** | `Alt+Space` 唤起全局 Command Center 面板 / 托盘弹窗 / 快捷指令面板 |
| **动效类型** | 面板入场：scale + opacity（**无位移、无模糊**，避免打断焦点） |
| **时长** | 进入 320ms / 退出 240ms |
| **缓动** | 进入 `[0.16, 1, 0.3, 1]`；退出 `[0.7, 0, 0.84, 0]` |
| **触发方式** | 快捷键事件驱动；Esc 或失焦反向播放 |
| **适用组件** | `CommandCenter`、`TrayPopup`、任何 `Popover` 容器 |
| **MotionVault** | [文字动效 01](https://xiyu519.github.io/MotionVault/text) 入场语汇的克制变体 |

**AI Prompt**

```text
Create a React component called PanelReveal using React + Tailwind + Framer Motion.
It wraps a floating panel and animates it on mount/unmount following the spec:
open = scale 0.97 -> 1 with opacity 0 -> 1 over 0.32s, ease [0.16, 1, 0.3, 1];
close = scale 1 -> 0.98 with opacity 1 -> 0 over 0.24s, ease [0.7, 0, 0.84, 0].
Use AnimatePresence with mode="wait". No y-offset, no blur, no rotation, no spring
overshoot. transform-origin at the trigger element. Must not block input:
pointer-events enabled immediately on open. Respect prefers-reduced-motion by
falling back to a 0.12s opacity-only fade.
```

**落地备注** · 这是规格文档第 51 节原文的动效，参数与本节一致；进入动画 320ms 内不得
锁定焦点，输入框需立即可输入。

---

#### M-03 · 页面切换

| 字段 | 值 |
|---|---|
| **使用场景** | 起始页 ↔ 对话态 ↔ 任务 / 设备 / 工具 / Provider / 插件 / 设置 之间的主导航切换 |
| **动效类型** | 内容逐行从遮罩下方揭示（`overflow-hidden` + y 位移），行级 stagger |
| **时长** | 每行 480ms；行间隔 120ms |
| **缓动** | `[0.16, 1, 0.3, 1]` |
| **触发方式** | 路由变化（`AnimatePresence` + `key={pathname}`） |
| **适用组件** | 页面级容器、`SectionHeader`、设置分组、任务列表首屏 |
| **MotionVault** | [文字动效 09 · 逐行揭示](https://xiyu519.github.io/MotionVault/text) |

**AI Prompt**

```text
Create a React component called LineReveal using React + Tailwind + Framer Motion.
It accepts a list of block elements (or splits children into rows) and reveals them
one by one: each row sits in an overflow-hidden wrapper and animates from y: '110%'
to y: 0 with opacity 0 -> 1, duration 0.48s, ease [0.16, 1, 0.3, 1], staggered by
0.12s per row. Add a subtle blur(6px) -> blur(0px) on each row that resolves in the
first 60% of the animation. Provide a `stagger` prop and a `delay` prop. Rows must
keep their layout flow so headers and controls never overlap during the transition.
Reduce-motion: single 0.2s opacity fade for the whole group.
```

**落地备注** · 页面级切换只用 **一次** `LineReveal`，不要嵌套（嵌套 stagger 会超过 1.5s，
违反「短时长」原则）。退出统一 240ms opacity 淡出。

---

#### M-04 · 首屏内容块入场

| 字段 | 值 |
|---|---|
| **使用场景** | 任务卡 / 设备卡 / 插件卡等列表首屏一次性入场 |
| **动效类型** | 模糊浮现，块级 stagger |
| **时长** | 每块 700ms；块间隔 60ms |
| **缓动** | `[0.16, 1, 0.3, 1]` |
| **触发方式** | 进入视口（`whileInView`，`once: true`） |
| **适用组件** | 卡片网格、列表容器、统计区块 |
| **MotionVault** | [文字动效 01](https://xiyu519.github.io/MotionVault/text) 交错版 |

**AI Prompt**

```text
Create a React component called StaggerIn using React + Tailwind + Framer Motion.
It renders children as staggered blocks: each block animates from { opacity: 0,
filter: 'blur(8px)', y: 10 } to { opacity: 1, filter: 'blur(0px)', y: 0 } with
duration 0.7s, ease [0.16, 1, 0.3, 1] and a 0.06s stagger between blocks. Use
whileInView with viewport={{ once: true, amount: 0.2 }} so it plays only on first
entry. Cap the stagger so total animation never exceeds 1.2s regardless of item
count (clamp index * stagger). Clear will-change: filter after completion.
```

**落地备注** · 列表项 > 15 时改为按窗口分批入场（`clamp(i, 0, 12) * stagger`），
避免长列表末尾元素延迟过久。

---

### 组 2 · 等待与进行中

---

#### M-05 · 工具调用 / Agent 思考中 · 流光文字 【已确认】

| 字段 | 值 |
|---|---|
| **使用场景** | Agent 思考中、工具调用进行中、计划生成中、任何「正在工作」的行内文案 |
| **动效类型** | 纯 CSS 渐变扫光（`background-clip: text`） |
| **时长** | 单次扫过 2500ms；间隔 5000ms |
| **缓动** | linear（扫光自身匀速） |
| **触发方式** | 状态驱动（`isWorking`）；支持 `hover` 触发单次 |
| **适用组件** | `ToolCallRow`、`ThinkingIndicator`、`StatusText`、`StreamingLabel` |
| **MotionVault** | [文字动效 05 · 流光文字](https://xiyu519.github.io/MotionVault/text) |

**AI Prompt**

```text
Create a React shimmer-text component using CSS only: apply a linear
gradient (120deg, base color #09090B with a bright white band 30% wide) via
background-clip: text and transparent text fill, then animate
background-position from -150% to 150% over 2.5s with a 5s repeat delay.
Support triggering a single pass on hover. Tailwind + a small custom
keyframes utility.
```

**落地备注** · MotionVault 原效果用 `#09090B` 基色 + 白带高光，**只适用于浅色底**，
深色底上文字会消失。落地时基色统一改用 `--text-tertiary`（保证可读），亮带在深色主题
用 92% 白、浅色主题用 `--text-primary`。`.shimmer` 只动 `background-position`，
零 `box-shadow`、零布局属性。reduce 模式下禁用循环，只保留静态色。
> 实现与对照见 [`motion-demo.html`](./motion-demo.html) 的 M-05。

---

#### M-06 · 流式输出前的缓冲

| 字段 | 值 |
|---|---|
| **使用场景** | 已发出请求、首个 token 尚未到达的空档期（通常 < 2s） |
| **动效类型** | 三点依次起落 |
| **时长** | 单点 600ms，整轮 1200ms 循环 |
| **缓动** | `[0.65, 0, 0.35, 1]` |
| **触发方式** | 状态驱动，首 token 到达即卸载 |
| **适用组件** | 对话流占位、`TypingIndicator` |
| **MotionVault** | [加载 02 · 三点跳跃](https://xiyu519.github.io/MotionVault/loaders) |

**AI Prompt**

```text
Create a React component called TypingDots using Tailwind CSS only (no JS timers).
Three 6px circles in a row; each animates y from 0 to -4px and opacity 0.4 -> 1
then back, with a 1.2s looping keyframe and 0.2s delay between dots, easing
cubic-bezier(0.65, 0, 0.35, 1). Dots use the current text color at 60% opacity.
Add an sr-only "正在生成" label. Disable the animation under
prefers-reduced-motion and show three static dots instead.
```

---

#### M-07 · 首屏骨架屏

| 字段 | 值 |
|---|---|
| **使用场景** | 任务列表 / 设备列表 / 插件列表首屏数据未就绪 |
| **动效类型** | 骨架块 + 微光扫过 |
| **时长** | 1600ms 循环 |
| **缓动** | `[0.65, 0, 0.35, 1]` |
| **触发方式** | 数据加载中 |
| **适用组件** | `TaskCardSkeleton`、`DeviceCardSkeleton`、`PluginGridSkeleton` |
| **MotionVault** | [加载 08 · 骨架屏](https://xiyu519.github.io/MotionVault/loaders) |

**AI Prompt**

```text
Create React skeleton components using Tailwind + a CSS shimmer: cards composed of
a 40px circular avatar placeholder, two text bars (widths 60% / 85%) and an optional
image block, all filled with --bg-inset and rounded per the card radius. A soft
highlight sweeps across every skeleton block via a ::after pseudo-element animating
translateX from -100% to 100% over 1.6s with a 0.2s stagger between blocks, easing
cubic-bezier(0.65, 0, 0.35, 1). Skeletons must match the real card's exact box
metrics so there is zero layout shift on swap. aria-busy="true" on the container.
```

---

#### M-08 · 多步任务进度

| 字段 | 值 |
|---|---|
| **使用场景** | 任务中心展开某任务的计划步骤（plan steps）时，展示当前执行到第几步 |
| **动效类型** | 步骤行依次点亮 + 已完成行描绘对勾 |
| **时长** | 每步 900ms |
| **缓动** | `[0.16, 1, 0.3, 1]` |
| **触发方式** | 状态推进（每个工具调用返回后前进一格） |
| **适用组件** | `TaskTimeline`、`PlanSteps`、`ToolCallList` |
| **MotionVault** | [加载 09 · 多步加载](https://xiyu519.github.io/MotionVault/loaders) |

**AI Prompt**

```text
Create a React component called StepProgress using React + Tailwind + Framer Motion.
It receives steps: { label, state: 'done' | 'active' | 'pending' }[].
Done rows advance instantly with a stroke-drawn check (SVG pathLength 0 -> 1 over
0.4s) and their label shifts to --text-secondary. The active row lights up with a
1px accent left-rail that grows from 0 to full height over 0.3s plus a shimmer on
the label; pending rows stay at --text-tertiary. Each state change transitions in
0.32s ease [0.16, 1, 0.3, 1]. Never re-animate completed rows on re-render.
```

---

#### M-09 · 任务完成度环

| 字段 | 值 |
|---|---|
| **使用场景** | 任务详情头部的完成百分比、设备配对进度、批量操作进度 |
| **动效类型** | SVG 环形描边 + 中心等宽数字同步计数 |
| **时长** | 900ms |
| **缓动** | `[0.16, 1, 0.3, 1]`（数字用同曲线插值） |
| **触发方式** | 数值变化（含 0 → n 首次入场） |
| **适用组件** | `ProgressRing`、`TaskHeader`、`PairingProgress` |
| **MotionVault** | [SVG 05 · 环形进度](https://xiyu519.github.io/MotionVault/svg) |

**AI Prompt**

```text
Create a React component called ProgressRing using SVG + Framer Motion. Props: value
(0-100), size (default 44), stroke (default 3). Draw a --border-subtle track circle
and an accent arc using strokeDasharray with pathLength=1, animating strokeDashoffset
from 1 to 1 - value/100 over 0.9s ease [0.16, 1, 0.3, 1]. Center label uses a tabular-nums
mono font and counts up in sync with the same easing (no spring overshoot). Rotate the
arc -90deg so it starts at 12 o'clock. Respect reduce-motion by jumping straight to the
final value in 0.2s.
```

**落地备注** · 数字必须 `font-variant-numeric: tabular-nums`（`tokens.css` 已有 `.num`），
否则计数时宽度抖动。

---

#### M-10 · 文件传输进度

| 字段 | 值 |
|---|---|
| **使用场景** | Windows ↔ Android 文件传输、模型/插件下载、批量导出 |
| **动效类型** | 细轨道填充 + 内部高光持续扫过 |
| **时长** | 2400ms；真实进度由事件驱动，不按时间走完 |
| **缓动** | linear（内部高光）；进度填充用 `[0.25, 1, 0.5, 1]` |
| **触发方式** | 进度事件（chunk 到达） |
| **适用组件** | `TransferBar`、`DownloadRow`、`PluginInstallBar` |
| **MotionVault** | [加载 14 · 流光进度](https://xiyu519.github.io/MotionVault/loaders) |

**AI Prompt**

```text
Create a React component called TransferBar using React + Tailwind + Framer Motion.
A 4px rounded track (--bg-inset) with an accent fill whose width reflects real
progress. The fill width animates with duration 0.3s ease [0.25, 1, 0.5, 1] so
discrete progress events look continuous. Inside the fill, a translucent highlight
band sweeps from left to right every 2.4s, looping, using a transform-only keyframe.
When progress reaches 100%, the sweep stops, the bar fades to --status-online over
0.4s and a "已完成" label fades in. Show "12.4 MB / 48.0 MB · 1.2 MB/s" in a mono
tabular-nums label above. aria-valuenow/valuemin/valuemax on a role="progressbar".
```

---

### 组 3 · 卡片（电脑版）

---

#### M-11 · 电脑版全部卡片 · 聚光灯卡片 【已确认】

| 字段 | 值 |
|---|---|
| **使用场景** | **Windows 端所有卡片统一底层交互**：任务卡、设备卡、工具卡、Provider 卡、插件卡、设置分组卡 |
| **动效类型** | 跟随指针的径向光晕 + 边框局部提亮（CSS 自定义属性驱动） |
| **时长** | 光晕跟随无补间（直接跟随指针）；进出 200ms |
| **缓动** | 光晕淡入淡出 `[0.16, 1, 0.3, 1]` |
| **触发方式** | `mousemove`（指针进入卡片范围）；离开时淡出归零 |
| **适用组件** | `Card`（基础组件，全站卡片继承）、`TaskCard`、`DeviceCard`、`ToolCard`、`ProviderCard`、`PluginCard` |
| **MotionVault** | [卡片动效 02 · 聚光灯卡片](https://xiyu519.github.io/MotionVault/cards) · 移动鼠标 |

**AI Prompt**

```text
Create a React spotlight card: a dark card (zinc-900 on zinc-950) with a
radial-gradient spotlight (radius 220px, white at 8% opacity fading to
transparent) that follows the cursor via CSS custom properties --x/--y
updated on mousemove. Also brighten the 1px border near the cursor using a
masked gradient border. Smooth, GPU-friendly (transform/opacity only).
```

**落地备注**
- 浅色主题下：光晕改用 `--accent` 6% 透明度，边框提亮改用 `--border-strong`，
  否则白底上光晕完全不可见。
- 光晕层用 `pointer-events: none` + `absolute inset-0`，并给容器
  `will-change: background` 之外的属性一律不动画化。
- **全站卡片共用这一个实现**（`<Card spotlight>`），不得为单个页面另写一份。
- reduce 模式下光晕完全禁用，卡片只保留 1px 边框色变（120ms）。

---

#### M-12 · 卡片网格聚焦

| 字段 | 值 |
|---|---|
| **使用场景** | 插件网格、工具网格、Provider 列表中「悬停某张、其余退后」的聚焦浏览 |
| **动效类型** | 悬停卡放大 + 提亮；同级卡缩小、降透明度、轻微模糊 |
| **时长** | 320ms |
| **缓动** | `[0.16, 1, 0.3, 1]` |
| **触发方式** | 悬停（键盘 `:focus-visible` 同样生效） |
| **适用组件** | `PluginGrid`、`ToolGrid`、`ProviderList`、`TemplateGallery` |
| **MotionVault** | [卡片 25 · 聚焦卡组](https://xiyu519.github.io/MotionVault/cards) |

**AI Prompt**

```text
Create a React component called FocusGrid using React + Tailwind + Framer Motion.
Wrap N cards in a group; on hover or focus of one card, that card animates to
scale 1.02 with opacity 1 and a --shadow-float, while the other siblings animate
to scale 0.985, opacity 0.6 and blur(1.5px). All transitions 0.32s ease
[0.16, 1, 0.3, 1], driven by a single group-level hovered index so there is no
per-card state. Keyboard: focus-visible triggers the same state. Restore in the
same duration on leave. Reduce-motion: only opacity changes.
```

---

#### M-13 · 横排卡片组展开

| 字段 | 值 |
|---|---|
| **使用场景** | 设备横向卡片组、桌面端「最近任务」横排卡组、主题/色板选择 |
| **动效类型** | 悬停项横向舒展，同级让位 |
| **时长** | 420ms |
| **缓动** | `[0.25, 1, 0.5, 1]` |
| **触发方式** | 悬停 / 聚焦 |
| **适用组件** | `DeviceShelf`、`RecentTaskShelf`、`PalettePicker` |
| **MotionVault** | [卡片 28 · 悬停扩展卡片组](https://xiyu519.github.io/MotionVault/cards) |

**AI Prompt**

```text
Create a React component called ExpandShelf using React + Tailwind + Framer Motion.
A horizontal row of equal-width panels inside a flex container. On hover/focus of
one panel it expands to flex-grow with a target width of roughly 60% of the row
while its siblings shrink proportionally; animate using flex-basis transitions
via Framer Motion's layout prop (no width keyframes). Duration 0.42s ease
[0.25, 1, 0.5, 1]. Expanded panel raises its label to full opacity and reveals one
line of secondary text; collapsed panels fade their label to 70%. Use layout="position"
and keep overflow-hidden with rounded corners so content clips cleanly.
```

---

#### M-14 · 审批卡展开详情

| 字段 | 值 |
|---|---|
| **使用场景** | **产品视觉签名**：权限审批卡「展开完整影响面」（几个文件 / 多大体积 / 可逆性 / 计划原文） |
| **动效类型** | 共享元素展开：小卡弹性展开为大详情卡，遮罩淡入 |
| **时长** | 展开 380ms / 收起 240ms |
| **缓动** | 进入 `[0.16, 1, 0.3, 1]`（**去掉弹簧过冲**）；退出 `[0.7, 0, 0.84, 0]` |
| **触发方式** | 点击卡片任意位置展开；Esc / 点击遮罩收起 |
| **适用组件** | `ApprovalCard`（核心）、`TaskDetailDrawer`、`ToolCallDetail` |
| **MotionVault** | [卡片 39 · 弹性展开卡](https://xiyu519.github.io/MotionVault/cards)（**降级为非弹性版**） |

**AI Prompt**

```text
Create a React component called ExpandableCard using React + Tailwind + Framer Motion.
Small state shows a compact card; clicking it expands the same element into a large
detail card using shared layout animation (layoutId on the container, the title and
the icon). Expanded state: backdrop fades in to --scrim over 0.2s; container grows to
the detail box over 0.38s ease [0.16, 1, 0.3, 1] with NO spring overshoot; body content
cross-fades in with a 0.08s delay. Collapse: 0.24s ease [0.7, 0, 0.84, 0], backdrop
fades out first. Esc and backdrop click collapse it. Focus moves into the card on
expand and returns to the trigger on collapse. role="dialog" + aria-modal in expanded
state; respect reduce-motion by swapping the layout animation for a plain opacity fade.
```

**落地备注** · 审批卡是本产品唯一允许 380ms 以上的入场（它是「有重量的决策物件」），
但**禁止回弹**——M-14 是 MotionVault 弹簧效果中唯一被采纳的一条，且已去弹性。

---

#### M-15 · 任务时间线堆叠

| 字段 | 值 |
|---|---|
| **使用场景** | 任务执行日志 / 审批历史的滚动浏览：卡片随滚动依次堆叠压上 |
| **动效类型** | 滚动驱动的卡片堆叠（前一张缩小 + 压暗） |
| **时长** | 跟随滚动，无固定时长 |
| **缓动** | 由滚动进度直接映射，`position: sticky` + `useScroll` |
| **触发方式** | 滚动 |
| **适用组件** | `TaskTimeline`、`AuditLog`、`SessionHistory` |
| **MotionVault** | [卡片 06 · 滚动堆叠卡](https://xiyu519.github.io/MotionVault/cards) |

**AI Prompt**

```text
Create a React component called ScrollStack using React + Tailwind + Framer Motion.
Each child card is sticky at the top with a small offset so cards stack as the user
scrolls. Use useScroll on the container plus useTransform so each card's scale goes
from 1 to 0.94 and its opacity from 1 to 0.7 as the next card covers it; also apply a
slight y translate. Keep every animated value bound to transform/opacity only.
Cap the depth effect to the top 3 cards. On reduce-motion, disable the scale/opacity
transform and render a plain vertical list.
```

---

### 组 4 · 按钮与操作反馈

---

#### M-16 · 主按钮悬停

| 字段 | 值 |
|---|---|
| **使用场景** | 主操作按钮（发送 / 批准 / 保存 / 立即配对）的悬停态 |
| **动效类型** | 光带周期性掠过 + 1px 内高光环 |
| **时长** | 单次扫过 2000ms；间隔 4000ms（悬停时连续） |
| **缓动** | linear |
| **触发方式** | 自动（低频）；悬停时连续 |
| **适用组件** | `Button variant="primary"`、`ApproveButton`、`PairButton` |
| **MotionVault** | [按钮 06 · 微光按钮](https://xiyu519.github.io/MotionVault/buttons) |

**AI Prompt**

```text
Create a React component called ShimmerButton using Tailwind + a custom keyframes
utility. A primary button (accent background, full pill radius per the design system)
with a soft light band sweeping across its surface: an absolutely-positioned
linear-gradient(skewX(-20deg), transparent, rgba(255,255,255,.28), transparent) layer
animating translateX from -120% to 120%. The sweep runs once every 4s when idle and
continuously (2s loop) on hover/focus-visible. Add an inset 1px highlight ring using
box-shadow inset 0 1px 0 rgba(255,255,255,.18) — static, not animated. Text stays on
top with relative z-index. transform/opacity only, overflow-hidden, and disabled
entirely under prefers-reduced-motion (static button, keep the inset ring).
```

---

#### M-17 · 高风险操作确认

| 字段 | 值 |
|---|---|
| **使用场景** | 危险 / 高风险操作防误触：删除任务、撤销设备授权、执行 risk=high/critical 工具 |
| **动效类型** | 长按填充（1.2s 从左扫到右），松手 200ms 回吐 |
| **时长** | 长按 1200ms；取消回吐 200ms |
| **缓动** | 填充 linear；回吐 `[0.7, 0, 0.84, 0]` |
| **触发方式** | 长按（指针 / 空格键按住） |
| **适用组件** | `DangerButton`、`RevokeDeviceButton`、审批卡的「始终允许」二次确认 |
| **MotionVault** | [按钮 12 · 长按确认](https://xiyu519.github.io/MotionVault/buttons) |

**AI Prompt**

```text
Create a React component called HoldToConfirm using React + Tailwind + Framer Motion.
The button requires a 1.2s press to fire. On pointerdown or Space keydown, a fill layer
grows from 0% to 100% width over exactly 1.2s with linear easing; on release before
completion the fill collapses back to 0 over 0.2s ease [0.7, 0, 0.84, 0]. The label
switches from "按住确认" to "松开取消" while pressed. Announce progress to screen
readers with an aria-live polite region at 25/50/75/100%. Keyboard accessible via
Space with key-repeat guarding. Reduced motion: replace the fill with a text-only
countdown "按住 1.2 秒确认".
```

**落地备注** · 这是本项目「审批有重量」的关键交互，**必须**用长按而不是二次弹窗。

---

#### M-18 · 次级按钮悬停

| 字段 | 值 |
|---|---|
| **使用场景** | 次级 / 文字按钮的悬停（「查看全部」「重试」「复制」） |
| **动效类型** | 文案上滑离场、新文案下方滑入 |
| **时长** | 260ms |
| **缓动** | `[0.25, 1, 0.5, 1]` |
| **触发方式** | 悬停 / 聚焦 |
| **适用组件** | `Button variant="ghost"`、`LinkButton`、列表行内操作 |
| **MotionVault** | [按钮 07 · 文字滑换](https://xiyu519.github.io/MotionVault/buttons) |

**AI Prompt**

```text
Create a React component called TextSwapButton using React + Tailwind + Framer Motion.
Two stacked labels live inside an overflow-hidden span; on hover/focus the first label
animates y from 0 to -100% and opacity to 0 while the second animates y from 100% to 0
and opacity to 1, both over 0.26s ease [0.25, 1, 0.5, 1]. The button width stays fixed
to the max of the two labels so there is no reflow. The accessible name always reflects
the current (visible) label. Disable under reduce-motion — keep the static label.
```

---

#### M-19 · 指针按下反馈

| 字段 | 值 |
|---|---|
| **使用场景** | 全站可点击元素的按下反馈（卡片、按钮、列表行、图标按钮） |
| **动效类型** | 点击处扩散一圈涟漪 |
| **时长** | 600ms |
| **缓动** | `[0.16, 1, 0.3, 1]` |
| **触发方式** | `pointerdown`（坐标为落点） |
| **适用组件** | `Card`、`Button`、`ListItem`、`IconButton` |
| **MotionVault** | [按钮 03 · 点击波纹](https://xiyu519.github.io/MotionVault/buttons) |

**AI Prompt**

```text
Create a React hook called useRipple plus a Ripple container using React + Tailwind +
Framer Motion. On pointerdown, spawn a span at the event coordinates with a 12px
diameter that scales to 2.5x the element's diagonal and fades from rgba(255,255,255,.18)
to transparent over 0.6s ease [0.16, 1, 0.3, 1], then self-removes. The container needs
position: relative and overflow: hidden. Ripples never block pointer events. Cap
concurrent ripples at 3. Disabled under reduce-motion. Must also work from keyboard
activation by rippling from the element center.
```

---

#### M-20 · 传输 / 下载按钮

| 字段 | 值 |
|---|---|
| **使用场景** | 插件安装、模型下载、导出任务日志等原地变形的操作按钮 |
| **动效类型** | 按钮变形成进度条 → 完成变对勾 → 复位 |
| **时长** | 全程约 2000ms（含 600ms 完成态停留） |
| **缓动** | 填充 `[0.25, 1, 0.5, 1]`；形态切换 `[0.16, 1, 0.3, 1]` |
| **触发方式** | 点击 |
| **适用组件** | `InstallButton`、`DownloadButton`、`ExportButton` |
| **MotionVault** | [按钮 11 · 下载变形](https://xiyu519.github.io/MotionVault/buttons) |

**AI Prompt**

```text
Create a React component called DownloadMorphButton using React + Tailwind + Framer
Motion. States: idle -> downloading -> done -> idle. On click, the pill button morphs
in place into a progress bar (same width/height, radius unchanged): a fill grows with
real progress using duration 0.3s ease [0.25, 1, 0.5, 1], while a mono percentage label
counts up. On completion the bar flips to a green check using a 0.4s path-length draw,
holds for 0.6s, then the button cross-fades back to its idle label over 0.3s. Never let
the button change its outer width. aria-live announces "下载中 42%" and "下载完成".
Reduce-motion: swap morphing for a plain label update.
```

---

### 组 5 · 模态与浮层

---

#### M-21 · 模态弹出

| 字段 | 值 |
|---|---|
| **使用场景** | 设置弹窗、Provider 配置、确认对话框、二维码配对弹窗 |
| **动效类型** | 遮罩淡入 + 弹窗轻微缩放（**无过冲**） |
| **时长** | 进入 320ms / 退出 240ms |
| **缓动** | 进入 `--ease-spring`（ζ≈1.03）；退出 `[0.7, 0, 0.84, 0]` |
| **触发方式** | 点击 / 事件驱动 |
| **适用组件** | `Dialog`、`Sheet`、`PairingModal`、`SettingsModal` |
| **MotionVault** | [弹簧 02 · 弹簧弹窗](https://xiyu519.github.io/MotionVault/spring)（**去过冲版**） |

**AI Prompt**

```text
Create a React Modal component using React + Tailwind + Framer Motion + a portal.
Backdrop animates opacity 0 -> 1 over 0.2s ease [0.16, 1, 0.3, 1] using --scrim.
Panel animates from { opacity: 0, scale: 0.97 } to { opacity: 1, scale: 1 } over
0.32s with a spring that has NO visible overshoot: { type: 'spring', stiffness: 380,
damping: 40, mass: 1 }. Exit: 0.24s ease [0.7, 0, 0.84, 0] to scale 0.98, opacity 0.
Use AnimatePresence. Focus trap inside the panel, restore focus to the trigger on
close, close on Esc and backdrop click. role="dialog" aria-modal="true" with
aria-labelledby. Lock body scroll while open without shifting layout.
Reduce-motion: duration-based fade only, scale 1.
```

---

#### M-22 · 气泡提示

| 字段 | 值 |
|---|---|
| **使用场景** | 权限等级的说明、工具风险标签、`Alt+Space` 提示、图标按钮含义 |
| **动效类型** | 气泡从触发元素边缘弹出（短距离 + 淡入） |
| **时长** | 进入 200ms / 退出 120ms |
| **缓动** | 进入 `--ease-spring`；退出 `[0.7, 0, 0.84, 0]` |
| **触发方式** | 悬停 + 键盘聚焦（延迟 400ms 显示） |
| **适用组件** | `Tooltip`、`RiskBadge help`、`IconButton label` |
| **MotionVault** | [弹簧 04 · 弹性气泡](https://xiyu519.github.io/MotionVault/spring) |

**AI Prompt**

```text
Create a React Tooltip using React + Tailwind + Framer Motion. Show on hover or
focus-visible after a 400ms open delay; hide immediately on leave/blur. The bubble
enters from { opacity: 0, scale: 0.96, y: 4 } to { opacity: 1, scale: 1, y: 0 } over
0.2s with spring { type: 'spring', stiffness: 420, damping: 44, mass: 1 } (no
overshoot), and exits in 0.12s ease [0.7, 0, 0.84, 0]. Position via Floating UI with
a 6px offset and flip/shift middleware. Rendered in a portal, pointer-events: none.
role="tooltip" linked with aria-describedby; dismissible with Esc.
```

---

#### M-23 · 设置开关

| 字段 | 值 |
|---|---|
| **使用场景** | 设置页所有布尔项：权限模式、开机自启、通知、开发者模式 |
| **动效类型** | 滑块位移 + 轨道变色（轻量弹性） |
| **时长** | 260ms |
| **缓动** | `--ease-spring`（ζ≈1.03） |
| **触发方式** | 点击 / 键盘 Space |
| **适用组件** | `Switch`、`PermissionToggle`、`DevModeToggle` |
| **MotionVault** | [弹簧 06 · 弹性开关](https://xiyu519.github.io/MotionVault/spring) |

**AI Prompt**

```text
Create a React Switch component using React + Tailwind + Framer Motion.
Track 44x26px, full pill radius; knob 22px circle. On toggle, the knob moves with
spring { type: 'spring', stiffness: 380, damping: 40, mass: 1 } (no overshoot) and
the track color cross-fades from --border-strong to --accent over 0.26s. The knob
must not stretch into an ellipse — keep scale uniform for the restrained style.
role="switch" with aria-checked, Space/Enter toggling, and a visible focus ring
using --focus-ring. Reduce-motion: 0.12s linear position change.
```

**落地备注** · MotionVault 原效果会把滑块拉成椭圆（橡皮糖感），本项目的克制基调下
**取消变形**，只保留位移与变色。

---

#### M-24 · 审批通过

| 字段 | 值 |
|---|---|
| **使用场景** | 审批通过、设备配对成功、任务完成的确认瞬间 |
| **动效类型** | SVG 圆圈描边 + 对勾描绘，结尾轻微缩放 |
| **时长** | 800ms（圆 350ms → 勾 350ms → 收尾 100ms） |
| **缓动** | `[0.16, 1, 0.3, 1]` |
| **触发方式** | 状态变化（approved / success） |
| **适用组件** | `ApprovalResult`、`PairingSuccess`、`TaskSuccessBadge` |
| **MotionVault** | [SVG 06 · 描绘打勾](https://xiyu519.github.io/MotionVault/svg) |

**AI Prompt**

```text
Create a React component called DrawCheck using React + Tailwind + Framer Motion and
inline SVG. The circle outline draws first (pathLength 0 -> 1 over 0.35s), then the
check draws (0.35s) with a 0.1s overlap; at the end the whole mark scales to 1.04 and
settles back to 1 over 0.1s. Stroke color --status-online, stroke-width 2,
stroke-linecap round. Ease [0.16, 1, 0.3, 1] for both draws. Play once per status
change and never re-trigger on re-render. Include a visually-hidden "已通过" label.
Reduce-motion: show the finished mark immediately.
```

---

### 组 6 · 状态与反馈

---

#### M-25 · 统计数字

| 字段 | 值 |
|---|---|
| **使用场景** | 任务中心统计（今日完成 / 待批准 / 失败）、设备数量、工具调用次数 |
| **动效类型** | 里程表式数字滚动（每位独立） |
| **时长** | 1200ms；位间 stagger 100ms |
| **缓动** | `[0.16, 1, 0.3, 1]` |
| **触发方式** | 进入视口（`once`）/ 数值变化 |
| **适用组件** | `StatNumber`、`DashboardMetrics`、`UsageCounters` |
| **MotionVault** | [文字 13 · 滚动数字](https://xiyu519.github.io/MotionVault/text) |

**AI Prompt**

```text
Create a React component called RollingCounter using React + Framer Motion. It receives
`value` and renders each digit in its own overflow-hidden 1em box containing a vertical
strip of 0-9; on value change the strip translates so the target digit lands centered,
duration 1.2s ease [0.16, 1, 0.3, 1], with a 0.1s stagger from the least-significant
digit. Font must be tabular-nums mono. Thousand separators render as static glyphs. No
overshoot, no bounce. Reduced motion: set the final value instantly. Add an
aria-label with the plain number and aria-live="polite" only when the value is
user-triggered.
```

---

#### M-26 · 输入校验失败

| 字段 | 值 |
|---|---|
| **使用场景** | API Key 校验失败、Pair Code 错误、Provider 配置错误 |
| **动效类型** | 整行左右抖动 + 边框闪红 + 错误提示自上滑入 |
| **时长** | 抖动 400ms；提示 200ms |
| **缓动** | 抖动 `[0.65, 0, 0.35, 1]`；提示 `[0.16, 1, 0.3, 1]` |
| **触发方式** | 校验失败事件；聚焦输入框即复位 |
| **适用组件** | `ApiKeyField`、`PairCodeInput`、`BaseUrlField` |
| **MotionVault** | [弹簧 05 · 抖动输入框](https://xiyu519.github.io/MotionVault/spring) |

**AI Prompt**

```text
Create a React component called WobbleField using React + Tailwind + Framer Motion.
On validation failure the input row shakes horizontally: keyframes x: [0, -8, 8, -6, 6,
-3, 3, 0] over 0.4s easing [0.65, 0, 0.35, 1], while the border color animates to
--status-error and an error message slides down from y: -4 with opacity 0 -> 1 over
0.2s ease [0.16, 1, 0.3, 1]. Shake plays exactly once per failure. Focusing the field
clears the error and resets the border over 0.15s. aria-invalid="true" plus
aria-describedby pointing at the message, and role="alert" on the message.
Reduce-motion: skip the shake, keep the border flash and message.
```

---

#### M-27 · 通知 / Toast

| 字段 | 值 |
|---|---|
| **使用场景** | 任务完成、审批被拒绝、设备离线、传输失败的轻量提示 |
| **动效类型** | 从右侧滑入堆叠；旧的向上让位 |
| **时长** | 进入 320ms / 退出 240ms |
| **缓动** | 进入 `[0.16, 1, 0.3, 1]`；退出 `[0.7, 0, 0.84, 0]` |
| **触发方式** | 事件推送；自动消失 5s（错误类常驻） |
| **适用组件** | `Toast`、`NotificationStack`、`TaskDoneToast` |
| **MotionVault** | [卡片 31 · 通知卡叠展开](https://xiyu519.github.io/MotionVault/cards) |

**AI Prompt**

```text
Create a React toast system using React + Tailwind + Framer Motion. Toasts stack in
the bottom-right corner; a new toast enters from x: 24 with opacity 0 -> 1 over 0.32s
ease [0.16, 1, 0.3, 1] and pushes existing toasts up using Framer Motion's layout
prop. Exit slides to x: 24 with opacity 0 and collapses height over 0.24s ease
[0.7, 0, 0.84, 0]. Auto-dismiss after 5s with a progress hairline on the bottom edge
(duration 5s linear); error toasts are sticky. Max 3 visible, extras queue. Swipe/dismiss
button available. Container is aria-live="polite" (assertive for errors) and
pointer-events: none except on the toasts themselves. Position within safe-area insets.
```

---

#### M-28 · 侧边导航切换

| 字段 | 值 |
|---|---|
| **使用场景** | 左侧主导航（任务 / 设备 / 工具 / Provider / 插件 / 设置）激活项切换 |
| **动效类型** | 激活指示块在两个导航项之间平滑位移 |
| **时长** | 300ms |
| **缓动** | `--ease-spring`（ζ≈1.03） |
| **触发方式** | 点击 / 键盘方向键 |
| **适用组件** | `SideNav`、`TabBar`、`SegmentedControl` |
| **MotionVault** | [弹簧 11 · 融合导航](https://xiyu519.github.io/MotionVault/spring)（**去 gooey 滤镜**） |

**AI Prompt**

```text
Create a React component called NavIndicator using React + Tailwind + Framer Motion.
A nav list where the active item is marked by a shared pill/short-rail element rendered
once and positioned with layoutId so it slides between items. Transition: spring
{ type: 'spring', stiffness: 380, damping: 40, mass: 1 } over roughly 0.3s with no
overshoot. Do NOT use SVG gooey/fusion filters. The active label goes to --text-primary,
inactive stay at --text-secondary. Height of the rail equals the item height minus 8px.
role="tablist"/role="tab" with aria-selected and arrow-key navigation.
Reduce-motion: switch instantly.
```

---

### 组 7 · 背景氛围（受限）

---

#### M-29 · 起始页背景

| 字段 | 值 |
|---|---|
| **使用场景** | 起始页 / 空状态页的底色氛围。**仅在无任务运行时启用** |
| **动效类型** | 指针附近的点阵浮出深色网格 |
| **时长** | 跟随指针；淡入淡出 200ms |
| **缓动** | `[0.16, 1, 0.3, 1]` |
| **触发方式** | `mousemove`（指针静止 8s 后自动淡出至 0） |
| **适用组件** | `StartPage`、`EmptyState`、`PairingWaiting` |
| **MotionVault** | [背景 06 · 鼠标点亮网格](https://xiyu519.github.io/MotionVault/backgrounds) |

**AI Prompt**

```text
Create a React component called SpotlightGrid using React + Tailwind + Framer Motion
(or a plain canvas if the grid exceeds 400 cells). A static dot grid painted with
--border-subtle covers the background; a rounded-square mask follows the pointer using
CSS custom properties --x/--y, inside which the dots switch to --text-tertiary. The
mask radius is 180px, edge-faded. Update via requestAnimationFrame, never React state.
Fade the whole layer to 0 when the pointer has been idle for 8 seconds and restore on
move. Fully static (no animation loop) under prefers-reduced-motion, and unmount when
any task is running to protect the idle CPU budget.
```

**落地备注** · 本项目性能预算（Idle CPU < 2%）下，这是**唯一**允许的常驻背景效果，
且必须具备空闲自动淡出与任务运行时卸载。

---

## 4. 落地检查清单

每条动效合入前逐项确认：

- [ ] **时长**在 §1.1 阶梯内，未自造数值
- [ ] **缓动**来自 §1.2，未出现未被批准的曲线
- [ ] **无回弹 / 无旋转 / 无夸张缩放**（缩放绝对值 ≤ 1.05）
- [ ] 只动画化 `transform` / `opacity`（`filter: blur` 仅限文字入场与 shimmer）
- [ ] 循环动效有**明确终点**，状态结束后组件卸载（无常驻动画）
- [ ] `prefers-reduced-motion: reduce` 下行为正确（循环 → 静态，位移 → 仅 opacity）
- [ ] 键盘可达：`:focus-visible` 与 hover 走同一套动效；不依赖 hover 传达信息
- [ ] 屏幕阅读器：状态变化有 `aria-live`，装饰动画不进入可访问性树（`aria-hidden`）
- [ ] 浅色 / 深色双主题均已适配（无硬编码 `zinc-*` / `#fff` / `#000`）
- [ ] 不会在动画期间造成布局位移（CLS ≈ 0），容器尺寸预先固定
- [ ] 文字类动效保留可选中与可复制（不用 `pointer-events: none` 覆盖正文）

---

## 5. 原生端落地映射

Web 的 Prompt 只作语义参照，两端按各自平台惯例实现，**参数取自 §1**。

| 动效 | Windows（WinUI 3 / XAML） | Android（Compose / Material 3） |
|---|---|---|
| M-01 模糊浮现 | `CompositionEffectBrush` 高斯模糊 + `Opacity`/`Offset` 关键帧（`Storyboard`） | `Modifier.graphicsLayer` + `Modifier.blur` 逐字 `animateFloatAsState` |
| M-02 面板唤出 | `Popup` + `ScaleTransform` 0.97→1 + `Opacity`，320ms `CubicEase(EaseOut)` | `AnimatedVisibility` + `scaleIn(initialScale = 0.97f)` |
| M-03 页面切换 | `Frame` 内容 `EntranceThemeTransition`（`FromBottom`） | `AnimatedContent` + 行级 `slideInVertically` |
| M-04 首屏入场 | `ItemsRepeater` + `ElementCompositionPreview` 逐项 `Offset`/`Opacity` | `LazyColumn` `animateItem()` + `stagger` |
| M-05 流光文字 | `LinearGradientBrush` 的 `StartPoint`/`EndPoint` 动画 | 自绘 `drawText` + `Brush.linearGradient` 位移 |
| M-06 三点等待 | `ProgressRing` 替代（Fluent 无三点规范） | `LinearProgressIndicator`（M3 不定态） |
| M-07 骨架屏 | `shimmer` 自绘（`LinearGradientBrush` + `TranslateTransform`） | `Modifier.placeholder` 自带 shimmer |
| M-09 环形进度 | `ProgressRing`（`Value` 直接驱动，自带过渡） | `CircularProgressIndicator(progress = { })` |
| M-11 聚光灯卡 | `PointerMoved` 更新 `RadialGradientBrush` 的 `Center`（`CompositionBrush`） | `pointerInput` 更新 `Brush.radialGradient` 中心 |
| M-14 展开详情 | `ConnectedAnimationService`（共享元素） | `SharedTransitionLayout` + `Modifier.sharedElement` |
| M-17 长按确认 | 自绘填充 + `DispatcherQueueTimer` 计时 | `Modifier.pointerInput` + `Animatable` |
| M-21 模态 | `ContentDialog` / `Flyout`（Fluent 自带过渡） | `ModalBottomSheet` / `AlertDialog`（M3 自带过渡） |
| M-23 开关 | `ToggleSwitch`（Fluent 自带） | `Switch`（M3 自带） |
| M-24 描绘打勾 | `Path` 的 `StrokeDashOffset` 关键帧动画 | `PathMeasure` + `animateFloatAsState` |

> Windows / Android **不使用**本文件里的 React 代码，只对齐时长、缓动与语义。

---

## 6. 明确不采纳的效果（附理由）

以下 MotionVault 效果虽好，但与 OpenAgent「克制、可信、精确」的基调或性能预算冲突，
**列入禁用清单**：

| 效果 | 分类 | 不采纳理由 |
|---|---|---|
| 果冻按钮 JELLY BUTTON | 弹簧 / 按钮 | 弹性变形 = 规格明令禁止的「弹跳」 |
| 磁吸按钮 / 磁吸卡 MAGNETIC | 按钮 / 卡片 | 指针驱动的位移会干扰精确点击 |
| 彩带庆祝 CONFETTI BURST | 按钮 | 与「基础设施」而非「玩具」的定位冲突 |
| 点赞爆裂 LIKE BURST | 按钮 | 产品无社交属性 |
| 液态金属 / 全息眩光 / 金属眩光 | 按钮 / 卡片 | 高光过强，违反「低视觉噪音」 |
| 故障艺术字 GLITCH TEXT / 雪花噪点 | 文字 | 影响可读性与专业感 |
| 极光文字 / 流动渐变网格 / 极光背景 | 文字 / 背景 | 规格明令禁止「大量渐变」与「AI 套路配色」 |
| 3D 倾斜卡 / 3D 翻转卡 / 任何 3D 类 | 卡片 / 3D | 需持续 GPU 计算，超出 60fps 与 CPU 预算 |
| 粒子类（流星雨 / 字符雨 / 电路脉冲） | 粒子 / 背景 | 常驻渲染，违反 Idle CPU < 2% |
| 滚动密码锁 / 滚动多米诺 / 滚动涨潮 | 滚动叙事 | 装饰性强于信息性，且滚动距离过长 |
| 胶片颗粒 FILM GRAIN | 背景 | 降低文字可读性 |
| 打字机 / 乱序解码 / 翻牌板 | 文字 | 延迟信息呈现；Agent 输出必须即时可读 |
| 文字漩涡 / 字重压感 / 环形文字轨 | 文字 | 干扰阅读与选择 |

---

**变更记录**

| 日期 | 变更 |
|---|---|
| 2026-09-26 | 初版：确立全局动效规范、29 条场景动效、禁用清单与原生端映射 |
