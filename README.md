# ChatGPT 本地工作区插件（Local Workspace）

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-blue" alt="Windows 10/11 x64">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.8-orange" alt=".NET Framework 4.8">
  <img src="https://img.shields.io/badge/version-2.2.1-brightgreen" alt="v2.2.1">
</p>

<p align="center">
  简体中文 · <a href="README.en.md">English</a>
</p>

让 ChatGPT 通过 **OpenAI 官方 Tunnel** 直接操作你的本机：读写文件、精确编辑、执行命令、查看 Git 变更。桌面程序内嵌**实时工作台**（WebView2，也可用浏览器打开），按对话查看每一次调用的时间线、命令输出和补丁 diff。单个 EXE、零安装、正式运行不需要 Node。

> 非官方社区项目，与 OpenAI 无隶属关系。

**它解决什么**：让 ChatGPT **网页对话**直接驱动你本机的文件、命令与 Git，并配一个**实时工作台**逐次回放每个工具调用。和 `codex` CLI、`@modelcontextprotocol/server-filesystem` 的区别在于：那些跑在终端 / TUI 里，这个跑在 **ChatGPT 网页里**，自带可视化时间线、diff 与命令输出，不必另开终端，也不依赖任何常驻 Node 服务。如果你想要的是"在 ChatGPT 对话里直接改本机代码并看见每一步"，这就是它；如果你已经在终端里用 Codex CLI，则不必换。

| 桌面程序：内嵌实时工作台（连接配置在第四个页签） | 浏览器工作台：调用时间线与命令输出 |
| --- | --- |
| ![桌面程序](docs/images/desktop-app.png) | ![实时工作台](docs/images/dashboard-timeline.png) |

![补丁审阅视图](docs/images/dashboard-patch-review.png)

## 2.2.1：任务提示按需查看

计划标题旁只保留一个轻量的 **任务详情** 按钮，默认不再显示大块提醒。点击后才查看原因、下一步或复制提示，关闭后不会被轮询或状态变化自动打开。你也可以直接在原对话中发“继续”。

![任务详情按钮](docs/images/dashboard-task-button.png)

历史执行记录默认折叠。步骤已完成但未填写逐步证据时，显示“证据未逐项登记”，不再以“等待验收”大卡片催促续做；完整的完成检查仍保留，不会把总说明自动当成逐项验收证据。详见 [2.2.1 发行说明](docs/RELEASE-2.2.1.md)。

- 新增 `check_task_completion`：交付前检查是否仍有未完成步骤、缺少登记证据、运行中的命令，或尚未记录恢复的执行失败。
- `update_plan` 增加步骤 `evidence`、任务 `task_state`、`reason` 与 `next_action`。阻塞和用户要求的暂停必须填写具体原因与下一步；普通进度更新会保留已有阻塞/暂停状态。
- 后续工具回执附带当前对话的任务提示。工具调用成功不再被当作整个任务已完成；静默移除未完成步骤会被拒绝，调整范围需说明原因。
- 超过两分钟没有新操作且仍有未完成工作时显示“暂无新调用 · 待确认”。计划里的“进行中”不会单独制造运行转圈；只有观察到实际执行时才显示运行状态。

![点击后查看任务详情](docs/images/dashboard-task-details.png)

截图为隔离测试数据。**完成检查依据模型登记的证据和本地执行状态，并非独立验收。插件不能阻止 ChatGPT 结束回复，也不能强制开启下一轮。** 复制提示不会自动发送，不新增部署、删除或对外操作授权。计划和执行记录仍保存在当前进程内，重启后需从原对话重新登记。详见 [2.2.0 发行说明](docs/RELEASE-2.2.0.md)。

升级后在 ChatGPT 刷新工具，确认 `version: 2.2.1`、`tool_count: 26`。多步骤任务使用 `update_plan` 记录完整目标，实际验证后补充 `evidence`，结束前调用 `check_task_completion`；返回 `can_finish: false` 时继续处理，或如实登记必要阻塞。

## 2.1.0：自动归组、附件导入与连接诊断

