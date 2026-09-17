# 本地工作区 ChatGPT 插件 · 1.6.0

通过官方 OpenAI Tunnel 操作本机文件、执行命令，通过独立浏览器工作台按对话查看实时状态、执行时间线和命令输出，并保留聊天卡片兼容。Windows 10/11 x64，使用系统 .NET Framework 4.8；不解包运行时。默认命令执行需要 Git for Windows；也可显式选择系统 PowerShell。正式运行无需 Node。

## 启动与确认连接

1. 打开 `dist/LocalWorkspace.exe`，使用已有 Tunnel ID/API Key，点击“启动连接”。同目录的官方 `tunnel-client.exe` 必须保留。
2. 在 ChatGPT 网页版打开“设置 → 插件 → 本地工作区”，滚动到底部“信息”，点击“刷新”。这是开发者连接设置页；插件目录的应用详情页只有“重新连接”时，请进入设置页操作。更新后操作列表应包含 25 个工具及 activity-v1 卡片模板。
3. 调用 `get_workspace_status`，应返回 `version: 1.6.0`、`tool_count: 25`、实际程序路径和本次进程实例 ID。
4. 桌面日志分别显示 `initialize`、`tools/list` 和工具调用回执。仅“隧道已连接”不能证明 ChatGPT 已刷新工具。

更新工具元数据后新开聊天验证。如果 ChatGPT 声称只能读，先检查实际返回的工具列表、版本与连接，不要把旧聊天缓存、旧插件或未运行服务直接当成系统权限不足。

配置沿用 `%LOCALAPPDATA%/LocalWorkspacePlugin/settings.json`，不要提交或公开其中的 Key。默认以当前 Windows 用户权限访问本地磁盘；工作目录不是操作系统沙箱。换电脑复制 dist 并重新配置即可。

## 25 个工具

| 用途 | 工具 |
| --- | --- |
| 对话登记及工作台直达链接 | `register_conversation` |
| 持续任务面板与独立活动查询 | `render_workspace`、`read_workspace_activity` |
| 工作区约定、计划和多文件补丁 | `open_workspace`、`update_plan`、`apply_patch` |
| 实际连接、版本与活动诊断 | `get_workspace_status` |
| 目录、文件属性和搜索 | `list_directory`、`file_info`、`search_files`、`search_text` |
| 读取文本和图片 | `read_file`、`read_image` |
| 创建目录、写入与精确编辑 | `create_directory`、`write_file`、`edit_file` |
| 执行命令、发送标准输入 | `exec_command`、`write_stdin` |
| 命令列表、增量输出、只读快照、停止 | `list_commands`、`poll_command`、`read_command`、`stop_command` |
| 修改审阅 | `show_changes`、`git_status`、`git_diff` |

`show_changes` 只包含本进程文件工具记录的修改；`git_status/git_diff` 可检查 shell 或其他编辑器产生的 Git 变更。Git diff 不含未跟踪文件正文。工具不会自行提交或推送。

## 独立浏览器实时工作台（推荐）

最新界面是一个本地编译的 React + shadcn/ui + Tailwind CSS 4 单页应用：对话栏可收起，未归属入口置底；顶部一行显示标题、目录和统计；执行计划折叠在时间线上方；右侧是按调用类型渲染的检查器，取代了过去的常驻终端。时间线每一行给出开始时间、状态与耗时，进行中的调用持续计时。

静态页面源码在 `src/ui/`（React 组件、`lib/` 里的显示逻辑与中文标签），样式源文件是 `src/dashboard.css`，模板是 `src/dashboard.template.html`。执行 `npm ci`、`npm run build:ui`：脚本先用 Tailwind CLI 编译 `src/dashboard.css`，再用 esbuild 打包 `src/ui/main.jsx`，两者内联进单个 `src/dashboard.html`——没有 CDN、没有模块加载器、运行时不需要 Node。新构建优先读取程序同目录的 `dashboard.html`，缺失时回退嵌入页面；支持刷新页面加载静态改动。旧运行实例仍只有嵌入页面，不能靠修改文件热更新：可运行 `node scripts/dashboard-preview.cjs http://127.0.0.1:<原工作台端口>/`，使用打印的新地址只读观察原实例；加 `--sample` 则用内置样例快照预览界面，无需任何实例。此入口不重启应用、不执行工具；原实例退出后需重新连接。

