# 版本升级说明

按版本倒序排列，最新版本在前。当前版本为 **2.4.0**，上一公开版本为 2.3.0；2.3.0 之后的本地修复（旧日志、旧对话和执行计划的完整清理）已并入 2.4.0。各节记录的是该版本发布时的情况，不代表当前运行状态。1.3.0～1.6.0 为历史开发 / 构建记录，仓库现有 tag 从 v1.7.0 开始。

演进顺序（从早到晚）：1.3.0 → 1.4.0 → 1.4.1 → 1.5.0 → 1.6.0 → 1.7.0 → 2.0.0 → 2.0.1 → 2.0.2 → 2.1.0 → 2.2.0 → 2.2.1 → 2.3.0 → 2.4.0

## 从 2.3.x 升级清单

1. **先收尾**：完成运行中的任务，再退出旧程序。退出桌面程序会结束它托管的命令树。
2. **替换文件**：用 `Apply-Update.ps1` 或手动替换发行包中的 `LocalWorkspace.exe`、`tunnel-client.exe`（0.0.16）、`dashboard.html`、`runtime-components.json` 与许可证。更新脚本默认拒绝覆盖运行中的程序，保留最近 3 个回滚目录。
3. **首次启动**：新版自动删除旧的 `activity.bin`、`activity-state.bin`、`plans.bin`、`threads.bin`，旧对话只迁移为加密的最小身份关联。启动后侧栏、计划和时间线为空，这是预期结果。`changes.bin`、`requests.bin`、`settings.json` 与 Windows 凭据保留。
4. **刷新 ChatGPT 工具**：在“设置 → 连接器 → 本地工作区 → 信息”点**刷新**，确认列表为 23 个工具；新开聊天调用 `get_workspace_status`，应返回 `version: 2.4.0`、`tool_count: 23`。
5. **Codex 用户**：把 `config.toml` 中 `enabled_tools`、`disabled_tools`、`[mcp_servers.<名>.tools.<工具>]` 里的旧工具名改成新名字（对照见下文“工具删合”），推荐配置见 [README](README.md#codex-推荐配置)。
6. **自建客户端**：`tool_count` 核对值改为 23；旧工具名按对照迁移；需要 `full_output` 时传 `include_full_output: true`；轮询改用 `read_command` 的 `yield_time_ms` + `offset`；文件属性改用 `list_directory`（`kind: "file"`）。
7. **继续旧任务**：旧计划和验证记录不再恢复，用 `update_plan` 重新登记当前计划和证据。重启前未确认完成的命令请求返回 `REQUEST_RECONCILIATION_REQUIRED`，不会被重跑。
8. **核对版本**：在工作台对话栏底部打开“诊断连接”，确认运行版本与磁盘版本一致（状态“版本一致”）。

### 旧 → 新对照

| 2.3.x | 2.4.0 |
| --- | --- |
| `poll_command` 轮询 | `read_command` + `yield_time_ms`（+ `offset`），只读 |
| `stop_command` 停止 | `write_stdin` 发送 `"\u0003"`（Ctrl-C） |
| `list_commands` | `get_workspace_status` 的 `result.commands` |
| `read_workspace_activity` | `get_workspace_status` 传 `path`，读 `result.workspace` |
| `file_info` | `list_directory`，文件返回 `kind: "file"` 与 `info` |
| 命令回执默认带 `full_output` | 只在 `output` 返回一次；`include_full_output: true` 才带 `full_output` |
| 重启后恢复对话、计划和调用记录 | 不恢复；启动时删除旧文件，只保留加密的最小身份关联 |
| 工作台右上角清空 / 诊断按钮 | 标题栏“⋯ → 清空已完成日志…”（需确认）；“诊断连接”固定在对话栏底部，版本号同一行 |
| 侧栏收起按钮在侧栏内 | 侧栏按钮在标题栏、标题左侧 |
| “跟随最新”复选框 | 停在底部自动跟随，往上翻出现“跳到最新” |
| 状态下拉筛选 | Ctrl+F / `/` 搜索 +“全部 / 进行中 / 失败 / 已返回”分段筛选 |
| 每类调用一种主色、浅 / 深色方案 | 固定浅色中性灰，只有失败用红色 |
| 窗口与页面底色不一致，启动可能闪一下 | 窗口、WebView2 底色与页面外壳、对话栏统一 `#f7f7f7`，主区 `#fff` |
| 工作台不能缩放 | Ctrl+滚轮 / Ctrl+加号、减号、0，67%–200%，写入 `settings.json` 的 `Zoom` |

## 2.4.0 · 2026-10-08

### 行为变化

- `tools/list` 只列出 23 个工具；旧名字仍可调用，但结果带 `deprecated` 标记，摘要以 `DEPRECATED:` 开头。
- 旧调用、对话标题、路径、计划和验证日志不再跨重启保留；对话、计划、调用正文只存在于当前 MCP 进程。
- 清空日志需要确认，且桌面和工作台两个入口效果一致。清空后重试原命令仍复用原会话，失败记录仍参与完成检查。
- 命令回执默认不再重复返回 `full_output`。
- 工作台布局、配色和交互全部重做（见下文“工作台界面”）；嵌入页面不再跟随系统深色模式。
- 桌面程序记住工作台缩放比例；`settings.json` 只保存 `Tunnel` 与 `Zoom`，API Key 在 Windows 凭据管理器。

### 工具删合（28 → 23）

| 旧工具（2.4.0 不再列出，2.5.0 移除） | 改用 | 写法 |
| --- | --- | --- |
| poll_command | read_command | `{session_id, yield_time_ms, offset: <上次 next_offset>}`，有新输出或退出即返回，只读、不消费输出 |
| stop_command | write_stdin | `{session_id, chars: "\u0003"}`（Ctrl-C 终止命令树） |
| list_commands | get_workspace_status | `result.commands` |
| read_workspace_activity | get_workspace_status | 传 `path`，读 `result.workspace`（不阻塞、不计入活动时间线） |
| file_info | list_directory | 同一 `path`；文件返回 `kind: "file"` 与 `info` |

- 旧名字本版仍可调用：结果带 `deprecated: {tool, replacement, removed_in: "2.5.0", note}`；`get_workspace_status` 返回 `deprecated_tools` 列表。
- `read_command` 保持 `readOnlyHint=true`，Codex `default_tools_approval_mode = "writes"` 时轮询不再弹审批。
- 服务说明（instructions）仍在 512 字符内。

### 新增字段（只增不改）

- `conversations[].source`：`codex`（按 Codex `_meta.threadId` 归组）/ `chatgpt`（按 `openai/session` 归组）/ `manual`（`register_conversation` 登记）。
- `conversations[].stats = {calls, running, failed, added, removed, last_at}`；Codex 调用按 `_meta.threadId` 归组（`association: host_session`）。
- 活动行新增 `turn_id`、`turn_source`（目前只有 `codex` 或 null）、`trace_id`（来自 W3C traceparent）。
- 写入类回执 `detail.files[i].added` / `removed`；多文件补丁的 `detail.added` / `removed` 合计全部已写入文件（修复超过 6 个文件时只合计前 6 个）。失败且没有部分写入时为 0。
- `/api/clear-logs` 返回 `scope_code`、`cleared_at`、`kept`；`/api/diagnostics` 的 `versions[]` 带 `id`、`status`（`ok` / `restart_required` / `unknown_running` / `missing`）。

### 工作台界面

- 三栏布局：对话栏 | 时间线 | 检查器。≥1280 px 默认打开检查器，1100–1279 px 停靠但默认收起，<1100 px 改为抽屉，≤720 px 对话栏收成图标栏。检查器可拖动调宽（320–640 px），开关、宽度、侧栏与计划折叠状态都会记住。
- 侧栏按钮在标题栏、标题左侧；侧栏第一行与标题行对齐（内嵌时就是“全部对话”）。
- 标题栏显示来源（Codex / ChatGPT / 手动登记，取自 `conversations[].source`）、连接状态、工作目录、开始与最近活动时间、调用 / 进行中 / 排队 / 失败数与改动行数。
- 时间线一行一次调用，用灰色图标区分类型，不再按类型着色；按 `turn_id` 或 30 秒间隔分组；停在底部自动跟随，往上翻出现“跳到最新”。
- 详情可就地展开或在检查器中查看；长内容先截断再按需展开；末尾新增改动汇总卡，超过 6 个文件时先列前 5 个，其余收进“显示其余 N 个文件”。
- 搜索改为 Ctrl+F / `/` 打开，状态筛选改为“全部 / 进行中 / 失败 / 已返回”分段控件；支持 ↑↓ / j k 浏览。
- 立即同步、诊断连接和“清空已完成日志…”收进 ⋯ 菜单，清空前需确认；诊断连接常驻对话栏底部，版本号在同一行；组件版本逐项给出状态。
- 新增静态加载骨架、空状态、连接失败页和断线提示条。
- 固定浅色：外壳与对话栏 `#f7f7f7`，主区白色；移除深色方案、分类配色、渐变与闪烁动画。

### 桌面与构建

- 桌面窗口、WebView2 默认底色统一为 `#f7f7f7`，与页面外壳一致，启动首帧不再白屏；嵌入页面固定浅色方案；宿主导航附带 `?host=desktop`。
- 在实时工作台页签，Ctrl+加号 / 减号按 67%、75%、80%、90%、100%、110%、125%、150%、175%、200% 逐级缩放，Ctrl+0 复位，Ctrl+滚轮使用 WebView2 步进并限制在同一范围；比例写入 `settings.json` 的 `Zoom`，下次启动恢复（已实测）。工具栏与页签改为中性灰扁平样式。
- 新应用图标（方括号加光标）：`assets/local-workspace.ico` 改为 9 帧（16/20/24/32/40/48/64/128 为 32 位 BMP 帧，256 为 PNG 帧；16–32 为手调版），网页图标与侧栏品牌标记同步更换。窗口标题栏改用 16 px 手调帧、任务栏用 32 px 帧（`Program.cs` 按系统图标尺寸从 EXE 资源加载，不再使用 `ExtractAssociatedIcon`）。
- 前端打包目标 chrome120；`playwright-core` 锁定 1.64.0；`Get-RuntimeComponents.ps1 -UpdateSdk` 先在临时目录验签再写入 vendor；`build.ps1` 校验 vendor 中 WebView2 程序集与 `runtime-components.json` 版本一致；`Apply-Update.ps1` 默认只保留最近 3 个回滚目录（`-KeepRollbacks`）。
- 诊断接口按进程缓存 Tunnel Client 与 WebView2 Runtime 版本，不再每次启动外部进程。

### 日志清理（并入 2.3.0 之后的本地修复）

- 修复 2.3.0 的日志生命周期回归：调用正文、对话标题、目录、执行计划和验证日志不再落盘；启动时实际删除旧 activity.bin、activity-state.bin、plans.bin 和 threads.bin，首次快照全部为空。
- 旧 threads.bin 先转为只含 Id、HostKey、ChatId 的加密最小关联，保存成功后才删除；保留命令防重和文件撤销的归属，重启不会误重跑命令。
- 桌面“更多 → 清空已完成日志”与工作台“⋯ → 清空已完成日志…”（需确认）效果一致，即时清除桌面页签计数；保留运行命令、任务证据、当前计划与已登记的对话。

### 组件与其他

- 官方 Tunnel Client 0.0.16、WebView2 SDK 1.0.4258.31；前端直接依赖和可兼容修补的传递依赖升级；MCP 维持 2026-07-28 与 legacy。
- 分页与工作台尾部读取不先复制完整历史。绑定只在身份变化时保存，达到上限时回收没有当前使用、重试或撤销引用的旧身份。
- 诊断区分运行版本与磁盘版本。SDK 缓存按版本隔离并校验内容；嵌入程序集复用同一加载实例，避免 .NET Framework 类型身份不一致；视图先挂载到窗口，初始化失败释放资源。
- 更新脚本同时处理程序、Tunnel、页面、清单与许可证并保留回滚文件；发行包按白名单打包，包含组件来源与校验值。
- 上游开发依赖仍有尚无补丁的 braces 告警（来自 shadcn CLI，仅开发使用）；系统共享 WebView2 Runtime 的更新限制单独记录。详见 [2.4.0 发行说明](docs/RELEASE-2.4.0.md)。
- 运行环境仍为 .NET Framework 4.8.1，不迁移。

## 2.3.0 · 2026-09-30

- 命令重试去重、输出分页/末尾读取；编辑版本校验、预览与失败诊断。
- 本地加密保存任务与活动；完成证据关联真实活动，失败处理需明确说明。
- 新增文件历史和带冲突检查的撤销/重做，工具数为 28。
- Git 私有审阅基准、Windows 凭据管理器迁移；保持单 EXE、无 Node 运行时依赖。
- 旧接口保留；旧进程须退出再启动后使用新后端。完整范围与恢复限制见 [2.3.0 发行说明](docs/RELEASE-2.3.0.md)。

## 2.2.1 · 2026-09-21 · 任务提示按需查看

收起 2.2.0 的常驻任务提醒，工具数保持 26。

- 计划标题旁只保留“任务详情”按钮，点击才展开；关闭后轮询和状态变化不会自动打开。
- 历史执行问题默认折叠；步骤完成但未填写逐项证据时使用中性说明，复制操作区分核对与续做。
- 完成检查契约保持不变，不把计划总说明自动视为逐项证据，也不强制宿主续跑。
- **生效方式**：运行中的 2.2.0 实例可通过“更多 → 刷新工作台”加载更新后的同目录页面；完整程序版本在退出旧程序后启动新版时生效。
- [完整发行说明](docs/RELEASE-2.2.1.md)。

## 2.2.0 · 2026-09-21 · 任务完成检查与续做提示

针对未完成就收尾的情况增加任务回执，工具数从 25 增至 26。

- 新增 check_task_completion，核对未完成步骤、缺少的登记证据、运行中的命令及尚未记录恢复的执行问题。
- update_plan 增加逐步 evidence、task_state、reason 和 next_action；保留暂停/阻塞状态，范围变化需说明原因。
- 工具回执附带剩余工作提示；工作台当时使用常驻卡片展示原因和续做入口，后由 2.2.1 改为按钮。
- 超过两分钟无操作仅标记待确认；区分命令超时与普通失败。检查依据登记证据和本地状态，不是独立验收，也不能开启下一轮模型回复。
- **生效方式**：启动新版并在 ChatGPT 刷新工具，核对 version: 2.2.0、tool_count: 26。
- [完整发行说明](docs/RELEASE-2.2.0.md)。

## 2.1.0 · 2026-09-20 · 自动归组、附件导入与连接诊断

工具数从 24 增至 25。

- 根据宿主 openai/session 元数据自动归组，保留手动登记与 thread_id 兼容。
- 新增 import_file，将聊天附件保存为不覆盖已有内容的新文件，返回大小、类型和 SHA256，最多 32 MiB。
- 工具正文改为简短摘要，完整数据位于 structuredContent.result；各工具分别声明返回 schema。自建客户端需停止从 content[0].text 解析 JSON。
- 一键诊断分别显示配置、隧道、握手、工具发现、实际调用与会话信号；本地测试不冒充 ChatGPT 端到端验收。
- **生效方式**：启动新版并在 ChatGPT 刷新工具，核对 version: 2.1.0、tool_count: 25。
- [完整发行说明](docs/RELEASE-2.1.0.md)。

## 2.0.2 · 2026-09-20

补全工作台图片与工作区详情，支持点击路径打开 Windows，并统一按钮样式。

- 图片预览展示 `read_image` 本次实际返回的字节，支持适应窗口 / 原始尺寸；只按需请求选中图片，图片内容不进入每秒快照。缓存为进程内最多 100 张 / 32 MiB，过期后明确提示重新读取。
- 工作区状态展示版本、程序位置、默认 Shell、协议、运行命令、已登记工作区与工具清单。
- 所有结构化路径支持点击在 Windows 资源管理器中打开目录或选中文件；HTTP/HTTPS 地址通过默认浏览器打开。不存在的路径给出错误提示；同源、令牌和 POST 校验保护本地打开动作，文件不会直接执行。
- 刷新、暂停、复制、侧栏按钮移除原生黑框，统一浅深色、悬停和键盘焦点（2.4.0 起工作台固定浅色）。桌面顶部状态页地址也可点击。
- MCP 工具数仍为 24，保留 legacy / modern 行为；`read_image` 回执新增 `preview_url`，图片原生 content 不变。
- **生效方式**：文件更新不等于运行中的进程已升级。完成当前任务后退出旧程序，从 `dist/LocalWorkspace.exe` 重新启动。旧进程中的历史图片没有保存原始字节，需在新版重新读取。

## 2.0.1 · 2026-09-18

修复 ChatGPT 连接器"安全校验未完成，执行被阻断"。

- **根因**：2.0.0 在 `tools/list` 给 24 个工具加了 `data:image/svg+xml;base64` 图标；ChatGPT（legacy 客户端）的连接器安全校验拒绝 data URI 图标，导致所有工具调用（含只读的 get_workspace_status）被宿主侧阻断。
- **修复**：icons 改为仅 modern 时代下发——legacy `tools/list` 与 1.7.0 逐字节一致（新增回归断言），modern `tools/list` 继续携带 icons；其余 2.0.0 能力（discover/MRTR/Tasks/trace）不受影响。
- 升级后请在 ChatGPT「设置 → 插件 → 本地工作区 → 信息」刷新工具列表；若仍显示阻断，删除连接器后重新添加（宿主侧缓存了校验失败的元数据快照）。

## 2.0.0 · 2026-09-18

对照 MCP 2026-07-28 规范的协议大版本：升级为 dual-era 服务器，legacy（ChatGPT Tunnel）路径零回归。

- **Modern 时代（2026-07-28）协议入口**：请求 `_meta` 携带 `io.modelcontextprotocol/protocolVersion` 即按无状态 modern 语义处理；新增 `server/discover`（supportedVersions / capabilities / instructions / serverInfo，可缓存）；所有 modern 结果带必填 `resultType:"complete"` 与 `_meta` 中的 `serverInfo` 回执标识；版本不匹配返回 -32022（UnsupportedProtocolVersionError），缺 clientCapabilities 返回 -32021；modern 时代按规范不再响应 `ping`。
- **可缓存 tools/list**：modern `tools/list` 附带 CacheableResult 必填字段 `ttlMs:300000` 与 `cacheScope:"private"`；capabilities 增加 `extensions` 字段。
- **MRTR 危险操作确认**：modern 客户端声明 `elicitation` 能力时，`apply_patch` 与覆盖已有文件的 `write_file` 先返回 `resultType:"input_required"` + `elicitation/create`（form 模式）；客户端带 `inputResponses` 与原样参数重试后才执行。`requestState` 为 HMAC-SHA256 签名的 base64url 载荷，绑定工具名 + 参数 SHA-256 指纹，10 分钟过期、nonce 一次性消费；篡改 / 改参 / 过期 / 重放均拒绝（CONFIRM_STATE_INVALID），decline 返回 CONFIRM_DECLINED 且不改文件。
- **Tasks 扩展（io.modelcontextprotocol/tasks）**：modern 客户端声明该扩展时，yield 窗口内未结束的 `exec_command` 返回 `resultType:"task"` 标准句柄（taskId 复用会话 ID，`pollIntervalMs:1000`、`ttlMs:3600000`）；`tasks/get` 轮询（working / completed 携带完整 CallToolResult / cancelled），`tasks/cancel` 协作式终止进程树，`tasks/update` 空确认；未声明扩展的客户端保持经典 `session_id` 会话结果。
- **OpenTelemetry trace 关联**：读取请求 `_meta.traceparent`（截断 200 字符）写入操作日志，工作台检查器元信息行显示 trace 短 ID；快照 activity 条目新增 `trace` 字段。
- **工具图标**：24 个工具全部附带 `icons`（16×16 内嵌 SVG data URI，按终端 / Git / 搜索 / 写入 / 工作区 / 读取六类配色），宿主 UI 可渲染。
- **诊断**：`get_workspace_status` 新增 `protocol_versions` 字段，明示双时代支持。
- **测试**：新增 `tests/modern.test.cjs`（discover、版本协商错误、resultType/缓存字段、icons、trace 记录、MRTR 全链路含篡改/改参/decline/重放、Tasks 生命周期、legacy 回退零回归）；全量 16 项测试通过。

## 1.7.0 · 2026-09-17

桌面程序重绘 + 实时工作台内嵌 + ChatGPT 卡片下线；工具数 25 → 24。

- **桌面外壳重绘**：对齐工作台设计语言（品牌墨绿主色、白卡片、发丝边框、圆角按钮）。单行工具栏：启动/停止合并按状态切换、"在浏览器打开"、"更多"自绘菜单（刷新工作台 / 清空日志 / 复制原始日志 / 复制工作台链接）；页签改为 实时工作台 / 操作记录 / 原始日志 / 连接配置 四个；操作记录为自绘表格（级别徽章、等宽内容、行复制），原始日志为自绘控制台（时间戳弱化、级别芯片、尾随跟随、横向平移）。
- **实时工作台内嵌**：首个页签经 WebView2 嵌入工作台页面，连接就绪自动载入；WebView2 组件以 manifest 资源内置、按需解析，保持单 EXE 发行；缺运行时回退浏览器并提示。去掉与窗口标题重复的大头部；连接配置独立成页签，配置缺失或格式错误时启动/保存会跳转并标红对应输入框。
- **ChatGPT 卡片下线**：移除 `render_workspace` 工具与 `resources/list`、`resources/read`、`resources/templates/list` 通道（`workspace-card.html` 不再内置），initialize 不再声明 resources 能力，指令改为引导模型使用本地工作台；`read_workspace_activity` 保留为对话内文本快照工具。可视化进度统一在桌面程序查看。
- 样例快照路径中性化（`E:/workspace/local-workspace`、`C:/Users/dev`），README 三张截图重截。

## 1.6.0 · 独立实时工作台与对话分组

- EXE 内置只读回环 HTTP 页面，桌面可打开浏览器或复制地址；不依赖 ChatGPT iframe，不引入 Node 或浏览器运行时。
- 新增 register_conversation（共 25 工具），返回本地 thread_id 与线程直达链接。已知真实 ChatGPT UUID 可显式绑定；未知不伪造。后续调用携带 thread_id，未携带者单列未归属，命令续读继承归属。
- 独立页面每秒更新：对话侧栏、执行时间线、实际命令状态／耗时／输出、计划、搜索与状态筛选、暂停恢复、登记指引。文件和命令操作不经过网页，页面只观察。
- 同一项目的计划和命令按线程分别显示；桌面日志增加对话列及筛选。服务重启后进程内历史与线程失效，重新登记并打开新地址。
- 工作台仅监听 127.0.0.1，校验 Host/Origin/Fetch-Site，拒绝写入请求；历史及输出仍有明确容量上限。线程分组不是账号权限边界。

## 1.5.0 · 实时工作区面板

- 增加 render_workspace / read_workspace_activity，共 24 个工具。仅展示入口绑定 activity-v1 模板，数据工具不再反复生成卡片。
- 每 2 秒观察跨工具活动、当前操作、计划、命令输出；操作开始立即记录，结束或失败更新原条目。
- 协议读取与有序工具工作线程分离，活动查询独立响应；文件修改保持顺序，输出序列化避免多线程 JSON 交错。Git 关闭独立标准输入，避免继承协议输入管道导致阻塞。
- 暂停/恢复、页面隐藏暂停、连接失败提示、实例更换保护、按宿主能力显示画中画入口。面板轮询不消耗模型输出、不重发执行命令。
- UI_RESOURCE / UI_CONNECTED 日志及展示心跳用于区分资源被读取与面板实际连接；活动和命令显示有明确范围及容量。
- 原 review-v4/v5 资源仍能读取，默认 Git Bash 与 PowerShell 能力保留。实例重启会清空原进程的计划、命令和活动记录。

## 1.4.1（历史构建记录）

- 修复旧桥接与历史卡片恢复：兼容 toolResponseMetadata 内直接及嵌套 mcp_tool_result，保留图片内容与后续主题更新中的最新结果。
- 标准 UI 握手被拒绝或超时时，不再把已有成功结果覆盖为等待宿主；旧 callTool 桥接仍可刷新卡片。
- 模板升级为 review-v5，保留 review-v4 资源读取；增加标准与兼容 CSP 元数据，卡片无外部资源依赖。
- 保留 22 个工具、默认 Git Bash 与显式 PowerShell 调用能力。
- 16 项卡片测试及完整 MCP 回归通过；隔离浏览器验证仅旧桥接宿主下的恢复与刷新。真实桌面客户端尚待验收，不把模拟验证当作桌面端成功。
- 当时 dist 与 dist-next 均为 1.4.1，构建前确认没有运行实例；该次没有启动应用或进行线上刷新。

## 1.4.0（历史构建记录）

- 默认解释器改为 Git Bash，接受 git_bash/bash；powershell/pwsh 保留显式选择。Git 安装发现支持 PATH 中的 Git 根路径与 Git for Windows 注册表，不回退至 WSL bash 或 PowerShell。
- workspace/status 返回 default_shell；命令返回实际 shell、shell_executable，列表和卡片显示解释器。卡片输入进度兼容 cmd 与旧 command。
- 初始化说明提醒宿主工具过滤不等于服务器只读；保留文本结果，不依赖图形卡片才能取得回执。桌面端工具发现与渲染没有真实验收，不能将网页版成功等同于桌面版成功。
- Apply-Update.ps1 在旧应用/隧道运行时拒绝更新；没有杀进程、自动重启或抢占 Tunnel。默认 shell 改变后需同步刷新工具元数据并使用新聊天。
- 当时保留运行中的 1.3.0；后续构建与切换情况见 1.4.1 历史记录。

## 1.3.0 · 2026-09-16

工具从原 10 个扩展到 22 个，卡片资源为 ui://local-workspace/review-v4.html。

- Codex 风格入口：open_workspace、update_plan、apply_patch，发现项目约定、显示真实计划、审阅多文件修改。
- exec_command 支持 cmd / cwd / yield_time_ms 与 shell；旧参数保持兼容。write_stdin 支持续读、输入、Ctrl-C 停止，不重复执行命令。
- 流式读取无需换行；累计输出供卡片，增量输出供模型，互不抢占。支持命令诊断、超时、错误和显式停止。
- 新增 Git 状态/差异、原生图片读取、目录创建、命令清单与连接诊断。
- 卡片自动续读、保持手动滚动位置、展示计划及部分补丁结果；宿主取消不冒充命令终止。
- 标准 MCP progress、输出 schema、中文调用状态与开始/返回日志。宿主是否展示卡片由宿主决定。
- DevSpace 官方可编辑源码位于 vendor/devspace，MIT 许可与 Git 历史保留，当前轻量 EXE 不依赖其 Node 服务。

沿用已有插件、Tunnel、凭据和访问范围。服务器新版 tools/list 返回 22 个；已有聊天可能仍缓存旧六个工具，需宿主刷新元数据后才可发现新增工具。网页版入口为“设置 → 插件 → 本地工作区 → 底部信息 → 刷新”，不是插件目录的应用详情页。2026-09-16 已在真实账号完成刷新，设置页显示全部 22 个工具和 review-v4 卡片模板。不要把工具缓存误判为本地只有读取权限。

官方依据：[MCP Apps](https://developers.openai.com/plugins/build/chatgpt-ui)、[工具契约](https://developers.openai.com/plugins/reference#tool-descriptor-parameters)、[Codex 项目约定](https://learn.chatgpt.com/docs/agent-configuration/agents-md)、[Codex app-server](https://learn.chatgpt.com/docs/app-server#api-overview)。