- **自动识别对话归属**：宿主传入官方 `openai/session` 元数据时，同一对话的调用自动进入同一时间线。没有该信号的客户端继续使用手动登记，不按目录或最近一次调用猜测归属。
- **接收聊天附件**：新增 `import_file`，按官方文件输入协议将附件保存为本地新文件，返回大小、类型和 SHA256；支持中文文件名，最多 32 MiB，已有文件不会被覆盖。
- **更精简的模型回执**：`content` 给出简短摘要，完整数据统一放在 `structuredContent.result`；26 个工具分别声明返回字段，减少重复正文。图片仍返回原生 image 内容。
- **一键诊断**：工作台右上角及桌面“更多 → 诊断连接”检查配置、隧道存活/就绪、MCP 握手、工具发现、成功调用与匿名会话信号。未发生的步骤明确显示待验证，检查不会重启连接。

![连接诊断](docs/images/dashboard-diagnostics.png)

诊断截图来自隔离的本地 MCP 验收进程，因此隧道显示“未检查”。已验证模拟宿主元数据、真实公网 HTTPS 文件导入及浏览器交互；ChatGPT Tunnel 是否转发会话元数据、具体账号是否提供附件选择，仍以实际宿主调用为准。协议依据见 [OpenAI 插件参考](https://developers.openai.com/plugins/reference)、[MCP 工具实现](https://developers.openai.com/plugins/build/mcp-server) 与 [Secure MCP Tunnels](https://developers.openai.com/api/docs/guides/secure-mcp-tunnels)。

升级时退出旧程序后启动新版，在 ChatGPT 设置中刷新工具并新开聊天，核对 `version: 2.2.1`、`tool_count: 26`。读取 `content[0].text` JSON 的自建客户端需改为读取 `structuredContent.result`。完整说明见 [2.1.0 发行说明](docs/RELEASE-2.1.0.md)。

## 延续 2.0.2 的工作台体验

- **看见实际读取的图片**：`read_image` 的时间线详情直接显示本次读取的 PNG、JPEG、GIF 或 WebP，支持适应窗口 / 原始尺寸，并显示尺寸、类型、大小与文件位置。保留读取时的原始字节，之后文件变化不会改写历史预览。
- **展开工作区状态**：`get_workspace_status` 显示版本、程序路径、面板地址、默认 Shell、运行命令数、已登记工作区及全部工具。
- **点击路径打开 Windows**：工作区目录、文件详情、目录条目、搜索结果、补丁路径与命令工作目录均可点击。目录在资源管理器中打开，文件在资源管理器中选中；HTTP/HTTPS 地址交给默认浏览器。失效路径会显示原因。
- **统一按钮样式**：刷新、暂停、复制、侧栏切换等按钮去掉浏览器默认黑框，采用轻背景和柔和圆角，保留键盘焦点与浅深色适配。

| 图片详情：读取时的实际内容 | 工作区状态：连接、目录与工具 |
| --- | --- |
| ![图片预览](docs/images/dashboard-image-preview.png) | ![工作区状态](docs/images/dashboard-workspace-status.png) |

以上截图使用内置示例数据。图片预览仅保留在当前进程内存中，最多 100 张 / 32 MiB，先达到的上限生效；预览被淘汰或程序重启后需重新读取，不会把历史路径当前的内容冒充原图。

## 特性一览

- **26 个本地工具**：文件读写、精确编辑、多文件补丁、搜索、命令执行与增量输出、Git 审阅、执行计划。
- **双时代 MCP 协议（v2.0）**：同一个 EXE 同时服务 2025-06-18 legacy 客户端（`initialize` 握手，ChatGPT Tunnel 现行方式，兼容握手与调用方式）与 [2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28) modern 无状态客户端：`server/discover` 能力发现、每请求 `_meta` 版本协商、`resultType`、可缓存 `tools/list`（`ttlMs`/`cacheScope`）、MRTR 危险操作确认、官方 Tasks 扩展长任务句柄、OpenTelemetry trace 关联与工具图标。
- **内嵌实时工作台**：桌面程序首个页签直接嵌入工作台页面（WebView2，随系统 Edge 附带；缺运行时自动回退浏览器），每秒同步，按对话隔离时间线，检查器按调用类型渲染（图片、工作区状态、diff、命令输出、读取正文、搜索命中）。
- **一体化桌面外壳**：单行工具栏（启动/停止合一、在浏览器打开、更多菜单）+ 四个页签（实时工作台 / 操作记录 / 原始日志 / 连接配置）；操作记录与原始日志为自绘视图，按级别着色、等宽排版、尾随跟随。
- **Codex 风格工作流**：`open_workspace` 读取 AGENTS.md 约定 → `update_plan` 展示计划 → `apply_patch` 预验证后提交多文件补丁。
- **单文件分发**：.NET Framework 4.8 原生 EXE（WebView2 组件以资源内嵌），只监听 127.0.0.1，不开放远程访问。

## 环境要求

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 / 11 x64 |
| 运行时 | 系统自带 .NET Framework 4.8（Win10 1903+ 默认已装） |
| 内嵌工作台 | 需要 WebView2 运行时（装有 Edge 的 Win10/11 默认已有）；缺失时自动回退为浏览器打开，不影响任何工具功能 |
| ChatGPT 账号 | 支持 **Developer Mode** 的付费套餐（Plus / Pro / Team / Enterprise 等，以官方为准），用于创建连接器与 Tunnel |
| 命令执行 | 默认需要 Git for Windows（隐藏 Git Bash）；也可显式选择系统 PowerShell |
| Node.js | 仅构建界面和跑测试需要，正式运行不需要 |

> **关于平台范围**：只支持 Windows 是刻意的取舍，不是没做完——目标是"单个免安装原生 EXE + 只监听回环 + 运行期零 Node/Python 依赖"。.NET Framework 4.8 在 Win10 1903+ 自带，因此发行包不需要任何运行时安装。macOS / Linux 暂无对应版本。

## 快速开始

### 1. 下载并解压

从 [Releases](../../releases) 下载发行包并解压到任意目录。目录内 `LocalWorkspace.exe` 与官方 `tunnel-client.exe` 必须同级保留。

### 2. 启用 Developer Mode 并创建连接器（最关键的一步）

这一步在 **ChatGPT 网页端**完成，不是在本插件里：

1. **启用 Developer Mode**：登录 ChatGPT 网页 → 设置（Settings）→ 找到 **Developer / 开发者模式** 并开启。看不到该开关通常意味着当前套餐不支持 Developer Mode（需付费套餐），这是最常见的卡点。
2. **创建连接器**：进入 ChatGPT 的插件 / 连接器页面（`chatgpt.com/plugins`），点击新建（+），按提示创建一个指向本地 MCP 服务的连接器。
3. **拿到 Tunnel ID 与 API Key**：发行包内随附的官方 `tunnel-client.exe` 负责把你的本机服务桥接到 ChatGPT，连接器创建流程会给出 **Tunnel ID** 与 **API Key**，下一步填入桌面程序。

> 官方权威步骤与最新界面标签以 [OpenAI Apps SDK Quickstart](https://developers.openai.com/apps-sdk/quickstart/) 为准（ChatGPT 设置项名称会随版本调整）。注意：`platform.openai.com/docs` 是 **API 文档**，不是这里的连接器 / Developer Mode 流程。

### 3. 启动连接

打开 `LocalWorkspace.exe`，在**连接配置**页签填入 Tunnel ID 与 API Key（未填或格式不对时点"启动连接"会自动跳到该页签并标红提示），回到工具栏点**启动连接**（连接后同一按钮变为**停止**）。配置保存在 `%LOCALAPPDATA%/LocalWorkspacePlugin/settings.json`——**不要提交或公开该文件**。

连接成功后自动切到**实时工作台**页签，内嵌页面每秒同步本机状态；"更多"菜单提供刷新工作台、清空/复制日志、复制工作台链接。

![桌面程序](docs/images/desktop-app.png)

### 4. 在 ChatGPT 中刷新插件

打开 ChatGPT 网页版"设置 → 连接器（Connectors）→ 本地工作区"，滚动到底部"信息"，点击**刷新**（这是开发者连接设置页；应用详情页只有"重新连接"时请进入设置页操作）。成功后操作列表应包含 26 个工具。

### 5. 验证

新开一个聊天，直接说：

> 调用 get_workspace_status 确认连接

应返回 `version: 2.2.1`、`tool_count: 26`、`protocol_versions`、实际程序路径和进程实例 ID。**原始日志**页签会依次出现 `initialize`、`tools/list` 与工具回执——仅"已连接"不能证明 ChatGPT 已刷新工具。

## 怎么用（调用方式）

所有工具由模型按需自动调用，你用自然语言下达任务即可。下面是典型场景：

### 文件操作

> 看看 E:/projects/demo 里有哪些文件，把 config.json 的端口改成 8080

→ 模型依次调用 `list_directory`、`read_file`、`edit_file`。

### 执行命令

> 在 E:/projects/demo 运行 npm test，把失败用例的输出贴给我

→ `exec_command` 启动（默认隐藏 Git Bash），长任务用 `poll_command` / `read_command` 增量取输出，必要时 `stop_command` 终止。

### 多文件改动（Codex 风格）

> 先 open_workspace 读取 E:/work/api 的约定，用 update_plan 列出计划，然后按 AGENTS.md 的规范给订单模块加一个导出接口，改完 git_diff 给我看

→ `open_workspace` → `update_plan` → `apply_patch` → `git_status` / `git_diff`。工具不会自行 commit 或 push。

### 打开实时工作台

连接后桌面程序的“实时工作台”页签已内嵌该页面；想在更大窗口或多屏观察时用工具栏**在浏览器打开**。宿主提供 `openai/session` 时自动归组，标题默认使用工作目录名；不提供时，模型先调用 `register_conversation` 并在后续请求中传入返回的 `thread_id`。你只需点明目录：

> 这个对话在 E:/work/pay 上做支付模块重构

工具会返回本地线程 ID 和直达该线程的工作台链接，之后的调用在时间线中按对话隔离展示。想自定义名称就补一句标题；想绑定真实 ChatGPT 会话可让模型带上已知的 `chat_id`（相同 `chat_id` 复用同一线程）。

> **归属边界**：`openai/session` 是匿名关联信号，不是 ChatGPT `/c/` 链接中的真实 ID，也不是身份认证。服务端在当前进程内按组织、用户和会话组合散列后关联；重启后需重新建立关联。既无宿主信号也无 `thread_id` 的调用进入“未归属”。

### 将聊天附件保存到本地

> 把这个聊天中的附件保存为 E:/projects/demo/inbox/需求说明.pdf

宿主支持文件输入时，模型调用 `import_file`；目标目录必须已存在，目标文件必须不存在。下载使用 HTTPS，失败不会留下半个目标文件。保存后可继续调用 `read_file`、`read_image` 或其他工具处理。签名下载链接不写入本地日志或工作台回执；此能力取决于宿主是否提供附件参数。

## 26 个工具

> 完整的输入参数、类型与返回字段见 [docs/TOOLS.md](docs/TOOLS.md)。下表只按用途归类。

| 用途 | 工具 |
| --- | --- |
| 对话登记及工作台直达链接 | `register_conversation` |
| 独立活动查询（对话内文本快照） | `read_workspace_activity` |
| 工作区约定、计划和多文件补丁 | `open_workspace`、`update_plan`、`apply_patch` |
| 实际连接、版本与活动诊断 | `get_workspace_status` |
| 交付前任务完成检查 | `check_task_completion` |
| 目录、文件属性和搜索 | `list_directory`、`file_info`、`search_files`、`search_text` |
| 读取文本和图片 | `read_file`、`read_image` |
| 接收聊天附件并保存新文件 | `import_file` |
| 创建目录、写入与精确编辑 | `create_directory`、`write_file`、`edit_file` |
| 执行命令、发送标准输入 | `exec_command`、`write_stdin` |
| 命令列表、增量输出、只读快照、停止 | `list_commands`、`poll_command`、`read_command`、`stop_command` |
| 修改审阅 | `show_changes`、`git_status`、`git_diff` |

`show_changes` 只包含本进程文件工具记录的修改；`git_status` / `git_diff` 可检查 shell 或其他编辑器产生的 Git 变更（Git diff 不含未跟踪文件正文）。

## 实时工作台详解

![补丁审阅视图](docs/images/dashboard-patch-review.png)

- 内嵌视图与浏览器页面是同一份单页应用，功能完全一致；左侧为全部对话、未归属与已登记线程；选中线程后，时间线、计划、命令输出分别过滤，多个对话用同一个项目也不会混淆。
- 时间线每行显示开始时间、工具、目标、状态与耗时；进行中的调用持续计时。每一行按调用类型着色，选中后右侧检查器按类型展示实际内容：写入/替换了哪些文件与改动行、读取正文（文本自动展开至 10 KB，二进制只标注不展开）、搜索命中、命令输出与退出码、计划进度。
- 默认"跟随最新"自动展开当前筛选范围内的最新调用；点击历史调用会固定详情。
- 支持搜索、状态筛选、暂停/恢复（仅暂停观察，不终止任务）和复制登记指令；连接失败保留旧结果并提示过期。
- 未传线程 ID 的调用进入"未归属"，不猜测归属；线程分组是可视化隔离，不是账号级授权隔离。
- 活动保留全局最近 100 条、最多 200 个线程，属于当前 MCP 进程；服务重启后需重新登记。
- 桌面程序的**操作记录**页签是同一活动流的表格视图（时间 / 线程 / 操作 / 内容），**原始日志**页签保留隧道与 MCP 的完整原始输出，均按级别着色、支持尾随跟随与行复制。

页面实现：静态资源在 `src/ui/`（React 组件与中文标签）、`src/dashboard.css`（Tailwind 4）、`src/dashboard.template.html`（模板）。`npm ci && npm run build:ui` 先用 Tailwind CLI 编译样式，再用 esbuild 打包组件，全部内联进单个 `src/dashboard.html`——没有 CDN、没有模块加载器、运行时不需要 Node。新构建优先读取程序同目录的 `dashboard.html`，缺失时回退嵌入页面。

旧实例热更新限制：运行中的旧程序只有嵌入页面，可用 `node scripts/dashboard-preview.cjs http://127.0.0.1:<原端口>/` 只读观察，加 `--sample` 用内置样例预览界面。此入口不重启应用、不执行工具。

## 命令执行细节

- 默认运行于隐藏 Git Bash（`shell: "git_bash"`，兼容旧名 `bash`）；需要 PowerShell 语法时显式传 `shell: "powershell"` 或 `"pwsh"`。找不到 Git Bash 会明确报错，不会偷换解释器，也不会误用 WSL bash。
- 支持 `cmd` / `cwd` / `yield_time_ms` 参数（兼容旧 `command` / `yield_ms`）；返回实际 shell 与可执行路径。
- `write_stdin` 省略 `chars` 即续读输出，发送 Ctrl-C 终止命令树；标准输入是管道而非 PTY，长任务续读同一会话，不要重复启动。
- 默认 300 秒超时（可设 1–3600 秒）；输出快照保留最近 128,000 字符并标记截断；输入接收阻塞时终止该命令树并报错，保证 MCP 服务不失联。

## 过程可见性

- 工具描述提供中文调用状态；宿主传入 progressToken 时发送开始/执行中/结束通知，未知总量不显示虚构百分比。
- 桌面立即记录调用开始，同步等待中每 2 秒记录仍在执行，返回时区分正常与失败。
- 对话内的工具回执以文本为准；可视化进度统一在桌面程序的内嵌工作台查看，ChatGPT 内不再挂载卡片面板。
- 旧版结果卡片保留文本阅读、分页、搜索、diff、会话与图片兼容；停止按钮只停止对应命令树。

## 从源码构建

```powershell
./build.ps1                                  # 构建到 dist-next（含工作台页面与内嵌 WebView2 资源）
npm ci; npm run build:ui                     # 重建工作台页面
node tests/mcp.test.cjs                      # 协议与工具
node tests/patch.test.cjs                    # 补丁引擎
node tests/activity.test.cjs                 # 活动记录
node tests/dashboard.test.cjs                # 工作台数据
node --test tests/dashboard-ui.test.cjs      # 真实浏览器 UI 回归
node --test tests/dashboard-media.test.cjs   # 实际 MCP 图片、状态和本地打开接口
./build.ps1 -OutputDirectory ./dist         # 退出旧程序后直接构建到 dist
```

`tests/dashboard-ui.test.cjs` 用真实浏览器跑 `src/dashboard.html`：检查时间线、检查器内容、计划卡片与筛选，在 1920/1366/640 三种宽度确认无横向溢出，浅深色下确认每类调用有独立主色。浏览器由 `scripts/browser-launch.cjs` 按 `$env:WORKSPACE_TEST_BROWSER` → 自带 Chromium → Chrome → Edge 顺序挑选。

真实 Tunnel 健康测试需显式设置 `$env:WORKSPACE_TUNNEL_SMOKE='1'` 后运行 `node tests/tunnel.test.cjs`；使用已有配置，不要与同一 Tunnel 的另一实例同时运行。

主要源码：`src/Program.cs`（桌面外壳与 Tunnel）、`src/UiKit.cs`（自绘控件与主题）、`src/WorkbenchHost.cs`（内嵌 WebView2 与资源解析）、`src/WorkspaceServer.cs`（协议与工具）、`src/Presentation.cs`（资源与 diff）、`src/PatchEditor.cs`（补丁）、`src/WorkspaceContext.cs`（约定与计划）、`src/WorkspaceActivity.cs`（活动记录）、`src/ui/`（工作台）。DevSpace 官方源码保留于 `vendor/devspace/`（MIT），当前 EXE 不依赖其 Node 服务，详见 [docs/DEVSPACE-SOURCE.md](docs/DEVSPACE-SOURCE.md)。

验证记录见 [VERIFICATION.md](VERIFICATION.md)，升级内容见 [UPGRADE-NOTES.md](UPGRADE-NOTES.md)。

## 更新与替换

`Apply-Update.ps1` 发现运行中的应用或隧道会拒绝覆盖，不会自动终止进程。先关闭应用（会结束其命令树），再运行更新脚本并从原 dist 启动。更新工具元数据后需在 ChatGPT 设置页刷新并**新开聊天**验证。

## 常见问题

**ChatGPT 说它只能读、不能改？** 先让它调用 `get_workspace_status`，核对版本、`tool_count: 26` 与连接状态；再在设置页刷新元数据并新开聊天。不要把旧聊天缓存、旧插件或未运行的服务当成系统权限不足。

**"已连接"但工具没反应？** 隧道连接 ≠ ChatGPT 已刷新工具。原始日志必须出现 `initialize` 与 `tools/list` 才算打通。

**内嵌工作台空白或提示不可用？** 内嵌视图依赖系统 WebView2 运行时（装有 Edge 即有）；缺失时页面会提示并可用"在浏览器打开"。也可在"更多"菜单刷新工作台。

**点"启动连接"没反应？** 配置缺失或格式不对时会跳到"连接配置"页签并把无效输入标红，按提示修正后再启动。

**面板一直"观察中"？** 表示当前没有工具或模型调用在运行；模型内部思考不在插件可见范围，不代表任务失败。

**命令报找不到 Git Bash？** 安装 [Git for Windows](https://git-scm.com/download/win)，或让模型显式传 `shell: "powershell"`。

## 安全说明

- 工作台只监听 `127.0.0.1` 动态端口，不开放远程访问或任意命令执行接口。打开 Windows 位置需要同源 POST 和当前进程令牌；文件仅在资源管理器中定位，点击程序或脚本路径不会执行它。
- 默认以当前 Windows 用户权限访问本地磁盘；工作目录**不是**操作系统沙箱，请在可信账号下使用。
- **目前没有命令级 / 路径级护栏**：`exec_command` 会以当前用户权限执行模型下达的任意命令，没有白名单，破坏性命令（如 `rm -rf`）也不会弹确认框。文件类工具会拒绝越界路径、且工具从不自动 `commit` / `push`，但 **shell 不受限**。请把模型指向的目录范围收窄，并盯着实时工作台的时间线复核每一步。
- `settings.json` 含 API Key，不要提交、截图或公开。
- 服务与活动记录属于当前进程，重启后需重新连接与登记；桌面日志仅保存在当前窗口，退出即清空。

## 协议兼容性

本服务器是 **dual-era**（双时代）实现，按客户端打开方式自动选择行为，两条路径互不干扰：

| 时代 | 触发方式 | 提供的能力 |
| --- | --- | --- |
| legacy（2025-06-18） | `initialize` 握手（ChatGPT Tunnel 现行方式） | 兼容原有握手：26 个工具、进度通知、结构化输出 |
| modern（[2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28)） | 请求 `_meta` 携带 `io.modelcontextprotocol/protocolVersion` | 无状态每请求协商、`server/discover`、`resultType`、`tools/list` 缓存字段（`ttlMs`/`cacheScope: private`）、`serverInfo` 回执标识 |

modern 时代按客户端声明的能力渐进启用：

- **MRTR 危险操作确认**：客户端声明 `elicitation` 能力时，`apply_patch` 与覆盖已有文件的 `write_file` 会先返回 `resultType: "input_required"` 与 `elicitation/create` 请求；客户端带 `inputResponses` + `requestState` 重试后才真正执行。`requestState` 经 HMAC-SHA256 完整性保护，绑定工具名与参数指纹、10 分钟过期且一次性消费（防篡改、防重放）。未声明该能力的客户端行为不变。
- **Tasks 扩展（`io.modelcontextprotocol/tasks`）**：客户端声明该扩展时，仍在运行的 `exec_command` 返回标准任务句柄（`resultType: "task"`），可用 `tasks/get` 轮询、`tasks/cancel` 取消；未声明的客户端继续拿经典 `session_id` 会话结果。
- **OpenTelemetry**：请求 `_meta` 中的 `traceparent` 会记入操作日志，实时工作台检查器显示 trace 短 ID，便于与宿主侧链路关联。
- **工具 icons 仅在 modern 时代下发**：ChatGPT（legacy）的连接器安全校验会拒绝 `data:` URI 图标并阻断工具执行，因此 legacy `tools/list` 保持无图标；2.1 的文件输入元数据和返回 schema 仍会正常下发。
- 版本不匹配返回 `UnsupportedProtocolVersionError`（-32022）；modern 时代按规范不再响应 `ping`。

## 官方依据

- [OpenAI Apps SDK Quickstart（Developer Mode 与连接器创建）](https://developers.openai.com/apps-sdk/quickstart/)
- [OpenAI MCP Apps UI 与桥接](https://developers.openai.com/plugins/build/chatgpt-ui)
- [工具元数据、输出结构与注解](https://developers.openai.com/plugins/reference#tool-descriptor-parameters)
- [MCP 进度通知](https://modelcontextprotocol.io/specification/2025-06-18/basic/utilities/progress)
- [MCP 2026-07-28 规范（modern 时代依据）](https://modelcontextprotocol.io/specification/2026-07-28)
- [MCP Tasks 扩展](https://modelcontextprotocol.io/extensions/tasks)
- [DevSpace 官方源码](https://github.com/Waishnav/devspace)

## 许可与致谢

本项目为非官方开源项目，与 OpenAI 无隶属关系。整体以 MIT 许可证发布（见 [LICENSE](LICENSE)）。

- 架构与工具设计参考并部分衍生自 [Waishnav/devspace](https://github.com/Waishnav/devspace)（MIT），其源码保留于 `vendor/devspace/`，含原始许可证文件。
- 内嵌视图使用 Microsoft WebView2 SDK（见 `vendor/webview2/LICENSE.WebView2.txt`），以资源形式内置于 EXE。
- 发行包中的 `tunnel-client.exe` 为 OpenAI 官方组件（Apache-2.0），随 GitHub Release 提供，不纳入本仓库版本管理。每个 Release 会固定一个 tunnel-client 版本，请使用与该 Release 说明一致的版本，不要跨 Release 混用。

## Star History

如果这个项目帮到了你，欢迎点个 Star 支持。
