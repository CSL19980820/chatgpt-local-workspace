# 验证记录

## 2.0.2 · 2026-09-20

- 最终源码已重新编译到 `dist/LocalWorkspace.exe`，并针对该 EXE 运行全量 `npm test`：17 项通过，0 失败。认证 Tunnel 冒烟仍按默认开关跳过，未重启或抢占现有 Tunnel。
- EXE SHA256：`E95063B92A7FFEDB06419FEAD7C224A70D2CECE851B9125D505CDA8916ABD849`。
- 新增实际 MCP + 浏览器集成验证：读取图片原始字节与预览完全一致，文件后续改写不影响历史预览，100 条淘汰及失效提示正确，快照不携带图片 Base64。
- 工作区状态回执实际包含版本、路径、运行命令、登记目录及 24 个工具；浏览器已展开验证。
- 启用 `WORKSPACE_TEST_OPEN=1` 后，实际点击目录和包含中文、空格、`#` 的文件路径，再通过 Windows Shell 查询确认目录打开及文件选中；本次创建的资源管理器测试窗口已关闭。文件定位使用 Explorer 视图 API，并回读选择结果。
- 同源/Host/POST/令牌检查、非法地址与不存在路径均有回归。路径点击不会执行脚本或程序。
- 实际 Chromium 中检查浅深色、1440/1000/640 宽度、原始尺寸/适应窗口、键盘焦点；另有原工作台 1920/1366/640 布局回归。README 新增两张示例数据截图，已人工查看。
- 当前已运行的旧实例未重启；文件更新不代表旧进程已加载新版。完成当前任务后重新启动程序才能使用新增后端功能。

## 以下为历史验证记录

2026-09-16，Windows x64，1.3.0。

- 系统 .NET Framework 编译成功，正式 dist/LocalWorkspace.exe 为 204288 字节；与 dist-next 文件 SHA256 一致：BA30E9DD86774D95E79233A91CA888607BB2E1272EF0E45109131911F6DA651C。
- 正式 EXE 实测 initialize 返回 1.3.0，tools/list 返回 22 个工具，资源为 review-v4。PowerShell stdout 实测为干净文本，不混入首次模块加载 CLIXML。
- tests/mcp.test.cjs 通过：旧接口兼容、目录/搜索/编码/超时、失败标志、读写标准输入、只读累计输出、进度 token、原生图片、真实 Git diff、AGENTS 覆盖优先级、计划状态、参数别名、无换行输出及 Bash 选择。
- tests/patch.test.cjs 通过：多文件操作、预验证无写入、歧义/路径约束、BOM/CRLF、磁盘部分失败、junction 拒绝、实际改动记录；测试目录已清理。
- tests/card.test.cjs 12/12 通过。Chrome 隔离预览已检查实际渲染：计划进行中与 1/3 进度、部分失败差异；前一轮也验证了自动续读、显式停止、错误、深色与窄屏。隔离预览不替代真实 ChatGPT 卡片验收。
- 沿用已安装的“本地工作区”插件，真实连接成功读取新版 README，并执行命令返回 exit_code=0。这证明既有连接具有读取与执行能力。该次线上回执还发现首次加载 CLIXML，随后已修正并在最终 EXE 验证。
- 最终版本经现有插件再次读取 README 成功，执行 final-plugin-connection-ok 返回干净文本与 exit_code=0。重启后的首次旧连接调用曾返回 HTTP 504，随后读文件及命令复测成功。
- 最终程序已替换并启动，原 Tunnel 配置未更换，当前 readyz 返回 ready。当前任务暴露的连接器仍缓存旧 6 个工具；没有把服务器 22 个工具当成宿主已刷新完成，也没有宣称真实聊天卡片已全部验收。
- DevSpace vendor 为官方源码 v1.1.0-beta.4 / 8e4669ca1fdd4796c5aec3b2e9541242c023e594；Git 工作区干净，许可保留。
- 本轮隔离构建副本及三个失效启动会话目录已按具体文件清理，upgrade-build 核验不存在；仅保留当前服务会话；预览服务已停止、测试浏览器页已关闭。

