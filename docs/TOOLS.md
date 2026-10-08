# 工具参数手册（2.4.0，23 个工具）

本页是 23 个本地工具的输入参数与返回字段参考，与 `src/WorkspaceServer.cs` 中 `BuildTools()` 注册的 schema 一致。工具由模型按需自动调用；本页供你核对参数、写自动化或排查调用失败。2.4.0 删合的 5 个旧名字见“[2.4.0 删合对照](#240-删合对照28--23)”。

## 目录

- [通用约定](#通用约定)
- [2.4.0 删合对照](#240-删合对照28--23)
- [对话、约定、计划与补丁](#对话约定计划与补丁)：`register_conversation`、`open_workspace`、`update_plan`、`check_task_completion`、`apply_patch`
- [诊断](#诊断)：`get_workspace_status`
- [目录、文件属性与搜索](#目录文件属性与搜索)：`list_directory`、`search_files`、`search_text`
- [读取与写入文件](#读取与写入文件)：`read_file`、`read_image`、`import_file`、`create_directory`、`write_file`、`edit_file`
- [命令执行](#命令执行)：`exec_command`、`write_stdin`、`read_command`
- [文件历史与撤销](#文件历史与撤销)：`workspace_history`、`restore_change`
- [修改审阅](#修改审阅)：`show_changes`、`git_status`、`git_diff`
- [工作台快照中的相关字段](#工作台快照中的相关字段)

## 通用约定

- **`thread_id`（所有工具可选，string）**：本地对话 ID。宿主传入 `_meta["openai/session"]`（ChatGPT）或 `_meta.threadId`（Codex，可带 `sessionId`）时，服务端自动关联对话；都没有时先调用 `register_conversation`，之后的调用传入它返回的 `thread_id`。两者皆无时进入“未归属”，不按路径或最近调用猜测。宿主元数据只用于关联，不用于认证；显式 ID 与已有宿主会话绑定冲突时拒绝调用。下面各表不再重复列出 `thread_id`。
- **返回结构**：`content` 是简短摘要；完整字段在 `structuredContent.result`，外层还有 `tool`、`isError`、`thread_id`、`activity_id` 与 `task`。`read_image` 额外返回原生 image 内容。各工具声明 `outputSchema`；失败以 `error_code` / `message` 返回，命令失败也可能保留退出码和输出。不要对摘要文本做 JSON 解析。
- **`structuredContent.task`**：已登记计划时附带同一对话的完成提示、未完成项数量和下一步；没有计划时为 null。不影响 `result` 中的业务字段。
- **`structuredContent.activity_id`**：本次实际操作的活动 ID，可填入 `update_plan` 步骤的 `activity_ids`。
- **路径**：必须是绝对路径，用正斜杠（如 `E:/work/api`）。`apply_patch` 补丁内的文件路径相对于 `cwd`。不要在下划线前插入 Markdown 转义反斜杠。
- **命令类结果**：`result` 含 `running`、`exit_code`、`timed_out`、`stopped`、`session_id`、`output`、`truncated` 等。`isError`、`exit_code≠0`、`running=true` 各有含义：报错不是成功，仍在运行不是完成。
- **只读 / 写入**：标题后的“只读”工具不改动磁盘，在 `annotations` 中标记 `readOnlyHint: true`，Codex `default_tools_approval_mode = "writes"` 时免审批；“写入”工具会改文件或执行命令。工具从不自动 `git commit` 或 `git push`。

## 2.4.0 删合对照（28 → 23）

下列旧名字不再出现在 `tools/list`，但 2.4.0 仍可调用，计划 2.5.0 移除。调用旧名字时，结果带 `structuredContent.result.deprecated = {tool, replacement, removed_in: "2.5.0", note}`，摘要以 `DEPRECATED:` 开头；`get_workspace_status` 的 `deprecated_tools` 列出全部旧名字。

| 旧工具 | 改用 | 写法 |
| --- | --- | --- |
| `poll_command` | `read_command` | `{session_id, yield_time_ms: 5000, offset: <上次 next_offset>}`；有新输出或退出即返回，只读、不消费输出。旧的 `stop: true` 改为 `write_stdin` 发送 Ctrl-C |
| `stop_command` | `write_stdin` | `{session_id, chars: "\u0003"}`，终止该命令树，返回最终快照 |
| `list_commands` | `get_workspace_status` | 读 `result.commands` |
| `read_workspace_activity` | `get_workspace_status` | 传 `path`，读 `result.workspace`（旧工具的 `viewer_id`、`bridge`、`display_mode` 不再列入 schema） |
| `file_info` | `list_directory` | 同一 `path`；文件返回 `kind: "file"`、`entries: []` 与 `info` |

> Codex 的 `config.toml` 中如果在 `enabled_tools`、`disabled_tools` 或 `[mcp_servers.<名>.tools.<工具>]` 写了旧名字，请改成新名字。推荐配置见 [README](../README.md#codex-推荐配置)。

## 对话、约定、计划与补丁

### `register_conversation`（写入）

登记本对话，返回本地 `thread_id` 与直达工作台链接。宿主不提供会话信号时，在工作前最先调用一次。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 工作区绝对目录 |
| `title` | string | 否 | 对话标题，1..120 字符；省略时用工作目录名 |
| `chat_id` | string | 否 | 已知的真实 ChatGPT `/c/` UUID；未知就不传，不要编造 |

返回 `thread_id`、`title`、`path`、`chat_id`、`chat_url`、`dashboard_url`、`instruction`。相同 `chat_id` 或相同 `thread_id` 重复登记会复用同一对话。这样登记的对话在工作台快照中 `source` 为 `manual`。

### `open_workspace`（只读）

编码前打开工作区：发现作用域内的 `AGENTS.override.md` / `AGENTS.md` 约定、Git 根、独立 skill 路径、可用 shell 与当前计划。不授予额外权限。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 工作目录绝对路径 |

Git 仓库首次打开时建立私有审阅基准，结果见 `review.available`；不改变正常暂存区、分支或远端。

### `update_plan`（写入）

发布简明的执行步骤及其真实状态，最多一个步骤为 `in_progress`。计划及验证日志只在当前 MCP 进程内保存，重启后清空；它不是调度器，也不证明工作已完成。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 工作区绝对目录 |
| `plan` | array | 是 | 1..20 个步骤，每项 `{step: string≤240, status: pending\|in_progress\|completed, evidence?: string≤1000, activity_ids?: string[]≤20}` |
| `explanation` | string | 条件 | 最多 2000 字；移除或重命名未完成步骤时必须说明范围变化 |
| `task_state` | string | 否 | `active` / `blocked` / `paused`；新计划默认 active，省略时保留已有的阻塞 / 暂停 |
| `reason` | string | 条件 | blocked / paused 必填，最多 1000 字 |
| `next_action` | string | 条件 | blocked / paused 必填，最多 1000 字 |
| `resolved_issue_id` | string | 否 | 完成检查返回的 `last_issue_id`，明确处理该失败；不能只靠时间消除失败 |
| `recovery_note` | string | 条件 | 传 `resolved_issue_id` 时必填，最多 1000 字 |
| `recovery_evidence_id` | string | 否 | 失败之后的一次成功操作的活动 ID |

- `evidence` 应记录实际验证结果或回执，不得填写未执行的验证。已完成步骤缺少证据时完成检查不通过。
- `activity_ids` 只接受同一对话、同一目录内成功且已结束的实际操作。
- `recovery_verified` 区分“已关联恢复活动”和“仅登记说明”。恢复执行时显式设置 `task_state: active`；这不会取消用户的授权边界。

### `check_task_completion`（只读）

交付前调用，核对当前对话下指定计划的完成条件。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | `update_plan` 使用的同一个绝对工作目录 |

返回 `can_finish`、`state`、`unfinished_steps`、`missing_evidence`、`running`、`reason`、`next_action`、`last_issue`、`last_issue_id`、`last_issue_at` 和 `resume_prompt`。没有该计划时返回 `state: untracked`、`can_finish: false` 和建立计划的提示。检查调用成功不等于 `can_finish: true`，应读取该字段。

状态包括 active、running、needs_attention、verification_required、idle_unconfirmed、blocked、paused 和 ready。所有步骤已完成且有证据、没有运行中的命令、没有未明确处理的失败、任务处于 active 时才返回 ready。两分钟无操作只表示待确认；暂停 / 阻塞状态必须被尊重。检查只核对登记字段和当前进程内的有界执行记录，不能独立验证证据或需求完整性；`resume_prompt` 供复制回原对话，不自动发送，也不新增授权。

### `apply_patch`（写入）

应用 Codex 风格多文件补丁：`*** Begin Patch` / `Add|Update|Delete File` / 可选 `Move to` / `@@` 上下文 / `*** End of File` / `*** End Patch`。写入前校验全部改动；拒绝歧义上下文、目标覆盖与路径越界。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `cwd` | string | 是 | 工作区绝对目录（补丁内路径相对于它） |
| `patch` | string | 是 | 完整的 Codex 补丁文本 |

返回 `files`、`count`、`partial` 与 `change_id`。`detail.files[i]` 带 `added` / `removed`，`detail.added` / `removed` 合计全部**已写入**的文件；未写入的文件不出现在 `files` 中。失败且没有部分写入时增删行数为 0。

## 诊断

### `get_workspace_status`（只读）

诊断实际连接的服务：版本、实例 ID、可执行路径、全部工具名、命令会话、运行中命令数与近期操作结果。任务开始时调用，用来确认连接；不要因为宿主缓存里缺少某个工具就推断为只读。2.4.0 起并入 `list_commands` 与 `read_workspace_activity`。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 否 | 工作区绝对目录；提供时 `workspace` 返回该目录的非阻塞实时快照 |

返回（节选）：`version`（2.4.0）、`tool_count`（23）、`tools`、`deprecated_tools`、`protocol_versions`、`running_commands`、`commands`（本服务拥有的命令会话：`session_id`、`thread_id`、运行 / 完成状态与退出码，不消费输出）、`conversations`、`activity`、`plans`。

带 `path` 时 `workspace` 为实时快照：运行中的工具、近期活动、计划与有界命令输出。即使有长调用在跑也立即返回，**不进入活动时间线**；不消耗输出、不执行命令、不改文件。不带 `path` 时 `workspace` 为 null。

## 目录、文件属性与搜索

### `list_directory`（只读）

列目录，带分页；`path` 为空时列出可用磁盘根。2.4.0 起并入 `file_info`：`path` 是文件时返回 `kind: "file"`、空 `entries` 与 `info`（大小、时间戳、属性）；目录返回 `kind: "directory"`，同样附带 `info`。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 否 | 绝对目录或文件路径；留空列出磁盘根 |
| `offset` | integer | 否 | 起始项，默认 0 |
| `limit` | integer | 否 | 每页 1..500，默认 100 |

### `search_files`（只读）

按 `*` / `?` 通配符搜索文件名，带分页。跳过 reparse point；预算上限与省略项显式标注。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 搜索目录 |
| `pattern` | string | 否 | 文件名通配符，默认 `*` |
| `recursive` | boolean | 否 | 含子目录，默认 true |
| `offset` | integer | 否 | 匹配偏移，默认 0 |
| `limit` | integer | 否 | 1..200 条，默认 50 |

### `search_text`（只读）

按字面文本搜索文件内容，返回路径、行 / 列与摘录。跳过超过 2 MiB 的文件与检测到的二进制文件；部分扫描显式标注（不要把 `truncated` / `skipped` 当成穷尽结果）。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 搜索目录 |
| `query` | string | 是 | 非空字面文本 |
| `pattern` | string | 否 | 文件名通配符，默认 `*` |
| `recursive` | boolean | 否 | 含子目录，默认 true |
| `case_sensitive` | boolean | 否 | 区分大小写，默认 false |
| `offset` | integer | 否 | 匹配偏移，默认 0 |
| `limit` | integer | 否 | 1..200 条，默认 50 |

## 读取与写入文件

### `read_file`（只读）

按行读取文本文件，带行号与续读标记 `next_line`。支持 UTF-8 与 BOM。返回 `sha256`（完整原始文件字节的哈希），可作为写入时的 `expected_sha256`。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 文件绝对路径 |
| `start_line` | integer | 否 | 起始行，从 1 开始 |
| `limit` | integer | 否 | 行数 1..1000，默认 200 |

### `read_image`（只读）

把本地 PNG / JPEG / GIF / WebP 作为原生 MCP 图片内容读取，供视觉检查，最大 4 MiB。不是截屏工具。回执带 `preview_url`，工作台据此显示读取时的原始字节（进程内最多 100 张 / 32 MiB）。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 图片绝对路径 |

### `import_file`（写入，公网下载）

将宿主提供的聊天附件保存为本地新文件。通过 `_meta["openai/fileParams"] = ["file"]` 向宿主声明文件参数；聊天能否提供附件由宿主决定。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 目标绝对路径；父目录需已存在，目标文件必须不存在 |
| `file` | object | 是 | 宿主提供的文件对象 |
| `file.download_url` | string | 是 | HTTPS 签名下载地址，443 端口，不含用户名 / 密码 |
| `file.file_id` | string | 是 | 宿主文件 ID，1..512 字符 |
| `file.mime_type` | string | 否 | 宿主声明的类型；回执使用下载响应的 Content-Type |
| `file.file_name` | string | 否 | 原始名称；不改变用户指定的目标路径 |

最多 32 MiB，下载期限 45 秒，最多 3 次重定向。失败会清理临时文件，不覆盖已有内容；成功返回 `path`、`file_id`、`mime_type`、`size_bytes`、`sha256` 与 `created: true`。下载 URL 不写入活动详情与本地日志。本工具不做文档解析或病毒扫描。

### `create_directory`（写入）

创建目录及缺失的父目录。已存在则成功且无改动；不删除或替换文件。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 目录绝对路径 |

### `write_file`（写入）

写入 UTF-8 文本，会创建父目录。覆盖已存在文件需显式 `overwrite=true`。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 文件绝对路径 |
| `content` | string | 是 | 完整新内容 |
| `overwrite` | boolean | 否 | 显式允许替换已存在文件 |
| `expected_sha256` | string | 否 | 来自 `read_file` 或预览的完整文件 SHA256，不符则拒绝；`missing` 表示预期文件不存在 |
| `dry_run` | boolean | 否 | 只预览，不写入 |

### `edit_file`（写入）

替换 `old_text` 的**唯一一次**出现。缺失或有歧义时失败，并给出换行差异或附近行提示，不自动模糊替换；保留原有编码与 BOM。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 文件绝对路径 |
| `old_text` | string | 是 | 非空、恰好出现一次的精确文本 |
| `new_text` | string | 是 | 替换后的文本 |
| `expected_sha256` | string | 否 | 同 `write_file` |
| `dry_run` | boolean | 否 | 只预览，不写入 |

`write_file` / `edit_file` 成功回执包含 `before_sha256`、`after_sha256`、`applied`、`changed`、`change_id`，以及 `detail.files[i].added` / `removed`。无改动或预览不产生可撤销记录。

## 命令执行

`exec_command`、`write_stdin`、`read_command` 都接受可选的 `include_full_output`（默认 false）：正文只在 `output` 返回；需要旧版有界 `full_output` 字段时显式传 true。`output_chars` 是当前保留的历史字符数；`logs_cleared` 表示该已完成会话的日志已被本地界面清空，退出状态、归属和重试保护仍保留。大输出优先用 `read_command` 分页。

### `exec_command`（写入，开放世界）

运行隐藏 shell 命令。默认 Git Bash；需要 PowerShell 语法时显式传 `shell`。等待窗口结束后仍在运行时返回 `session_id`，用 `read_command`（只读）或 `write_stdin` 续读，不要重跑。shell 之间不自动回退；不支持 PTY。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `cwd` | string | 是 | 已存在的工作目录绝对路径 |
| `cmd` | string | 否 | shell 命令（`command` 为兼容旧名） |
| `shell` | enum | 否 | `git_bash`（默认，别名 `bash`）/ `powershell` / `pwsh` |
| `yield_time_ms` | integer | 否 | 等待 0..10000 ms，默认 1000（`yield_ms` 为兼容旧名） |
| `timeout_seconds` | integer | 否 | 1..3600 秒后终止，默认 300 |
| `tty` | boolean | 否 | 只支持 false |
| `request_id` | string | 否 | 1..128 个 ASCII 字母、数字、点、下划线或连字符的重试键 |

`request_id`：同一对话内相同 ID 与执行参数复用原会话（`request_replayed: true`），参数不符则拒绝；需要宿主会话信号或已登记的 `thread_id`。重启或淘汰后返回 `REQUEST_RECONCILIATION_REQUIRED`，不会再次执行。记录最多 10000 个键，满后须核对并归档已停止实例的状态再重置，不自动丢弃旧键。

> 安全：目前没有命令级护栏，`exec_command` 以当前用户权限执行任意命令，破坏性命令也不弹确认。请收窄目录范围并在工作台时间线复核。

### `write_stdin`（写入）

续接命令会话：省略 `chars`（或传空）即轮询新输出；传 `chars` 写入 stdin；发送 Ctrl-C（U+0003，JSON 中写作 `"\u0003"`）终止进程树，替代 2.4.0 之前的 `stop_command`。标准输入是管道而不是 PTY。不要盲目重试已发送的输入；只需查看输出时优先用只读的 `read_command`。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `session_id` | string | 是 | `exec_command` 返回的会话 |
| `chars` | string | 否 | 精确输入；空 / 省略表示轮询（`text` 为兼容旧名） |
| `yield_time_ms` | integer | 否 | 等待 0..10000 ms，默认 1000 |
| `close` | boolean | 否 | 写入后关闭 stdin，默认 false |

### `read_command`（只读）

读取命令输出的有界累计快照，**不消费**输出，不停止、不重启命令。2.4.0 起并入 `poll_command` 的等待能力：传 `yield_time_ms` 时等待命令退出；同时传 `offset`（通常为上次的 `next_offset`）时，有超过该位置的新输出或命令退出就返回。停止命令请用 `write_stdin` 发送 Ctrl-C。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `session_id` | string | 是 | `exec_command` 返回的会话 |
| `offset` | integer | 否 | 原始输出的字符位置，负数表示距末尾的字符数 |
| `length` | integer | 否 | 1..32000，默认 8000 |
| `yield_time_ms` | integer | 否 | 等待 0..10000 ms，默认 0（不等待）；`yield_ms` 为兼容旧名 |

分页返回 `next_offset`、`has_more`、`oldest_offset`、`gap`、`total_characters`；历史被淘汰时游标移到保留区间起点并标明 `gap`。例如 `offset: -4000, length: 4000` 读取末尾 4000 字符，下一页使用 `next_offset`。

轮询示例：`{session_id, offset: 0, yield_time_ms: 5000}` → 读到 `next_offset: 1234` → 下次 `{session_id, offset: 1234, yield_time_ms: 5000}`。

## 文件历史与撤销

### `workspace_history`（只读）

返回当前对话的直接文件操作记录 `changes`，每项含 `id`、`tool`、`at`、`status`、`undone` 和文件前后哈希。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 工作区绝对目录 |

保留上限 100 次操作 / 48 MiB 原始字节；单文件不超过 16 MiB。记录加密保存，重启后仍可用。

### `restore_change`（写入）

撤销或重做一次直接文件操作，默认只预览。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `change_id` | string | 是 | 写入回执或 `workspace_history` 中的 ID |
| `redo` | boolean | 否 | 重做而不是撤销，默认 false |
| `apply` | boolean | 否 | 实际执行，默认 false（只预览） |

应用时整组校验当前文件状态，不覆盖外部改动。返回 `change_id`、`applied`、`action`、`files`、`count`。声明 `elicitation` 的 modern 客户端会在实际恢复前收到 MRTR 确认。范围只限 `write_file` / `edit_file` / `apply_patch` 的记录；不包含附件导入或目录创建，不撤销 shell、部署、远程 Git 等副作用，也不删除残留的空目录。状态为 prepared / restoring / interrupted 的记录需要人工核对。

## 修改审阅

### `show_changes`（只读）

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 要审阅的绝对目录 |
| `since` | enum | 否 | `recorded`（默认）/ `workspace_open` / `last_shown` |
| `mark_reviewed` | boolean | 否 | 审阅后推进 `last_shown` 基准；结果被截断时拒绝推进 |

`recorded` 是本进程直接文件工具的修改汇总，不含 shell 或外部编辑。`workspace_open` / `last_shown` 返回整个 Git 仓库相对打开时 / 上次标记审阅时的差异，包含未忽略的新文件及外部修改；使用独立 index 与私有引用，不改动正常暂存区和分支。

### `git_status`（只读）

读取 Git 工作树状态，包含命令或外部编辑器产生的改动。需要 PATH 上有 Git；不 stage、不 commit、不 push。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 已存在的仓库目录 |

### `git_diff`（只读）

读取真实 Git diff。默认读未暂存的已跟踪改动；`staged=true` 读索引 diff。未跟踪文件由 `git_status` 列出，不在 diff 中。禁用外部 diff 驱动与文本转换。

| 参数 | 类型 | 必填 | 说明 |
| --- | --- | --- | --- |
| `path` | string | 是 | 已存在的仓库目录 |
| `staged` | boolean | 否 | 读暂存改动而不是未暂存改动 |

## 工作台快照中的相关字段

工作台通过本机 `/api/snapshot` 读取下列字段（只增不改）：

- **活动行**：`turn_id`（只来自 Codex 的 `_meta["x-codex-turn-metadata"].turn_id`，否则 null）、`turn_source`（`codex` 或 null）、`trace_id`（W3C `traceparent` 的 trace-id，否则 null）。工作台用 `turn_id` 把同一轮次的调用分为一组；为 null 时按 30 秒间隔分组。
- **`conversations[].source`**：`codex`（按 Codex `_meta.threadId` 归组）、`chatgpt`（按 `openai/session` 归组）、`manual`（`register_conversation` 登记）。`manual` 对应 `association: "manual"`，其余对应 `association: "host_session"`。
- **`conversations[].stats`**：`{calls, running, failed, added, removed, last_at}`。
- **`/api/clear-logs`**：返回 `scope_code`（当前为 `completed_logs`）、`cleared_at`、`kept`。
- **`/api/diagnostics`**：`versions[]` 每项带 `id`、`running`、`installed`、`restart_required` 和 `status`（`ok` / `restart_required` / `unknown_running` / `missing`）。

协议层的历史变化与迁移见 [2.4.0 发行说明](RELEASE-2.4.0.md) 和 [2.3.0 发行说明](RELEASE-2.3.0.md)。
