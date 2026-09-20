# 工具参数手册（25 个）

本页是 25 个本地工具的输入参数参考，与 `src/WorkspaceServer.cs` 中 `BuildTools()` 注册的 schema 一致。工具由模型按需自动调用，你用自然语言下达任务即可；本页供你核对参数、写自动化或排查调用失败时使用。

## 通用约定

- **所有工具都接受可选的 `thread_id`**（string）：本地对话 ID。宿主传入请求 `_meta["openai/session"]` 时，服务端按组织、用户、会话组合自动关联；不提供该信号时调用 `register_conversation`，并在后续调用中传入其 `thread_id`。两者皆无时进入“未归属”，不按路径或最近调用猜测。宿主元数据用于关联，不用于认证。显式 ID 与现有宿主会话绑定冲突时拒绝调用。`thread_id` 不在下面各工具表中重复列出。
- **2.1 返回约定**：`content` 为简短摘要，完整字段读取 `structuredContent.result`；外层同时有 `tool`、`isError` 与 `thread_id`。`read_image` 额外保留原生 image 内容。各工具的 `outputSchema` 描述字段，失败以 `error_code` / `message` 返回；命令失败也可能保留退出码和输出。不要再对摘要文本执行 JSON 解析。
- **路径必须是绝对路径**，用正斜杠（如 `E:/work/api`）。`apply_patch` 的补丁内文件路径相对于 `cwd`。不要在下划线前插入 Markdown 转义反斜杠。
- **返回信封**：每个工具返回 `content`（文本，权威结果）+ `structuredContent`（`{tool, result, isError}`）。命令类工具的 `result` 含 `running`、`exit_code`、`timed_out`、`stopped`、`session_id`、`output`、`truncated` 等字段——`isError`、`exit_code≠0`、`running=true` 各有含义：报错不是成功，仍在运行不是完成。
- **只读 / 写入**：下表"类型"列中，只读工具不改动磁盘；写入工具会改文件或执行命令。工具从不自动 `git commit` 或 `git push`。

## 对话与工作台

### `register_conversation` （写入）
登记本对话，返回本地 `thread_id` 与直达工作台链接。每个新对话调用一次；建议在工作前最先调用。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 工作区绝对目录 |
| `title` | string | 否 | 对话标题，1..120 字符；**省略时自动用工作目录名派生** |
| `chat_id` | string | 否 | 已知的真实 ChatGPT `/c/` UUID；未知就别传，绝不编造 |

返回：`thread_id`、`title`、`path`、`chat_id`、`chat_url`、`dashboard_url`、`instruction`。带相同 `chat_id` 或相同 `thread_id` 重复登记会复用同一对话，不会新建。

### `read_workspace_activity` （只读）
读取某工作区的非阻塞实时快照：运行中的工具、近期活动、计划、有界命令输出与 UI 连接诊断。不消耗输出、不执行命令、不改文件。桌面程序会自行观察活动；仅当需要在对话里拿文本快照时使用。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 工作区绝对目录 |
| `viewer_id` | string | 否 | UI 实例标识，最长 80 字符 |
| `bridge` | enum | 否 | `standard` 或 `legacy` |
| `display_mode` | enum | 否 | `inline` / `pip` / `fullscreen` / `unknown` |

## 工作区约定、计划与补丁

### `open_workspace` （只读）
编码前打开工作区：发现作用域内的 `AGENTS.override.md` / `AGENTS.md` 约定、Git 根、独立 skill 路径、可用 shell 与当前计划。不授予额外权限。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 工作目录绝对路径 |

### `update_plan` （写入）
发布简明的执行步骤及其真实状态。最多一个步骤可为 `in_progress`。计划是进程本地的，不是调度器，也不证明工作已完成。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 工作区绝对目录 |
| `plan` | array | 是 | 1..20 个步骤，每项 `{step: string≤240, status: pending\|in_progress\|completed}` |
| `explanation` | string | 否 | 本次计划更新的简要原因 |