启动连接后，桌面程序点击“打开实时工作台”。它打开本机浏览器页面，直接从本机每秒同步，不依赖 ChatGPT 卡片显示。只监听 127.0.0.1 的动态端口，不开放远程访问或执行接口；网页地址也会通过 register_conversation 和 get_workspace_status 返回。

- 左侧为全部对话、未归属与已登记线程；选中线程后，时间线、计划、命令输出分别过滤，即使多个对话使用同一个项目也不会混淆。
- 时间线显示开始时间、工具、目标、运行状态与耗时；进行中的调用每秒累加耗时，后台命令的状态跟随真实进程更新。每一行按调用类型着色（命令、读取、写入、搜索、目录、Git、计划、信息各一种主色），选中行用该类型主色铺底并配一圈同色细边，不再用左侧竖线标记选中。选中一次调用后，右侧检查器按类型展示它实际做了什么：写入或替换了哪些文件、写入方式与改动行，读取文件的正文（先判定是不是文本：是文本就展开，最多 10 KB，超出部分明确说明只展开了一部分；不是文本则只说明不展开，路径留在卡片标题与悬浮提示里，不单独占行），搜索命中位置，执行了什么命令以及输出和退出码，计划进度或工作区信息。
- 右侧检查器占满可用高度：写入、读取、命令、搜索、目录、Git 差异与计划的卡片撑到面板底部，卡片里的正文区域随高度伸缩，内容超出时只滚动该区域，不会把整块内容顶下去；步骤、行表这类列表保持行高不变，多出来的空间留在卡片里。
- 默认跟随当前筛选范围内的最新调用并自动展开详情：选“全部对话”跟随全局，选某个对话只跟随该线程；状态、耗时与检查器内容每秒更新。点击历史调用会固定详情，勾回“跟随最新”或切换对话即可恢复。
- 支持搜索、状态筛选、跟随最新、暂停／恢复和复制登记指令。暂停页面仅暂停观察，不终止任务；连接失败保留旧结果并明确提示过期。
- 每个新对话先调用 register_conversation，提供 title、path；只在已知真实 ChatGPT /c/ UUID 时传 chat_id。工具返回本地 thread_id 和直达该线程的 dashboard_url。模型应先告知对话名称及链接，再执行任务，并在每次工具调用传 thread_id。
- 网页“登记对话指引”可填写名称、目录及可选 ChatGPT 链接，生成要发给 GPT 的指令。链接只是显式绑定，不是宿主自动提供或验证的身份。未知 chat ID 时使用本地线程 ID，不能伪造。
- 未传 ID 的调用明确进入未归属，不使用“最近活动对话”猜测归属。对已有命令的后续调用沿用其归属；显式传入不同线程会拒绝。线程分组是可视化隔离，不是不同账号的授权隔离。
- 桌面操作记录新增对话列与筛选。当前所有记录、最多 200 个线程及计划均属于本次 MCP 进程；服务重启后需重新登记并从桌面重新打开工作台。活动保留全局最近 100 条，命令显示最多 8 个及尾部输出；不会伪装成完整永久审计。

## ChatGPT 内嵌实时任务面板

先 `open_workspace` 读取项目约定，然后在执行多步任务前调用 `render_workspace({path: "E:/your-workspace"})` 打开面板。后续数据工具只返回数据，不再每次生成新卡片。模型也可能延后展示；可以先单独要求“打开实时面板”，看到面板后再发任务。

- 面板每 2 秒通过只读 `read_workspace_activity` 查询当前操作、最近活动、计划及命令输出。即使没有正在运行的命令，也继续观察下一步操作。
- “观察中”表示当前没有工具或命令运行，不等于模型完成任务。模型内部思考不在插件可见范围。
- 暂停更新仅停止面板轮询，不停止任务。页面隐藏时暂停，回到页面后同步；连接失败会暂停并提供恢复入口。
- 服务实例改变后明确提示旧记录失效，不把新实例当成原任务继续；手动恢复后观察新实例。
- 宿主提供 `requestDisplayMode` 时显示“保持显示”按钮，按宿主回执切换画中画。宿主拒绝或保留内嵌模式时显示原因。
- 展示范围是当前 MCP 进程、所选目录及子目录；保留最近 100 条工具活动，面板显示最近 25 条。最多展示 8 个命令，每个命令保留尾部 8,000 字符；省略与截断会标明。
- 文件工具仍按到达顺序执行。活动查询使用独立通道，不排在长调用后面，也不消耗模型的增量命令输出。

