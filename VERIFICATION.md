# 验证记录

按时间倒序排列，最新在前。各节只记录当时实际执行的验证，不代表当前运行状态。时间均为本机时间（UTC+8）。

## 2.4.1 正式发行 · 2026-10-09

只改桌面“原始日志”的显示：折叠 Tunnel Client 启动时 INFO 级别的 fx 依赖注入日志。前端、图标、工具、接口和 Tunnel Client 版本都没有变化。源码与提交、标签 `v2.4.1` 一致。

### 改动

- `src/Program.cs` 新增 `TunnelLogFold`：只有 JSON 行、`level` 为 INFO、`msg` 与名单**完全相同**时才算启动噪声。名单 12 项：`provided`、`replaced`、`decorated`、`supplied`、`invoking`、`run`、`OnStart hook executing`、`OnStart hook executed`、`OnStop hook executing`、`OnStop hook executed`、`initialized custom fxevent.Logger`、`started`。
- `FlushLogs` 遇到这类行不再逐行追加，改为在第一条的位置放一行“已折叠 N 条 Tunnel 启动内部日志（fx 依赖注入 INFO；WARN/ERROR 不折叠，完整日志见 Tunnel 状态页 /ui）”，之后原地更新 N。每次启动连接、清空日志，或与上一条折叠行间隔超过 10 秒时另起一行。
- 版本号改为 2.4.1（`WorkspaceServer.Version`、测试、预览样例）。写法保持 C# 5（.NET Framework 自带 csc）。

### 名单核对（真实 0.0.16 输出）

- 用 `dist-next/tunnel-client.exe`（0.0.16）以 `--log.format json --log.level info` 启动：假 Key、假 Tunnel ID、控制面指向 `127.0.0.1:9`，`MCP_COMMAND` 是一个约 3 秒后退出的假脚本。没有读取或使用真实 API Key，结束时只结束这个探测进程。
- 两次探测各 253、254 行。fx 事件及条数：`provided` 71、`run` 69、`invoking` 18、`OnStop hook executing` 14、`OnStop hook executed` 14、`OnStart hook executing` 13、`OnStart hook executed` 13、`supplied` 10、`initialized custom fxevent.Logger` 1。这 9 个都在名单里。
- 没有出现 `replaced`、`decorated`、`started`。它们是 fx 同一组的成功事件，按原样列入名单。`running` 不是 fx 事件，也没有出现，没有列入。
- 其余 INFO（`🩺 HEALTH URL`、`🌐 WEB UI`、`tunnel-client startup summary`、`mcp channel route resolved`、`starting/stopping control-plane poller`、`poller started/stopped`、`stdio MCP command started/exited`、`🟢 tunnel-client started` 等）和全部 WARN 都不在名单里，照常显示。

### 折叠前后对比

用预览模式的主窗体（不启动隧道，不读写 `settings.json`，窗口不显示）把上面第二次探测的 254 行送进真实的 `Log` / `FlushLogs`，再读取原始日志的行数。`[Dashboard]` 一行不送入，因为它会创建内嵌 WebView2。

| | 2.4.0（`dist/LocalWorkspace.exe` 的副本） | 2.4.1（`dist-next`） |
| --- | --- | --- |
| 整段（启动 + 约 3 秒后假 MCP 退出引起的停止） | 253 行 | 31 行（30 行原样 + 1 行“已折叠 223 条”） |
| 启动到 `🟢 tunnel-client started` | 215 行 | 21 行（20 行原样 + 1 行折叠） |

停止阶段的 OnStop 日志与启动相隔约 3 秒，在 10 秒内，所以并入同一行折叠计数。

### 产物

- `scripts/Get-RuntimeComponents.ps1`：Tunnel Client 0.0.16、WebView2 SDK 1.0.4258.31，校验通过。
- `npm run build:ui`：`src/dashboard.html` 521,878 字节，SHA256 `1EF21B3E28235EC8772A24E91217146B2C5BCCAFEAB5C935FC4DAEB8F71BD6C8`，与 2.4.0 相同。
- `build.ps1` 输出 `dist-next/LocalWorkspace.exe`（00:17:39）：1,972,736 字节，文件版本 2.4.1.0，SHA256 `03ECC3F4C6EF637DB8CCE3886DD666FCB2DF3939F71ECF8F341934DC54026B12`。
- `tunnel-client.exe` 与 `runtime-components.json` 与 2.4.0 相同（Tunnel SHA256 `4226BB74A72388147882881F29F79801B95E285D731A138071812E3AA6766571`）。

### 自动化测试

- 新增 `tests/log-fold.test.cjs`：用 .NET Framework csc 编译 `tests/fixtures/LogFoldProbe.cs`，通过反射调用 EXE 中的 `TunnelLogFold` 和预览主窗体。样例 `tests/fixtures/tunnel-0.0.16-startup-sample.jsonl` 取自上面的真实 0.0.16 输出，每种 `msg` 一行，去掉了用户路径和实例 ID，另加几行人工构造的失败事件和边界行（ERROR `OnStart hook failed`、ERROR `start failed`、WARN `run`、DEBUG `provided`、INFO `running`、带 `[Workspace]` 的 INFO `provided`）。
  - 断言：只有名单内的 INFO 行被折叠，WARN / ERROR 和有用的 INFO 一律保留；名单与预期完全一致。
  - 原始日志里每条保留的行都在，折叠行只有一行，位置在第一条被折叠的行处；10 秒内再来一批仍更新同一行；清空后重新计数。
