# 版本升级说明

按版本倒序排列，最新版本在前。当前发布版本为 **2.2.1**；各节的工具数、构建状态和验证边界描述对应版本当时的情况，不代表当前运行状态。1.3.0～1.6.0 为历史开发/构建记录，仓库现有 tag 从 v1.7.0 开始。

已记录的演进顺序（从早到晚）：

1.3.0 → 1.4.0 → 1.4.1 → 1.5.0 → 1.6.0 → 1.7.0 → 2.0.0 → 2.0.1 → 2.0.2 → 2.1.0 → 2.2.0 → 2.2.1

## 2.2.1 · 任务提示按需查看

2026-09-21。收起 2.2.0 的常驻任务提醒，工具数保持 26。

- 计划标题旁只保留“任务详情”按钮，点击才展开；关闭后轮询和状态变化不会自动打开。
- 历史执行问题默认折叠；步骤完成但未填写逐项证据时使用中性说明，复制操作区分核对与续做。
- 完成检查契约保持不变，不把计划总说明自动视为逐项证据，也不强制宿主续跑。
- **生效方式**：运行中的 2.2.0 实例可通过“更多 → 刷新工作台”加载更新后的同目录页面；完整程序版本在退出旧程序后启动新版时生效。
- [完整发行说明](docs/RELEASE-2.2.1.md)。

## 2.2.0 · 任务完成检查与续做提示

2026-09-21。针对未完成就收尾的情况增加任务回执，工具数从 25 增至 26。

- 新增 check_task_completion，核对未完成步骤、缺少的登记证据、运行中的命令及尚未记录恢复的执行问题。
- update_plan 增加逐步 evidence、task_state、reason 和 next_action；保留暂停/阻塞状态，范围变化需说明原因。
- 工具回执附带剩余工作提示；工作台当时使用常驻卡片展示原因和续做入口，后由 2.2.1 改为按钮。
- 超过两分钟无操作仅标记待确认；区分命令超时与普通失败。检查依据登记证据和本地状态，不是独立验收，也不能开启下一轮模型回复。
- **生效方式**：启动新版并在 ChatGPT 刷新工具，核对 version: 2.2.0、tool_count: 26。
- [完整发行说明](docs/RELEASE-2.2.0.md)。

## 2.1.0 · 自动归组、附件导入与连接诊断

2026-09-20。工具数从 24 增至 25。

- 根据宿主 openai/session 元数据自动归组，保留手动登记与 thread_id 兼容。
- 新增 import_file，将聊天附件保存为不覆盖已有内容的新文件，返回大小、类型和 SHA256，最多 32 MiB。
- 工具正文改为简短摘要，完整数据位于 structuredContent.result；各工具分别声明返回 schema。自建客户端需停止从 content[0].text 解析 JSON。
- 一键诊断分别显示配置、隧道、握手、工具发现、实际调用与会话信号；本地测试不冒充 ChatGPT 端到端验收。
- **生效方式**：启动新版并在 ChatGPT 刷新工具，核对 version: 2.1.0、tool_count: 25。
- [完整发行说明](docs/RELEASE-2.1.0.md)。

## 2.0.2 升级说明

2026-09-20。补全工作台图片与工作区详情，支持点击路径打开 Windows，并统一按钮样式。

- 图片预览展示 `read_image` 本次实际返回的字节，支持适应窗口 / 原始尺寸；只按需请求选中图片，图片内容不进入每秒快照。缓存为进程内最多 100 张 / 32 MiB，过期后明确提示重新读取。
- 工作区状态展示版本、程序位置、默认 Shell、协议、运行命令、已登记工作区与工具清单。
- 所有结构化路径支持点击在 Windows 资源管理器中打开目录或选中文件；HTTP/HTTPS 地址通过默认浏览器打开。不存在的路径给出错误提示；同源、令牌和 POST 校验保护本地打开动作，文件不会直接执行。
- 刷新、暂停、复制、侧栏按钮移除原生黑框，统一浅深色、悬停和键盘焦点。桌面顶部状态页地址也可点击。
- MCP 工具数仍为 24，保留 legacy / modern 行为；`read_image` 回执新增 `preview_url`，图片原生 content 不变。
- **生效方式**：文件更新不等于运行中的进程已升级。完成当前任务后退出旧程序，从 `dist/LocalWorkspace.exe` 重新启动。旧进程中的历史图片没有保存原始字节，需在新版重新读取。

