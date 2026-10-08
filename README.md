# ChatGPT 本地工作区插件（Local Workspace）

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-blue" alt="Windows 10/11 x64">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.8-orange" alt=".NET Framework 4.8">
  <img src="https://img.shields.io/badge/version-2.4.0-brightgreen" alt="v2.4.0">
</p>

<p align="center">
  简体中文 · <a href="README.en.md">English</a>
</p>

让 ChatGPT 通过 **OpenAI 官方 Tunnel** 直接操作你的本机：读写文件、精确编辑、执行命令、查看 Git 变更。桌面程序内嵌**实时工作台**（WebView2，也可以在浏览器打开），按对话查看每一次工具调用的时间线、命令输出和补丁 diff。单个 EXE，免安装，运行时不需要 Node。

> 非官方社区项目，与 OpenAI 无隶属关系。

**它解决什么**：让 ChatGPT **网页对话**直接驱动本机的文件、命令与 Git，并用实时工作台逐次回放每个工具调用。`codex` CLI、`@modelcontextprotocol/server-filesystem` 跑在终端或 TUI 里；这个插件跑在 **ChatGPT 网页里**，自带可视化时间线、diff 与命令输出，不用另开终端，也不依赖常驻的 Node 服务。同一个 EXE 也可以作为 stdio MCP 服务器给 Codex 使用（见“[Codex 推荐配置](#codex-推荐配置)”）。

| 桌面程序：内嵌实时工作台（连接配置在第四个页签） | 实时工作台：按回合分组的调用时间线 |
| --- | --- |
| ![桌面程序](docs/images/desktop-app.png) | ![实时工作台](docs/images/dashboard-timeline.png) |

当前版本 **2.4.0**（2026-10-08，上一公开版本 2.3.0）。各版本变化见 [升级说明](UPGRADE-NOTES.md)，接口迁移和上游限制见 [2.4.0 发行说明](docs/RELEASE-2.4.0.md)，验证结果见 [验证记录](VERIFICATION.md)。

## 目录

- [2.4.0 的变化](#240-的变化)
- [特性一览](#特性一览) · [环境要求](#环境要求) · [快速开始](#快速开始) · [怎么用](#怎么用)
- [23 个工具](#23-个工具)（含 [2.4.0 工具删合对照](#240-工具删合对照)、[Codex 推荐配置](#codex-推荐配置)）
- [实时工作台](#实时工作台) · [桌面程序](#桌面程序)
- [命令执行细节](#命令执行细节) · [过程可见性](#过程可见性) · [版本历史](#版本历史)
- [从源码构建](#从源码构建) · [更新与替换](#更新与替换) · [常见问题](#常见问题) · [安全说明](#安全说明) · [协议兼容性](#协议兼容性)

## 2.4.0 的变化

- **工具 28 → 23**：`poll_command`、`stop_command`、`list_commands`、`read_workspace_activity`、`file_info` 并入现有工具，不再出现在 `tools/list`。本版仍可调用，结果中标注 `deprecated` 和替代写法，计划 **2.5.0** 移除。对照见“[2.4.0 工具删合对照](#240-工具删合对照)”。**Codex `config.toml` 里写了旧名字的（`enabled_tools`、`disabled_tools`、`tools.<名字>` 等）要改成新名字。**
- **命令轮询只读**：`read_command` 新增 `yield_time_ms`，带 `offset=next_offset` 时有新输出或命令退出就返回，并保持 `readOnlyHint=true`。Codex 用 `default_tools_approval_mode = "writes"` 时，轮询不再每次弹审批。停止命令改为用 `write_stdin` 发送 Ctrl-C（U+0003）。
- **工作台重做**：三栏布局（对话栏 | 时间线 | 检查器），在 1280 / 1100 / 720 px 处逐级收起，窄窗口下检查器改为抽屉；标题栏显示来源、连接状态与调用 / 失败 / 改动统计；计划改为标题栏下方可折叠的一栏；时间线一行一次调用，用灰色图标区分类型，按 Codex 轮次或 30 秒间隔分组；详情可就地展开，长内容先截断；新增改动汇总卡、Ctrl+F 搜索与分段筛选、j/k 键盘浏览；诊断连接和版本号固定在对话栏底部同一行。详见“[实时工作台](#实时工作台)”。
- **对话来源与统计**：`conversations[].source` 给出 `codex` / `chatgpt` / `manual`；`conversations[].stats` 给出调用数、运行中、失败数与增删行数；写入类回执逐文件给出 `added` / `removed`（多文件补丁合计全部已写入文件）；活动行新增 `turn_id` / `turn_source` / `trace_id`。Codex 调用按 `_meta.threadId` 自动归组，从 `x-codex-turn-metadata` 读取轮次 ID。
- **桌面观感**：桌面窗口、WebView2 默认底色与工作台外壳、对话栏统一为中性灰 `#f7f7f7`，主区为白色 `#fff`，启动首帧与页面一致，不再白屏闪烁；嵌入页面固定浅色方案。Ctrl+滚轮、Ctrl+加号 / 减号 / 0 缩放工作台（67%–200%），缩放比例写入 `settings.json`，下次启动沿用。工具栏与页签改为扁平样式。
- **新应用图标**：白色圆角块上的黑色方括号加块状光标。EXE 内嵌 16/20/24/32/40/48/64/128/256 共 9 帧（16–32 为逐像素手调版），标题栏、任务栏、网页标签图标与浏览器中的侧栏品牌标记统一为同一图形。
- **旧记录实际删除**（修复 2.3.0 把调用正文落盘、重启后重现旧日志的问题）：旧调用、对话标题、路径、执行计划和验证日志不再恢复；启动时删除旧的 `activity.bin`、`activity-state.bin`、`plans.bin`、`threads.bin`。继续旧任务要重新登记计划和验证。命令防重复执行与文件撤销仍保留加密的最小身份关联。
- **手动清空**：工作台标题栏“⋯ → 清空已完成日志…”（需确认）和桌面“更多 → 清空已完成日志”执行同一清理，桌面页签计数立即清零。运行中的命令、当前计划、任务证据和已登记的对话保留。
- **组件升级**：官方 Tunnel Client **0.0.16**、WebView2 SDK **1.0.4258.31**；Radix UI、Lucide、Playwright、shadcn、cn 及可兼容修补的开发依赖已更新。MCP 继续支持 **2026-07-28** 与 legacy 握手。
- **更少输出与写盘**：命令回执默认只在 `output` 返回一次正文，兼容字段 `full_output` 要传 `include_full_output: true`；分页只复制需要的区间。身份绑定只在变化时写盘，满额时回收没有当前使用、重试或撤销引用的旧绑定。
- **版本核对**：“诊断连接”逐项列出运行进程与磁盘文件的版本，并标出“版本一致 / 重启后才会使用新版本 / 运行版本未知 / 没有找到程序文件”。构建时核对内置 WebView2 程序集与 `runtime-components.json` 声明的版本一致。
- **更新回滚目录**：`Apply-Update.ps1` 默认只保留最近 3 个回滚目录（`-KeepRollbacks` 可调），占用中的目录跳过并提示。

下载 [v2.4.0 Windows x64 发行包](https://github.com/CSL19980820/chatgpt-local-workspace/releases/tag/v2.4.0)，组件校验值见 [runtime-components.json](runtime-components.json)。从 2.3.x 升级的步骤见 [升级说明](UPGRADE-NOTES.md#从-23x-升级清单)。

## 特性一览

- **23 个本地工具**：文件读写、精确编辑、多文件补丁、搜索、命令执行与增量输出、文件历史与撤销、Git 审阅、执行计划与完成检查。
- **双时代 MCP 协议**：同一个 EXE 同时服务 2025-06-18 legacy 客户端（`initialize` 握手，ChatGPT Tunnel 现行方式）与 [2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28) modern 无状态客户端：`server/discover`、每请求 `_meta` 版本协商、`resultType`、可缓存 `tools/list`、MRTR 危险操作确认、官方 Tasks 扩展、OpenTelemetry trace 关联与工具图标。
- **内嵌实时工作台**：桌面程序首个页签直接嵌入工作台页面（WebView2，随系统 Edge 附带；缺运行时自动回退浏览器），每秒同步。三栏布局，按对话隔离、按回合分组；详情按调用类型渲染（图片、工作区状态、diff、命令输出、读取正文、搜索命中），可就地展开或在检查器中查看。
- **一体化桌面外壳**：单行工具栏（启动 / 停止合一、在浏览器打开、更多菜单）+ 四个页签（实时工作台 / 操作记录 / 原始日志 / 连接配置）；操作记录与原始日志为自绘视图，按级别着色、等宽排版、尾随跟随。
- **Codex 风格工作流**：`open_workspace` 读取 AGENTS.md 约定 → `update_plan` 展示计划 → `apply_patch` 预验证后提交多文件补丁 → `check_task_completion` 交付前核对。
- **单文件分发**：.NET Framework 4.8 原生 EXE（WebView2 组件以资源内嵌），只监听 127.0.0.1，不开放远程访问。

## 环境要求

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 / 11 x64 |
| 运行时 | 系统自带 .NET Framework 4.8（Win10 1903+ 默认已装） |
| 内嵌工作台 | WebView2 运行时（装有 Edge 的 Win10/11 默认已有）；缺失时自动回退为浏览器打开，不影响任何工具功能 |
| ChatGPT 账号 | 支持 **Developer Mode** 的付费套餐（Plus / Pro / Team / Enterprise 等，以官方为准），用于创建连接器与 Tunnel |
| 命令执行 | 默认需要 Git for Windows（隐藏 Git Bash）；也可以显式选择系统 PowerShell |
| Node.js | 只有构建界面和跑测试需要，正式运行不需要 |

> **关于平台范围**：只支持 Windows 是刻意的取舍：目标是“单个免安装原生 EXE + 只监听回环 + 运行时不依赖 Node/Python”。.NET Framework 4.8 在 Win10 1903+ 自带，发行包不需要安装任何运行时。暂无 macOS / Linux 版本。

## 快速开始

### 1. 下载并解压

从 [Releases](../../releases) 下载发行包，解压到任意目录。`LocalWorkspace.exe` 与官方 `tunnel-client.exe` 必须放在同一目录。

### 2. 启用 Developer Mode 并创建连接器

这一步在 **ChatGPT 网页端**完成，不在本插件里：

1. **启用 Developer Mode**：登录 ChatGPT 网页 → 设置（Settings）→ 找到 **Developer / 开发者模式** 并开启。看不到这个开关，通常是当前套餐不支持 Developer Mode，这是最常见的卡点。
2. **创建连接器**：进入 ChatGPT 的插件 / 连接器页面（`chatgpt.com/plugins`），点击新建（+），按提示创建指向本地 MCP 服务的连接器。
3. **拿到 Tunnel ID 与 API Key**：发行包随附的官方 `tunnel-client.exe` 负责把本机服务桥接到 ChatGPT；连接器创建流程会给出 **Tunnel ID** 与 **API Key**，下一步填入桌面程序。

> 官方步骤与最新界面名称以 [OpenAI Apps SDK Quickstart](https://developers.openai.com/apps-sdk/quickstart/) 为准。`platform.openai.com/docs` 是 **API 文档**，不是这里的连接器 / Developer Mode 流程。

### 3. 启动连接

打开 `LocalWorkspace.exe`，在**连接配置**页签填入 Tunnel ID 与 API Key（没填或格式不对时，点“启动连接”会跳到该页签并标红），回到工具栏点**启动连接**（连接后同一按钮变为**停止**）。Tunnel ID 和工作台缩放比例保存在 `%LOCALAPPDATA%/LocalWorkspacePlugin/settings.json`；API Key 保存在 Windows 凭据管理器，旧版明文配置会在启动时迁移。

连接成功后自动切到**实时工作台**页签。

### 4. 在 ChatGPT 中刷新插件

打开 ChatGPT 网页版“设置 → 连接器（Connectors）→ 本地工作区”，滚动到底部“信息”，点击**刷新**（这是开发者连接设置页；应用详情页只有“重新连接”时请进入设置页操作）。成功后操作列表应包含 **23 个工具**；2.4.0 起旧名字不再列出。

### 5. 验证

新开一个聊天，直接说：

> 调用 get_workspace_status 确认连接

应返回 `version: 2.4.0`、`tool_count: 23`、`protocol_versions`、实际程序路径和进程实例 ID。**原始日志**页签会依次出现 `initialize`、`tools/list` 与工具回执；只显示“已连接”不能证明 ChatGPT 已刷新工具。Tunnel 0.0.16 要求 legacy 请求先完成 `initialize` 与 `notifications/initialized`；modern 请求按协议携带完整协商信息。

## 怎么用

所有工具由模型按需自动调用，你用自然语言下达任务即可。

### 文件操作

> 看看 E:/projects/demo 里有哪些文件，把 config.json 的端口改成 8080

→ 模型依次调用 `list_directory`、`read_file`、`edit_file`。

### 执行命令

> 在 E:/projects/demo 运行 npm test，把失败用例的输出贴给我

→ `exec_command` 启动（默认隐藏 Git Bash）；长任务用 `read_command`（`yield_time_ms` + `offset=next_offset`）等待并取输出，需要输入时用 `write_stdin`，发送 Ctrl-C 终止。

### 多文件改动（Codex 风格）

> 先 open_workspace 读取 E:/work/api 的约定，用 update_plan 列出计划，然后按 AGENTS.md 的规范给订单模块加一个导出接口，改完 git_diff 给我看

→ `open_workspace` → `update_plan` → `apply_patch` → `git_status` / `git_diff`。工具不会自行 commit 或 push。

### 按对话归组

宿主提供 `openai/session`（ChatGPT）或 `_meta.threadId`（Codex）时，调用自动归入同一对话，标题默认用工作目录名。不提供时，模型先调用 `register_conversation`，之后的请求带上返回的 `thread_id`。你只需点明目录：

> 这个对话在 E:/work/pay 上做支付模块重构

工具会返回本地线程 ID 和直达该线程的工作台链接。想自定义名称就补一句标题；已知真实 ChatGPT 会话 ID 时可以让模型带上 `chat_id`（相同 `chat_id` 复用同一线程）。

> **归属边界**：`openai/session` 是匿名关联信号，不是 ChatGPT `/c/` 链接中的真实 ID，也不是身份认证。服务端按组织、用户和会话组合散列后关联，并加密保存。既没有宿主信号也没有 `thread_id` 的调用进入“未归属”。

### 将聊天附件保存到本地

> 把这个聊天中的附件保存为 E:/projects/demo/inbox/需求说明.pdf

宿主支持文件输入时，模型调用 `import_file`；目标目录必须已存在，目标文件必须不存在。下载使用 HTTPS，失败不会留下半个文件；签名下载链接不写入本地日志或工作台回执。此能力取决于宿主是否提供附件参数。

## 23 个工具

> 完整的输入参数、类型与返回字段见 [docs/TOOLS.md](docs/TOOLS.md)。下表按用途归类。

| 用途 | 工具 |
| --- | --- |
| 对话登记及工作台直达链接 | `register_conversation` |
| 工作区约定、计划和多文件补丁 | `open_workspace`、`update_plan`、`apply_patch` |
| 交付前任务完成检查 | `check_task_completion` |
| 连接、版本、命令会话与活动诊断（带 `path` 时附工作区实时快照） | `get_workspace_status` |
| 目录、文件属性和搜索 | `list_directory`（`path` 为文件时返回属性）、`search_files`、`search_text` |
| 读取文本和图片 | `read_file`、`read_image` |
| 接收聊天附件并保存为新文件 | `import_file` |
| 创建目录、写入与精确编辑 | `create_directory`、`write_file`、`edit_file` |
| 执行命令、发送标准输入、停止（Ctrl-C） | `exec_command`、`write_stdin` |
| 命令输出只读查看与等待 | `read_command` |
| 文件历史与撤销 / 重做 | `workspace_history`、`restore_change` |
| 修改审阅 | `show_changes`、`git_status`、`git_diff` |

`show_changes` 默认只包含本进程文件工具记录的修改；它的 `workspace_open` / `last_shown` 基准还会包含外部修改和未忽略的新文件，使用私有审阅引用，不改动正常暂存区和分支。`git_status` / `git_diff` 提供常规 Git 视图（Git diff 不含未跟踪文件正文）。

### 2.4.0 工具删合对照

| 旧工具（2.4.0 隐藏，2.5.0 移除） | 改用 | 写法 |
| --- | --- | --- |
| `poll_command` | `read_command` | `{session_id, yield_time_ms: 5000, offset: <上次 next_offset>}`；有新输出或退出即返回，不消费输出 |
| `stop_command` | `write_stdin` | `{session_id, chars: "\u0003"}`（Ctrl-C，终止该命令树） |
| `list_commands` | `get_workspace_status` | 读 `result.commands`（含 `thread_id`、运行状态与退出码） |
| `read_workspace_activity` | `get_workspace_status` | 传 `path`，读 `result.workspace`；同样不阻塞、不进入活动时间线 |
| `file_info` | `list_directory` | 同一个 `path`；文件返回 `kind: "file"` 与 `info`，目录也附带 `info` |

旧名字还能调用一个版本：结果的 `structuredContent.result.deprecated` 给出 `tool`、`replacement`、`removed_in: "2.5.0"` 和说明，摘要首行以 `DEPRECATED:` 开头；`get_workspace_status` 的 `deprecated_tools` 列出这些旧名字。宿主缓存的旧工具元数据不会立刻失效，但请刷新工具列表。

### Codex 推荐配置

Codex（CLI / IDE / 桌面）可以直接以 stdio 方式启动 `LocalWorkspace.exe --mcp`，不需要桌面程序或 Tunnel。建议在 `~/.codex/config.toml` 中：

```toml
[mcp_servers.local_workspace]
command = "E:/path/to/LocalWorkspace.exe"   # 改成实际路径
args = ["--mcp"]
default_tools_approval_mode = "writes"     # 只读工具（含 read_command 轮询）免审批，写入与命令仍需确认
tool_timeout_sec = 60                      # exec_command / read_command 单次最多等待 10 秒，60 秒足够

[mcp_servers.local_workspace.tools.read_command]
output_token_limit = 12000

[mcp_servers.local_workspace.tools.read_file]
output_token_limit = 12000
```

> **从 2.3.x 升级的 Codex 用户**：`enabled_tools`、`disabled_tools` 或 `[mcp_servers.<名>.tools.<工具>]` 中如果写了 `poll_command`、`stop_command`、`list_commands`、`read_workspace_activity`、`file_info`，请按上表改为新名字，否则 2.5.0 起这些配置不再匹配任何工具。上面的配置按 Codex 文档整理，尚未在真实 Codex 会话中逐项实测。

## 实时工作台

![实时工作台三栏布局](docs/images/dashboard-three-pane.png)

*1920 宽窗口：左侧对话栏，中间按回合分组的调用时间线，右侧检查器显示选中补丁的逐行改动。*

桌面程序的首个页签内嵌这个页面（WebView2）。工具栏的**在浏览器打开**会打开同一份单页应用，功能完全一致。工作台只负责观察，不执行工具，也不修改文件。页面每秒同步一次本机状态。

### 布局与窗口宽度

页面分三栏：**对话栏 | 时间线 | 检查器**。窄窗口下逐级收起，不出现横向滚动条。

| 窗口宽度 | 表现 |
| --- | --- |
| ≥ 1280 px | 三栏并排，检查器默认打开 |
| 1100–1279 px | 检查器仍停靠在右侧，但默认收起，点标题栏的检查器按钮打开 |
| < 1100 px | 检查器改为右侧抽屉，覆盖在时间线上，按 Esc 或再点按钮关闭；不会自动弹出 |
| ≤ 960 px | 对话栏收窄到 220 px，页边距缩小 |
| ≤ 720 px | 对话栏收成 52 px 图标栏，悬停显示名称 |

- 这里的宽度是页面宽度（CSS px）。桌面程序缩放后，页面宽度 = 窗口宽度 ÷ 缩放比例，例如 1400 px 窗口在 125% 下按 1120 px 布局。
- 拖动检查器左边缘可调整宽度；键盘焦点在边缘上时，按 ←/→ 每次调整 16 px。宽度范围 320–640 px，并始终给时间线留出至少 480 px。默认宽度：窗口小于 1440 px 时 360 px，小于 1800 px 时 400 px，更宽时 480 px。
- 标题栏最左侧（标题前）的侧栏按钮用来收起或展开对话栏，收起和展开时按钮位置不变；≤720 px 的图标栏模式中不显示。
- 对话栏折叠状态、检查器的开关和宽度、计划栏的展开状态记在本机浏览器存储里，下次打开时沿用。
- 时间线内容区最宽 1120 px，只在超宽窗口时限宽。没有任何调用时不显示检查器。

### 对话栏

- 第一行与标题栏的标题行对齐：在浏览器中打开时是品牌行“本地工作区”；嵌入桌面程序时不显示品牌行（窗口标题已有程序名），第一行就是**全部对话**。
- 接着是**全部对话**和“对话 N”分组。“对话 N”标题旁的 **+** 用来生成登记指令。有调用正在运行时，“全部对话”显示转圈，否则显示调用总数。
- 每个对话占两行：第一行是标题，第二行是“来源 · N 分钟前”。右侧在有调用运行时显示转圈，否则用红色数字显示失败次数。当前对话用底色标出。
- 还没有登记对话时，提示“还没有登记的对话。点 + 登记后，调用会按对话归类。”
- 底部固定显示**未归属**和**诊断连接**；当前版本号（如 `v2.4.0`）以浅灰小字显示在“诊断连接”这一行的右侧。对话栏收起或处于图标栏时只显示图标。

### 标题栏与对话统计

- **第一行**：
  - 侧栏按钮，然后是对话标题。
  - 来源标签：取自服务端的 `conversations[].source`（Codex / ChatGPT / 手动登记），由宿主绑定判定；旧版服务端没有这个字段时按调用信息推断。有对话链接时还会显示直达原对话的外链。
  - 连接状态：实时 / 已暂停 / 已断开 / 连接中。悬停可看到“同步于 hh:mm:ss”。
  - 右侧图标按钮：**搜索与筛选**、**暂停 / 恢复自动同步**、**⋯ 更多操作**、**检查器开关**。
- **第二行**：工作目录（点击在资源管理器中打开）、“开始 hh:mm”、“最近活动 X 前”、调用统计（N 次调用、N 进行中、N 排队、N 失败）和改动统计（“N 个文件 +新增 −删除”）。窗口变窄时，次要信息逐项隐藏。

### 计划栏

![计划栏与任务详情入口](docs/images/dashboard-task-button.png)

*计划栏固定在标题栏下方，不会压住时间线；“任务详情”按需打开。*

- 默认收起成一行：计划图标、分段进度条、“已完成/总数”、当前步骤和展开箭头。展开后列出全部步骤：已完成显示对勾，进行中显示圆点或转圈，待办显示空心圆。步骤很多时，计划栏最高占窗口的 40%，超出部分在栏内滚动。
- 在“全部对话”视图下，计划按对话分组，并显示各自的说明。
- 计划栏位于时间线滚动区之外，不会盖住任何调用行。
- **任务详情**按钮打开对话框，可以查看阻塞原因、核对记录或复制续做提示。关闭后，轮询和状态变化不会自动重新打开它。你也可以直接在原对话里发“继续”。

![任务详情](docs/images/dashboard-task-details.png)

*点击“任务详情”后查看任务状态、执行记录与可复制的提示；不会自动发送。*

### 时间线

- **一行一次调用**。行首是灰色类型图标：命令（终端）、写入（新建 / 删除 / 移动 / 修改各有图标）、读取（文件）、搜索（放大镜）、目录（文件夹）、图片、计划（清单）、差异（对比）、工作区状态（活动）。运行中的调用改为转圈，失败的调用改为红色警示图标。图标之外没有按类型区分的颜色。
- **行内文字**由动作、等宽显示的命令或文件名，以及补充说明组成，如“等 N 个文件”“第 a–b 行”“N 处”“N 项”“不是文本”。行尾依次显示增删行数、失败原因（如“退出 1”）、耗时（运行中持续计时）和开始时间（内容区宽度 ≥ 720 px 时显示）。悬停一行可看到完整开始时间。
- **按回合分组**：相邻调用都带 `turn_id`（目前来自 Codex）时按 `turn_id` 精确分组；否则与上一次调用间隔超过 30 秒就另起一组。组头格式为“hh:mm 工作了 X ── N 次调用”，进行中的组显示“正在工作 X”；组内只有一次调用时不显示调用数。
- 停在底部时，新调用出现后自动滚到最新；往上翻看历史时停止跟随，并出现**跳到最新**按钮。
- **点击一行**：检查器关闭时，详情在行下就地展开，可以同时展开多行；检查器打开时，改为选中这一行并在右侧显示详情。

![就地展开的命令输出](docs/images/dashboard-inline-expand.png)

*关闭检查器后点击调用，在时间线中直接展开代码块；长输出先显示末尾，需要时再显示前面。*

### 调用详情与代码块

就地展开和检查器使用同一套详情视图。

- **代码块**：头部显示类型图标、弱化的目录、加粗的文件名、标签与增删行数，以及复制按钮；正文带行号，diff 只用底色区分增删行；底部列出位置、写入方式、写入大小、退出码、运行时长、工作目录、复制输出等事实。
- **长内容先截断**，展开后整块最高占窗口的 60%，超出部分在块内滚动：

  | 类型 | 先显示 | 展开按钮 |
  | --- | --- | --- |
  | diff | 40 行 | 显示其余 N 行 |
  | 文件正文 | 24 行 | 显示其余 N 行 |
  | 命令输出 | 最后 24 行 | 显示前面 N 行 |
  | 搜索命中 | 12 处 | 显示其余 N 处 |
  | 目录条目 | 30 项 | 显示其余 N 项 |
  | 文本 | 40 行 | 显示其余 N 行 |

- **按调用类型展示**：写入和补丁逐个文件列出改动行，被拒绝的写入只列出路径；读取展开文本正文，最多 10 KB，超出部分有提示，二进制文件只标“不是文本”；搜索显示命中位置；命令显示输出与退出码；`read_image` 显示读取时的实际图片，可在适应窗口和原始尺寸之间切换；`get_workspace_status` 显示版本、路径、Shell、运行命令与工具列表。
- **检查器额外内容**：元信息行“状态 · 开始 · 耗时 · trace 短 ID”（请求带 `traceparent` 时显示 trace）；可折叠的**执行证据**：活动 ID、文件 SHA256、`change_id`。
- 详情中的目录、文件、搜索结果和工作目录都可以点击，在资源管理器中打开或选中。点击程序或脚本路径不会执行它。

![补丁审阅](docs/images/dashboard-patch-review.png)

*检查器中的多文件补丁：每个文件一个代码块，底色区分增删行，底部给出位置与写入方式。*

| 图片详情：读取时的实际内容 | 工作区状态：连接、目录与工具 |
| --- | --- |
| ![图片预览](docs/images/dashboard-image-preview.png) | ![工作区状态](docs/images/dashboard-workspace-status.png) |

![文件恢复记录与执行证据](docs/images/dashboard-reliability.png)

*检查器中的执行证据：活动 ID、文件 SHA256 与可撤销的 `change_id`。*

图片预览只保存在当前进程内存中，最多 100 张 / 32 MiB；被淘汰或程序重启后需要重新读取，不会把路径当前的内容冒充原图。

### 改动汇总

时间线末尾有一张汇总卡，标题为“本对话改动了 N 个文件”（“全部对话”视图下为“当前视图改动了 N 个文件”），并附总增删行数。

- 文件较多时自动折叠：超过 6 个文件时先列出前 5 个，其余收进“显示其余 N 个文件”，点开展开，再点“收起”折回。
- 每个文件一行：文件类型图标、弱化的相对目录、加粗的文件名、新建 / 删除 / 移动标签，右侧是增删行数。
- 点击某个文件，会跳到对应调用并短暂高亮。
- 文件超出汇总上限时，提示“另有 N 个文件未在列表中展开”。

### 搜索与筛选

![搜索与分段筛选](docs/images/dashboard-search-filter.png)

*Ctrl+F 打开搜索；分段筛选与关键词同时生效，右侧显示“匹配数 / 总数”。*

- 打开方式：按 **Ctrl+F**，或在输入框以外按 **/**，也可以点标题栏的搜索按钮。输入框为空时按 Esc 关闭。
- 搜索范围是调用、文件和命令。分段筛选有四档：**全部 / 进行中 / 失败 / 已返回**。
- 右侧计数显示“N 次”，筛选生效时显示“匹配数 / 总数”。
- 没有匹配时提示“没有匹配的调用”，并提供**清除筛选**按钮。

### 键盘操作

| 按键 | 作用 |
| --- | --- |
| Ctrl+F / `/` | 打开搜索 |
| Esc | 关闭空的搜索框；在窄窗口中关闭检查器抽屉 |
| ↓ / `j`，↑ / `k` | 焦点在时间线行上时移到下一行或上一行；检查器打开时同步选中 |
| ← / → | 焦点在检查器边缘时调整检查器宽度 |
| Ctrl+滚轮、Ctrl+加号 / 减号 / 0 | 缩放工作台（仅桌面程序内嵌时，见“[缩放](#缩放)”） |

### ⋯ 菜单与清空日志

![清空已完成日志](docs/images/dashboard-cleanup-2.4.0.png)

*清空已完成日志；运行中的命令、执行计划、任务证据和已登记的对话都会保留。*

标题栏的 **⋯** 菜单提供三项操作：**立即同步**、**诊断连接**和**清空已完成日志…**。

1. 选择**清空已完成日志…**后先弹出确认框：“会删除已完成的调用记录和命令输出，无法撤销。运行中的命令、执行计划、任务证据和已登记的对话都会保留。”
2. 点**清空**后，窗口底部短暂提示本次清理的条数；失败时提示原因。

桌面程序的“更多 → 清空已完成日志”执行同一清理，并同步把桌面页签计数清零。清空后重试原命令仍复用原会话，失败记录仍参与完成检查，不会因为隐藏日志而误报任务成功。

### 诊断连接

![连接诊断与组件版本](docs/images/dashboard-diagnostics.png)

*诊断对话框分别核对运行进程与磁盘文件的版本，再逐项检查配置、隧道、握手、工具发现、实际调用与会话信号。*

- 入口：对话栏底部的**诊断连接**（常驻）、标题栏 **⋯ → 诊断连接**、连接失败页上的**诊断连接**按钮、桌面程序“更多 → 诊断连接”；也可以在页面地址后加 `#diagnostics` 直接打开。
- 版本按行列出正在运行的版本、磁盘文件版本和状态：版本一致 / 重启后才会使用新版本 / 运行版本未知 / 磁盘上没有找到程序文件。取不到的版本显示为“—”。
- 未发生的检查步骤明确标为待验证。诊断不会重启连接。窗口较矮时，对话框整体可以滚动到底部。

### 空状态、加载与错误

![空状态、加载骨架与连接失败](docs/images/dashboard-states.png)

*从左到右：还没有调用、首屏加载骨架、无法连接本地工作区。*

- **加载中**：显示静态骨架，没有闪烁动画。
- **暂无调用记录**：说明调用会在哪里出现，并提供**登记对话**按钮。
- **无法连接本地工作区**：显示失败原因，并提供**重试**和**诊断连接**按钮。
- **已有数据时断开**：保留现有内容，顶部出现提示条，可点**重试**。
- **服务实例已改变**：程序重启后提示“服务实例已改变，请重新打开工作台”，不会把新旧进程的记录混在一起。

### 外观

固定浅色方案，不跟随系统深色模式。外壳和对话栏为 `#f7f7f7`，主区为白色 `#fff`；桌面窗口和 WebView2 的默认底色同为 `#f7f7f7`，启动首帧与页面外壳同色。整体为中性灰，没有按调用类型区分的配色、渐变或闪烁动画，只有失败使用红色。字体优先 Segoe UI Variable Text，其次 Segoe UI，中文使用 Microsoft YaHei UI。

### 同步与暂停

- 页面每秒同步一次。点**暂停**只停止页面同步，不会暂停或终止正在执行的任务；暂停期间运行中调用的耗时仍在本地计时。点**恢复**或 ⋯ → **立即同步**可以马上刷新。
- 页面切到后台时自动停止轮询，回到前台后继续。

### 归属与保留范围

- 没有宿主会话信号、也没有传入线程 ID 的调用进入“未归属”，工作台不会猜测归属。线程分组只是可视化隔离，不是账号级授权隔离。
- 活动及验证记录只保留当前进程最近 100 条，对话列表与计划在重启后清空。最小对话身份关联最多 200 条，与请求防重和文件撤销账本一起加密保存。旧请求重试需要明确核对，不会自动重跑。

### 页面实现与预览

静态资源在 `src/ui/`（React 组件与中文标签）、`src/dashboard.css`（Tailwind 4）和 `src/dashboard.template.html`（模板）。`npm ci && npm run build:ui` 先用 Tailwind CLI 编译样式，再用 esbuild 打包组件，全部内联进单个 `src/dashboard.html`；页面没有 CDN 和模块加载器，运行时不需要 Node。程序优先读取同目录的 `dashboard.html`，缺失时回退到嵌入页面。

预览与旧实例观察（不重启应用，也不执行工具）：

- `node scripts/dashboard-preview.cjs http://127.0.0.1:<原端口>/`：只读观察运行中的旧实例。
- `--sample`：用内置样例预览界面；`--sample=rich`：多文件补丁、长输出与回合分组样例。
- `--diag=slow|error`：模拟诊断变慢或失败；`--clear=fail`：模拟清空失败。

## 桌面程序

- **窗口**：默认 1000×640，最小 820×480。单行工具栏：**启动连接 / 停止**、**在浏览器打开**、**更多**。
- **状态标签**：工具栏右侧显示隧道状态：已停止 / 正在连接 / 已连接 / 连接异常。“已连接”只表示隧道健康检查通过，不代表 ChatGPT 已刷新工具。文档中的桌面截图在预览模式下拍摄，没有启动隧道，所以显示“已停止”。
- **四个页签**：
  - **实时工作台**：内嵌上面的工作台页面；未连接时显示“启动连接”入口。
  - **操作记录**：同一活动流的表格视图（时间 / 线程 / 操作 / 内容），可按对话筛选，双击行或 Ctrl+C 复制。
  - **原始日志**：隧道与 MCP 的完整原始输出，按级别着色，尾随跟随。
  - **连接配置**：Tunnel ID 与 API Key，配置只保存在本机。
- **更多菜单**：刷新工作台、诊断连接、清空已完成日志、复制原始日志、复制工作台链接。调用日志、对话列表和执行计划在程序退出后清空。
- **WebView2**：缺少运行时时，“实时工作台”页签提示原因并提供“在浏览器打开”；工具功能不受影响。

### 缩放

- 在**实时工作台**页签按 Ctrl+加号 / 减号逐级缩放：67%、75%、80%、90%、100%、110%、125%、150%、175%、200%；Ctrl+0 恢复 100%。Ctrl+滚轮使用 WebView2 自身的步进，同样限制在 67%–200%。其他页签不响应这些按键。
- 缩放比例写入 `settings.json` 的 `Zoom` 字段，下次启动恢复。实测：Ctrl+加号到 110%、Ctrl+滚轮到 125% 后，`settings.json` 写入 `"Zoom":1.25`，重新启动程序后工作台恢复为 125%（见 [验证记录](VERIFICATION.md)）。
- 超出范围的值会被限制：写入 5 按 200% 处理，写入 0.1 按 67% 处理，无效值按 100% 处理。
- 在浏览器中打开工作台时，使用浏览器自身的缩放，不写入 `settings.json`。

## 命令执行细节

- 默认运行于隐藏 Git Bash（`shell: "git_bash"`，兼容旧名 `bash`）；需要 PowerShell 语法时显式传 `shell: "powershell"` 或 `"pwsh"`。找不到 Git Bash 会明确报错，不会偷换解释器，也不会误用 WSL bash。
- 支持 `cmd` / `cwd` / `yield_time_ms` 参数（兼容旧 `command` / `yield_ms`）；返回实际 shell 与可执行路径。
- 命令在等待窗口后仍在运行时返回 `session_id`：只看输出用只读的 `read_command`（可带 `yield_time_ms` 等待），需要输入用 `write_stdin`，发送 Ctrl-C 终止命令树。标准输入是管道而不是 PTY；长任务续读同一会话，不要重复启动。
- 可选 `request_id` 防止重复执行：同一对话内相同 ID 与参数复用原会话，重启后不会误重跑。
- 默认 300 秒超时（可设 1–3600 秒）；输出快照保留最近 128,000 字符并标记截断；输入接收阻塞时终止该命令树并报错，保证 MCP 服务不失联。

## 过程可见性

- 工具描述提供中文调用状态；宿主传入 progressToken 时发送开始 / 执行中 / 结束通知，未知总量不显示虚构百分比。
- 桌面立即记录调用开始，同步等待中每 2 秒记录仍在执行，返回时区分正常与失败。
- 对话内的工具回执以文本为准；可视化进度统一在桌面程序的实时工作台查看，ChatGPT 内不挂载卡片面板。

## 版本历史

完整内容见 [升级说明](UPGRADE-NOTES.md)；以下只列各版本要点和当时的工具数。

| 版本 | 日期 | 要点 | 工具数 |
| --- | --- | --- | --- |
| [2.4.0](docs/RELEASE-2.4.0.md) | 2026-10-08 | 工具删合、工作台重做、对话来源与统计、旧记录实际删除、手动清空、组件升级、缩放 | 23 |
| [2.3.0](docs/RELEASE-2.3.0.md) | 2026-09-30 | 命令重试去重与输出分页、文件版本校验与预览、文件历史与撤销、Git 私有审阅基准、凭据迁入 Windows 凭据管理器 | 28 |
| [2.2.1](docs/RELEASE-2.2.1.md) | 2026-09-21 | 任务提示改为“任务详情”按钮，点击才打开 | 26 |
| [2.2.0](docs/RELEASE-2.2.0.md) | 2026-09-21 | `check_task_completion`、计划逐步证据与阻塞 / 暂停状态 | 26 |
| [2.1.0](docs/RELEASE-2.1.0.md) | 2026-09-20 | 按 `openai/session` 自动归组、`import_file`、回执改为 `structuredContent.result`、一键诊断 | 25 |
| 2.0.2 | 2026-09-20 | 工作台显示实际读取的图片和工作区状态、点击路径打开 Windows | 24 |
| 2.0.0 / 2.0.1 | 2026-09-18 | dual-era MCP（discover、MRTR、Tasks、trace）；2.0.1 起 legacy 不下发图标 | 24 |

完成检查依据模型登记的证据和本地执行状态，并非独立验收；插件不能阻止 ChatGPT 结束回复，也不能强制开启下一轮。复制的续做提示不会自动发送。

## 从源码构建

```powershell
./scripts/Get-RuntimeComponents.ps1         # 下载固定版本的官方 Tunnel，校验 SHA256，复制许可证
npm ci                                      # 安装锁定的前端构建与测试依赖
./build.ps1                                  # 先运行 npm run build:ui，再编译到 dist-next（含工作台页面与内嵌 WebView2 资源）
npm test                                    # 全部测试（node --test "tests/*.test.cjs"）
./scripts/package-release.ps1               # 白名单打包，包含 SHA256 清单与组件许可证
./build.ps1 -OutputDirectory ./dist         # 退出旧程序后直接构建到 dist
```

只改界面时运行 `npm run build:ui` 重新生成 `src/dashboard.html`。测试文件：

| 文件 | 覆盖 |
| --- | --- |
| `tests/mcp.test.cjs` | 协议、23 个工具、命令轮询、弃用旧名、Codex 归组与轮次字段 |
| `tests/modern.test.cjs` | modern 时代：discover、协商、MRTR、Tasks、legacy 回退 |
| `tests/patch.test.cjs` | 补丁引擎 |
| `tests/activity.test.cjs` | 活动记录与独立观察 |
| `tests/dashboard.test.cjs` | 工作台数据、线程隔离、HTTP 校验 |
| `tests/dashboard-ui.test.cjs` | 真实浏览器 UI 回归（16 个子测试） |
| `tests/dashboard-media.test.cjs` | 实际 MCP 图片、工作区状态和本地打开接口 |
| `tests/log-cleanup.test.cjs` | 旧日志删除、手动清空、任务证据与绑定回收 |
| `tests/native-workbench.test.cjs` | 隐藏原生 WebView2：底色、配色方案、缩放、嵌入页面与桌面计数 |
| `tests/reliability.test.cjs` | 重试、输出游标、版本校验、撤销 / 重做、凭据存储 |
| `tests/task-completion.test.cjs` | 完成检查、失败回执与续做界面 |
| `tests/workspace-2.1.test.cjs` | 宿主会话、附件契约与诊断 |
| `tests/tunnel.test.cjs` | 真实 Tunnel 健康检查（默认跳过） |

`tests/dashboard-ui.test.cjs` 用真实浏览器跑 `src/dashboard.html`，16 个子测试覆盖：三栏默认布局与选中同步、开始时间与持续计时、时间正序与回合分组、就地展开与各类型字段、被拒绝写入的展示、检查器开关与调宽、中性配色（无分类色、渐变、无限动画和彩色侧边线）、侧栏选中与折叠、计划栏不遮挡调用行、搜索与分段筛选、“⋯”菜单清空日志的确认与提示、矮窗口对话框滚动、640–1920 共 9 种宽度在检查器开 / 关下均无横向溢出且无脚本错误、检查器仅在 ≥1280 px 默认打开、静态骨架与连接失败页。浏览器由 `scripts/browser-launch.cjs` 按 `$env:WORKSPACE_TEST_BROWSER` → 自带 Chromium → Chrome → Edge 顺序挑选。

`$env:WORKSPACE_TEST_EXE` 可指定被测 EXE（默认 `dist-next`）；跑新构建前请清除旧值。真实 Tunnel 健康测试需显式设置 `$env:WORKSPACE_TUNNEL_SMOKE='1'`，使用已有配置，不要与同一 Tunnel 的另一实例同时运行。

主要源码：`src/Program.cs`（桌面外壳与 Tunnel）、`src/UiKit.cs`（自绘控件与主题）、`src/WorkbenchHost.cs`（内嵌 WebView2、缩放与资源解析）、`src/WorkspaceServer.cs`（协议与工具）、`src/WorkspaceThreads.cs`（对话归属与来源）、`src/WorkspaceActivity.cs`（活动记录）、`src/Presentation.cs`（资源与 diff）、`src/PatchEditor.cs`（补丁）、`src/WorkspaceContext.cs`（约定与计划）、`src/ui/`（工作台）。DevSpace 官方源码保留在 `vendor/devspace/`（MIT），当前 EXE 不依赖其 Node 服务，详见 [docs/DEVSPACE-SOURCE.md](docs/DEVSPACE-SOURCE.md)。

## 更新与替换

`Apply-Update.ps1` 一并更新 EXE、Tunnel、页面、组件清单和许可证：先保留回滚副本，每个文件复制后核对哈希，失败时恢复；默认只保留最近 3 个回滚目录。程序运行时默认拒绝覆盖；显式加 `-StageWhileRunning` 可以保留旧映像并准备下次启动的新版，不终止或重启进程。

先完成当前任务、关闭应用（会结束它托管的命令树），再运行更新脚本并从原 dist 启动。磁盘文件已更新不等于运行中的进程已升级。更新后在 ChatGPT 设置页刷新工具并**新开聊天**验证。

## 常见问题

**ChatGPT 说它只能读、不能改？** 先让它调用 `get_workspace_status`，核对版本、`tool_count: 23` 与连接状态；再在设置页刷新元数据并新开聊天。不要把旧聊天缓存、旧插件或未运行的服务当成系统权限不足。

**“已连接”但工具没反应？** 隧道连接不等于 ChatGPT 已刷新工具。原始日志必须出现 `initialize` 与 `tools/list` 才算打通。

**模型还在调用 `poll_command` 等旧名字？** 2.4.0 仍会执行，并在结果里给出替代写法。刷新 ChatGPT 工具列表，或修改 Codex 配置里的旧名字。

**内嵌工作台空白或提示不可用？** 内嵌视图依赖系统 WebView2 运行时（装有 Edge 即有）；缺失时页面会提示，可以用“在浏览器打开”。也可以在“更多”菜单刷新工作台。

**工作台显示“实时”但没有新调用？** “实时”只表示页面正在与本机同步；当前没有工具调用在运行时，时间线不会变化。模型内部思考不在插件可见范围，不代表任务失败。

**点“启动连接”没反应？** 配置缺失或格式不对时会跳到“连接配置”页签并把无效输入标红，按提示修正后再启动。

**命令报找不到 Git Bash？** 安装 [Git for Windows](https://git-scm.com/download/win)，或让模型显式传 `shell: "powershell"`。

## 安全说明

- 工作台只监听 `127.0.0.1` 动态端口，不开放远程访问或任意命令执行接口。打开 Windows 位置需要同源 POST 和当前进程令牌；文件只在资源管理器中定位，点击程序或脚本路径不会执行它。
- 默认以当前 Windows 用户权限访问本地磁盘；工作目录**不是**操作系统沙箱，请在可信账号下使用。
- **目前没有命令级 / 路径级护栏**：`exec_command` 会以当前用户权限执行模型下达的任意命令，没有白名单，破坏性命令（如 `rm -rf`）也不会弹确认框。文件类工具会拒绝越界路径，工具也从不自动 `commit` / `push`，但 **shell 不受限**。请把模型指向的目录范围收窄，并在实时工作台的时间线里复核每一步。
- API Key 保存在 Windows 凭据管理器；不要公开旧版配置、凭据或聊天附件链接。
- 最小对话身份关联、请求防重与直接文件历史在本地加密保存；调用正文、对话标题、计划、桌面原始日志、命令进程与图片预览只属于当前运行。

## 协议兼容性

本服务器是 **dual-era**（双时代）实现，按客户端打开方式自动选择行为，两条路径互不干扰：

| 时代 | 触发方式 | 提供的能力 |
| --- | --- | --- |
| legacy（2025-06-18） | `initialize` 握手（ChatGPT Tunnel 现行方式） | 兼容原有握手：23 个工具、进度通知、结构化输出 |
| modern（[2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28)） | 请求 `_meta` 携带 `io.modelcontextprotocol/protocolVersion` | 无状态每请求协商、`server/discover`、`resultType`、`tools/list` 缓存字段（`ttlMs`/`cacheScope: private`）、`serverInfo` 回执标识 |

modern 时代按客户端声明的能力渐进启用：

- **MRTR 危险操作确认**：客户端声明 `elicitation` 能力时，`apply_patch`、覆盖已有文件的 `write_file` 和实际执行的 `restore_change` 先返回 `resultType: "input_required"` 与 `elicitation/create` 请求；客户端带 `inputResponses` + `requestState` 重试后才执行。`requestState` 经 HMAC-SHA256 完整性保护，绑定工具名与参数指纹，10 分钟过期且一次性消费。未声明该能力的客户端行为不变。
- **Tasks 扩展（`io.modelcontextprotocol/tasks`）**：客户端声明该扩展时，仍在运行的 `exec_command` 返回标准任务句柄（`resultType: "task"`），可用 `tasks/get` 轮询、`tasks/cancel` 取消；未声明的客户端继续拿经典 `session_id` 结果。
- **OpenTelemetry**：请求 `_meta` 中的 `traceparent` 记入操作日志，活动行带 `trace_id`，检查器显示 trace 短 ID。
- **工具 icons 只在 modern 时代下发**：ChatGPT（legacy）的连接器安全校验会拒绝 `data:` URI 图标并阻断工具执行，因此 legacy `tools/list` 不带图标；文件输入元数据和返回 schema 正常下发。
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

本项目为非官方开源项目，与 OpenAI 无隶属关系，整体以 MIT 许可证发布（见 [LICENSE](LICENSE)）。

- 架构与工具设计参考并部分衍生自 [Waishnav/devspace](https://github.com/Waishnav/devspace)（MIT），其源码保留在 `vendor/devspace/`，含原始许可证文件。
- 内嵌视图使用 Microsoft WebView2 SDK（见 `vendor/webview2/LICENSE.WebView2.txt`），以资源形式内置于 EXE。
- 发行包中的 `tunnel-client.exe` 为 OpenAI 官方组件（Apache-2.0），随 GitHub Release 提供，不纳入本仓库版本管理。每个 Release 固定一个 tunnel-client 版本，请使用与该 Release 说明一致的版本，不要跨 Release 混用。

## Star History

如果这个项目帮到了你，欢迎点个 Star 支持。