- `npm test` 第一次（00:17:45 开始，未设置 `WORKSPACE_TEST_EXE`，测试对象为 `dist-next`）：37 项，通过 35，失败 2。失败的是 `dashboard-ui` 的“时间线按时间正序排列”及其所在的 `dashboard` 组：该用例把样例里的 `HH:MM:SS` 文本直接排序，样例调用分布在当前时间之前约 46 分钟内，跨过零点时 23:xx 排到了 00:xx 后面。与本次改动无关，测试本身没有修改。
- 第二次（00:47:25 开始，用时 20 秒，样例不再跨零点）：37 项，通过 37，失败 0，跳过 0。`diagnostics doctor` 实际运行并通过。
- 认证 Tunnel 冒烟（`WORKSPACE_TUNNEL_SMOKE`）和公网附件冒烟未启用。
- 测试前后，用户 `settings.json` 的 SHA256 一致。

### 发行包

- `scripts/package-release.ps1` 生成 `work/release/local-workspace-2.4.1-windows-x64.zip`：11,000,321 字节，SHA256 `323ee295e4c32d6f5fafe55f657408976c0e635fd93d56c2dd9047662bb84b79`。包外 `SHA256SUMS.txt` 记录同一哈希。
- 解压核对：共 37 个文件，包内 `SHA256SUMS.txt` 列出的 36 个文件哈希全部一致。包内 `LocalWorkspace.exe` 与上面 `dist-next` 的 EXE 字节相同，文件版本 2.4.1.0。
- 设置 `WORKSPACE_TEST_EXE` 指向解压后的 EXE，运行完整 `npm test`（00:48:26 开始，用时 20 秒）：37 项，通过 37，失败 0。`task-completion` 的计时问题这次没有出现。
- 包内这份 VERIFICATION.md 是打包时的版本，本小节上面这几条是打包后补记的，其余内容与仓库一致（压缩包不能包含自身的哈希）。

### 验证边界

- 没有在真实 ChatGPT 会话中启动隧道并查看折叠效果；折叠效果只用假 Key 的探测输出和预览主窗体验证。
- 桌面程序运行时没有截图。

## 2.4.0 正式发行 · 2026-10-08

正式发行构建，源码与提交、标签 `v2.4.0` 一致。相对候选构建 4，前端、图标和后端源码都没有改动，只改了两处：

- 文档：README 中英文引用 4 张新截图，修正补丁截图的图注，说明桌面状态标签；本文件 2.3.0 一节补充截图说明。
- 预览脚本：rich 样例补上计划的任务信息，以及一条带 `change_id` 和修改前后 SHA256 的 `edit_file`；预览选中单个对话时，对话栏不再只剩这一条，与真实后端一致。

### 输入

- `docs/images/` 共 15 张：README 引用 14 张，`dashboard-task-completion.png` 未引用但保留。全部与前端交付的哈希逐一核对，一致。
- 前端文件（`src/ui/**`、`src/dashboard.*`、`assets/`、`tests/dashboard-ui.test.cjs`）与前端定稿哈希一致。
- `scripts/Get-RuntimeComponents.ps1`：Tunnel Client 0.0.16、WebView2 SDK 1.0.4258.31，官方 ZIP 的 SHA256 校验通过。

### 产物

- `npm run build:ui`：`src/dashboard.html` 521,878 字节，SHA256 `1EF21B3E28235EC8772A24E91217146B2C5BCCAFEAB5C935FC4DAEB8F71BD6C8`，与候选 3、4 相同。
- `build.ps1` 输出 `dist-next/LocalWorkspace.exe`（23:46:50）：1,970,688 字节，文件版本 2.4.0.0，SHA256 `CC0D46AEE05660CC728167189ED24FE0AA0431BDB554807089A6AAA282970831`。C# 编译器每次都会写入新的时间戳和模块 ID，所以哈希与候选 4 不同，大小相同。
- `tunnel-client.exe`：22,591,488 字节，SHA256 `4226BB74A72388147882881F29F79801B95E285D731A138071812E3AA6766571`。

### 自动化测试

- `npm test`（23:47 开始，用时 19 秒，未设置 `WORKSPACE_TEST_EXE`，测试对象为 `dist-next`）：共 36 项，通过 36，失败 0，跳过 0。`dist-next` 已包含官方 Tunnel Client，所以 `diagnostics doctor` 实际运行并通过。
- 认证 Tunnel 冒烟未启用：没有设置 `WORKSPACE_TUNNEL_SMOKE`，该用例在内部跳过，Node 仍计为通过。公网附件下载冒烟也未启用。
- 测试前后，用户 `settings.json` 的 SHA256 一致。