### `apply_patch` （写入）
应用 Codex 风格多文件补丁：`*** Begin Patch` / `Add|Update|Delete File` / 可选 `Move to` / `@@` 上下文 / `*** End of File` / `*** End Patch`。写入前校验全部改动；拒绝歧义上下文、目标覆盖与路径越界。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `cwd` | string | 是 | 工作区绝对目录（补丁内路径相对于它） |
| `patch` | string | 是 | 完整的 Codex 补丁文本 |

## 诊断

### `get_workspace_status` （只读）
诊断实际连接的服务：版本、实例 ID、可执行路径、全部工具名、运行中命令数与近期操作结果。任务开始时调用，用来确认连接，不要凭缓存的工具缺失推断为只读。

无参数（仅通用 `thread_id`）。

## 目录、文件属性与搜索

### `list_directory` （只读）
列目录，带分页。`path` 为空时列出可用磁盘根。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 否 | 绝对路径；留空列出磁盘根 |
| `offset` | integer | 否 | 起始项，默认 0 |
| `limit` | integer | 否 | 每页 1..500，默认 100 |

### `file_info` （只读）
读取文件 / 目录的大小、时间戳与属性。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 绝对路径 |

### `search_files` （只读）
按 `*` / `?` 通配符搜索文件名，带分页。跳过 reparse point；预算上限与省略项会显式标注。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 搜索目录 |
| `pattern` | string | 否 | 文件名通配符，默认 `*` |
| `recursive` | boolean | 否 | 含子目录，默认 true |
| `offset` | integer | 否 | 匹配偏移，默认 0 |
| `limit` | integer | 否 | 最多 1..200 条，默认 50 |

### `search_text` （只读）
按字面文本搜索文件内容，返回路径、行 / 列与摘录。跳过超过 2 MiB 的文件与检测到的二进制文件；部分扫描会显式标注（不要把 `truncated`/`skipped` 当成穷尽结果）。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 搜索目录 |
| `query` | string | 是 | 非空字面文本 |
| `pattern` | string | 否 | 文件名通配符，默认 `*` |
| `recursive` | boolean | 否 | 含子目录，默认 true |
| `case_sensitive` | boolean | 否 | 区分大小写，默认 false |
| `offset` | integer | 否 | 匹配偏移，默认 0 |
| `limit` | integer | 否 | 最多 1..200 条，默认 50 |

## 读取与写入文件

### `read_file` （只读）
按行读取文本文件，带行号与显式续读标记（`next_line`）。支持 UTF-8 与 BOM。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 文件绝对路径 |
| `start_line` | integer | 否 | 起始行，1 起 |
| `limit` | integer | 否 | 行数 1..1000，默认 200 |

### `read_image` （只读）
把本地 PNG / JPEG / GIF / WebP 作为原生 MCP 图片内容读取，供视觉检查。最大 4 MiB。不是截屏工具。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 图片绝对路径 |

### `import_file` （写入 / 公网下载）
将宿主提供的聊天附件保存为本地新文件。通过 `_meta["openai/fileParams"] = ["file"]` 向宿主声明文件参数；聊天是否能提供附件由宿主决定。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 目标绝对路径；父目录需已存在，目标文件必须不存在 |
| `file` | object | 是 | 宿主提供的文件对象，字段见下方 |
| `file.download_url` | string | 是 | HTTPS 签名下载地址，443 端口，不含用户名/密码 |
| `file.file_id` | string | 是 | 宿主文件 ID，1..512 字符 |
| `file.mime_type` | string | 否 | 宿主声明的类型，保存回执使用下载响应的 Content-Type |
| `file.file_name` | string | 否 | 原始名称；不会改变用户指定的目标路径 |

最多 32 MiB，下载期限 45 秒，最多 3 次重定向。失败会清理临时文件，不覆盖已有内容；成功返回 `path`、`file_id`、`mime_type`、`size_bytes`、`sha256` 与 `created: true`。下载 URL 不写入活动详情与本地日志。保存后可继续用其他工具处理；本工具不做文档解析或病毒扫描。

### `create_directory` （写入）
创建目录及缺失的父目录。已存在则成功且无改动；不删除或替换文件。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 目录绝对路径 |