## 2.0.1 升级说明

2026-09-18。修复 ChatGPT 连接器"安全校验未完成，执行被阻断"。

- **根因**：2.0.0 在 `tools/list` 给 24 个工具加了 `data:image/svg+xml;base64` 图标；ChatGPT（legacy 客户端）的连接器安全校验拒绝 data URI 图标，导致所有工具调用（含只读的 get_workspace_status）被宿主侧阻断。
- **修复**：icons 改为仅 modern 时代下发——legacy `tools/list` 与 1.7.0 逐字节一致（新增回归断言），modern `tools/list` 继续携带 icons；其余 2.0.0 能力（discover/MRTR/Tasks/trace）不受影响。
- 升级后请在 ChatGPT「设置 → 插件 → 本地工作区 → 信息」刷新工具列表；若仍显示阻断，删除连接器后重新添加（宿主侧缓存了校验失败的元数据快照）。

## 2.0.0 升级说明

2026-09-18。对照 MCP 2026-07-28 规范的协议大版本：升级为 dual-era 服务器，legacy（ChatGPT Tunnel）路径零回归。

- **Modern 时代（2026-07-28）协议入口**：请求 `_meta` 携带 `io.modelcontextprotocol/protocolVersion` 即按无状态 modern 语义处理；新增 `server/discover`（supportedVersions / capabilities / instructions / serverInfo，可缓存）；所有 modern 结果带必填 `resultType:"complete"` 与 `_meta` 中的 `serverInfo` 回执标识；版本不匹配返回 -32022（UnsupportedProtocolVersionError），缺 clientCapabilities 返回 -32021；modern 时代按规范不再响应 `ping`。
- **可缓存 tools/list**：modern `tools/list` 附带 CacheableResult 必填字段 `ttlMs:300000` 与 `cacheScope:"private"`；capabilities 增加 `extensions` 字段。
- **MRTR 危险操作确认**：modern 客户端声明 `elicitation` 能力时，`apply_patch` 与覆盖已有文件的 `write_file` 先返回 `resultType:"input_required"` + `elicitation/create`（form 模式）；客户端带 `inputResponses` 与原样参数重试后才执行。`requestState` 为 HMAC-SHA256 签名的 base64url 载荷，绑定工具名 + 参数 SHA-256 指纹，10 分钟过期、nonce 一次性消费；篡改 / 改参 / 过期 / 重放均拒绝（CONFIRM_STATE_INVALID），decline 返回 CONFIRM_DECLINED 且不改文件。
- **Tasks 扩展（io.modelcontextprotocol/tasks）**：modern 客户端声明该扩展时，yield 窗口内未结束的 `exec_command` 返回 `resultType:"task"` 标准句柄（taskId 复用会话 ID，`pollIntervalMs:1000`、`ttlMs:3600000`）；`tasks/get` 轮询（working / completed 携带完整 CallToolResult / cancelled），`tasks/cancel` 协作式终止进程树，`tasks/update` 空确认；未声明扩展的客户端保持经典 `session_id` 会话结果。
- **OpenTelemetry trace 关联**：读取请求 `_meta.traceparent`（截断 200 字符）写入操作日志，工作台检查器元信息行显示 trace 短 ID；快照 activity 条目新增 `trace` 字段。
- **工具图标**：24 个工具全部附带 `icons`（16×16 内嵌 SVG data URI，按终端 / Git / 搜索 / 写入 / 工作区 / 读取六类配色），宿主 UI 可渲染。
- **诊断**：`get_workspace_status` 新增 `protocol_versions` 字段，明示双时代支持。
- **测试**：新增 `tests/modern.test.cjs`（discover、版本协商错误、resultType/缓存字段、icons、trace 记录、MRTR 全链路含篡改/改参/decline/重放、Tasks 生命周期、legacy 回退零回归）；全量 16 项测试通过。

## 1.7.0 升级说明

2026-09-17。桌面程序重绘 + 实时工作台内嵌 + ChatGPT 卡片下线；工具数 25 → 24。

