# DevSpace 可编辑源码与能力核对

核对时间：2026-09-16。本文只把上游当作实现参考，不把下载源码等同于安装、接通或生产验收。

## 来源、版本、许可

- 官方项目：https://github.com/Waishnav/devspace
- 本地完整源码：`vendor/devspace/`，含独立 Git 元数据、源码、测试、文档和许可证，共 193 个 Git 跟踪文件。
- 固定上游 tag：`v1.1.0-beta.4`；commit：`8e4669ca1fdd4796c5aec3b2e9541242c023e594`。
- 本地编辑分支：`local-workspace-reference`。可直接修改源码，无需从压缩或打包产物逆向。
- 实时 npm registry 显示 `latest=1.0.8`、`beta=1.1.0-beta.4`。上述 beta tag 中的 `package.json` 仍写 `1.0.8`，所以精确版本以 tag 和 commit 为准，不以该字段推断。
- MIT License，Copyright (c) 2026 Waishnav。原始 `vendor/devspace/LICENSE` 已保留；复制实质代码或再分发时保留该版权和许可。
- 此次只获取公开源码，没有安装上游依赖、执行其安装/启动脚本、读取其旧账户配置或替换当前服务。源码是可编辑参考，当前运行程序仍由本项目 `src/` 和 `build.ps1` 生成。

## 上游实际暴露的工具

以下来自固定版本的工具注册代码，非 README 宣传或仅存在于内部的函数。

| 范围 | 工具 | 实现位置 |
| --- | --- | --- |
| 共有 | `open_workspace`、`read`、`show_changes` | `src/server.ts` |
| Codex 模式 | `apply_patch`、`exec_command`、`write_stdin` | `src/tool-surfaces/codex.ts` |
| Claude 模式 | `write`、`edit`、`bash` | `src/tool-surfaces/claude.ts` |
| 可选附件能力 | `download_artifact`，由配置和支持条件控制 | `src/artifact-tools.ts` |

不能将两种模式相加宣称同一连接拥有全部工具。上游将搜索、目录和 Git 操作交由 shell 组合，并没有为每个动作增加独立 MCP 工具。其内部子代理适配器、工作树管理和 CLI 能力也不等于全部都作为 MCP 工具公开。

## 对本插件最有用的实现

1. **过程会话**：`src/process-sessions.ts` 保存命令运行状态、退出码、增量输出、输出截断标记；长命令在等待窗口后返回 session ID，后续读写而非重复执行。应保留输出上限、完成态、取消及进程树清理。默认等待 10 秒，上限 12 秒；完成会话有保留期限。支持 PTY，但本插件并不需要为普通 PowerShell 命令引入完整 Node/PTY 依赖。
2. **稳定响应结构**：Codex 工具返回 `running`、`exit_code`、`wall_time_ms`、`output_truncated`、`session_id`；失败和仍在运行不能同样显示为“完成”。本插件可直接采用这种明确状态而保留自身实现。
3. **卡片连接和历史恢复**：`src/ui/workspace-app.tsx` 使用 MCP Apps 的 `App.connect()` 与 `ontoolresult`，接收结果后再渲染；同时监听 `openai:set_globals` 恢复 ChatGPT 侧工具结果。`src/ui/tool-result.ts` 优先识别 `structuredContent`，再取 `_meta` 中的 UI 数据，避免卡片只依赖一次性内存状态。
4. **显示与模型上下文分开**：上游把简要可推理结果放到文本/`structuredContent`，完整变更展示载荷放到 `_meta`。本插件应让不渲染卡片的宿主也能看到状态、错误及关键输出，不能让 UI 成为唯一证据入口。
5. **补丁与修改汇总**：`src/apply-patch.ts` 覆盖新增、修改、删除和移动；`src/review-checkpoints.ts` 保存审阅基准，`show_changes` 按轮汇总。先保证现有编辑工具的精确匹配和真实 diff，再按需求引入完整补丁语法。
6. **工作区与路径**：`src/workspaces.ts` / `src/roots.ts` 负责工作区、指令发现和文件路径边界。shell 仍以当前用户权限运行，不能仅凭工作目录宣称实现操作系统级沙箱。

## 对“过程中效果不可见”的证据边界

当前上游 Codex 模式的 `exec_command` / `write_stdin` 注册没有附卡片资源，工作区与最终 diff 才使用应用资源。直接换成 DevSpace 并不能证明每一步都会实时可见。

本插件需要分别检查：实际被隧道启动的可执行文件版本、`tools/list` 的真实工具/元数据、资源 MIME 和卡片协议握手、长命令是否及时返回可检查会话，以及模型可读结果是否包含足够状态。只验证本地 HTML 或源码中的卡片字符串，不能宣称 ChatGPT 的真实会话已显示成功。

## 后续直接修改入口

- 修改当前轻量插件：项目根 `src/`；以项目根构建脚本生成程序。
- 对照/修改 DevSpace 本身：`vendor/devspace/src/server.ts`、`vendor/devspace/src/tool-surfaces/`、`vendor/devspace/src/ui/`。
- 上游开发命令见其 README；执行安装或开发前先检查脚本和依赖。当前未安装依赖，也未验证其构建或运行。
- 获取上游更新前在该嵌套仓库查看 `git status`，保留本地编辑；不要用覆盖下载替代合并。

没有下载包、解压目录或临时测试副本需要保留；`vendor/devspace/` 是本次明确交付的源码。