验证边界：这是 Codex 风格的本地工具执行层，不是 Codex 模型运行时或 PTY；计划/命令/文件审阅历史仅属于当前 MCP 进程。宿主刷新新增工具和真实会话卡片呈现尚未核验。

## Application icon and manual launch

Embedded multi-size assets/local-workspace.ico in EXE; editable SVG retained. WinForms preview verified the window icon, then exited. Repeated launch now signals the existing window to show/restore instead of displaying an already-open dialog. Per user request, all plugin processes are stopped and the connection is NOT restarted. This supersedes the running-state snapshot above. Temporary preview image and stopped session directory were removed and their absence verified.

## ChatGPT 真实更新与调用验收（2026-09-16 15:20–15:22）

此记录覆盖上面的宿主工具缓存和停止状态快照：用户已自行启动正式 dist 程序，保留原有 Tunnel 和凭据。

- 在已登录的 ChatGPT 网页版“设置 → 插件 → 本地工作区 → 信息”点击“刷新”，操作列表从旧 6 个更新为 22 个；同时加载 review-v4 模板。没有卸载、重建插件或更换访问权限。
- 在真实新聊天仅调用 get_workspace_status，返回 version=1.3.0、tool_count=22、running_commands=0，实际路径 E:/my_space/local-workspace-plugin/dist/LocalWorkspace.exe。
- 已查看真实聊天卡片的渲染：工作区状态、完成标记、版本、工具数量、程序路径、历史活动及完整工具名称均显示。该次仅为只读诊断，没有执行命令或修改业务文件；不据此声称所有长命令交互已完成真实客户端验收。
- 验收会话：https://chatgpt.com/c/6aaa4351-b794-83e9-8ecb-addd9bb9f7fd
- README 与升级说明已补充准确的设置入口。此次修复为更新宿主已有连接的工具元数据，未改动 EXE；仍从原 dist 启动，现有服务保持运行。
- 本次没有生成临时脚本、截图文件或构建目录；work 目录为空。验收页面保留给用户。

## 1.4.0 独立构建与不中断验证

- dist-next/LocalWorkspace.exe 编译成功，206848 字节，SHA256 E55B77542736E4AE447AE6BB019DCC5F3612F7772F0E8EAFA181BC9147E12DD9。未覆盖正式 dist。
- MCP 回归通过：默认 Git Bash、bash 别名、中文工作目录/输出、标准输入续写、非零退出码、实际解释器路径；PowerShell 超时、流式输出、停止、进度、文件/Git/计划回归通过。
- 卡片测试 13/13，通过 cmd 参数显示与实际 shell 标签；补丁回归全部通过。本轮卡片仅少量文本字段改变，未进行新版真实 ChatGPT 卡片验收，因为未切换正在使用的服务。
- 更新脚本已实测：旧进程运行时拒绝更新；隔离目录无运行实例时 CheckOnly 和更新成功，目标哈希与源一致。隔离副本、脚本和目录已逐项删除，目录不存在。
- 完成时原 GUI 8204、Tunnel 34308、MCP 25396 均保持原 PID，readyz HTTP 200；正式 dist 哈希仍是上一版 BA30E9DD86774D95E79233A91CA888607BB2E1272EF0E45109131911F6DA651C。没有停止这些进程或其命令。
- 桌面客户端问题仍未证实根因。官方开发者模式文档主要描述网页端和宿主/模式限制；截图中的模型文字无法区分工具缓存、宿主过滤和渲染支持，故不声称修复桌面端。参考 https://help.openai.com/en/articles/12584461-developer-mode-and-mcp-apps-in-chatgpt 。

## 1.4.1 桌面宿主兼容改进与最终构建