在桌面程序日志中查看 `UI_RESOURCE` 与 `UI_CONNECTED`：前者只是模板被读取，后者说明面板已经成功发起活动查询。`get_workspace_status` 的 ui 字段和面板“连接与展示诊断”提供最近心跳；没有心跳可能是未挂载、页面隐藏或用户暂停，不能只凭隧道正常宣称界面已显示。

## 过程可见性

- 工具描述提供中文调用状态；宿主传入 progressToken 时发送开始、执行中和结束通知。未知总量不显示虚构百分比。
- 桌面立即记录调用开始；同步等待中每 2 秒记录仍在执行，返回时区分正常结果与失败。
- 长命令及时返回会话 ID。新实时面板通过活动快照持续显示输出、耗时、退出码、超时及停止状态，不消耗 poll_command 的增量输出。
- 刷新失败后暂停并显示原因；宿主取消卡片不等于命令已经终止。停止指定命令需调用 stop_command。
- 旧结果卡片保留文本阅读、分页、搜索、编辑 diff、诊断、会话及图片兼容；旧命令卡片仍通过 read_command 更新，停止按钮只停止对应命令树。新版数据工具返回结构化结果，由统一实时面板展示活动。
- 是否呈现卡片、折叠调用或显示进度最终取决于宿主；插件不能强制显示模型内部思考，也不能绕过 ChatGPT 工具授权。

命令默认运行于隐藏 Git Bash（`shell: "git_bash"`，也接受旧名称 `bash`）。需要 PowerShell 语法时必须明确传 `shell: "powershell"` 或 `shell: "pwsh"`；找不到 Git Bash 时明确报错，不会偷偷切换解释器。Git Bash 从 Git 安装目录或 Git for Windows 注册表发现，不会误用 Windows 的 WSL bash。返回结果包含实际 shell 和可执行路径；支持 cmd / cwd / yield_time_ms 参数，旧 command / yield_ms 保持兼容。write_stdin 省略 chars 即续读，发送 Ctrl-C 可终止命令树。即使输出没有换行也能持续读取。标准输入是管道而非 PTY。长任务继续读取同一会话，不要重复启动。默认 300 秒超时，可设 1–3600 秒；输出快照保留最近 128,000 字符并标记截断。输入接收阻塞时终止该命令树并报错，避免 MCP 服务失去响应。PowerShell 错误会停止本次命令。

## Codex 风格工作流

先用 open_workspace 读取全局及当前 Git 根到工作目录的 AGENTS.override.md / AGENTS.md，同层 override 优先，最多 32768 字符并明确截断；无 Git 根只读当前目录。它列出本地技能入口，不执行文档内指令，不解析自定义 TOML fallback。

复杂工作用 update_plan 显示实际步骤，每次最多一个进行中；计划和命令会话属于当前 MCP 进程，重启后不保留。apply_patch 支持 Begin/End Patch 的新增、更新、移动、删除，文件路径相对 cwd，所有文件预验证后再提交。磁盘提交不是跨文件事务；部分失败准确列出已发生的改动，不假装回滚成功。补丁保留 BOM、CRLF 与原末尾换行状态，拒绝歧义匹配、越界及重解析点。

这些工具构成本地执行层；不会自行启动模型循环，也不提供伪终端。任务结果仍以实际工具回执和验证为准。

## 源码与开发

- 当前插件：src/Program.cs（桌面及 Tunnel）、src/WorkspaceServer.cs（协议及工具）、src/Presentation.cs（资源及 diff）、src/workspace-card.html（聊天卡片）、src/FileSearch.cs（搜索）、src/WorkspaceContext.cs（约定与计划）、src/PatchEditor.cs（补丁）、src/WorkspaceActivity.cs（并发安全活动与展示诊断）。
- DevSpace 官方可编辑源码：vendor/devspace/，保留 MIT 许可、独立 Git 历史和可编辑分支。来源、版本和源码导航见 [DEVSPACE-SOURCE.md](docs/DEVSPACE-SOURCE.md)。当前 EXE 不依赖其 Node 服务。
- `./build.ps1` 默认构建到 dist-next；验证通过并停止本插件后，替换 dist/LocalWorkspace.exe。

