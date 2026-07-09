<!-- mcp-name: io.github.bimwright/nwd-mcp -->

<p align="center">
  <img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/nwd-mcp.png" alt="nwd-mcp" width="180" />
</p>

<h1 align="center">nwd-mcp</h1>

<p align="center">
  <a href="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#capabilities--architecture"><img src="https://img.shields.io/badge/Navisworks-2022--2027-2D9B9B" alt="Navisworks 2022-2027" /></a>
  <a href="#tool-surface"><img src="https://img.shields.io/badge/MCP-29%20or%2030%20tools-6C47FF" alt="MCP tools" /></a>
</p>

<p align="center">
  English · <a href="README.vi.md">Tiếng Việt</a> · 简体中文 · <a href="README.ja.md">日本語</a>
</p>

`nwd-mcp` 是一个面向 **Autodesk Navisworks Manage** 自动化的专业级 Model Context Protocol（MCP）gateway。它让 AI agent 能够通过本地 stdin/stdout 查询、检查、导航并脚本化 Autodesk Navisworks Manage 桌面会话。

---

## 能力 & 架构

- **支持的宿主：** 仅支持 Autodesk Navisworks Manage。（Freedom 和 Simulate 不支持。）
- **支持的版本：** 2022、2023、2024、2025、2026 和 2027。
- **双进程模型：** 轻量级 `.NET 8` 控制台 server 与一个按版本拆分的进程内 plug-in 通信。有线传输在每一个受支持年份（2022–2027）均为 **TCP NDJSON over loopback**；六个 plug-in shell 都面向 `.NET Framework 4.8` / `net48`。（与 bimwright 家族其他成员不同，nwd-mcp **不**使用 Named Pipe 传输 —— loopback-TCP 约定在所有版本上统一适用。）
- **安全优先：** 每会话随机加密 token 校验、TCP 传输仅绑定 loopback，以及对返回给模型的错误消息中的绝对文件路径进行净化。
- **多实例路由：** 自动检测多个正在运行的 Navisworks Manage 实例，并支持动态切换目标。

---

## 当前状态

| 组件 | 状态 |
|---|---|
| MCP gateway server（.NET 8） | ✅ 编译无警告（Debug + Release） |
| 单元测试（45 个 xUnit） | ✅ 全部通过 |
| Plug-in handler 实现 | ✅ 已在真实 Navisworks Manage 会话中验证 |
| Plug-in 项目（net48） | ✅ 可针对 Navisworks Manage SDK 编译 |

> **注意：** plug-in handler 层使用真实的 Navisworks .NET API 调用（并非桩函数或伪造数据），并且已在真实的 Navisworks Manage 实例中执行验证。首次运行清单见 [walkthrough.md](walkthrough.md)。

---

## 工具面

Phase 1 在启用全部 toolsets 时默认提供 **29 个工具**，在同时指定 --toolsets all 和 --enable-send-code 时提供 **30 个工具**。每个工具都使用 `nwd_*` 前缀。

### 1. 目标/元工具（3 个）

* `nwd_list_available_targets` —— 列出所有已发现的活跃 Navisworks 会话。
* `nwd_get_current_target` —— 报告 server 当前指向的会话。
* `nwd_switch_target` —— 将 gateway 指向另一个活跃的会话。

### 2. 查询/读取工具（8 个）

* `nwd_health_check` —— 检查活跃会话状态与心跳。
* `nwd_get_document_info` —— 获取当前文档名称、文件路径与模型数量。
* `nwd_get_model_statistics` —— 获取图元、模型与选择集的数量统计。
* `nwd_get_model_tree` —— 获取有边界限制的模型树节点层级。
* `nwd_get_item_properties` —— 获取某个图元的属性分类与属性列表。
* `nwd_batch_get_properties` —— 一次性获取多个图元的属性。
* `nwd_find_items` —— 使用高级属性/分类过滤器查询图元。
* `nwd_find_items_by_name` —— 按显示名称搜索图元。

### 3. 选择工具（3 个）

* `nwd_get_current_selection` —— 获取当前用户选择的图元 ID。
* `nwd_clear_selection` *(写入)* —— 清除当前选择。
* `nwd_select_items_by_search` *(写入)* —— 选择匹配属性/名称过滤器的图元。

### 4. 选择集工具（3 个）

* `nwd_list_sets` —— 获取选择集与搜索集，递归遍历文件夹。
* `nwd_get_selection_set_items` —— 获取某个集合内的图元。
* `nwd_execute_search_set` *(混合)* —— 执行搜索集；可选地选中匹配项。