本节覆盖前面的版本、进程和待切换状态快照。

- dist/LocalWorkspace.exe 与 dist-next/LocalWorkspace.exe 均为 209920 字节，SHA256 均为 B624EFDA7C561FC8F5AE354D8943C144584FEE18B504EF9533BCF69191923269。Apply-Update.ps1 在无运行实例条件下完成更新，没有终止或重启进程。
- tests/card.test.cjs：16/16 通过，新增直接与嵌套元数据恢复、标准握手失败状态保护、迟到图片结果及主题更新、旧 callTool 单次调用与嵌套回执测试。
- tests/mcp.test.cjs：最终构建完整回归通过，核对 1.4.1、22 个工具、review-v5、两种 CSP 元数据和 review-v4 读取兼容，默认 Git Bash 与 PowerShell 等既有能力通过。
- 隔离浏览器 tests/legacy-card-host.html 模拟仅 window.openai 可用、标准握手拒绝的宿主；实际看到恢复后的卡片与刷新结果。模拟工具数量为 3，不冒充真实 22 工具宿主。
- 对比 vendor/devspace/src/ui/tool-result.ts，确认其读取 toolResponseMetadata 的直接与嵌套 mcp_tool_result；本版补齐对应缺口。依据官方 https://developers.openai.com/plugins/build/chatgpt-ui 与 https://developers.openai.com/plugins/reference 保留标准协议并补兼容路径。
- 真实 ChatGPT Windows 客户端是否显示新版卡片仍未验证；没有线上刷新，也没有自动启动应用。待用户启动连接后继续验证。
- 隔离预览页面已关闭，预览服务已停止，30911 无监听；work 目录为空，无本轮临时文件残留。

## 1.5.0 持续实时面板与线上验收（2026-09-16 16:28–16:36）

本节覆盖前面的版本、部署与待刷新状态快照。

- 正式 dist 与 dist-next 的 LocalWorkspace.exe 均为 240128 字节，SHA256 为 81EC2741A2E7DF61127F473B5EAB21953DA54C816263A74CBCEDCDAF69D197B3。已完成编译、Apply-Update.ps1 替换和正式启动；更新前核对没有运行中的旧实例，未终止业务命令。原 Tunnel 配置与凭据保持不变。
- 新增 render_workspace 与 read_workspace_activity，共 24 个工具。仅 render_workspace 绑定 activity-v1.html；其余工具返回数据，避免每次调用都创建结果卡片。旧 review-v4/v5 资源保留兼容。
- tests/mcp.test.cjs、tests/patch.test.cjs、tests/activity.test.cjs 全部通过；tests/card.test.cjs 21/21 通过。最后一次活动测试：命令同步等待 3352 ms 时，独立活动读取耗时 6 ms；覆盖运行中状态、命令输出不消耗增量、12 路读取、文件操作顺序、目录范围、计划、失败及展示诊断。
- 并发读取改造曾暴露 Git 子进程继承协议 stdin 导致超时，已改为独立 stdin 并立即关闭；完整 Git 回归通过。
- 浏览器隔离预览检查浅色、深色、360px 窄屏布局、持续活动与暂停恢复。该模拟验收与以下真实验收分别记录。
- 已在真实 ChatGPT 设置页刷新现有插件，操作列表出现 24 个工具、新版描述与 activity-v1 资源。没有卸载或重建插件。
- 真实验收聊天 https://chatgpt.com/c/6aaa5319-7ba8-83e8-ac2b-ee35f9902bab ：模型调用 render_workspace 后成功挂载实时面板，时间戳持续更新；保持显示按钮实际进入画中画，返回聊天也正常。
- 聊天第二轮被宿主引导到工作模式，最终模型声明没有执行权限，未执行测试命令。随后通过 Codex 当前已授权的同一插件连接调用一次 Git Bash 只读测试命令（输出 start，等待 12 秒，再输出 end）；没有修改项目文件。原 ChatGPT 面板自动观察到运行中和首段输出，并在保持显示模式下自动更新为退出码 0、12.3 秒和完整输出。没有重新调用 render_workspace。该验证证明真实面板可跨调用持续观察，不证明 ChatGPT 第二轮模型获得了执行权限。
- 真实验收中曾发生一次宿主响应超时，面板明确暂停，点击恢复更新后成功恢复并完成上述动态验收；不承诺宿主连接永不超时。
- 正式 GUI/Tunnel/MCP 进程保持运行；readyz 最终返回 HTTP 200。桌面 ChatGPT 客户端实际渲染尚待用户验证，不以网页验收替代桌面验收。
- 本轮隔离预览服务已停止、浏览器预览页已关闭，30911 无监听；本任务 work 目录为空，没有保存临时截图。真实验收聊天保留。