### `write_file` （写入）
写入 UTF-8 文本。覆盖已存在文件需显式 `overwrite=true`。会创建父目录。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 文件绝对路径 |
| `content` | string | 是 | 完整新内容 |
| `overwrite` | boolean | 否 | 显式允许替换已存在文件 |

### `edit_file` （写入）
替换 `old_text` 的**唯一一次**出现。缺失或有歧义则失败；保留原有编码与 BOM。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 文件绝对路径 |
| `old_text` | string | 是 | 非空、恰好出现一次的精确文本 |
| `new_text` | string | 是 | 替换后的文本 |

## 命令执行

### `exec_command` （写入，开放世界）
运行隐藏 shell 命令。默认 Git Bash；需要 PowerShell 语法时显式传 `shell`。命令在等待窗口后仍在运行会返回 `session_id`，用 `write_stdin` / `poll_command` 续读而不是重跑。无 shell 间自动回退；不支持 PTY（`tty` 只能为 false）。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `cwd` | string | 是 | 已存在的工作目录绝对路径 |
| `cmd` | string | 否 | shell 命令（`command` 为兼容旧名） |
| `shell` | enum | 否 | `git_bash`（默认，`bash` 别名）/ `powershell` / `pwsh` |
| `yield_time_ms` | integer | 否 | 等待 0..10000 ms，默认 1000（`yield_ms` 为兼容旧名） |
| `timeout_seconds` | integer | 否 | 1..3600 秒后终止，默认 300 |
| `tty` | boolean | 否 | 只支持 false |

> 安全：目前没有命令级护栏，`exec_command` 以当前用户权限执行任意命令，破坏性命令也不弹确认。请收窄目录范围并盯时间线复核。

### `write_stdin` （写入）
续接命令会话：省略 `chars`（或传空）即轮询新输出；传 `chars` 写入 stdin；发送 Ctrl-C（U+0003）终止进程树。管道而非 PTY。不要盲目重试已发送的输入。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `session_id` | string | 是 | `exec_command` 返回的会话 |
| `chars` | string | 否 | 精确输入；空 / 省略表示轮询（`text` 为兼容旧名） |
| `yield_time_ms` | integer | 否 | 等待 0..10000 ms，默认 1000 |
| `close` | boolean | 否 | 写入后关闭 stdin，默认 false |

### `poll_command` （写入）
读取命令会话的新增输出与退出状态；`stop=true` 终止该命令树。只消费增量输出。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `session_id` | string | 是 | `exec_command` 返回的会话 |
| `stop` | boolean | 否 | 终止该进程树 |
| `yield_ms` | integer | 否 | 等待 0..10000 ms，默认 1000 |

### `read_command` （只读）
读取命令输出的有界累计快照，**不消费**它。适合 UI 自动刷新与反复查看；不停止、不重启命令。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `session_id` | string | 是 | `exec_command` 返回的会话 |

### `stop_command` （写入）
显式停止本服务拥有的命令进程树，返回最终输出快照。不会停止无关进程。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `session_id` | string | 是 | `exec_command` 返回的会话 |

### `list_commands` （只读）
列出本服务拥有的命令会话，含运行 / 完成状态与退出码。不消费输出。

无参数（仅通用 `thread_id`）。

## 修改审阅

### `show_changes` （只读）
显示本服务进程中 `write_file` / `edit_file` 记录的前后改动，按目录作用域汇总。**不含** shell 或外部编辑器的改动，且不会重置审阅基准。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 要审阅的绝对目录 |

### `git_status` （只读）
读取 Git 工作树状态，含通过命令或外部编辑器产生的改动。需要 PATH 上有 Git；不 stage、不 commit、不 push。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 已存在的仓库目录 |

### `git_diff` （只读）
读取仓库的真实 Git diff。默认读未暂存的已跟踪改动；`staged=true` 读索引 diff。未跟踪文件由 `git_status` 列出而非 diff。禁用外部 diff 驱动与文本转换。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 已存在的仓库目录 |
| `staged` | boolean | 否 | 读暂存改动而非未暂存 |
