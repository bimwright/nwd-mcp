<!-- mcp-name: io.github.bimwright/nwd-mcp -->

<p align="center">
  <img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/nwd-mcp.png" alt="nwd-mcp" width="180" />
</p>

<h1 align="center">nwd-mcp</h1>

<p align="center">
  <a href="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#能力--架构"><img src="https://img.shields.io/badge/Navisworks-2022--2027-2D9B9B" alt="Navisworks 2022-2027" /></a>
  <a href="#工具面"><img src="https://img.shields.io/badge/MCP-33%20tools-6C47FF" alt="MCP tools" /></a>
</p>

<p align="center">
  <a href="README.md">English</a> · <a href="README.vi.md">Tiếng Việt</a> · 简体中文 · <a href="README.ja.md">日本語</a>
</p>

`nwd-mcp` 是一个面向 **Autodesk Navisworks Manage** 自动化的专业级 Model Context Protocol（MCP）gateway。它让 AI agent 能够通过本地 stdin/stdout 查询、检查、导航并脚本化 Autodesk Navisworks Manage 桌面会话。

---

## 能力 & 架构

- **支持的宿主：** 仅支持 Autodesk Navisworks Manage。（Freedom 和 Simulate 不支持。）
- **支持的版本：** 2022、2023、2024、2025、2026 和 2027。
- **双进程模型：** 轻量级 `.NET 8` 控制台 server 与一个按版本拆分的进程内 plug-in 通信。有线传输在每一个受支持年份（2022–2027）均为 **TCP NDJSON over loopback**；六个 plug-in shell 都面向 `.NET Framework 4.8` / `net48`。（与 bimwright 家族其他成员不同，nwd-mcp **不**使用 Named Pipe 传输 —— loopback-TCP 约定在所有版本上统一适用。）
- **安全优先：** 每会话随机加密 token 校验、TCP 传输仅绑定 loopback，以及对返回给模型的错误消息中的绝对文件路径进行净化。
- **多实例路由：** 网关绑定到一个 Navisworks 实例（以 PID 和进程启动时间识别），绝不会自行切换到其他实例。存在多个匹配实例且尚未选择时，命令返回 `AMBIGUOUS_TARGET`。已绑定的实例退出后，命令返回 `TARGET_LOST` 或 `TARGET_CHANGED`（未发送），直到你用 `nwd_switch_target` 确认目标。选择器为 `pid:<n>`、`id:<target_id>`、年份 2022–2027 或 `auto`。运行多个代理或同年份的多个实例时，请在会话开始时调用 `nwd_list_available_targets`（显示 `pid` 和 `window_title`），再用 `nwd_switch_target("pid:<n>")` 固定。年份或 `auto` 不会移动已有绑定（`TARGET_REBIND_REQUIRED`），离开一个仍在运行且显式选择的实例需要 `rebind=true`。`nwd_list_available_targets` 和 `nwd_get_current_target` 不会选择目标。写入只在已确认的文档上运行：先与用户确认报告的文件就是要修改的文件，然后调用一次 `nwd_switch_target("pid:<n>", verify=true)`。在此之前写入返回 `TARGET_UNCONFIRMED`；如果确认后打开了其他文件，则返回 `DOCUMENT_CHANGED`（命令未执行）。`nwd_open_file` 会把确认移到它打开的文件。服务器与插件来自不同版本时，写入返回 `PAIRING_REQUIRED`；读取仍可用。
- **Navisworks 忙碌时：** 当模态对话框正在等待用户，或 UI 线程一直忙到命令的开始期限时，命令返回 `HOST_BUSY`：命令未开始，之后也不会执行，因此 Navisworks 空闲后可以重新发送。`TIMEOUT` 现在只表示命令已经开始但结果未知：重新发送写入前请先检查。`nwd_health_check` 在 Navisworks 忙碌时仍会应答，并返回 `host_busy` 和 `busy_reason`（`modal_dialog` 或 `ui_thread_busy`）。