### 5. 视点/导航工具（4 个）

* `nwd_list_viewpoints` —— 枚举已保存的视点与文件夹。
* `nwd_get_current_viewpoint` —— 获取当前相机与视图状态。
* `nwd_goto_viewpoint` *(写入)* —— 将视口相机导航到已保存的视点。
* `nwd_save_viewpoint` *(写入)* —— 将当前视图保存为命名的视点。

### 6. 可见性工具（2 个）

* `nwd_hide_items` *(写入)* —— 隐藏/显示指定的图元。
* `nwd_unhide_all` *(写入)* —— 把所有隐藏的图元恢复为可见。

### 7. 逃生舱脚本（1 个）

* `nwd_send_code` *(写入，需 opt-in)* —— 针对 Navisworks API 编译并执行进程内 C# 代码。

### 8. ToolBaker 治理工具（6 个）

* `nwd_list_baked_tools` —— 列出所有已验证、已编译的可复用工具。
* `nwd_run_baked_tool` *(写入)* —— 按名称并携带参数运行一个已接受的烘焙工具。
* `nwd_list_bake_suggestions` —— 列出自适应工作流建议。
* `nwd_accept_bake_suggestion` *(写入)* —— 验证、编译并部署一个建议的工具。
* `nwd_dismiss_bake_suggestion` *(写入)* —— 忽略/暂缓一个活跃建议。
* `nwd_create_bake_issue_draft` —— 为所请求的工具生成一份 GitHub issue 草稿。

---

## 安全配置

### 只读模式

可通过 `--read-only` 标志或 `BIMWRIGHT_NWD_READ_ONLY=1` 环境变量强制开启严格只读模式。

- 所有具备写入能力的 toolsets 都不会被注册。
- 混合工具（例如 `nwd_execute_search_set`）会被修改为强制只读参数边界（`select=false`），并输出一个 `read_only_enforced` 响应标记。
- 只读工具面恰好为 **20 个工具**。

### SendCode 的 opt-in

动态 C# 脚本（`nwd_send_code`）**默认禁用**。MCP server 仅在以 `--enable-send-code` 或 `BIMWRIGHT_NWD_ENABLE_SEND_CODE=1` 启动时才会暴露该工具 —— 这一 server 端开关是阻止该工具被注册的权威控制。plug-in 还会从其环境中读取 `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=1`，作为第二重、有文档记录的 opt-in 信号。

### ToolBaker 持久化

ToolBaker 的 sqlite 存储（`bake.db`）与使用审计日志（`audit.jsonl`）会持久化在本地以下路径：

```text
%LOCALAPPDATA%\Bimwright\nwd-mcp\baked\
```

---

## 本地开发与编译

Autodesk Navisworks API 的 DLL **不**在本仓库中重新发布。

- **Server 与测试：** 可在任意没有 Navisworks 的机器上构建并运行。
  ```powershell
  dotnet test tests\Bimwright.Nwd.Tests\Bimwright.Nwd.Tests.csproj -c Debug
  ```
- **Plug-in 编译：** 需要本地安装 Navisworks Manage。
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug /p:NavisworksInstallDir="C:\Program Files\Autodesk\Navisworks Manage 2026"
  ```
  如果 Navisworks Manage 安装在非默认路径，请覆盖该 hint 属性：
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug /p:NavisworksInstallDir="D:\Autodesk\Navisworks 2026"
  ```

---

## bimwright 家族

为 AEC 工具链亲手打造的 MCP gateway —— 同一套架构，predictable / auditable / reversible：

- [**rvt-mcp**](https://github.com/bimwright/rvt-mcp) —— Autodesk® Revit®
- [**dwg-mcp**](https://github.com/bimwright/dwg-mcp) —— Autodesk® AutoCAD®
- [**nwd-mcp**](https://github.com/bimwright/nwd-mcp) —— Autodesk® Navisworks®
- [**ipt-mcp**](https://github.com/bimwright/ipt-mcp) —— Autodesk® Inventor®
- [**bim-wiki**](https://github.com/bimwright/bim-wiki) —— 越南语优先的 BIM 知识库

---

## 许可证

Apache-2.0。详见 [LICENSE](LICENSE)。

Navisworks 与 Autodesk 是 Autodesk, Inc. 的注册商标。bimwright 是一个独立的开源项目，与 Autodesk, Inc. 无关联、无赞助、无背书。
