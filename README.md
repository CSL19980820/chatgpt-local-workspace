# ChatGPT 本地工作区插件（Local Workspace）

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-MIT-green" alt="MIT License"></a>
  <img src="https://img.shields.io/badge/platform-Windows%2010%2F11%20x64-blue" alt="Windows 10/11 x64">
  <img src="https://img.shields.io/badge/.NET%20Framework-4.8-orange" alt=".NET Framework 4.8">
  <img src="https://img.shields.io/badge/version-1.6.0-brightgreen" alt="v1.6.0">
</p>

<p align="center">
  简体中文 · <a href="README.en.md">English</a>
</p>

让 ChatGPT 通过 **OpenAI 官方 Tunnel** 直接操作你的本机：读写文件、精确编辑、执行命令、查看 Git 变更，并在浏览器里打开一个**实时工作台**，按对话查看每一次调用的时间线、命令输出和补丁 diff。单个 EXE、零安装、正式运行不需要 Node。

> 非官方社区项目，与 OpenAI 无隶属关系。

| 桌面程序：连接状态与操作记录 | 实时工作台：调用时间线与命令输出 |
| --- | --- |
| ![桌面程序](docs/images/desktop-app.png) | ![实时工作台](docs/images/dashboard-timeline.png) |

![补丁审阅视图](docs/images/dashboard-patch-review.png)

## 特性一览

- **25 个本地工具**：文件读写、精确编辑、多文件补丁、搜索、命令执行与增量输出、Git 审阅、执行计划。
- **独立浏览器实时工作台**：本地编译的 React + shadcn/ui 单页应用，每秒同步，按对话隔离时间线，检查器按调用类型渲染（diff、命令输出、读取正文、搜索命中）。
- **ChatGPT 内嵌任务面板**：`render_workspace` 打开的卡片每 2 秒只读刷新，不消耗模型输出。
- **Codex 风格工作流**：`open_workspace` 读取 AGENTS.md 约定 → `update_plan` 展示计划 → `apply_patch` 预验证后提交多文件补丁。
- **单文件分发**：.NET Framework 4.8 原生 EXE，只监听 127.0.0.1，不开放远程访问。

## 环境要求

| 项目 | 要求 |
| --- | --- |
| 操作系统 | Windows 10 / 11 x64 |
| 运行时 | 系统自带 .NET Framework 4.8（Win10 1903+ 默认已装） |
| ChatGPT 账号 | 支持创建自定义插件 / Tunnel 的套餐 |
| 命令执行 | 默认需要 Git for Windows（隐藏 Git Bash）；也可显式选择系统 PowerShell |
| Node.js | 仅构建界面和跑测试需要，正式运行不需要 |

## 快速开始

### 1. 下载并解压

从 [Releases](../../releases) 下载发行包并解压到任意目录。目录内 `LocalWorkspace.exe` 与官方 `tunnel-client.exe` 必须同级保留。

### 2. 创建 Tunnel