Navisworks 为 MCP 命令显示一张活动卡片。提示默认开启，在最新结果之后停留 20 秒（可选 10、20、30 或 60）。悬停暂停；离开后重新计满该间隔。有意悬停会平滑展开当前卡片的紧凑三行结果视口，每项均有独立边框（工具、结果、成功／失败，以及本地完成时间，采用24小时制 `HH:mm:ss`）。保留该卡片内的所有结果，旧调用在上、新调用在下；初始显示最新三次调用，通过与RVT一致的细滚动条向上滚动可查看更早的调用。移开后平滑收起。新结果会立即追加：位于底部时自动跟随最新调用，向上阅读旧调用时保持滚动位置；回到底部后恢复自动跟随。关闭 branding 也能使用，并遵循减少动态效果设置。有长度上限的摘要仅保留在内存中，关闭或重置卡片时丢弃，不会开启 Record，也不保存原始请求、结果 JSON 或脚本源代码。BIMwright 字标默认关闭，需打开 Show branding 才会出现。`nwd_health_check` 不会进入卡片。**Bimwright** 功能区有 **Toasts**、**Toast Brand**、**Record** 和 **Status**。**Record** 为旧网关提供主机默认设置；当前网关使用下文介绍的服务器调用日志设置。`send_code` 只保存长度和 SHA-256，不保存脚本。设置写在 `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwdmcp.config.json`。`BIMWRIGHT_NWD_ENABLE_TOAST` 和 `BIMWRIGHT_NWD_RECORD_CALLS` 在下次启动时覆盖对应开关。

卡片标题显示 Navisworks 年份和最近一次调用的代理名称（例如 `nwd-mcp 2025 · Claude`）；无论是否开启品牌标识，指向卡片时都会渐显 Navisworks 进程 ID（`· PID 12345`）。旧版网关不发送代理名称，此时标题不显示名称。展开卡片底部或 **Status** 中的 **Pause for today** 会让本机所有 Navisworks 的全部卡片（包括错误）静默到本地午夜。在卡片上该按钮会变为 **Resume**，误点可立即撤销；功能区的 **Toasts** 或重新开启提示也会结束暂停。暂停以本地日期保存在 `toastSnoozedDate` 中，暂停期间 `nwd_report_task_result` 返回 `toast_shown: false`。请同时更新网关和插件。

**Status → 提示位置：**分别选择左／右与上／下（默认左上）。立即应用并保存；更改角落会清除拖动偏移。启用 **Allow dragging the toast** 后可拖动标题行，普通单击仍关闭卡片。关闭拖动会返回所选角落但保留位置，再次启用可恢复。**Reset position** 删除偏移。位置以相对 Navisworks 窗口的 DIP 保存，并限制在所在显示器的工作区内。底部锚定向上展开；空间不足时使用相反方向。动画遵循 Windows 设置。保存失败在 Status 内提示，新设置仍在当前会话生效。工具／结果的工具提示包含已脱敏的结果、状态、完成时间及可用的执行时长。暂不协调不同应用之间的提示重叠。

---

## 当前状态

| 组件 | 状态 |
|---|---|
| MCP gateway server（.NET 8） | ✅ 编译无警告（Debug + Release） |
| 单元测试（155 个 xUnit） | ✅ 全部通过 |
| Plug-in handler 实现 | ✅ 已在真实 Navisworks Manage 会话中验证 |
| Plug-in 项目（net48） | ✅ 可针对 Navisworks Manage SDK 编译 |

> **注意：** plug-in handler 层使用真实的 Navisworks .NET API 调用（并非桩函数或伪造数据），并且已在真实的 Navisworks Manage 实例中执行验证。

---

## 安装