### 发行包

- `scripts/package-release.ps1` 生成 `work/release/local-workspace-2.4.0-windows-x64.zip`：10,994,649 字节，SHA256 `c891b72dc963dc67c2f6253d80c39081d856511237074d9b188b0207609bbdd8`。包外 `SHA256SUMS.txt` 记录同一哈希。
- 解压核对：共 37 个文件，包内 `SHA256SUMS.txt` 列出的 36 个文件哈希全部一致。包内 `LocalWorkspace.exe` 与上面 `dist-next` 的 EXE 字节相同。README 引用的 14 张截图全部在包内，未引用的 `dashboard-task-completion.png` 不在包内。
- 设置 `WORKSPACE_TEST_EXE` 指向解压后的 EXE，运行完整 `npm test`：
  - 第一次（23:48）：36 项，通过 35，失败 1。失败项是 `task-completion` 中的超时用例：它在命令超时 1 秒后只等 1.5 秒，就要求 `last_issue` 为 `COMMAND_TIMEOUT`，结果读到的是 `exec_command: COMMAND_FAILED`。
  - 随后单独运行该测试文件 5 次（解压包 3 次、`dist-next` 2 次），全部通过。
  - 第二次完整运行（23:49:26，用时 20 秒）：36 项，通过 36，失败 0。
  - 判断这是多个测试文件并行时的计时余量问题，与发行包无关，留待后续放宽等待时间。
- 包内这份 VERIFICATION.md 是打包时的版本，本小节上面这几条是打包后补记的，其余内容与仓库一致（压缩包不能包含自身的哈希）。

### 验证边界

- 没有替换本机正式 `dist/`。构建前的 `dist/LocalWorkspace.exe` 和 `dist-next/` 已备份到项目目录之外。
- 没有在真实 ChatGPT 会话中刷新工具并逐个调用 23 个工具。其余同候选 4 的“验证边界”。

## 2.4.0 候选构建 4 · 2026-10-08

本版在候选构建 3 的基础上只修改窗口图标的加载方式（`src/Program.cs`），前端、图标文件和其他后端代码不变。本节同时收录候选 3 的桌面图标实测，作为对比。

### 改动

- 不再用 `Icon.ExtractAssociatedIcon` 取图标（它只返回 32 px 帧，候选 3 标题栏的小图标是由 32 px 帧缩小来的）。
- 新增 `AppIcons`：从本程序 EXE 资源中找到第一个 RT_GROUP_ICON，用 `LoadImage` 按需要的尺寸加载对应帧。
  - 窗口句柄创建后，按窗口 DPI 取 `SM_CXSMICON` / `SM_CXICON`（有 `GetSystemMetricsForDpi` 时用它，否则退回 `GetSystemMetrics`）。
  - 分别加载小、大两个图标，用 `WM_SETICON` 设为 `ICON_SMALL` / `ICON_BIG`。
  - WinForms 之后再次发送 `WM_SETICON` 时，替换为这两个句柄；收到 `WM_DPICHANGED` 后重新加载。
- `Form.Icon` 改用系统图标尺寸的帧。空白状态页绘制的 40×40 图标直接用 ico 中的 40 px 帧。
- 句柄释放：先换上新句柄再销毁旧句柄，窗体释放时 `DestroyIcon` 全部句柄。只有资源加载失败、退回 `ExtractAssociatedIcon` 时，才释放该 `Icon` 对象。
- 写法保持 C# 5（.NET Framework 4.8 自带 csc）。
- 其他用到图标的地方已检查：
  - 菜单窗口 `UiMenuForm` 无边框，不在任务栏显示；
  - `MessageBox` 不带窗口图标；
  - 无托盘图标。
- `src/Program.cs` SHA256 `288B17B00700DA2AD042BB15BCA4D760E7A1FE968F13F8F6BF0E30A4CD106341`。

### 产物

- `build.ps1` 输出 `dist-next/LocalWorkspace.exe`（23:19:22）：1,970,688 字节，SHA256 `A992329DF1E7F20770493B5A54A836BA2D6801A1050CA7A914C966DB6FE36D14`。
- 同目录 `dashboard.html` 与项目内 `src/dashboard.html` 一致，SHA256 `1EF21B3E28235EC8772A24E91217146B2C5BCCAFEAB5C935FC4DAEB8F71BD6C8`，与候选 3 相同。
- 内嵌图标资源与候选 3 相同，见候选 3 的“图标帧”。

### 自动化测试

- `npm test`（23:19:27 开始，用时 20 秒，已清除 `WORKSPACE_TEST_EXE`）：共 36 项，通过 35，失败 0，跳过 1（`diagnostics doctor`，需要带官方 Tunnel Client 的发行目录）。
- `native-workbench` 测试用反射构造主窗体（会执行新的图标加载代码），通过。
- 认证 Tunnel 冒烟与公网附件冒烟仍未启用。

### 桌面图标实测（候选 3 与候选 4 对比）