- **桌面外壳重绘**：对齐工作台设计语言（品牌墨绿主色、白卡片、发丝边框、圆角按钮）。单行工具栏：启动/停止合并按状态切换、"在浏览器打开"、"更多"自绘菜单（刷新工作台 / 清空日志 / 复制原始日志 / 复制工作台链接）；页签改为 实时工作台 / 操作记录 / 原始日志 / 连接配置 四个；操作记录为自绘表格（级别徽章、等宽内容、行复制），原始日志为自绘控制台（时间戳弱化、级别芯片、尾随跟随、横向平移）。
- **实时工作台内嵌**：首个页签经 WebView2 嵌入工作台页面，连接就绪自动载入；WebView2 组件以 manifest 资源内置、按需解析，保持单 EXE 发行；缺运行时回退浏览器并提示。去掉与窗口标题重复的大头部；连接配置独立成页签，配置缺失或格式错误时启动/保存会跳转并标红对应输入框。
- **ChatGPT 卡片下线**：移除 `render_workspace` 工具与 `resources/list`、`resources/read`、`resources/templates/list` 通道（`workspace-card.html` 不再内置），initialize 不再声明 resources 能力，指令改为引导模型使用本地工作台；`read_workspace_activity` 保留为对话内文本快照工具。可视化进度统一在桌面程序查看。
- 样例快照路径中性化（`E:/workspace/local-workspace`、`C:/Users/dev`），README 三张截图重截。

## 1.6.0 独立实时工作台与对话分组

- EXE 内置只读回环 HTTP 页面，桌面可打开浏览器或复制地址；不依赖 ChatGPT iframe，不引入 Node 或浏览器运行时。
- 新增 register_conversation（共 25 工具），返回本地 thread_id 与线程直达链接。已知真实 ChatGPT UUID 可显式绑定；未知不伪造。后续调用携带 thread_id，未携带者单列未归属，命令续读继承归属。
- 独立页面每秒更新：对话侧栏、执行时间线、实际命令状态／耗时／输出、计划、搜索与状态筛选、暂停恢复、登记指引。文件和命令操作不经过网页，页面只观察。
- 同一项目的计划和命令按线程分别显示；桌面日志增加对话列及筛选。服务重启后进程内历史与线程失效，重新登记并打开新地址。
- 工作台仅监听 127.0.0.1，校验 Host/Origin/Fetch-Site，拒绝写入请求；历史及输出仍有明确容量上限。线程分组不是账号权限边界。

## 1.5.0 实时工作区面板

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

## 1.3.0 升级说明

2026-09-16。工具从原 10 个扩展到 22 个，卡片资源为 ui://local-workspace/review-v4.html。

- Codex 风格入口：open_workspace、update_plan、apply_patch，发现项目约定、显示真实计划、审阅多文件修改。
- exec_command 支持 cmd / cwd / yield_time_ms 与 shell；旧参数保持兼容。write_stdin 支持续读、输入、Ctrl-C 停止，不重复执行命令。
- 流式读取无需换行；累计输出供卡片，增量输出供模型，互不抢占。支持命令诊断、超时、错误和显式停止。
- 新增 Git 状态/差异、原生图片读取、目录创建、命令清单与连接诊断。
- 卡片自动续读、保持手动滚动位置、展示计划及部分补丁结果；宿主取消不冒充命令终止。
- 标准 MCP progress、输出 schema、中文调用状态与开始/返回日志。宿主是否展示卡片由宿主决定。
- DevSpace 官方可编辑源码位于 vendor/devspace，MIT 许可与 Git 历史保留，当前轻量 EXE 不依赖其 Node 服务。

沿用已有插件、Tunnel、凭据和访问范围。服务器新版 tools/list 返回 22 个；已有聊天可能仍缓存旧六个工具，需宿主刷新元数据后才可发现新增工具。网页版入口为“设置 → 插件 → 本地工作区 → 底部信息 → 刷新”，不是插件目录的应用详情页。2026-09-16 已在真实账号完成刷新，设置页显示全部 22 个工具和 review-v4 卡片模板。不要把工具缓存误判为本地只有读取权限。

官方依据：[MCP Apps](https://developers.openai.com/plugins/build/chatgpt-ui)、[工具契约](https://developers.openai.com/plugins/reference#tool-descriptor-parameters)、[Codex 项目约定](https://learn.chatgpt.com/docs/agent-configuration/agents-md)、[Codex app-server](https://learn.chatgpt.com/docs/app-server#api-overview)。
