# Local Workspace 2.1.0

本次更新保留 2.0.2 的原图预览、工作区详情、Windows 路径打开和按钮样式，新增自动会话归组、聊天附件导入与连接诊断。

## 新功能

- 请求包含官方 `openai/session` 元数据时自动归组；组织和用户标识参与区分。显式手动登记继续可用，标题修改不会被后续路径调用覆盖。匿名会话标识不是 ChatGPT 真实会话链接，也不是权限凭据。
- 第 25 个工具 `import_file` 使用 `openai/fileParams` 声明输入，接收 `file_id`、`download_url` 和可选文件名、类型。用户指定本地目标路径；目录需已存在，文件必须是新文件。最多 32 MiB、整体下载期限 45 秒、最多 3 次重定向，采用临时文件与原子创建，返回 SHA256。
- 文件下载使用 TLS 1.2，保留服务器证书校验。拒绝 HTTP、带用户信息的 URL、非 443 端口和内网/回环目标；兼容 TUN 代理将域名映射到 198.18/15 的合成 DNS，仍拒绝直接使用该网段的 IP 地址。签名 URL 不写入工作台回执和日志。
- 工作台诊断面板分别显示配置、隧道存活、隧道就绪、握手、发现、成功调用和会话信号；桌面“更多”菜单也提供入口。配置检查调用同目录官方 Tunnel Client 的 `doctor --json --explain`，只呈现已知检查项目和状态。不会重启服务。
- 各工具声明返回字段；模型正文使用短摘要，完整字段统一读取 `structuredContent.result`。原生图片内容和命令输出仍可访问。

## 升级

1. 解压发行包，保留 `LocalWorkspace.exe`、`dashboard.html`、`tunnel-client.exe` 及许可证文件。
2. 退出旧程序后启动新版，沿用原有配置。
3. 在 ChatGPT 设置中刷新连接器工具，并在新聊天中调用 `get_workspace_status`。
4. 核对 `version: 2.1.0` 与 `tool_count: 25`。

自建客户端如果解析 `content[0].text` 中的 JSON，需要改为读取 `structuredContent.result`。输出的业务字段保留，正文摘要不再是 JSON。自动归组是当前进程内状态，重启后重新建立；没有宿主会话元数据时继续使用 `register_conversation`。

## 验证范围

- 本地真实 EXE 的 legacy / modern 协议、文件和命令工具回归；各工具的真实结果通过声明的 JSON Schema 校验。
- 模拟宿主元数据的会话稳定性、不同会话/组织/用户区分、手动兼容、命名保持，以及命令和任务误用另一会话 ID 的拒绝行为。
- modern 确认后重试与自动归组组合；失败回执和活动记录不泄漏签名下载 URL。
- 公网 HTTPS 文件下载并逐字节及 SHA256 比对，HTTP 404、已有目标和内网地址失败处理；失败后不留下临时文件。
- 实际浏览器中的诊断打开、重复检查、关闭后重新打开及窄窗口布局；既有图片、路径与工作区详情回归。
- 使用发行目录中的官方 `doctor` 验证无效配置；受控本地健康端点验证“存活通过、就绪失败、工具调用待验证”能够分别呈现。

最终 `dist/LocalWorkspace.exe` 的测试运行报告 19 个条目通过。认证 Tunnel 冒烟脚本未启用，未将其退出成功计为宿主连通证据。

这些验证使用隔离进程。**没有将本地模拟请求标成 ChatGPT 端到端验收**：真实 ChatGPT Tunnel 是否转发匿名会话信号、具体账号是否显示附件选择，需要从宿主实际发起调用。诊断页的“未检查”“待验证”保留这一差别。发布包继续固定 OpenAI Tunnel Client v0.0.14；本次未升级隧道组件。

## 官方依据

- [插件参考：文件输入与客户端元数据](https://developers.openai.com/plugins/reference)
- [构建 MCP 服务：工具说明与返回值](https://developers.openai.com/plugins/build/mcp-server)
- [优化工具元数据](https://developers.openai.com/plugins/guides/optimize-metadata)
- [Secure MCP Tunnels：doctor 与健康接口](https://developers.openai.com/api/docs/guides/secure-mcp-tunnels)

标准 Codex 插件目录打包留作独立工作，本版仍按现有 Windows 桌面程序与 ChatGPT Tunnel 方式分发。
