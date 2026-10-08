# 2.4.0 · 工具精简、工作台重做与日志完整清理

2026-10-08。程序版本 2.4.0，工具数 **23**（2.3.0 为 28）。本版同时包含 2.3.0 之后的本地修复（旧日志、旧对话和执行计划的完整清理）。升级步骤见 [升级说明](../UPGRADE-NOTES.md#从-23x-升级清单)，验证结果见 [验证记录](../VERIFICATION.md)。

## 概览

- 5 个工具并入现有工具，`tools/list` 从 28 个减为 23 个；旧名字本版仍可调用，2.5.0 移除。
- `read_command` 支持等待新输出且保持只读，命令轮询不再触发写入审批。
- 对话来源（`source`）、对话统计（`stats`）、逐文件增删行数和轮次字段（`turn_id` 等）进入工作台快照。
- 实时工作台重做为三栏布局；桌面窗口与页面外壳统一 `#f7f7f7`，工作台可缩放并记住比例。
- 启动时实际删除旧调用、对话和计划文件；桌面与工作台提供一致的“清空已完成日志”。
- 官方 Tunnel Client 0.0.16、WebView2 SDK 1.0.4258.31。

## 工具删合与迁移

| 旧工具 | 改用 | 写法 |
| --- | --- | --- |
| `poll_command` | `read_command` | `{session_id, yield_time_ms: 5000, offset: <上次 next_offset>}`；有新输出或退出即返回，不消费输出 |
| `stop_command` | `write_stdin` | `{session_id, chars: "\u0003"}`（Ctrl-C，终止命令树） |
| `list_commands` | `get_workspace_status` | 读 `result.commands` |
| `read_workspace_activity` | `get_workspace_status` | 传 `path`，读 `result.workspace`；不阻塞、不进入活动时间线 |
| `file_info` | `list_directory` | 同一 `path`；文件返回 `kind: "file"`、`entries: []` 与 `info` |

- **弃用契约**：旧名字不出现在 `tools/list`，但仍按原行为执行；结果的 `structuredContent.result.deprecated` 为 `{tool, replacement, removed_in: "2.5.0", note}`，摘要首行以 `DEPRECATED:` 开头；`get_workspace_status` 返回 `deprecated_tools`。
- **只读轮询**：`read_command` 新增 `yield_time_ms`（0..10000 ms）；同时传 `offset` 时，有超过该位置的新输出或命令退出就返回。工具保持 `readOnlyHint=true`，Codex `default_tools_approval_mode = "writes"` 时免审批。
- **Codex 配置**：`enabled_tools`、`disabled_tools`、`[mcp_servers.<名>.tools.<工具>]` 中的旧名字要改为新名字，2.5.0 起旧名字不再匹配。推荐配置见 [README](../README.md#codex-推荐配置)。
- **自建客户端**：按 `tools/list` 实际返回调用；`tool_count` 核对值为 23。服务说明（instructions）为 507 字符，完整任务、完成检查、轮询命令及授权边界都在宿主读取的前 512 字符内。

## 接口与字段

均为只增不改：

- `conversations[].source`：`codex` / `chatgpt` / `manual`，由对话的宿主绑定判定（Codex 绑定按 `_meta.threadId`，ChatGPT 按 `openai/session`，手动登记为 `manual`）。
- `conversations[].stats = {calls, running, failed, added, removed, last_at}`。Codex 调用按 `_meta.threadId`（加 `sessionId`）自动归组，`association: host_session`。
- 活动行：`turn_id`（来自 `_meta["x-codex-turn-metadata"].turn_id`）、`turn_source`（`codex` 或 null）、`trace_id`（W3C `traceparent` 的 trace-id）。
- 写入类回执：`detail.files[i].added` / `removed`；多文件补丁的 `detail.added` / `removed` 合计全部已写入文件（修复超过 6 个文件时只合计前 6 个）。部分失败时只统计已写入的文件，未写入的文件不出现在 `files` 中。
- `/api/clear-logs` 返回 `scope_code`（`completed_logs`）、`cleared_at`、`kept`；`/api/diagnostics` 的 `versions[]` 带 `id` 和 `status`（`ok` / `restart_required` / `unknown_running` / `missing`）。
- 命令回执默认只在 `output` 返回正文；`include_full_output: true` 显式启用有界兼容字段 `full_output`。累计查看用 `read_command`，大日志优先传 `offset`、`length`；分页与工作台尾部快照只复制需要的区间。`output_chars` 是当前保留的历史字符数，`logs_cleared` 标出已清理的会话。

## 工作台界面

- **三栏布局**：对话栏、时间线、检查器。窗口 ≥1280 px 时检查器默认打开；1100–1279 px 停靠但默认收起；1100 px 以下改为抽屉；≤720 px 时对话栏收成图标栏。侧栏按钮在标题栏、标题左侧，对话栏第一行与标题行对齐。检查器可拖动调宽（320–640 px），开关、宽度、对话栏折叠和计划栏展开状态都会记住。
- **标题栏与统计**：对话来源（Codex / ChatGPT / 手动登记，取自 `source`）、直达原对话的链接、连接状态（实时 / 已暂停 / 已断开 / 连接中）；工作目录、开始时间和最近活动时间；调用、进行中、排队、失败数和改动行数。
- **计划栏**：移到标题栏下方，默认收起成一行进度，展开后列出全部步骤，不再遮挡调用。“任务详情”仍然只在点击后打开。
- **时间线**：一行一次调用，用灰色类型图标区分，不再按类型着色；相邻调用按 Codex `turn_id` 分组，没有时按 30 秒间隔分组；停在底部自动跟随，往上翻出现“跳到最新”。
- **详情**：点击在行下就地展开，或在检查器中查看；代码块带行号和复制按钮，diff 只用底色区分增删行；长 diff、长输出和长搜索结果先截断。时间线末尾新增改动汇总卡，超过 6 个文件时先列前 5 个，其余收进“显示其余 N 个文件”；点击文件跳到对应调用。
- **搜索与键盘**：Ctrl+F 或 `/` 打开搜索；“全部 / 进行中 / 失败 / 已返回”分段筛选；↑↓ 或 j / k 在调用之间移动。
- **操作入口**：立即同步、诊断连接和“清空已完成日志…”在标题栏 ⋯ 菜单；清空前需要确认，完成后提示清理条数。诊断连接常驻对话栏底部，版本号在同一行；组件版本逐项标出“版本一致”“重启后才会使用新版本”等状态。
- **状态与外观**：静态加载骨架、空状态、连接失败页，以及断线后保留旧数据的提示条。固定浅色：外壳与对话栏 `#f7f7f7`、主区白色，与桌面窗口底色一致；移除深色方案、分类配色、渐变和闪烁动画。
- **测试**：`tests/dashboard-ui.test.cjs` 重写为 16 个子测试，覆盖 640–1920 共 9 种宽度、检查器开和关两种状态下的布局，以及中性配色、计划栏不遮挡、清空确认和错误页。

## 桌面程序

- 窗口（`UiKit` `Theme.Window`）与 WebView2 `DefaultBackgroundColor` 为 `#f7f7f7`，与页面外壳（`--frame`）一致，主区（`--surface`）为 `#fff`。实测启动后约 0.4 秒内宿主区域整片为 `#f7f7f7`，约 0.57 秒页面绘出，没有白屏。
- WebView2 配色方案固定为 Light，宿主导航附带 `?host=desktop`，页面据此隐藏品牌行。
- 缩放：在实时工作台页签，Ctrl+加号 / 减号按 67%、75%、80%、90%、100%、110%、125%、150%、175%、200% 逐级调整，Ctrl+0 复位；Ctrl+滚轮使用 WebView2 步进并限制在 67%–200%。比例写入 `settings.json` 的 `Zoom`，下次启动恢复；超出范围的值被限制到边界，无效值按 100% 处理。
- 工具栏与页签改为中性灰扁平样式。
- 新应用图标：白色圆角块上的黑色方括号加块状光标。`assets/local-workspace.ico` 含 16/20/24/32/40/48/64/128/256 共 9 帧，其中 16–32 为逐像素手调版，256 为 PNG 压缩帧，其余为 32 位 BMP 帧以兼容资源管理器；网页图标（favicon）与浏览器中的侧栏品牌标记使用同一图形，嵌入桌面时侧栏不显示品牌行。
- 窗口图标改为从 EXE 资源按系统尺寸分别加载：标题栏小图标用 16 px 手调帧，任务栏和 Alt+Tab 的大图标用 32 px 帧；此前通过 `ExtractAssociatedIcon` 只取得 32 px 帧，标题栏图标由它缩小而来。程序目前未声明 DPI 感知，高缩放下由系统整体拉伸，20/24 px 帧暂不用于标题栏。

## 日志生命周期

旧版仍从磁盘恢复侧栏对话与执行计划，造成时间线为空、历史内容却仍可见。新版在启动时实际删除 `activity.bin`、`activity-state.bin`、`plans.bin` 和 `threads.bin`；对话标题、路径、计划、调用正文和验证日志只保留在当前 MCP 进程。最小身份（Id、HostKey、ChatId）迁移成功后才删除旧对话文件，命令重试与文件撤销的归属继续保留。

桌面“更多 → 清空已完成日志”和工作台“⋯ → 清空已完成日志…”清理已完成调用与命令日志，工作台在执行前要求确认，桌面页签计数立即清零。运行中命令、当前计划、任务证据及已登记的对话保留。清空后重试原命令仍复用其会话，失败记录仍参与完成检查，不会因为隐藏日志误报任务成功。

最小绑定只在新身份或绑定字段变化时写盘，改标题与重复读取不会重写加密文件。达到 200 个身份时回收没有当前使用、也没有命令重试或文件撤销引用的旧绑定；所有身份都受保护时拒绝新增，不丢弃归属。身份保存失败时保留待保存状态，后续请求先重新保存，成功前拒绝启动命令等副作用。

“诊断连接”的组件版本表区分当前进程与磁盘文件。WebView2 原生加载器使用 SDK 版本目录和内容校验；每个嵌入的托管程序集只加载一次，避免 .NET Framework 中同名程序集的类型身份不一致。视图挂载后初始化，失败释放资源。

## 官方组件与依赖

| 组件 | 发布版本 | 本次变化 |
| --- | --- | --- |
| OpenAI Tunnel Client | 0.0.16 | 从 0.0.14 升级；官方 ZIP SHA256 固定并校验 |
| Microsoft WebView2 SDK | 1.0.4258.31 | 从 1.0.2903.40 升级；net462 与 x64 Loader 嵌入 EXE |
| Radix UI | 1.7.0 | 从 1.6.7 升级 |
| Lucide React | 1.53.0 | 从 1.46.0 升级 |
| Playwright Core | 1.64.0 | 从 1.63.0 升级，锁定精确版本，仅开发验证 |
| shadcn CLI | 4.21.4 | 从 4.21.0 升级，仅开发工具 |
| cn | 0.4.0 | 从 0.3.0 升级 |
| Parcel watcher | 2.6.0 | 兼容接口的开发依赖覆盖，移除其旧 braces 依赖链 |
| MCP TypeScript SDK / source-map-js | 1.32.1 / 1.2.2 | 修补开发依赖中的已知告警，不把 Node 服务带入发行包 |

React 19.3.0、Tailwind 4.3.3、esbuild 0.28.2 为核查时的最新版本；前端打包目标 chrome120。MCP 保持 [2026-07-28](https://modelcontextprotocol.io/specification/2026-07-28) 与 2025-06-18 legacy 路径。

[Tunnel 0.0.16 官方发行记录](https://github.com/openai/tunnel-client/releases/tag/v0.0.16)增加响应耗时观察和可选 W3C trace context 转发。桌面通过 `MCP_FORWARD_TRACE_CONTEXT=true` 启用标准 `traceparent` / `tracestate` 元数据转发；不转发 baggage，也不把整体响应耗时说成纯服务器执行时间。legacy 调用必须完成 `initialize` 和 `notifications/initialized`，modern 调用携带完整协商元数据；不自动伪造宿主初始化。

[SDK 官方发行说明](https://learn.microsoft.com/en-us/microsoft-edge/webview2/release-notes/sdk/1-0-4258-31)与 [NuGet 包](https://www.nuget.org/packages/Microsoft.Web.WebView2/1.0.4258.31)用于来源核验。组件 URL 和 SHA256 见 [runtime-components.json](../runtime-components.json)，固定下载入口为 `scripts/Get-RuntimeComponents.ps1`；`-UpdateSdk` 先在临时目录验签再写入 vendor，`build.ps1` 校验 vendor 中的程序集版本与清单一致。

## 升级与回滚

关闭旧程序前先完成运行任务；退出桌面会结束它托管的命令树。发行包中的 EXE、Tunnel、页面与组件许可证一并替换，启动新版后刷新 ChatGPT 工具元数据。调用 `get_workspace_status` 应返回 `version: 2.4.0`、`tool_count: 23`，再在“诊断连接”中核对运行进程版本与磁盘版本一致。

源代码更新使用 `Get-RuntimeComponents.ps1`、`build.ps1` 和 `Apply-Update.ps1`。更新脚本预检全部文件，保留回滚目录（默认最近 3 个，`-KeepRollbacks` 可调，占用中的目录跳过并提示），逐文件复制并核对哈希，异常时恢复已修改文件。默认拒绝覆盖运行实例；显式 `-StageWhileRunning` 准备下次启动，不重启现有进程。

旧计划及验证日志不再恢复，继续旧任务须重新登记当前计划和证据。`changes.bin`、`requests.bin`、`settings.json` 及 Windows 凭据继续保留；日志清理不会删除文件恢复历史或命令防重记录。

## 验证及上游限制

验证记录见 [VERIFICATION.md](../VERIFICATION.md)，包含实际后端、真实 Chromium 浏览器和原生 WebView2 控件。截图使用隔离数据，不把本地请求计为 ChatGPT 宿主验收。

当前机器的共享 Evergreen Runtime 仍为 147.0.3912.86。已验证微软签名后尝试 Bootstrapper、完整安装包和现有更新服务；安装器返回 0x80040828，更新服务返回 0x80040719，winget 未匹配到已有安装。没有卸载共享 Runtime、修改系统策略或重启应用。新版 SDK 的当前基础调用已在此 Runtime 上通过原生页面验收；需要 SDK 新增 API 的功能仍需兼容的新 Runtime。

`npm audit --omit=dev` 为 0 告警。完整开发树仍有 7 个 high 告警，均来自 shadcn 的 `braces` 依赖链；[官方告警](https://github.com/advisories/GHSA-vfj7-8cjw-p6xm)尚无修复版本。已升级能够兼容修补的依赖，没有通过强制降级最新工具链制造“零告警”。发行包不含 Node、npm 或这些开发包。