```powershell
./build.ps1
node tests/mcp.test.cjs
node tests/patch.test.cjs
node tests/activity.test.cjs
node tests/dashboard.test.cjs
node --test tests/card.test.cjs
node --test tests/dashboard-ui.test.cjs
node tests/preview-server.cjs
```

`tests/dashboard-ui.test.cjs` 用真实浏览器跑 `src/dashboard.html`：它启动样例预览服务，检查开始时间、进行中计时、每类调用的检查器内容、计划卡片与筛选，按 1920/1366/640 三种宽度确认没有横向溢出和脚本错误，并在浅色与深色下确认每类调用各有自己的主色。浏览器由 `scripts/browser-launch.cjs` 按 `$env:WORKSPACE_TEST_BROWSER` → 自带 Chromium → 系统 Chrome → 系统 Edge 的顺序挑选，每个候选给 25 秒（可用 `$env:WORKSPACE_BROWSER_TIMEOUT` 调整）并附带跳过首次运行的启动参数；运行时打印实际使用的浏览器，全部候选都起不来才跳过并列出各自的失败原因。此前的硬编码路径在只装了 Edge 或只有自带 Chromium 的机器上会直接失败。

`work/audit-dashboard.cjs` 是同一条路径的视觉验收：驱动样例页点遍每个调用并输出检查器内容与几何数据，在 1920/1366/640 浅色和 1920/640 深色下截图到 `work/audit-*.png`、`work/audit-dark-*.png`；`work/live-audit.cjs` 则启动 dist-next 实例、跑一条真实命令，检查页面显示的耗时是否随之增长。两者都不是测试套件的一部分。

Node 仅用于开发测试。实际 Tunnel 健康测试需显式设置 `$env:WORKSPACE_TUNNEL_SMOKE='1'`，再运行 `node tests/tunnel.test.cjs`；使用已有配置，结束后清理测试连接。不要与同一 Tunnel 的另一实例同时运行。

验证范围见 [VERIFICATION.md](VERIFICATION.md)，升级内容见 [UPGRADE-NOTES.md](UPGRADE-NOTES.md)。

## 官方依据

- [OpenAI MCP Apps UI 与桥接](https://developers.openai.com/plugins/build/chatgpt-ui)
- [工具元数据、输出结构与注解](https://developers.openai.com/plugins/reference#tool-descriptor-parameters)
- [MCP 进度通知](https://modelcontextprotocol.io/specification/2025-06-18/basic/utilities/progress)
- [DevSpace 官方源码](https://github.com/Waishnav/devspace)

官方接口提供工具、资源、进度、结构化结果和 UI 桥接，不会自动将 Codex/ChatGPT 内部所有工具开放给第三方插件。

## 安全切换与客户端边界

当前正式 dist 已升级为与 dist-next 相同的构建（1.6.0，含新版面板）。`Apply-Update.ps1` 发现运行中的应用或隧道会拒绝覆盖，不会自动终止进程。后续更新时先关闭应用，再运行更新脚本并从原 dist 启动；关闭应用会结束其命令树。

原 review-v4/review-v5 资源保留读取兼容，新面板 URI 为 activity-v1。历史工具结果卡片和新实时面板是不同的展示入口，更新后应刷新插件元数据并新开聊天。

本地回归与浏览器模拟不替代真实客户端验收。具体构建、线上刷新和真实卡片验证记录见 VERIFICATION.md；桌面端实际效果由用户继续验证。

## 许可与致谢

本项目为非官方开源项目，与 OpenAI 无隶属关系。整体以 MIT 许可证发布（见 [LICENSE](LICENSE)）。

- 架构与工具设计参考并部分衍生自 [Waishnav/devspace](https://github.com/Waishnav/devspace)（MIT），其源码保留于 `vendor/devspace/`，含原始许可证文件。
- 发行包中的 `tunnel-client.exe` 为 OpenAI 官方组件（Apache-2.0），随 GitHub Release 提供，不纳入本仓库版本管理。