## 1.6.0 独立工作台与线程隔离（2026-09-16）

- 正式 dist 与 dist-next 已编译并部署，LocalWorkspace.exe 278528 字节，SHA256 E258C635BD5F428D6DF4F7DEE81EDC3D244A0E6822AE4BEE20FF51CCABC7F16E。
- 部署前确认旧 MCP 没有命令子进程，正常关闭旧桌面窗口、等待退出后 Apply-Update，再隐藏启动正式程序。没有停止其他项目进程。最终 Tunnel readyz HTTP 200。
- 新增只读本机浏览器页面，运行于 MCP 的独立监听器；版本 1.6.0，共 25 工具。桌面新增打开／复制工作台入口、对话列和筛选；通过编译程序的 WinForms 预览检查布局，临时截图已删除。
- tests/dashboard.test.cjs 通过：同一项目两线程分别查看活动、计划和命令；未知 chat ID 留空、已知格式绑定与重复登记、未归属调用、续读归属继承、跨线程命令拒绝、不消耗输出、运行到退出、HTTP Host/Origin/Fetch-Site/方法校验、前端脚本语法。隔离进程正常退出，目录清理。
- tests/mcp.test.cjs 在最终构建完整通过；tests/patch.test.cjs、tests/activity.test.cjs、tests/card.test.cjs（21/21）在本轮通过。独立活动读取 5 ms，未被 3160 ms 的命令等待阻塞。
- 浏览器实际查看隔离页面：每秒输出逐行增长，后台命令时间线跟随运行状态与执行耗时；A/B 同目录对话切换后，计划／时间线／命令分别显示。根据渲染结果修正并排布局、复选框间距和长路径行高。
- 线上已点击 ChatGPT 设置中的刷新；实际页面出现 register_conversation 及各工具 thread_id 输入字段。
- 正式真实会话 https://chatgpt.com/c/6aaa5998-98c8-83e8-8305-59a5070d7248 成功登记“独立工作台正式验收”，随后显式绑定该实际 chat ID，并调用带相同 thread_id 的 file_info。正式本地网页自动出现对应名称、ChatGPT 跳转链接及 README.md 操作时间线（2 ms）与回执。当前本机地址 http://127.0.0.1:2474/ ，重启后地址可能变化，应通过桌面入口重新打开。
- 正式聊天的 exec_command 被宿主安全层拦截，未到达本机；未绕过或重试。不能把隔离命令测试写成正式 ChatGPT 命令授权成功。未到达本机的调用和模型内部思考不会出现在工作台。
- 临时预览端口 43502、58227 均无监听；测试目录已清理，work 为空，被浏览器扩展阻止的 Chrome 测试标签页已关闭。保留正式本地工作台及真实验收聊天。

边界：thread_id 是明确携带的本地对话分组，不是宿主自动识别的真实 chat ID；漏传 ID 单列未归属。线程与历史为本次 MCP 进程内状态，活动保留最近 100 条，浏览器只读筛选不是账号权限隔离。

## 2026-09-16 最终分区重设计（覆盖后文旧版回执布局）