从 [GitHub Releases](https://github.com/bimwright/nwd-mcp/releases/latest) 下载 `NwdMcp.Setup-*-win-x64.zip`。v1.0.0 含 Manage **2025** 插件。解压后运行 `install.ps1`，MCP 指向已安装的 `nwd-mcp.exe`。NuGet 包 `Bimwright.Nwd.Server` 只包含 server：不含 Navisworks 插件。请从安装 ZIP 安装；仅在已从 ZIP 安装好插件、且想把 server 用作全局 .NET 工具（命令 `bimwright-nwd`）时才使用 `dotnet tool install -g Bimwright.Nwd.Server`。

常用开关：`--read-only` / `BIMWRIGHT_NWD_READ_ONLY=1`。

---

## 工具面

默认启动注册全部 toolset，共 **35 个工具**。`--read-only` 为 **25 个**。每个工具都使用 `nwd_*` 前缀。

### 1. 目标/元工具（5 个）

* `nwd_list_available_targets` —— 列出所有已发现的活跃 Navisworks 会话。
* `nwd_get_current_target` —— 报告 server 当前指向的会话。
* `nwd_switch_target` —— 将 gateway 指向另一个活跃的会话；`verify=true` 还会为写入确认当前打开的文档。
* `nwd_report_task_result` —— 完成工作后报告一次（completed、failed 或 cancelled，一行摘要，可选 Markdown `body`）：在 Navisworks 提示窗显示卡片并保存报告。由代理自行申报，并非验证结果。修改文档的写入响应会带有 `_report`，直到你报告为止。
* `nwd_get_report_history` —— 读取 `%LOCALAPPDATA%\Bimwright\nwd-mcp\reports` 中保存的报告，按时间从新到旧；指定 `report_id` 时返回该报告全文。

### 2. 文件工具（3 个）

* `nwd_list_recent_files` —— 按文件菜单顺序，列出当前 Manage 年份的最近文件。
* `nwd_open_file` *(写入)* —— 打开一个文件。当前文件有未保存更改时拒绝，除非 `discard_changes` 为 true。
* `nwd_import_model` *(写入)* —— 把模型导入当前打开的文档。`append`（默认）作为独立模型加入；`merge` 则合并进去。

### 3. 查询/读取工具（8 个）

* `nwd_health_check` —— 检查主机／文档状态、运行中服务器／插件的版本、构建ID和一致状态。默认离线；`check_updates=true` 将两个组件与官方稳定版比较。没有主机时仍返回服务器身份（`ok:false`、`host_connected:false`）。
* `nwd_get_document_info` —— 获取当前文档名称、文件路径与模型数量。
* `nwd_get_model_statistics` —— 获取图元、模型与选择集的数量统计。
* `nwd_get_model_tree` —— 获取有边界限制的模型树节点层级。
* `nwd_get_item_properties` —— 获取某个图元的属性分类与属性列表。
* `nwd_batch_get_properties` —— 一次性获取多个图元的属性。
* `nwd_find_items` —— 使用高级属性/分类过滤器查询图元。
* `nwd_find_items_by_name` —— 按显示名称搜索图元。

### 4. 选择工具（3 个）

* `nwd_get_current_selection` —— 获取当前用户选择的图元 ID。
* `nwd_clear_selection` *(写入)* —— 清除当前选择。
* `nwd_select_items_by_search` *(写入)* —— 选择匹配属性/名称过滤器的图元。

### 5. 选择集工具（3 个）

* `nwd_list_sets` —— 获取选择集与搜索集，递归遍历文件夹。
* `nwd_get_selection_set_items` —— 获取某个集合内的图元。
* `nwd_execute_search_set` *(混合)* —— 执行搜索集；可选地选中匹配项。

### 6. 视点/导航工具（4 个）

* `nwd_list_viewpoints` —— 枚举已保存的视点与文件夹。
* `nwd_get_current_viewpoint` —— 获取当前相机与视图状态。
* `nwd_goto_viewpoint` *(写入)* —— 将视口相机导航到已保存的视点。
* `nwd_save_viewpoint` *(写入)* —— 将当前视图保存为命名的视点。

### 7. 可见性工具（2 个）

* `nwd_hide_items` *(写入)* —— 隐藏/显示指定的图元。
* `nwd_unhide_all` *(写入)* —— 把所有隐藏的图元恢复为可见。

### 8. 逃生舱脚本（1 个）

* `nwd_send_code` *(写入)* —— 在 Navisworks UI 线程上运行 C# 脚本，`doc` 为当前文档。最后一个表达式作为 `result` 返回，控制台输出作为 `stdout` 返回；脚本运行期间 Navisworks 会等待。

### 9. ToolBaker 治理工具（6 个）

* `nwd_list_baked_tools` —— 列出所有已验证、已编译的可复用工具。
* `nwd_run_baked_tool` *(写入)* —— 按名称并携带参数运行一个已接受的烘焙工具。
* `nwd_list_bake_suggestions` —— 列出自适应工作流建议。
* `nwd_accept_bake_suggestion` *(写入)* —— 验证、编译并部署一个建议的工具。
* `nwd_dismiss_bake_suggestion` *(写入)* —— 忽略/暂缓一个活跃建议。
* `nwd_create_bake_issue_draft` —— 为所请求的工具生成一份 GitHub issue 草稿。

---

## 运行时 health 与更新

选择目标后，在会话开始时调用一次 `nwd_health_check(check_updates=true)` 以发现新的稳定版。网络不可用或不被允许时，使用默认离线检查并保留not checked/unknown状态。之后的常规探测保持离线。`setup_gateway`／`setup_plugin` 描述已加载的程序集；SHA-256只观察捕获时磁盘上的文件，不能证明内存已重新加载或发布者可信。缺少身份信息的旧插件将版本／构建证据报告为 `unknown`。主机探测使用UI线程，Navisworks忙时可能超时。

`check_updates=true` 在总计5秒的网络预算内查询一个官方稳定版及其标签修订。分别检查 `update.components.server` 和 `.plugin`：版本相同并不足够，缺少证据仍为 `unknown`。`checked` 只确认发布查询成功。各组件的 `update_available` 比较语义版本；false或null通知不能证明发布身份一致。源修订比较不验证制品字节。

检查 `update.notification` 并遵循 `agent_action`。`NEW_RELEASE_AVAILABLE` 表示新版本；`RELEASE_REVIEW_REQUIRED`／`RELEASE_IDENTITY_UNVERIFIED` 要求证据审查；`LOCAL_VERSION_AHEAD` 仅说明本地版本较新；`UPDATE_CHECK_UNAVAILABLE` 通过 `lookup_reason` 报告外部查询失败，不意味着本地运行时故障，也不请求安装授权。保留 `protected_components` 并解释 `protected_component_reasons`，不得降级较新的本地组件。`unobserved_components` 需要连接／运行时观察，不能推断主机已关闭或构建损坏。`identity_observed` 和 `comparison_reason` 区分运行时证据不足与发布证据不可用。

`update_guidance.mode` 为 `check_only`、`retry_check`、`review`、`none` 或 `prepare_update`。只有 `bundle_update_proposal_allowed=true` 的 `prepare_update` 返回安装步骤并需要授权；可复用已有授权。其他模式只检查／报告／审查。较新的本地组件、未确认的另一组件、标签查询失败或未观察到的另一组件都会阻止完整ZIP安装建议。health不会下载软件包、安装、重启或保存／关闭文档。请遵循 [AGENTS.md](AGENTS.md) 的预览、授权、备份与验证流程；当前安装器不提供bundle+server原子回滚。不得自动替换开发构建。

重启Navisworks并重新连接客户端后，在**同一连接**上将新的 `initialize.serverInfo` 和 `tools/list` 的元数据／模式／注解与 `client_verification` 比较。服务器无法证明客户端已刷新缓存；没有客户端证据时仍为未验证。

## 安全配置

### 只读模式

可通过 `--read-only` 标志或 `BIMWRIGHT_NWD_READ_ONLY=1` 环境变量强制开启严格只读模式。

- 只注册标注为只读的工具。临时界面工具 `nwd_goto_viewpoint`、`nwd_clear_selection`、`nwd_select_items_by_search` 保留（它们只改变视图或选择，不改变模型）。`nwd_switch_target` 和所有写入工具会被移除；请在启动时用 `--target pid:<n>` 固定目标。
- `nwd_execute_search_set(select=true)` 仍会选中匹配项：与上面的临时界面工具一样，它只改变选择集，不改变模型。
- 只读工具面恰好为 **25 个工具**。`nwd_list_recent_files` 保留；`nwd_open_file` 和 `nwd_import_model` 会被移除。

### 工具调用记录（Record）

调用日志**默认关闭**。`--enable-call-log` 将全部 MCP 调用（包括本地工具和失败）记录到 `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwd-mcp-calls.jsonl`；转发到插件的命令还会在同目录的 `mcp-calls.jsonl` 中记录原生结果。`--disable-call-log` 阻止此网关创建或追加这两个日志。MCPB 的 `call_log` 默认 false，并传递同样的开关参数。

优先顺序为 CLI → `BIMWRIGHT_NWD_ENABLE_CALL_LOG` → 服务器 JSON 的 `enableCallLog` → false。匹配版本的插件按每个请求的选择覆盖功能区/Status 的 **Record**、已保存的 `recordCalls` 和 `BIMWRIGHT_NWD_RECORD_CALLS`，不会修改这些设置或其他网关。旧网关未传递选择时仍使用主机开关；旧插件不识别此覆盖，因此应使用匹配的构建。`send_code` 参数只保存 `code_length` 和 `code_sha256`，不保存脚本正文。

### 权限与 auto mode
除 `nwd_send_code` 外，每个工具都声明了 MCP 注解（`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`）。除可选更新检查会访问GitHub的 `nwd_health_check` 外，open-world均为false；health仍为只读、非破坏性工具。Claude Desktop 据此处理：只读工具允许一次即可，破坏性工具（`nwd_open_file`, `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, `nwd_dismiss_bake_suggestion`）每次都会询问。

Claude Code 的 auto mode 会用分类器检查每次 MCP 调用。若要让只读工具跳过此检查，请把它们加入 `.claude/settings.json` 的 `permissions.allow`（如果你的 MCP 条目名称不是 `nwd-mcp`，请相应替换）：

```json
{
  "permissions": {
    "allow": [
      "mcp__nwd-mcp__nwd_list_recent_files",
      "mcp__nwd-mcp__nwd_list_available_targets",
      "mcp__nwd-mcp__nwd_get_current_target",
      "mcp__nwd-mcp__nwd_report_task_result",
      "mcp__nwd-mcp__nwd_get_report_history",
      "mcp__nwd-mcp__nwd_health_check",
      "mcp__nwd-mcp__nwd_get_document_info",
      "mcp__nwd-mcp__nwd_get_model_statistics",
      "mcp__nwd-mcp__nwd_get_model_tree",
      "mcp__nwd-mcp__nwd_get_item_properties",
      "mcp__nwd-mcp__nwd_batch_get_properties",
      "mcp__nwd-mcp__nwd_find_items",
      "mcp__nwd-mcp__nwd_find_items_by_name",
      "mcp__nwd-mcp__nwd_get_current_selection",
      "mcp__nwd-mcp__nwd_clear_selection",
      "mcp__nwd-mcp__nwd_select_items_by_search",
      "mcp__nwd-mcp__nwd_list_sets",
      "mcp__nwd-mcp__nwd_get_selection_set_items",
      "mcp__nwd-mcp__nwd_execute_search_set",
      "mcp__nwd-mcp__nwd_list_viewpoints",
      "mcp__nwd-mcp__nwd_get_current_viewpoint",
      "mcp__nwd-mcp__nwd_goto_viewpoint",
      "mcp__nwd-mcp__nwd_list_baked_tools",
      "mcp__nwd-mcp__nwd_list_bake_suggestions",
      "mcp__nwd-mcp__nwd_create_bake_issue_draft"
    ]
  }
}
```

**不要**使用 `mcp__nwd-mcp__*`：通配符会让 `nwd_send_code` 和所有写入工具都跳过检查。请在存放模型的项目文件夹中启动 agent，并在要求打开或修改文件时写明具体文件名。

### ToolBaker 持久化

ToolBaker 的 sqlite 存储（`bake.db`）与使用审计日志（`audit.jsonl`）会持久化在本地以下路径：

```text
%LOCALAPPDATA%\Bimwright\nwd-mcp\baked\
```

### 响应大小

工具结果为紧凑 JSON。超过 64 KiB 时服务器在结果中加入 `_response_warning` 字段（超过 256 KiB 时警告更强）；超过 1 MiB 预算时结果被拒绝并返回 `RESPONSE_TOO_LARGE`——已完成的写入工具除外，它会返回压缩后的成功摘要（`response_compacted`、`mutation_applied`），而不是误报失败。
四个工具接受 `output=file`，将完整结果写入本机文件并返回其路径、schema 和预览：`nwd_get_model_tree`（JSON）、`nwd_batch_get_properties`（SQLite）、`nwd_find_items_by_name`（NDJSON）和 `nwd_run_baked_tool`（自动检测格式）。`nwd_send_code` 没有 `output` 参数；超过 1 MiB 的输出会以同样方式自动落盘。
Spill 文件位于 `%LOCALAPPDATA%\Bimwright\nwd-mcp\spill`，卸载时删除。每个文件自写入起保留 36 小时。文件只会因超过保留时间而被删除，没有文件数量上限。可通过 `--spill-retention-hours <n>`（1–8760）、`BIMWRIGHT_NWD_SPILL_RETENTION_HOURS` 或服务器 JSON 的 `spillRetentionHours` 修改；缺失或无效的值按 36 处理。
响应保护默认开启。`--disable-response-guard`（或 `BIMWRIGHT_NWD_ENABLE_RESPONSE_GUARD=0`、JSON `enableResponseGuard: false`）会关闭警告和 1 MiB 预算。传输上限 `--max-response-bytes`（默认 10 MiB）仍然生效，超过它的结果仍会被拒绝或落盘。`--response-warn-bytes`、`--response-strong-warn-bytes` 和 `--response-budget-bytes`（环境变量 `BIMWRIGHT_NWD_RESPONSE_WARN_BYTES`、`…_STRONG_WARN_BYTES`、`…_BUDGET_BYTES`；JSON `responseWarnBytes`、`responseStrongWarnBytes`、`responseBudgetBytes`）用于设置阈值。各值须不小于 1024，且满足 warn ≤ strong ≤ budget ≤ max，否则服务器不会启动。所有设置的优先级为 CLI → 环境变量 → JSON。

## 基准测试

Navisworks Manage 2025 上全部 35 个工具的耗时、结果大小、估计 token 和大小策略结果：[docs/benchmarks/2026-11-16-navisworks-manage-2025.md](docs/benchmarks/2026-11-16-navisworks-manage-2025.md)。

---

## 本地开发与编译

Autodesk Navisworks API 的 DLL **不**在本仓库中重新发布。

- **Server 与测试：** 可在任意没有 Navisworks 的机器上构建并运行。
  ```powershell
  dotnet test tests\Bimwright.Nwd.Tests\Bimwright.Nwd.Tests.csproj -c Debug
  ```
- **Plug-in 编译：** 无需安装 Navisworks。每个年份的项目都针对该年份的 API 引用程序集（Autodesk 签名）编译，这些程序集来自 NuGet 包 `Speckle.Navisworks.API`，仅在编译时使用，绝不会复制到输出或 bundle 中。
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug
  ```
  `scripts\package-client-setup.ps1` 会把全部六个年份（2022–2027）构建进安装 ZIP。

---

## bimwright 家族

连接 AI 助手与 BIM、CAD 应用的开源工具。

**bimwright** 这个名字由 **BIM** 和 **wright** 组成。wright 是英语中表示制作者或建造者的旧词，如 *shipwright*（造船工）。

- [**rvt-mcp**](https://github.com/bimwright/rvt-mcp) —— Autodesk® Revit®
- [**dwg-mcp**](https://github.com/bimwright/dwg-mcp) —— Autodesk® AutoCAD®
- [**nwd-mcp**](https://github.com/bimwright/nwd-mcp) —— Autodesk® Navisworks®
- [**ipt-mcp**](https://github.com/bimwright/ipt-mcp) —— Autodesk® Inventor®
- [**bim-wiki**](https://github.com/bimwright/bim-wiki) —— 越南语优先的 BIM 知识库

---

## 许可证

Apache-2.0。详见 [LICENSE](LICENSE)。

Navisworks 与 Autodesk 是 Autodesk, Inc. 的注册商标。bimwright 是一个独立的开源项目，与 Autodesk, Inc. 无关联、无赞助、无背书。