在 ChatGPT 的开发者设置中创建连接器 / 插件，获得 **Tunnel ID** 与 **API Key**（参考 [OpenAI 官方文档](https://platform.openai.com/docs/)）。

### 3. 启动连接

打开 `LocalWorkspace.exe`，填入 Tunnel ID 与 API Key，点击"启动连接"。配置保存在 `%LOCALAPPDATA%/LocalWorkspacePlugin/settings.json`——**不要提交或公开该文件**。

![桌面程序](docs/images/desktop-app.png)

### 4. 在 ChatGPT 中刷新插件

打开 ChatGPT 网页版"设置 → 插件 → 本地工作区"，滚动到底部"信息"，点击**刷新**（这是开发者连接设置页；应用详情页只有"重新连接"时请进入设置页操作）。成功后操作列表应包含 25 个工具。

### 5. 验证

新开一个聊天，直接说：

> 调用 get_workspace_status 确认连接

应返回 `version: 1.6.0`、`tool_count: 25`、实际程序路径和进程实例 ID。桌面日志会依次出现 `initialize`、`tools/list` 与工具回执——仅"隧道已连接"不能证明 ChatGPT 已刷新工具。

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

连接后在桌面程序点击"打开实时工作台"，浏览器页面每秒同步本机状态，不依赖 ChatGPT 卡片。建议每个新对话先说：

> 先调用 register_conversation 登记本对话，名称"重构支付模块"，目录 E:/work/pay

工具会返回本地线程 ID 和直达该线程的工作台链接，之后的调用在时间线中按对话隔离展示。

### ChatGPT 内嵌面板

> 打开实时面板（render_workspace），目录 E:/work/api

面板每 2 秒通过只读 `read_workspace_activity` 刷新当前操作、计划与命令输出；"观察中"表示当前没有工具在跑，不等于任务完成。

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

`show_changes` 只包含本进程文件工具记录的修改；`git_status` / `git_diff` 可检查 shell 或其他编辑器产生的 Git 变更（Git diff 不含未跟踪文件正文）。

## 实时工作台详解

![补丁审阅视图](docs/images/dashboard-patch-review.png)

- 左侧为全部对话、未归属与已登记线程；选中线程后，时间线、计划、命令输出分别过滤，多个对话用同一个项目也不会混淆。
- 时间线每行显示开始时间、工具、目标、状态与耗时；进行中的调用持续计时。每一行按调用类型着色，选中后右侧检查器按类型展示实际内容：写入/替换了哪些文件与改动行、读取正文（文本自动展开至 10 KB，二进制只标注不展开）、搜索命中、命令输出与退出码、计划进度。
- 默认"跟随最新"自动展开当前筛选范围内的最新调用；点击历史调用会固定详情。
- 支持搜索、状态筛选、暂停/恢复（仅暂停观察，不终止任务）和复制登记指令；连接失败保留旧结果并提示过期。
- 未传线程 ID 的调用进入"未归属"，不猜测归属；线程分组是可视化隔离，不是账号级授权隔离。
- 活动保留全局最近 100 条、最多 200 个线程，属于当前 MCP 进程；服务重启后需重新登记。

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
- 卡片是否呈现、折叠或显示进度最终取决于宿主；插件不能强制显示模型内部思考，也不能绕过 ChatGPT 工具授权。
- 宿主提供 `requestDisplayMode` 时内嵌面板显示"保持显示"（画中画）按钮。
- 旧版结果卡片保留文本阅读、分页、搜索、diff、会话与图片兼容；停止按钮只停止对应命令树。

## 从源码构建

```powershell
./build.ps1                                  # 构建到 dist-next
npm ci; npm run build:ui                     # 重建工作台页面
node tests/mcp.test.cjs                      # 协议与工具
node tests/patch.test.cjs                    # 补丁引擎
node tests/activity.test.cjs                 # 活动记录
node tests/dashboard.test.cjs                # 工作台数据
node --test tests/card.test.cjs              # 聊天卡片
node --test tests/dashboard-ui.test.cjs      # 真实浏览器 UI 回归
```

`tests/dashboard-ui.test.cjs` 用真实浏览器跑 `src/dashboard.html`：检查时间线、检查器内容、计划卡片与筛选，在 1920/1366/640 三种宽度确认无横向溢出，浅深色下确认每类调用有独立主色。浏览器由 `scripts/browser-launch.cjs` 按 `$env:WORKSPACE_TEST_BROWSER` → 自带 Chromium → Chrome → Edge 顺序挑选。

真实 Tunnel 健康测试需显式设置 `$env:WORKSPACE_TUNNEL_SMOKE='1'` 后运行 `node tests/tunnel.test.cjs`；使用已有配置，不要与同一 Tunnel 的另一实例同时运行。

主要源码：`src/Program.cs`（桌面与 Tunnel）、`src/WorkspaceServer.cs`（协议与工具）、`src/Presentation.cs`（资源与 diff）、`src/PatchEditor.cs`（补丁）、`src/WorkspaceContext.cs`（约定与计划）、`src/WorkspaceActivity.cs`（活动记录）、`src/ui/`（工作台）。DevSpace 官方源码保留于 `vendor/devspace/`（MIT），当前 EXE 不依赖其 Node 服务，详见 [docs/DEVSPACE-SOURCE.md](docs/DEVSPACE-SOURCE.md)。

验证记录见 [VERIFICATION.md](VERIFICATION.md)，升级内容见 [UPGRADE-NOTES.md](UPGRADE-NOTES.md)。

## 更新与替换

`Apply-Update.ps1` 发现运行中的应用或隧道会拒绝覆盖，不会自动终止进程。先关闭应用（会结束其命令树），再运行更新脚本并从原 dist 启动。更新工具元数据后需在 ChatGPT 设置页刷新并**新开聊天**验证。

## 常见问题

**ChatGPT 说它只能读、不能改？** 先让它调用 `get_workspace_status`，核对版本、`tool_count: 25` 与连接状态；再在设置页刷新元数据并新开聊天。不要把旧聊天缓存、旧插件或未运行的服务当成系统权限不足。

**"隧道已连接"但工具没反应？** 隧道连接 ≠ ChatGPT 已刷新工具。桌面日志必须出现 `initialize` 与 `tools/list` 才算打通。

**面板一直"观察中"？** 表示当前没有工具或模型调用在运行；模型内部思考不在插件可见范围，不代表任务失败。

**命令报找不到 Git Bash？** 安装 [Git for Windows](https://git-scm.com/download/win)，或让模型显式传 `shell: "powershell"`。

## 安全说明

- 工作台只监听 `127.0.0.1` 动态端口，不开放远程访问或执行接口。
- 默认以当前 Windows 用户权限访问本地磁盘；工作目录**不是**操作系统沙箱，请在可信账号下使用。
- `settings.json` 含 API Key，不要提交、截图或公开。
- 服务与活动记录属于当前进程，重启后需重新连接与登记。

## 官方依据

- [OpenAI MCP Apps UI 与桥接](https://developers.openai.com/plugins/build/chatgpt-ui)
- [工具元数据、输出结构与注解](https://developers.openai.com/plugins/reference#tool-descriptor-parameters)
- [MCP 进度通知](https://modelcontextprotocol.io/specification/2025-06-18/basic/utilities/progress)
- [DevSpace 官方源码](https://github.com/Waishnav/devspace)

## 许可与致谢

本项目为非官方开源项目，与 OpenAI 无隶属关系。整体以 MIT 许可证发布（见 [LICENSE](LICENSE)）。

- 架构与工具设计参考并部分衍生自 [Waishnav/devspace](https://github.com/Waishnav/devspace)（MIT），其源码保留于 `vendor/devspace/`，含原始许可证文件。
- 发行包中的 `tunnel-client.exe` 为 OpenAI 官方组件（Apache-2.0），随 GitHub Release 提供，不纳入本仓库版本管理。

## Star History

如果这个项目帮到了你，欢迎点个 Star 支持。