用两个版本的 `LocalWorkspace.exe --preview`（预览模式：不启动隧道，不读取或修改设置）加载同一份样例工作台。

- 显示器：2560×1440，推荐缩放 150%。
- 先在 150% 下测两个版本，再临时切到 100% 测两个版本，最后恢复 150%（已复核）。
- 截图前把窗口置顶。

| 项目 | 候选 3 | 候选 4 |
| --- | --- | --- |
| `WM_GETICON` 小图标 | 32×32（32 px 帧） | 16×16，与 16 px 手调帧逐像素一致 |
| `WM_GETICON` 大图标 | 32×32 | 32×32，与 32 px 帧一致 |
| 100% 标题栏 | 32 px 帧缩成 16 px，笔画发灰 | 16 px 手调帧原样显示，方括号和光标边缘清晰 |
| 150% 标题栏 | 系统把窗口整体拉伸 1.5 倍，图标偏细、偏淡 | 16 px 手调帧拉伸到 24 px，笔画更粗，但有拉伸模糊 |
| 任务栏按钮 | 由大图标缩放，100% 与 150% 均清晰 | 与候选 3 相同 |

- 程序没有声明 DPI 感知，窗口 DPI 始终是 96，所以 150% 下取到的仍是 16/32 px 帧，由系统整体拉伸。20/24 px 手调帧要在程序声明 DPI 感知后才会用于标题栏；代码已按窗口 DPI 取尺寸，届时无需再改图标加载。声明 DPI 感知会影响整个窗口的布局与缩放，不在本版范围内。
- 150% 截图中的任务栏按钮是橙红色：窗口被置前时，系统用闪烁提示代替抢焦点，不是图标问题。
- 截图（`lwp-shots/candidate4/`）：
  - compare-c3-c4：标题栏图标、任务栏按钮放大对比；
  - s100/s150-c3/c4-titlebar、-titlebar-icon、-taskbar-button；
  - s100/s150-c3/c4-wm-geticon-small/big；
  - s100-c4-printwindow：整窗。
- 截图只包含本程序窗口和本程序的任务栏按钮。
- 候选 3 的单独截图见 `lwp-shots/candidate3/`：desktop-window、desktop-titlebar、taskbar-crop、wm-geticon-small/big。

### 验证边界

- 只在本机 Windows 实测 100% 与 150% 缩放，未测 125%/175%、多显示器混合 DPI 和高对比度主题。
- 资源管理器、快捷方式和 Alt+Tab 未单独截图。前两者直接读取 EXE 内对应尺寸的帧；Alt+Tab 使用大图标。
- 其余同候选 3 的“验证边界”。正式发行包哈希在最终打包时补记。

## 2.4.0 候选构建 3 · 2026-10-08

本版在候选构建 2 的基础上只更换应用图标（方括号加光标）及前端对应文件，后端代码不变。

### 输入

- 前端：第四次冻结版。在第三次冻结的 35 个文件之外，新写回 `assets/local-workspace.svg`、`src/dashboard.template.html`（网页图标）、`src/ui/components/sidebar.jsx`（品牌标记）、`src/dashboard.css` 和 `tests/dashboard-ui.test.cjs`。项目内共 37 个文件与登记的 SHA256 逐个核对，37/37 一致；这 5 个文件的修改时间均为 23:06:58，此后多次复核未再变化。
- 图标：`assets/local-workspace.ico` 由定稿 PNG 生成（不是验证用的测试 ico），见下文“图标帧”。
- 构建树与项目的根目录、`src`、`tests`、`scripts`、`vendor`、`assets`、`docs` 共 311 个文件逐个比对，差异 0。

### 产物

- 项目内 `npm run build:ui`（23:10:39）：`src/dashboard.html` 521,878 字节（CSS 39 KB + script 467 KB），SHA256 `1EF21B3E28235EC8772A24E91217146B2C5BCCAFEAB5C935FC4DAEB8F71BD6C8`。
- `build.ps1` 输出 `dist-next/LocalWorkspace.exe`（23:10:49）：1,967,616 字节，SHA256 `FA34203E5592E705FCFBFC03D6441D1DAEB234D771885365FC23482C93923B5B`；同目录 `dashboard.html` 与项目内生成结果哈希一致。体积比候选 2 增加约 94 KB，来自新的多帧图标。

### 图标帧

`assets/local-workspace.ico`：117,191 字节，SHA256 `AF6200F0270D474B43925549D212CE391D926309776792830D287E15BCE6EF78`，共 9 帧，全部为 32 位色深。

| 尺寸 | 格式 | 字节 | 来源 |
| --- | --- | --- | --- |
| 16 | BMP（BGRA + 掩码） | 1,128 | 16 px 手调版 1× |
| 20 | BMP | 1,720 | 20 px 手调版 1× |
| 24 | BMP | 2,440 | 24 px 手调版 1× |
| 32 | BMP | 4,264 | 32 px 手调版 1× |
| 40 | BMP | 6,760 | 20 px 手调版 2× |
| 48 | BMP | 9,640 | 24 px 手调版 2× |
| 64 | BMP | 16,936 | 32 px 手调版 2× |
| 128 | BMP | 67,624 | 母版 4× |
| 256 | PNG | 6,529 | 母版 8×（与源 PNG 字节一致） |

