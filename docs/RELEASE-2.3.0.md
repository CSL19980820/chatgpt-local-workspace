# Local Workspace 2.3.0

2026-09-30。28 个工具，继续使用系统 .NET Framework 4.8；无新增运行时或数据库依赖。

## 命令与编辑可靠性

- `exec_command.request_id` 将重试绑定到对话、目录、Shell、命令和超时。相同请求返回原会话，不再执行一次；参数变化被拒绝。重启或会话淘汰后只返回需要核对的错误，不自动重跑。等待时间可调整；标准输入不提供重试去重。
- `read_command` 新增 `offset` / `length`：按字符游标分页，负数读取末尾，返回 `next_offset`、`has_more`、`oldest_offset`、`gap`。分页不消费增量输出，也不重复返回全文。原有不带分页参数的累计快照保持兼容。
- `read_file` 返回完整原始文件的 `sha256`。`write_file` / `edit_file` 支持可选 `expected_sha256` 和 `dry_run`，返回修改前后哈希与真实变更 ID。`missing` 表示预期文件不存在。预览不写文件；不匹配时要求重新读取。
- 精确编辑失败提供换行差异或附近行提示；重叠的多个匹配也会拒绝，不自动执行模糊替换。

## 本地恢复与证据

- 对话关联、计划、最近 100 条活动、命令请求去重记录和文件操作历史，保存到 `%LOCALAPPDATA%/LocalWorkspacePlugin/state-v1`。使用 Windows 当前用户 DPAPI 加密和临时文件原子替换。同一状态目录只允许一个 MCP 进程写入；测试或独立实例使用 `WORKSPACE_STATE_DIR` 指定不同目录。
- 命令进程和图片预览不跨重启恢复。未确认结束的命令在重启后标记为 `PROCESS_RESTARTED`，需要核对效果，不能误判为仍在运行或已完成。已保存活动中的命令输出只是当次有界快照。
- 回执的 `structuredContent.activity_id` 可以填写到计划步骤的 `activity_ids`。引用必须属于同一对话和目录，且对应成功、已结束的操作；过期或无效引用不会通过检查。仍可登记文字 `evidence`，但文字说明不等于独立验证。
- 更新计划时间不再自动消除失败。通过 `resolved_issue_id` 精确指向完成检查返回的 `last_issue_id`，填写 `recovery_note` 说明实际处理；可再用 `recovery_evidence_id` 关联失败之后的成功操作。工作台区分处理说明与已关联的恢复证据。插件仍不能强制宿主继续执行或验证业务语义。

## 文件历史与审阅

- 新增 `workspace_history`：查看当前对话 `write_file` / `edit_file` / `apply_patch` 的记录、修改前后哈希和恢复 ID。
- 新增 `restore_change`：默认预览；`apply=true` 撤销，另加 `redo=true` 重做。先验证整组文件仍然匹配，再执行恢复；冲突时不覆盖外部修改，写入失败尝试回退已处理的文件，回退失败明确报告。恢复中断的记录需人工核对。
- 文件历史最多保留 100 次操作、约 48 MiB 原始前后字节；单文件上限 16 MiB。超大操作必须拆分。记录不包括附件导入、目录创建、shell、部署、远程 Git 等副作用，恢复文件也不会清除空目录。历史存储是有界恢复辅助，不是备份系统。
- `open_workspace` 在 Git 仓库建立当前对话的审阅基准，返回可用状态。`show_changes` 新增 `since=workspace_open|last_shown`；`mark_reviewed=true` 更新上次审阅基准。截断的审阅不能推进基准。
- Git 基准使用临时 index 和 `refs/local-workspace/review/` 私有引用，不修改正常暂存区、分支或远端。范围是整个仓库，含非忽略的新文件与外部修改，不等于将这些修改归因给模型。缺失基准不伪造历史。
- 不传 `since` 的 `show_changes` 保留原来的进程内工具改动汇总。跨重启查看直接文件记录使用 `workspace_history`。

## 凭据与升级

API Key 保存到 Windows 凭据管理器的 `LocalWorkspacePlugin/tunnel-api-key`。启动新版桌面程序时，只有在凭据保存成功后才将旧 `settings.json` 中的明文 Key 移除；失败会保留原配置并明确报错。不要在同一配置上混用旧版和新版桌面程序。

正式文件位于 `dist/`，验证构建位于 `dist-next/`。`Apply-Update.ps1 -StageWhileRunning` 可在保留旧进程的情况下替换下次启动的文件，并输出旧映像备份位置。更新磁盘上的程序不代表旧进程已升级；现有连接保持运行，退出旧程序后再启动 `dist/LocalWorkspace.exe`，刷新宿主工具元数据并核对 `version=2.3.0`、`tool_count=28`。旧进程退出时未保存的 2.2.x 内存计划不能由新版追溯恢复。

## 实现参考

设计研读参考了 [FileMCP](https://github.com/anhnv02/file-mcp) 的请求去重和文件哈希契约、[Desktop Commander](https://github.com/wonderwhy-er/DesktopCommanderMCP) 的输出分页与编辑诊断、[Mieruko Workbench](https://github.com/Mieruko/MCP_Plugins_With_ChatGPTWeb) 的恢复前冲突检查，以及 [DevSpace](https://github.com/Waishnav/devspace) 的私有审阅基准。本版在现有 C# 工具层内实现，没有引入这些项目的运行时。SSE 和多工作区调度不在本版范围内。

实际验证与发布文件哈希见 [VERIFICATION.md](../VERIFICATION.md)。本地 MCP / 浏览器测试不代表真实 ChatGPT Tunnel 已刷新和加载新版。