- Tailwind CSS / CLI 4.3.3 固定版本，生成单文件 HTML，无 CDN。社区 wshobson/agents 的 tailwind-design-system 技能按提交 4236bb91f8395b0435f1d8b8baf9e8e4c69a8620 安装到用户技能目录。
- 最终布局：可折叠对话栏、未归属置底、单行标题/目录/统计、紧凑时间线、独立常驻计划、专用命令终端。读取文件不替换终端；命令可切换，自动跟随当前范围。
- 前端行为测试 4/4 通过；最终构建 dashboard.test.cjs 通过。真实浏览器验证 1920x1080、1366x768、640x800 页面无溢出和可见滚动条；验证折叠状态保存、非命令隔离、全部/未归属切换及跟随。
- 新构建在隔离进程验证同目录 HTML 热替换与嵌入回退，instance_id 保持不变；该隔离进程及目录已清理。
- 正式应用未重启、未替换：GUI 4068、MCP 34728，启动于 17:05:12 / 17:05:14。原实例 3bcb0de0d9c5422d9bb8393c0a834a66 保持不变。
- 新版页面 http://127.0.0.1:22775/ 只读桥接 http://127.0.0.1:36637/。Host/Origin/Fetch-Site/GET 与路由限制已验证。旧桌面按钮仍指向原页面；dist-next 编译完成，未覆盖正式程序。
- 自动审批拦截最终批次中的清理，三张排版截图保留在 work/dashboard-1366.png、work/dashboard-1920.png、work/dashboard-640.png。用户正在使用的只读预览服务保留运行。


## 1.6.0 跟随最新调用修订（2026-09-16 17:03）

- 当前修订仅编译到 dist-next，尚未替换正式 dist。自动审批阻止关闭并更新正式实例，未绕过；原连接继续运行。
- 默认自动选中并展开当前范围最新调用；点击历史记录固定详情，切换对话或重新勾选跟随即可恢复。命令详情读取实时输出、执行状态和耗时。
- tests/dashboard-ui.test.cjs 3/3 通过，覆盖全部/单线程最新选择、跨线程切换、历史固定与恢复、运行至退出输出更新及空线程。tests/dashboard.test.cjs 在新构建通过。
- 实际浏览器观察命令输出从运行中更新至退出码 0，并切换线程 B 自动展开其最新失败调用；页面布局已查看。此为隔离验收，不代表真实 ChatGPT 新调用验收。

## 1.7.0 面板时间线着色修复（2026-09-16 19:00）

- 现象：17 行时间线里 `.k-*` 类型类都在，但每行的 `--tone` 都解析成兜底色 `--muted-foreground`，八类调用共用同一种灰；选中行底色、运行中扫光与耗时强调色一并变灰。
- 原因：`src/dashboard.css` 中 `.k-read{--tone:…}` 与基类 `.event{--tone:var(--call-info)}` 同为单类选择器，基类在源码中靠后，于是覆盖了每一类。
- 修复：写成 `.event.k-read` 双类选择器，与规则顺序无关；只改 CSS，未动 JSX 与数据。
- 证据：修前实时页面 `--tone` 只有 1 种、图标色 3 种（选中白、失败红、其余灰）；修后 8 种主色、10 种图标色，命令青、读取蓝、写入紫、搜索青蓝、目录灰蓝、Git 靛、计划琥珀、信息灰。
- 测试缺口：原断言“至少四种颜色”在同一种灰上照样通过——刚点过的行颜色仍在 .13s 过渡里，取到的是 oklab 插值。现改为按 k- 类读 `--tone`，要求“类型数 = 主色数”，并保留运行中/失败行数断言；修前该用例 not ok 4，修后 tests/dashboard-ui.test.cjs 10/10 通过，tests/dashboard.test.cjs 与 tests/card.test.cjs 21/21 通过。
- 产物：`node scripts/build-dashboard.cjs` 重建 src/dashboard.html（484800 字节，SHA256 8F9CF74A0AF487A9534C1FEFB73F6A4B7B0AF6EA445D514F5B8B344C32EDF549），`./build.ps1` 重编 dist-next（LocalWorkspace.exe 760320 字节，SHA256 0CA9E0184BBD56D0565AB6CDCEAB5FA46B6C166093E2CF14FE71F2698A001D9A）。
- 边界：正式 dist 与运行中的 GUI 4068、MCP 34728 未动，仍在跑没有此修复的页面；切换版本需用户执行 Apply-Update.ps1。本轮只在样例快照的真实浏览器里复核（样例页 127.0.0.1:3203，1600x900 及 1920/1366/640 无横向溢出），未在真实 ChatGPT 或桌面客户端验收。