- 解码每一帧与对应源 PNG 逐像素比较，9/9 一致。
- 从候选 3 的 EXE 读取 RT_GROUP_ICON / RT_ICON 资源：1 个图标组、9 帧，尺寸 16–256，每帧字节与 ico 中对应帧一致；由 EXE 资源重组出的 ico 与 `assets/local-workspace.ico` SHA256 相同。候选 2 的 EXE 仍是旧图标。

### 自动化测试

- `npm test`（23:11:15 开始，用时 19 秒，已清除 `WORKSPACE_TEST_EXE`）：共 36 项，通过 35，失败 0，跳过 1（`diagnostics doctor`，需要带官方 Tunnel Client 的发行目录）。
- 用 JUnit 报告复跑一次（23:12:52，18 秒）：19 个顶层测试 +`dashboard` 测试组的 16 个子测试，1 项跳过，0 失败；Node 的 36 项计数包含该测试组本身。
- `tests/dashboard-ui.test.cjs` 单独运行：17 项全部通过（测试组 1 项 + 16 个子测试，Chrome），其中包含品牌标记为 20 px 手调图标的断言。
- 认证 Tunnel 冒烟与公网附件冒烟仍未启用。

### 桌面图标实测

已并入候选构建 4 的“桌面图标实测”对比表。候选 3 通过 `Icon.ExtractAssociatedIcon` 取图标，窗口的大、小图标都是 32 px 帧；这一点在候选 4 中修复。

### WebView2 网页图标与品牌标记

使用候选 3 的程序集和全新 WebView2 用户数据目录：

- 网页图标：`link[rel=icon]` 的 href（3,263 字符）与模板中的新图标一致，路径数据与母版 `local-workspace.svg` 相同；WebView2 `FaviconUri` 与之相同，`GetFaviconAsync` 取得 16×16 PNG。
- 侧栏品牌标记：页面检测到 WebView2（`window.chrome.webview`）时隐藏品牌行，这是设计行为，桌面内嵌时侧栏第一行为“全部对话”。品牌标记元素已加载（20×20），`src` 与 `sidebar.jsx` 中的 20 px 手调版一致。为了在 WebView2 中查看，检查时临时移除页面的 `embedded` 类：品牌行显示在 (16,16)，标记 20×20，截图 wv-brand-visible-1400 与 4 倍放大图 wv-browser-brand-x4。
- 截图：wv-embedded-1400、wv-brand-visible-1400、wv-browser-brand-x4、wv-browser-sidebar-top、embedded/browser-favicon。

### 验证边界

