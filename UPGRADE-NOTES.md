# 1.3.0 升级说明

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

## 1.4.0（历史构建记录）

- 默认解释器改为 Git Bash，接受 git_bash/bash；powershell/pwsh 保留显式选择。Git 安装发现支持 PATH 中的 Git 根路径与 Git for Windows 注册表，不回退至 WSL bash 或 PowerShell。
- workspace/status 返回 default_shell；命令返回实际 shell、shell_executable，列表和卡片显示解释器。卡片输入进度兼容 cmd 与旧 command。
- 初始化说明提醒宿主工具过滤不等于服务器只读；保留文本结果，不依赖图形卡片才能取得回执。桌面端工具发现与渲染没有真实验收，不能将网页版成功等同于桌面版成功。
- Apply-Update.ps1 在旧应用/隧道运行时拒绝更新；没有杀进程、自动重启或抢占 Tunnel。默认 shell 改变后需同步刷新工具元数据并使用新聊天。
- 当时保留运行中的 1.3.0；当前构建与切换状态以以下 1.4.1 记录为准。

## 1.4.1（已编译到原 dist，等待用户启动）

- 修复旧桥接与历史卡片恢复：兼容 toolResponseMetadata 内直接及嵌套 mcp_tool_result，保留图片内容与后续主题更新中的最新结果。
- 标准 UI 握手被拒绝或超时时，不再把已有成功结果覆盖为等待宿主；旧 callTool 桥接仍可刷新卡片。
- 模板升级为 review-v5，保留 review-v4 资源读取；增加标准与兼容 CSP 元数据，卡片无外部资源依赖。
- 保留 22 个工具、默认 Git Bash 与显式 PowerShell 调用能力。
- 16 项卡片测试及完整 MCP 回归通过；隔离浏览器验证仅旧桥接宿主下的恢复与刷新。真实桌面客户端尚待验收，不把模拟验证当作桌面端成功。
- dist 与 dist-next 均为 1.4.1，更新前确认没有运行实例。没有启动应用或进行线上刷新。

## 1.5.0 实时工作区面板

- 增加 render_workspace / read_workspace_activity，共 24 个工具。仅展示入口绑定 activity-v1 模板，数据工具不再反复生成卡片。
- 每 2 秒观察跨工具活动、当前操作、计划、命令输出；操作开始立即记录，结束或失败更新原条目。
- 协议读取与有序工具工作线程分离，活动查询独立响应；文件修改保持顺序，输出序列化避免多线程 JSON 交错。Git 关闭独立标准输入，避免继承协议输入管道导致阻塞。
- 暂停/恢复、页面隐藏暂停、连接失败提示、实例更换保护、按宿主能力显示画中画入口。面板轮询不消耗模型输出、不重发执行命令。
- UI_RESOURCE / UI_CONNECTED 日志及展示心跳用于区分资源被读取与面板实际连接；活动和命令显示有明确范围及容量。
- 原 review-v4/v5 资源仍能读取，默认 Git Bash 与 PowerShell 能力保留。实例重启会清空原进程的计划、命令和活动记录。


## 1.6.0 独立实时工作台与对话分组

- EXE 内置只读回环 HTTP 页面，桌面可打开浏览器或复制地址；不依赖 ChatGPT iframe，不引入 Node 或浏览器运行时。
- 新增 register_conversation（共 25 工具），返回本地 thread_id 与线程直达链接。已知真实 ChatGPT UUID 可显式绑定；未知不伪造。后续调用携带 thread_id，未携带者单列未归属，命令续读继承归属。
- 独立页面每秒更新：对话侧栏、执行时间线、实际命令状态／耗时／输出、计划、搜索与状态筛选、暂停恢复、登记指引。文件和命令操作不经过网页，页面只观察。
- 同一项目的计划和命令按线程分别显示；桌面日志增加对话列及筛选。服务重启后进程内历史与线程失效，重新登记并打开新地址。
- 工作台仅监听 127.0.0.1，校验 Host/Origin/Fetch-Site，拒绝写入请求；历史及输出仍有明确容量上限。线程分组不是账号权限边界。