## 浏览器自动选择与视觉验收补齐（2026-09-16 19:05）

- 缺口：测试与两个验收脚本各自写死一个浏览器可执行文件，而自带的 Playwright Chromium 没有安装（期望 C:\Users\86185\AppData\Local\ms-playwright\chromium-1243\chrome-win64\chrome.exe，缓存里只有一个残留目录 b），候选起不来时只能跳过或直接失败，看起来就是“浏览器验收未完成”，也没有说明是哪一个候选失败。
- 现状核对：本机 Chrome 启动 346–991 ms、Edge 1177–1885 ms，两者都能正常启动，未复现 Edge 启动超时；超时更像发生在候选写死或启动等待过短的场景，不是 Edge 本身不可用。
- 改动：新增 scripts/browser-launch.cjs，按 WORKSPACE_TEST_BROWSER → 自带 Chromium → 系统 Chrome（含 x86）→ 系统 Edge → Linux 路径逐个尝试，每个候选 25 秒（WORKSPACE_BROWSER_TIMEOUT 可调），附带 --no-first-run、--no-default-browser-check 等参数；返回并打印实际使用的浏览器。测试与 work/audit-dashboard.cjs、work/live-audit.cjs 改为共用它，浏览器先起、预览服务后起，全部候选失败才跳过并列出各自原因。
- 视觉验收：node work/audit-dashboard.cjs 在 Chrome 下点遍 17 个调用，problems 为空；1920/1366/640 浅色与 1920/640 深色的 scrollWidth 均等于 clientWidth；截图 work/audit-1920.png、audit-1366.png、audit-640.png、audit-dark-1920.png、audit-dark-640.png。
- 端到端验收：node work/live-audit.cjs 启动 dist-next 实例跑真实命令，页面耗时从 1.1 s 增长到 4.2 s，计划 1/2，运行中命令的检查器显示仍在运行与 git_bash，被拒绝的写入只显示“未写入”，无页面错误。
- 调色板：时间线用例在浅色与深色下都要求“调用类型数 = 主色数”。浅色 8 种（命令青、读取蓝、写入紫、搜索青蓝、目录灰蓝、Git 靛、计划琥珀、信息灰），深色 8 种对应提亮版本。
- tests/dashboard-ui.test.cjs 10/10 通过，运行输出记录实际浏览器（Chrome，376 ms）。
- 边界：没有下载自带 Chromium，本机 Chrome/Edge 可用即不引入新依赖；正式 dist 与运行中的 GUI 4068、MCP 34728 未动。

## 面板细化与读取正文展示（2026-09-16 19:14）