- 未运行认证 Tunnel 冒烟和公网附件冒烟；没有在真实 ChatGPT 或 Codex 中刷新并加载 23 个工具，README 中的 Codex 推荐配置未做实机验证。
- 新版 SDK 的基础调用在系统现有 Runtime 147.0.3912.86 上通过；Runtime 升级受系统安装器限制，详见 [2.4.0 发行说明](docs/RELEASE-2.4.0.md#验证及上游限制)。
- `docs/images/` 截图由前端按 2.4.0 界面重拍，文件名保持不变。
- 没有 commit、push、tag 或发布；没有替换正式 `dist/`。正式发行包哈希在最终打包时补记。

## 2.4.0 候选构建 2 · 2026-10-08

### 输入

- 前端：第三次冻结版。项目内 35 个文件（`src/ui` 33 个 + `src/dashboard.css` + `tests/dashboard-ui.test.cjs`）与冻结时登记的 SHA256 逐个核对，35/35 一致。
- 后端：23 个工具及弃用旧名兼容；宿主底色 `#f7f7f7`（`UiKit` `Theme.Window` 与 WebView2 `DefaultBackgroundColor`）；工作台缩放与 `settings.json` 的 `Zoom`。
- 构建树（临时目录）与项目的根目录、`src`、`tests`、`scripts`、`vendor`、`assets`、`docs` 共 311 个文件逐个比对，差异 0。

### 产物

- 项目内 `npm run build:ui`（22:58:51）：`src/dashboard.html` 518,232 字节（CSS 39 KB + script 466 KB，esbuild 目标 chrome120），SHA256 `65FEF18272AF9A0702A66ECB26B8E647FEC9237D12250223EAF15C29FF356259`。
- `build.ps1` 输出 `dist-next/LocalWorkspace.exe`（22:59:01）：1,870,848 字节，SHA256 `3FDFA8FD55B0AAC36C5C63C658755FCB70044AB40AD1743070CF77581E88EBD3`；同目录 `dashboard.html` 与项目内生成结果哈希一致。
- 本版取代候选构建 1（`24C43481…`，宿主底色仍为旧值）和改底色后的中间构建（`A268D631…`），自身又被候选构建 3 取代。

### 自动化测试

- `npm test`（22:59:13 开始，用时 19 秒，已清除 `WORKSPACE_TEST_EXE`）：13 个测试文件，共 36 项（20 个顶层测试 + `dashboard-ui` 的 16 个子测试）；通过 35，失败 0，跳过 1。
  - 跳过项：`diagnostics doctor`，需要带官方 Tunnel Client 的发行目录。
  - `tunnel.test` 没有设置认证冒烟开关时内部跳过认证冒烟，但被 Node 计为通过；公网附件冒烟未启用。
- `tests/dashboard-ui.test.cjs` 单独运行：16/16 通过（Chrome）。
- `log-cleanup`：`#footer` 含 2.4.0（“诊断连接”行右侧的 v2.4.0）、点击 `#diagnostics`、`#threads .thread b` 断言均通过，未修改断言。
- 原生工作台证据：`embedded_dashboard_loaded=true`、`desktop_log_counters_cleared=true`、footer 为 `v2.4.0`，SDK 1.0.4258.31，Runtime 147.0.3912.86。
- 本轮修正两处测试时序问题：`dashboard-media` 与 `reliability` 在改变视口宽度后立即判断检查器是否打开，偶发等不到元素。改为等待两帧布局稳定后再判断，必要时点开检查器，最多重试 3 次；修正后单独各连跑 3 次均通过，再跑全量通过。断言内容未放宽。

### WebView2 实机复验

使用候选 2 的程序集、rich 样例快照和全新 WebView2 用户数据目录，由独立检查程序驱动原生 WebView2 控件。

- 宿主：窗口底色 `#f7f7f7`，`DefaultBackgroundColor` `#f7f7f7`，`PreferredColorScheme` Light，导航地址带 `?host=desktop`。
- 启动帧：开始导航后 0.06–0.41 秒，宿主区域整片为 `#f7f7f7`，没有白屏；约 0.57 秒页面绘出，对话栏 `#f7f7f7`、主区 `#fff`。这几张屏幕截图受 DPI 缩放影响有偏移，只用于判断颜色；顶部一个采样点为 `#f3f3f3`，落在系统标题栏 / 工具栏一带，未作为页面颜色依据。
- 页面颜色：`html` 外壳 `rgb(247,247,247)`，`.sidebar` `rgb(247,247,247)`，`.main-sheet` `rgb(255,255,255)`。
- 侧栏按钮：`#collapse` 在标题栏、`#title` 左侧（1400 px 宽时 x=287，标题 x=323，同一行），不在侧栏内。点击后侧栏变为 52 px、按钮文字变为“展开侧栏”，再点恢复 260 px。≤720 px（图标栏）时不显示该按钮。
- 版本号：`#footer` “v2.4.0” 与 `#diagnostics` “诊断连接” 在同一行（y 相同）；侧栏收起或图标栏时隐藏版本号，只留图标。
- 断点：1280 px 默认停靠并打开检查器；1279、1100 px 默认收起；1000 px 打开后为抽屉（宽 440 px）；700 px 对话栏为 52 px 图标栏。
- 缩放 125%：`ZoomFactor=1.25`，CSS 视口宽 1120 px（1400 ÷ 1.25），`devicePixelRatio` 1.25；复位后为 1。
- 改动汇总卡：8 个文件时先列 5 个，并显示“显示其余 3 个文件”，与设计规则一致（超过 6 个文件时折叠，展示前 5 个）。
- 截图：startup-00…05、startup-11、wv-wide-1400、wv-1400-sidebar-collapsed、wv-1100、wv-1000-drawer、wv-700-rail、wv-zoom-125（验证工作区，不进入仓库）。

### 缩放重启记忆（真实桌面程序）

在改底色后的中间构建上运行真实 `MainForm`（候选 2 的缩放代码与之相同）：

- 预检：没有运行中的 LocalWorkspace 进程，单实例互斥量空闲。先备份真实 `%LOCALAPPDATA%\LocalWorkspacePlugin\settings.json`（`settings.json.bak-20261008-225240`）。
- 第一次启动：在实时工作台页签按 Ctrl+加号到 110%，再 Ctrl+滚轮到 125%；`settings.json` 随即写入 `"Zoom":1.25`。
- 第二次启动（新进程）：宿主读取的 `Zoom` 与 WebView2 `ZoomFactor` 均恢复为 1.25，底色 `#f7f7f7`。
- 边界值（同一检查程序）：125% 时再放大为 150%；写入 5 被限制为 2.0（200%），写入 0.1 被限制为 0.67（67%）；复位为 1.0。
- 结束后还原 `settings.json`，SHA256 `6FD96BEB9603A0D4DF0D03F72106B705FCFF16ECB96504EA7239C9C939C2E2E1` 与备份前一致。副作用：真实 WebView2 用户数据目录（`data\webview2`）留下了检查用本地地址 127.0.0.1:8294 的页面本地存储（localStorage），不影响 `settings.json` 和凭据。

### 候选构建 1 的补充实测（同一前端布局，宿主底色为旧值）

- 1400 px：检查器停靠，默认 360 px；拖动左边缘向左 120 px 后为 480 px，恢复后 360 px。
- 1100 px 默认收起，打开后停靠 360 px；1099 px 与 1000 px 默认收起，1000 px 打开后为 440 px 抽屉。
- 字体：页面字体栈为 `"Segoe UI Variable Text", "Segoe UI", "Microsoft YaHei UI", system-ui, sans-serif`；中文标题实际使用 Microsoft YaHei UI Bold。

候选 2 的测试边界与候选 3 相同（见上）；候选 2 尚未更换应用图标。

## 历史验证记录

以下记录保留原内容，只统一标题格式，并把一段英文记录译为中文。其中涉及“每类调用一种主色”“浅 / 深色”“跟随最新”“仍从磁盘恢复对话和计划”等内容，描述的是当时的界面与行为，2.4.0 起已移除或改变。2.3.1、2.3.2 为 2.3.0 之后的本地修复构建，其改动已并入 2.4.0。

## 2.3.2 · 2026-10-08 · 历史对话、计划和验证日志完整清理（本地修复，已并入 2.4.0）

- 根因：2.3.1 已清空调用正文，但仍从 threads.bin 和 plans.bin 恢复侧栏对话及执行计划；只检查空时间线没有覆盖用户看到的完整工作台。
- 对话展示、计划及验证记录改为当前 MCP 进程内状态。启动实际删除 activity.bin、activity-state.bin、plans.bin、threads.bin；旧对话仅迁移 Id、HostKey、ChatId 三个身份字段到 thread-bindings.bin，原标题、目录及创建时间不保存。保留请求防重和文件撤销归属。
- 已编译并通过 Apply-Update.ps1 更新正式 dist/LocalWorkspace.exe。dist 与 dist-next 均为 1,708,032 字节，SHA256 均为 `057B88CF009123E1264F88B6BB59314E2AA03FBACF8482D1A9B2F5E13A275D09`。旧 EXE 回滚副本为 dist/LocalWorkspace.before-2.3.2.exe。
- 针对正式 EXE 的完整回归：26 项通过，0 失败。覆盖旧文件实际删除、空侧栏和空计划的真实浏览器渲染、正常退出、异常退出、多次重启、当前调用和证据、100 条上限、重启后旧命令禁止误重跑、文件撤销和对话归属保护。
- 真实 state-v1 目录确认空闲后，用正式 MCP 做两次独立启动：版本均为 2.3.2，对话、计划、调用、命令均为 0。原 activity-state.bin（32,614 字节）、plans.bin（15,446 字节）、threads.bin（2,774 字节）已删除，activity.bin 同样确认不存在。changes.bin、requests.bin、settings.json 前后哈希一致。
- 已查看真实状态目录清理后的浏览器截图，旧侧栏及 19/26 计划消失。必要交付证据保留在 work/log-cleanup-live.png、work/log-cleanup-live-result.json；临时验证脚本和隔离状态截图已清理。
- 未启动 Tunnel 或桌面、未重启现有服务；认证 Tunnel 和公网附件冒烟仍未启用。旧计划不再恢复，继续任务须重新登记计划与当前验证。

## 2.3.1 · 2026-10-06 · 日志退出清理（本地修复，已并入 2.4.0）

- 根因：2.3.0 提交 `6c29708` 把包含 Preview/Detail 的调用时间线写入 activity.bin，并在启动时恢复，与桌面菜单的退出清空说明冲突。实际旧桌面进程于本地时间 19:10 启动，截图却显示前一天 23:34 的调用。
- 调用正文留在进程内，最小验证状态独立保存到 activity-state.bin；启动先迁移旧状态，成功保存后删除 activity.bin，不恢复旧时间线。任务、对话、撤销和请求去重存储不删除。全量回归发现命令失败在重启前后可能分别使用 command/session 与 activity ID，新增固定夹具验证同一失败映射，拒绝无关失败的恢复记录。
- 系统 .NET Framework 编译成功。清空仅测试子进程继承的 Tunnel 环境后执行完整 npm test：26 项，25 通过、0 失败、1 跳过。新增日志回归涵盖旧文件迁移、正文不落盘、启动空时间线、正常退出、异常退出、多次重启、100 条上限、旧证据及明确失败恢复保留。
- 已执行 Apply-Update.ps1 -StageWhileRunning，正式 dist/LocalWorkspace.exe 与 dist-next 构建一致：1,708,032 字节，SHA256 `4BA0B502F9038F1117127852B66E99ED194B3FF1A1D87600CEBF7A6186DAB6C8`。针对正式 dist EXE 再运行 log-cleanup/reliability：6 项全部通过。
- 旧桌面 PID 12984、MCP PID 21568 及创建时间保持不变，没有停止或重启现有连接。旧映像备份为 dist/LocalWorkspace.running-20261006-194038.exe；保留给正在运行的旧进程及回滚。
- 生效边界：正式磁盘文件已更新，但当前桌面仍在运行 2.3.0；真实 state-v1/activity.bin 要等旧程序退出、2.3.1 首次启动时才自动迁移清理。本次重启测试全部使用独立临时状态，没有清空用户真实任务或历史数据。只刷新工作台页面不能激活后端修复。
- 测试边界：dist-next 缺少官方 Tunnel Client 的 doctor 集成项跳过；认证 Tunnel 冒烟及公网附件下载未启用。没有将本地测试表述为真实 ChatGPT 重连验收。

## 2.3.0 · 2026-09-30

- 最终源码使用系统 .NET Framework 编译到 `dist-next/LocalWorkspace.exe`，通过 `Apply-Update.ps1 -StageWhileRunning` 放入 `dist/LocalWorkspace.exe`。两个文件均为 1,705,984 字节，SHA256 均为 `768A48B055660B6D5B121B29CDD4D693D13C57F70C4195C447E4B2940B4E0EA4`。
- 设置 `WORKSPACE_TEST_EXE` 指向正式 dist EXE 后运行完整 `npm test`：23 项通过、0 失败。实际 MCP 返回 2.3.0、28 个工具；legacy、modern、MRTR、长任务、文件与补丁、图片、附件契约和诊断回归通过。
- 新增验证：同请求不重复执行；参数冲突拒绝；进程重启后的旧请求不重跑；字符分页、末尾读取与缓冲缺口；文件哈希冲突、预览不写入、精确匹配失败；单文件与整组撤销的外部修改保护；真实重启后对话、计划、证据与文件历史恢复；未确认完成的命令标为需核对。
- 实际 Git 仓库夹具验证工作区/上次审阅基准、非忽略的新文件与外部编辑；正常 index 原始字节与 HEAD 均未变化。验证 Windows 凭据读写使用独立随机目标和合成 key，结束后删除该测试凭据，没有读取用户密钥。
- 实际 Chromium 验证任务详情、文件恢复记录、活动证据、预览和浅深色布局；窄屏标题不会竖排，页面无横向溢出。人工查看渲染并修复标题压缩问题。`docs/images/dashboard-reliability.png` 为隔离数据交付截图。
  - 2.4.0 注：该文件现已换成 2.4.0 界面的样例截图（`--sample=rich`），仍用隔离数据制作，不含真实对话或用户文件。
- 所有测试 MCP 进程使用独立 `WORKSPACE_STATE_DIR`。测试子进程、浏览器和临时目录已清理；早期失败留下的 `workspace-reliability-fSkwNM` 目录也已删除并核验不存在。
- 发布前后原桌面 PID 4376、Tunnel PID 1104、MCP PID 21076 保持不变，未重启连接。必要旧映像备份保留在 `dist/LocalWorkspace.running-20260930-004043.exe`，用于旧进程继续运行和回滚；不是新版本运行证据。退出旧程序后再启动正式 EXE，新后端与凭据迁移才生效。
- 验证边界：认证 Tunnel 冒烟由开关关闭（该测试文件仍被 Node 汇总为通过），公网附件下载冒烟未启用。没有声称真实 ChatGPT 已刷新并加载 28 个工具。此前 2.2.x 进程中的内存计划无法由新版本追溯恢复。

## 2.0.2 · 2026-09-20

- 最终源码已重新编译到 `dist/LocalWorkspace.exe`，并针对该 EXE 运行全量 `npm test`：17 项通过，0 失败。认证 Tunnel 冒烟仍按默认开关跳过，未重启或抢占现有 Tunnel。
- EXE SHA256：`E95063B92A7FFEDB06419FEAD7C224A70D2CECE851B9125D505CDA8916ABD849`。
- 新增实际 MCP + 浏览器集成验证：读取图片原始字节与预览完全一致，文件后续改写不影响历史预览，100 条淘汰及失效提示正确，快照不携带图片 Base64。
- 工作区状态回执实际包含版本、路径、运行命令、登记目录及 24 个工具；浏览器已展开验证。
- 启用 `WORKSPACE_TEST_OPEN=1` 后，实际点击目录和包含中文、空格、`#` 的文件路径，再通过 Windows Shell 查询确认目录打开及文件选中；本次创建的资源管理器测试窗口已关闭。文件定位使用 Explorer 视图 API，并回读选择结果。
- 同源/Host/POST/令牌检查、非法地址与不存在路径均有回归。路径点击不会执行脚本或程序。
- 实际 Chromium 中检查浅深色、1440/1000/640 宽度、原始尺寸/适应窗口、键盘焦点；另有原工作台 1920/1366/640 布局回归。README 新增两张示例数据截图，已人工查看。
- 当前已运行的旧实例未重启；文件更新不代表旧进程已加载新版。完成当前任务后重新启动程序才能使用新增后端功能。

## 1.3.0 · 2026-09-16

Windows x64。

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

## 应用图标与重复启动（1.3.0）

EXE 内嵌多尺寸 assets/local-workspace.ico，并保留可编辑的 SVG。WinForms 预览确认窗口图标后退出。重复启动改为通知已有窗口显示 / 还原，不再弹出“已在运行”对话框。按用户要求停止了全部插件进程，未重启连接；本条覆盖上面的运行状态快照。临时预览图和已停止的会话目录已删除并确认不存在。

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