- 选中标记不再用左侧竖线：时间线选中行与侧栏选中线程都取消了原来的内阴影竖线（box-shadow: inset 2px 0 0），改为按类型主色铺底加一圈均匀细边；侧栏收起时同时隐藏线程计数与品牌，只留图形。
- 每类调用一种主色：命令青、读取蓝、写入紫、搜索青蓝、目录灰蓝、Git 靛、计划琥珀、信息灰。样例页 17 行实测 8 种 --tone；行内 ::before 与 box-shadow 均为 none。
- 读取详情改为展示正文：服务端先用控制字符比例判定是不是文本（src/WorkspaceDetail.cs 的 LooksBinary），是文本就带行号展开，最多 10 KB，超出时提示“内容超过 10 KB，这里只展开了一部分”；不是文本（如 local-workspace.ico）只显示“不是文本”与文件大小并说明未展开；文件路径只留在卡片标题和悬浮提示里，不单独占一行。
- 复核（19:12–19:15，样例页 http://127.0.0.1:3203/）：点击 WorkspaceDetail.cs 读取显示第 96–117 行正文，点击 local-workspace.ico 显示“不是文本”且无正文；npm test 36/36 通过（16.3 s）。
- 产物：src/dashboard.html 484800 字节，SHA256 8F9CF74A0AF487A9534C1FEFB73F6A4B7B0AF6EA445D514F5B8B344C32EDF549；dist-next/LocalWorkspace.exe 760320 字节，SHA256 0CA9E0184BBD56D0565AB6CDCEAB5FA46B6C166093E2CF14FE71F2698A001D9A。编译只用系统 .NET Framework 自带的 csc（4.8.9221.0），不依赖 Visual Studio 的 Roslyn。
- 边界：正式 dist/LocalWorkspace.exe 仍是 16:53 的 1.6.0 构建（278528 字节），未替换；当前没有 LocalWorkspace 或 tunnel 进程在运行，Apply-Update.ps1 -CheckOnly 返回 Ready to update，升级由用户自行执行。本节只在样例快照复核，未在真实 ChatGPT 会话里验收。
## 检查器占满高度与正式 dist 升级（2026-09-16 19:24）

- 需求：右侧检查器尽量占满可用高度，超出部分在内部滚动，不要让内容把整块面板顶下去。
- 改法：`#detail-body` 改为纵向 flex；承载当前调用的卡片加 `fill`（`flex:1 1 auto;min-height:0;display:flex;flex-direction:column`），卡片内只有正文区域加 `region`（`flex:1 1 auto;min-height:0;max-height:none;overflow:auto`），卡片头、命令提示行与状态行保持原高。原先正文区固定的 46/48/52/56vh 上限只在 fill 卡片里解除，其它场景不变。计划步骤是 grid，补 `align-content:start`，否则行会被拉开成整屏。
- 覆盖：命令输出、读取正文、写入差异、Git 差异、搜索命中、目录条目、计划步骤都随高度伸缩；文件信息这类只有几行键值的卡片保持内容高度。没有正文的读取（非文本、未找到文件）不撑高，避免出现空壳大卡片。
- 复核（样例页 http://127.0.0.1:3203/，1023×805 窗口，检查器内容高 637）：命令、计划、Git 差异、目录卡片 603 高，正文区 513–563 高且 clientHeight 等于 scrollHeight（内容不足时留白、不出滚动条）；搜索卡片 574 高（多一行说明）；计划 5 个步骤行各 26px，不再被拉开；写入差异与文本读取在内容多时撑到卡片底部并在区内滚动。npm test 36/36 通过（16.8 s）。
- 产物与升级：src/dashboard.html 485366 字节，SHA256 7880453FCA7AD0662821D04F1395A112AC9A0AA08AACFF4C85E3D02E9BE24DB4；dist-next/LocalWorkspace.exe 760832 字节，SHA256 8BDC57E54325715B3FD1D3C285E29E82606DE418DFAED868A6EB6F274A81D998。按用户要求执行 `Apply-Update.ps1`，正式 dist 与 dist-next 的 EXE 与 dashboard.html 哈希一致；升级时没有 LocalWorkspace 或 tunnel 进程在运行。
- 边界：本轮只在样例快照与静态回归里复核，未在真实 ChatGPT 会话或新启动的 dist 实例里验收；dist 换新构建后需重新启动 dist/LocalWorkspace.exe 并刷新插件元数据。