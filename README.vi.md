<!-- mcp-name: io.github.bimwright/nwd-mcp -->

<p align="center">
  <img src="https://raw.githubusercontent.com/bimwright/.github/master/assets/logos/nwd-mcp.png" alt="nwd-mcp" width="180" />
</p>

<h1 align="center">nwd-mcp</h1>

<p align="center">
  <a href="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml"><img src="https://github.com/bimwright/nwd-mcp/actions/workflows/build.yml/badge.svg" alt="build" /></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/license-Apache%202.0-blue.svg" alt="license" /></a>
  <a href="#tính-năng--kiến-trúc"><img src="https://img.shields.io/badge/Navisworks-2022--2027-2D9B9B" alt="Navisworks 2022-2027" /></a>
  <a href="#danh-sách-công-cụ-tool-surface"><img src="https://img.shields.io/badge/MCP-33%20tools-6C47FF" alt="MCP tools" /></a>
</p>

<p align="center">
  <a href="README.md">English</a> · Tiếng Việt · <a href="README.zh-CN.md">简体中文</a> · <a href="README.ja.md">日本語</a>
</p>

`nwd-mcp` là cổng kết nối Model Context Protocol (MCP) chuyên nghiệp dành cho việc tự động hóa **Autodesk Navisworks Manage**. Giải pháp này cho phép các trợ lý AI truy vấn, kiểm tra, điều hướng và viết mã kịch bản trực tiếp lên các phiên chạy Navisworks Manage desktop thông qua đầu vào/đầu ra tiêu chuẩn (stdin/stdout).

---

## Tính Năng & Kiến Trúc
- **Ứng dụng Máy chủ Hỗ trợ:** Chỉ hỗ trợ Autodesk Navisworks Manage (Không hỗ trợ Freedom và Simulate).
- **Các Phiên bản Hỗ trợ:** Từ 2022 đến 2027.
- **Mô hình Hai Tiến trình:** Máy chủ điều phối `.NET 8` gọn nhẹ giao tiếp với plug-in chạy trực tiếp bên trong Navisworks theo từng phiên bản cụ thể. Truyền tải dữ liệu là **TCP NDJSON over loopback** cho mọi năm được hỗ trợ (2022–2027); toàn bộ sáu plug-in shell đều nhắm tới `.NET Framework 4.8` / `net48`. (Khác với phần còn lại của gia đình bimwright, nwd-mcp **không** sử dụng truyền tải Named Pipe — quy ước loopback-TCP được áp dụng thống nhất trên tất cả các phiên bản.)
- **Bảo mật Tối đa:** Xác thực qua mã token ngẫu nhiên tạo theo từng phiên, chỉ liên kết với loopback TCP (`127.0.0.1`), và tự động ẩn/lọc đường dẫn tệp tuyệt đối trong các thông báo lỗi trả về cho mô hình AI.
- **Điều hướng Nhiều Phiên chạy:** Gateway gắn với một instance Navisworks, nhận diện bằng PID và thời điểm khởi động process, và không bao giờ tự chuyển sang instance khác. Có nhiều instance khớp mà chưa chọn thì lệnh trả `AMBIGUOUS_TARGET`. Instance đang gắn thoát thì lệnh trả `TARGET_LOST` hoặc `TARGET_CHANGED` (không gửi đi) cho tới khi bạn xác nhận target bằng `nwd_switch_target`. Selector gồm `pid:<n>`, `id:<target_id>`, năm 2022–2027, hoặc `auto`. Khi chạy nhiều agent hoặc nhiều instance cùng năm, gọi `nwd_list_available_targets` (có `pid` và `window_title`) rồi ghim `nwd_switch_target("pid:<n>")` ở đầu phiên. Năm hoặc `auto` không bao giờ dời binding đang có (`TARGET_REBIND_REQUIRED`), và muốn rời một instance còn sống đã chọn tường minh thì cần `rebind=true`. `nwd_list_available_targets` và `nwd_get_current_target` không bao giờ chọn target. Lệnh ghi chỉ chạy trên document đã xác nhận: hỏi lại người dùng rằng file được báo là file cần sửa, rồi gọi `nwd_switch_target("pid:<n>", verify=true)` một lần. Trước đó lệnh ghi trả `TARGET_UNCONFIRMED`; nếu từ lúc xác nhận đã có file khác được mở thì trả `DOCUMENT_CHANGED` (lệnh chưa chạy). `nwd_open_file` dời xác nhận sang file nó mở. Server và plugin khác bản phát hành thì lệnh ghi trả `PAIRING_REQUIRED`; lệnh đọc vẫn chạy.
- **Navisworks bận:** khi có dialog modal đang chờ người dùng, hoặc luồng UI bận tới hạn bắt đầu của lệnh, lệnh trả `HOST_BUSY`: lệnh chưa chạy và sẽ không chạy sau đó, nên gửi lại được khi Navisworks rảnh. `TIMEOUT` giờ chỉ dành cho lệnh đã bắt đầu mà chưa rõ kết cục: kiểm tra trước khi gửi lại lệnh ghi. `nwd_health_check` vẫn trả lời khi Navisworks bận, kèm `host_busy` và `busy_reason` (`modal_dialog` hoặc `ui_thread_busy`).

Navisworks hiện một thẻ cho các lệnh MCP. Thẻ bật sẵn và ở lại 20 giây sau kết quả mới nhất (chọn 10, 20, 30 hoặc 60). Đưa chuột vào thì tạm dừng; rời thẻ thì đếm lại đủ khoảng chờ. Hover có chủ ý sẽ mở rộng mượt khung ba thẻ kết quả thấp gọn, có viền riêng trong thẻ hiện tại (tool, kết quả, thành công/lỗi và giờ hoàn tất theo giờ máy, định dạng 24 giờ `HH:mm:ss`). Giữ mọi kết quả thuộc thẻ đó, cũ ở trên và mới ở dưới; ban đầu hiện ba call mới nhất, cuộn lên để xem call cũ bằng scrollbar mỏng theo mẫu RVT. Rời chuột thì thu gọn mượt. Call mới được thêm ngay: ở đáy thì khung tự theo call mới nhất; khi cuộn lên đọc call cũ thì giữ nguyên vị trí cuộn. Cuộn lại xuống đáy sẽ tiếp tục tự theo call mới. Khung hoạt động cả khi tắt branding và tôn trọng chế độ giảm chuyển động. Các tóm tắt có giới hạn độ dài chỉ nằm trong RAM, bị bỏ khi đóng hoặc reset thẻ; không bật Record và không giữ request, JSON kết quả hay mã nguồn script thô. Wordmark BIMwright tắt cho đến khi bật Show branding. `nwd_health_check` không lên thẻ. Tab ribbon **Bimwright** có **Toasts**, **Toast Brand**, **Record** và **Status**. **Record** là mặc định phía host cho gateway cũ; gateway hiện tại dùng cấu hình call log phía server, xem phần Ghi lệnh bên dưới. `send_code` chỉ lưu độ dài và SHA-256, không lưu mã nguồn. Cấu hình nằm ở `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwdmcp.config.json`. `BIMWRIGHT_NWD_ENABLE_TOAST` và `BIMWRIGHT_NWD_RECORD_CALLS` ghi đè hai công tắc đó ở lần mở sau.

**Status → vị trí toast:** chọn riêng Trái/Phải và Trên/Dưới (mặc định trên-trái). Áp dụng và lưu ngay; đổi góc sẽ xóa độ lệch đã kéo. Bật **Allow dragging the toast** để kéo hàng tiêu đề; click thường vẫn đóng thẻ. Tắt kéo đưa thẻ về góc đã chọn nhưng giữ vị trí cũ; bật lại để khôi phục. **Reset position** xóa độ lệch. Vị trí lưu theo cửa sổ Navisworks bằng DIP, giới hạn trong vùng làm việc của màn hình. Neo dưới mở lên; thiếu chỗ thì đổi hướng mở. Animation tôn trọng thiết lập Windows. Lỗi lưu được báo ngay trong Status, giá trị mới vẫn có hiệu lực trong phiên. Tooltip của tool/kết quả có nội dung đã che thông tin nhạy cảm, trạng thái, giờ hoàn tất và thời lượng nếu có. Chưa phối hợp chống chồng toast giữa nhiều app.

---

## Trạng thái Hiện tại

| Thành phần | Trạng thái |
|---|---|
| Máy chủ MCP gateway (.NET 8) | ✅ Biên dịch sạch cảnh báo (Debug + Release) |
| Unit tests (155 xUnit) | ✅ Tất cả đều qua |
| Các triển khai plug-in handler | ✅ Đã xác minh trên một phiên Navisworks Manage thực tế |
| Các dự án plug-in (net48) | ✅ Biên dịch thành công theo Navisworks Manage SDK |

> **Lưu ý:** Lớp plug-in handler sử dụng các lời gọi Navisworks .NET API thực tế (không phải stub hay dữ liệu giả tạo) và đã được thực thi trên một phiên Navisworks Manage thực tế.

---

## Cài đặt

Tải [GitHub Releases](https://github.com/bimwright/nwd-mcp/releases/latest) (`NwdMcp.Setup-*-win-x64.zip`). v1.0.0 gồm plugin Manage **2025**. `install.ps1` trong ZIP; trỏ MCP vào `nwd-mcp.exe` đã cài. Gói NuGet `Bimwright.Nwd.Server` chỉ chứa máy chủ: không có plug-in Navisworks. Cài từ ZIP cài đặt; chỉ dùng `dotnet tool install -g Bimwright.Nwd.Server` khi plug-in đã được cài từ ZIP và bạn muốn máy chủ dưới dạng .NET tool toàn cục (lệnh `bimwright-nwd`).

Cờ: `--read-only` / `BIMWRIGHT_NWD_READ_ONLY=1`.

---

## Danh Sách Công Cụ (Tool Surface)

Một lần chạy mặc định đăng ký đủ mọi nhóm: **35 công cụ**. `--read-only` còn **25**. Mỗi công cụ dùng tiền tố `nwd_*`.

### 1. Công cụ Quản lý / Meta (5)
* `nwd_list_available_targets` — Liệt kê tất cả các phiên chạy Navisworks đang hoạt động.
* `nwd_get_current_target` — Báo cáo phiên chạy hiện tại mà máy chủ đang kết nối.
* `nwd_switch_target` — Chuyển đổi cổng điều khiển sang một phiên chạy tích cực khác; `verify=true` còn xác nhận document đang mở cho lệnh ghi.
* `nwd_report_task_result` — Báo cáo một lần khi xong việc (completed, failed hoặc cancelled, tóm tắt một dòng, `body` Markdown tùy chọn): hiện card trên toast Navisworks và lưu thành file. Do agent tự khai, không phải xác nhận. Kết quả của lệnh ghi đổi document mang `_report` cho tới khi báo cáo.
* `nwd_get_report_history` — Đọc các báo cáo đã lưu trong `%LOCALAPPDATA%\Bimwright\nwd-mcp\reports`, mới nhất trước, hoặc đọc đầy đủ một báo cáo theo `report_id`.

### 2. Công cụ tệp (3)
* `nwd_list_recent_files` — Liệt kê file gần đây của năm Manage đang chạy, theo thứ tự menu File.
* `nwd_open_file` *(Ghi)* — Mở một file. Từ chối khi file hiện tại còn thay đổi chưa lưu, trừ khi `discard_changes` là true.
* `nwd_import_model` *(Ghi)* — Đưa một mô hình vào tài liệu đang mở. `append` (mặc định) thêm thành model riêng; `merge` gộp vào tài liệu.

### 3. Công cụ Truy vấn / Đọc (8)
* `nwd_health_check` — Kiểm tra host/document, version/build đang chạy của server và plugin, cùng trạng thái khớp version/revision. Mặc định offline; `check_updates=true` đối chiếu cả hai component với stable release chính thức. Không có host vẫn trả identity server với `ok:false`, `host_connected:false`.
* `nwd_get_document_info` — Lấy thông tin tài liệu hoạt động: tên, đường dẫn tệp, và số lượng mô hình liên kết.
* `nwd_get_model_statistics` — Lấy số liệu thống kê: số lượng phần tử, số lượng mô hình, và số đối tượng đang được chọn.
* `nwd_get_model_tree` — Lấy cấu trúc cây phân cấp mô hình giới hạn.
* `nwd_get_item_properties` — Lấy các danh mục và danh sách thuộc tính của một phần tử cụ thể.
* `nwd_batch_get_properties` — Lấy hàng loạt thuộc tính của nhiều phần tử cùng lúc.
* `nwd_find_items` — Tìm kiếm phần tử thông qua các bộ lọc nâng cao (thuộc tính/danh mục).
* `nwd_find_items_by_name` — Tìm kiếm nhanh phần tử theo tên hiển thị.

### 4. Công cụ Lựa chọn / Selection (3)
* `nwd_get_current_selection` — Lấy danh sách Element ID của các phần tử đang được chọn trong UI.
* `nwd_clear_selection` *(Ghi)* — Xóa bỏ các lựa chọn hiện tại.
* `nwd_select_items_by_search` *(Ghi)* — Tự động chọn các phần tử khớp với bộ lọc thuộc tính/tên.

### 5. Công cụ Tập hợp Lựa chọn / Sets (3)
* `nwd_list_sets` — Liệt kê các tập hợp chọn (selection sets) và tìm kiếm (search sets), đệ quy qua các thư mục.
* `nwd_get_selection_set_items` — Lấy danh sách phần tử thuộc một tập hợp cụ thể.
* `nwd_execute_search_set` *(Hỗn hợp)* — Thực thi tìm kiếm của một tập hợp tìm kiếm; tùy chọn chọn các phần tử khớp.

### 6. Công cụ Điểm nhìn / Viewpoints (4)
* `nwd_list_viewpoints` — Liệt kê các điểm nhìn đã lưu và các thư mục tương ứng.
* `nwd_get_current_viewpoint` — Lấy trạng thái máy ảnh và góc nhìn hiện tại.
* `nwd_goto_viewpoint` *(Ghi)* — Điều hướng máy ảnh đến một điểm nhìn đã lưu.
* `nwd_save_viewpoint` *(Ghi)* — Lưu góc nhìn hoạt động thành một điểm nhìn có tên.

### 7. Công cụ Hiển thị / Visibility (2)
* `nwd_hide_items` *(Ghi)* — Ẩn/hiển thị các phần tử được chỉ định.
* `nwd_unhide_all` *(Ghi)* — Khôi phục trạng thái hiển thị của tất cả các phần tử bị ẩn.

### 8. Viết mã Kịch bản / Escape Hatch (1)
* `nwd_send_code` *(Ghi)* — Chạy script C# trên UI thread của Navisworks, với `doc` là tài liệu đang mở. Trả biểu thức cuối trong `result` và nội dung Console trong `stdout`; Navisworks chờ trong lúc script chạy.

### 9. Công cụ Đóng gói ToolBaker (6)
* `nwd_list_baked_tools` — Liệt kê danh sách các công cụ tự viết đã được xác thực, biên dịch và đăng ký.
* `nwd_run_baked_tool` *(Ghi)* — Chạy một công cụ đã đóng gói theo tên kèm theo tham số.
* `nwd_list_bake_suggestions` — Liệt kê các gợi ý tự động hóa quy trình lặp đi lặp lại.
* `nwd_accept_bake_suggestion` *(Ghi)* — Xác thực, biên dịch, đóng gói và triển khai một gợi ý thành công cụ Governed.
* `nwd_dismiss_bake_suggestion` *(Ghi)* — Bác bỏ hoặc tạm ẩn một gợi ý tích cực.
* `nwd_create_bake_issue_draft` — Tạo bản nháp GitHub issue cho các công cụ được yêu cầu.

---

## Health runtime và cập nhật

Gọi `nwd_health_check(check_updates=true)` một lần đầu phiên sau khi chọn target để phát hiện stable release mới. Nếu mạng không có hoặc không được phép, dùng mặc định offline và giữ trạng thái not checked/unknown. Các probe thường về sau vẫn offline. `setup_gateway`/`setup_plugin` mô tả assembly đang nạp; SHA-256 chỉ quan sát file trên đĩa tại thời điểm capture, không chứng minh memory đã reload hay publisher đáng tin. Plugin cũ thiếu identity trả version/build `unknown`. Probe host chạy qua UI thread nên có thể timeout khi Navisworks bận.

`check_updates=true` dùng một stable release/tag revision chính thức và tổng ngân sách mạng 5 giây. Xem riêng `update.components.server` và `.plugin`: version bằng nhau chưa đủ, thiếu bằng chứng vẫn `unknown`; so revision không xác minh bytes artifact. `checked` chỉ xác nhận lookup thành công. `update_available` của từng component so semantic version; false hoặc notification null không chứng minh khớp release.

Đọc `update.notification` và làm theo `agent_action`: `NEW_RELEASE_AVAILABLE` báo version mới; `RELEASE_REVIEW_REQUIRED`/`RELEASE_IDENTITY_UNVERIFIED` yêu cầu review; `LOCAL_VERSION_AHEAD` chỉ thông báo bản local mới hơn; `UPDATE_CHECK_UNAVAILABLE` báo lỗi lookup bên ngoài qua `lookup_reason`, không suy ra lỗi runtime hay xin phép cài đặt. Giữ `protected_components`, giải thích `protected_component_reasons`; không downgrade bản mới hơn. `unobserved_components` cần kiểm tra connection/runtime, không mặc định host đã đóng hoặc build bị hỏng. `identity_observed` và `comparison_reason` tách thiếu metadata runtime khỏi thiếu bằng chứng release.

`update_guidance.mode` gồm `check_only`, `retry_check`, `review`, `none`, `prepare_update`. Chỉ `prepare_update` với `bundle_update_proposal_allowed=true` mới đưa các bước cài và cần quyền cài đặt; tái sử dụng quyền đã có. Các mode khác chỉ check/report/review. Component mới hơn, peer chưa xác minh, lỗi lookup tag hoặc peer chưa quan sát chặn đề xuất full ZIP. Health không tự download package/cài/restart hay save/close document. Làm theo [AGENTS.md](AGENTS.md) để preview, xin phép, backup và xác minh; installer hiện chưa rollback nguyên tử bundle+server. Không tự thay development build.

Sau restart Navisworks và reconnect client, đối chiếu `initialize.serverInfo` và metadata/schema/annotations của `tools/list` mới trên **cùng connection** với `client_verification`. Server không tự xác minh được client đã refresh cache; thiếu bằng chứng client thì chưa xác minh.

## Cấu Hình An Toàn

### Chế độ Chỉ Đọc (Read-Only Mode)
Có thể kích hoạt chế độ chỉ đọc nghiêm ngặt bằng cờ `--read-only` hoặc biến môi trường `BIMWRIGHT_NWD_READ_ONLY=1`.
- Chỉ đăng ký các công cụ được đánh dấu chỉ đọc. Ba công cụ giao diện tạm thời vẫn còn: `nwd_goto_viewpoint`, `nwd_clear_selection` và `nwd_select_items_by_search` (chỉ đổi khung nhìn hoặc vùng chọn, không đổi mô hình). `nwd_switch_target` và mọi lệnh ghi bị gỡ; ghim target lúc khởi động bằng `--target pid:<n>`.
- `nwd_execute_search_set(select=true)` vẫn chọn các item tìm được: giống các tool giao diện tạm thời ở trên, nó chỉ đổi selection chứ không đổi model.
- Tổng số lượng công cụ khả dụng ở chế độ chỉ đọc là đúng **25 công cụ**. `nwd_list_recent_files` vẫn còn; `nwd_open_file` và `nwd_import_model` bị gỡ.

### Ghi lệnh (Record)
Call log **tắt mặc định**. `--enable-call-log` ghi mọi lời gọi MCP, kể cả tool cục bộ và lỗi, vào `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwd-mcp-calls.jsonl`; lệnh chuyển tới plugin còn ghi kết quả native vào `mcp-calls.jsonl` cùng thư mục. `--disable-call-log` ngăn tạo hoặc ghi thêm vào cả hai log cho gateway này. Checkbox MCPB `call_log` mặc định false và truyền hai flag đó.

Thứ tự ưu tiên: CLI → `BIMWRIGHT_NWD_ENABLE_CALL_LOG` → JSON server `enableCallLog` → false. Mỗi request mang lựa chọn này tới plugin cùng bản, thắng nút **Record** trên ribbon/Status, `recordCalls` đã lưu và `BIMWRIGHT_NWD_RECORD_CALLS`, không đổi setting hay gateway khác. Công tắc host vẫn là mặc định cho gateway cũ không gửi lựa chọn; plugin cũ không hiểu override nên cần dùng bộ build tương ứng. Tham số `send_code` chỉ lưu `code_length` và `code_sha256` thay thân script.

### Quyền (permissions) & auto mode
Mọi tool trừ `nwd_send_code` đều khai báo annotation MCP (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint`). Open-world là false trừ `nwd_health_check` vì tùy chọn kiểm tra cập nhật truy cập GitHub; health vẫn read-only, không destructive. Claude Desktop dựa vào đó: tool đọc chỉ cần cho phép một lần, còn tool destructive (`nwd_open_file`, `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, `nwd_dismiss_bake_suggestion`) luôn hỏi.

Auto mode của Claude Code kiểm tra từng lời gọi MCP bằng classifier. Muốn bỏ qua bước này cho các tool chỉ đọc, thêm chúng vào `permissions.allow` trong `.claude/settings.json` (đổi `nwd-mcp` nếu mục MCP của bạn dùng tên khác):

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

**Không** dùng `mcp__nwd-mcp__*`: wildcard sẽ duyệt luôn `nwd_send_code` và mọi tool ghi mà không kiểm tra gì. Hãy khởi động agent trong thư mục dự án chứa model, và nêu đúng tên file khi yêu cầu mở hoặc thay đổi file.

### Lưu trữ ToolBaker
Cơ sở dữ liệu lưu trữ sqlite (`bake.db`) và nhật ký kiểm tra quy trình (`audit.jsonl`) của ToolBaker được duy trì cục bộ tại:
```text
%LOCALAPPDATA%\Bimwright\nwd-mcp\baked\
```

### Kích thước phản hồi
Kết quả tool là JSON nén gọn. Trên 64 KiB server thêm trường `_response_warning` vào kết quả (mạnh hơn trên 256 KiB); quá ngân sách 1 MiB kết quả bị từ chối với `RESPONSE_TOO_LARGE` — trừ tool ghi đã chạy xong, khi đó nó trả về bản tóm tắt thành công đã nén (`response_compacted`, `mutation_applied`) thay vì báo lỗi.
Bốn tool chấp nhận `output=file` để ghi toàn bộ kết quả ra một file cục bộ trên cùng máy và trả về đường dẫn, schema và bản xem trước: `nwd_get_model_tree` (JSON), `nwd_batch_get_properties` (SQLite), `nwd_find_items_by_name` (NDJSON) và `nwd_run_baked_tool` (định dạng tự nhận diện). `nwd_send_code` không có tham số `output`; đầu ra trên 1 MiB tự động spill theo cùng cách.
Các file spill nằm trong `%LOCALAPPDATA%\Bimwright\nwd-mcp\spill` và bị xóa khi gỡ cài đặt. Mỗi file được giữ 36 giờ kể từ lúc ghi. File chỉ bị xóa vì quá tuổi; không có giới hạn số file. Đổi thời gian bằng `--spill-retention-hours <n>` (1–8760), `BIMWRIGHT_NWD_SPILL_RETENTION_HOURS` hoặc khóa JSON `spillRetentionHours` của server; thiếu hoặc sai giá trị thì dùng 36.
Guard bật mặc định. `--disable-response-guard` (hoặc `BIMWRIGHT_NWD_ENABLE_RESPONSE_GUARD=0`, JSON `enableResponseGuard: false`) tắt cảnh báo và ngân sách 1 MiB. Trần transport `--max-response-bytes` (mặc định 10 MiB) vẫn áp dụng, nên kết quả vượt trần vẫn bị từ chối hoặc spill. `--response-warn-bytes`, `--response-strong-warn-bytes` và `--response-budget-bytes` (env `BIMWRIGHT_NWD_RESPONSE_WARN_BYTES`, `…_STRONG_WARN_BYTES`, `…_BUDGET_BYTES`; JSON `responseWarnBytes`, `responseStrongWarnBytes`, `responseBudgetBytes`) đặt các ngưỡng. Mỗi ngưỡng phải từ 1024 trở lên và theo thứ tự warn ≤ strong ≤ budget ≤ max, nếu không server không khởi động. Thứ tự ưu tiên của mọi thiết lập là CLI → biến môi trường → JSON.

## Đo hiệu năng

Thời gian, kích thước kết quả, token ước tính và kết quả chính sách kích thước của đủ 33 công cụ trên Navisworks Manage 2025: [docs/benchmarks/2026-09-30-navisworks-manage-2025.md](docs/benchmarks/2026-09-30-navisworks-manage-2025.md).

---

## Phát Triển Cục Bộ & Biên Dịch
Các tệp DLL Autodesk Navisworks API **không được phân phối trực tiếp** trong kho lưu trữ này.

- **Máy chủ và Bộ kiểm thử:** Có thể biên dịch và chạy trên bất kỳ máy nào mà không cần cài đặt Navisworks.
  ```powershell
  dotnet test tests\Bimwright.Nwd.Tests\Bimwright.Nwd.Tests.csproj -c Debug
  ```
- **Biên dịch Plug-In:** Yêu cầu máy phát triển phải cài đặt sẵn Autodesk Navisworks Manage.
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug /p:NavisworksInstallDir="C:\Program Files\Autodesk\Navisworks Manage 2026"
  ```
  Nếu Navisworks Manage được cài đặt ở đường dẫn tùy chỉnh, hãy ghi đè thuộc tính chỉ dẫn:
  ```powershell
  dotnet build src\plugin-navis26\Bimwright.Nwd.Plugin.Navis26.csproj -c Debug /p:NavisworksInstallDir="D:\Autodesk\Navisworks 2026"
  ```

---

## Họ bimwright

Các công cụ mã nguồn mở kết nối trợ lý AI với ứng dụng BIM và CAD.

Tên **bimwright** ghép **BIM** với **wright**, một từ tiếng Anh cổ chỉ người thợ chế tạo hoặc xây dựng — như trong *shipwright* (thợ đóng tàu).

Xem [cách đặt tên các gateway](https://github.com/bimwright/.github/blob/master/profile/README.vi.md#cách-đặt-tên).

- [**rvt-mcp**](https://github.com/bimwright/rvt-mcp) — Autodesk® Revit®
- [**dwg-mcp**](https://github.com/bimwright/dwg-mcp) — Autodesk® AutoCAD®
- [**nwd-mcp**](https://github.com/bimwright/nwd-mcp) — Autodesk® Navisworks®
- [**ipt-mcp**](https://github.com/bimwright/ipt-mcp) — Autodesk® Inventor®
- [**bim-wiki**](https://github.com/bimwright/bim-wiki) — Kho kiến thức BIM ưu tiên tiếng Việt

---

## Giấy phép

Apache-2.0. Xem chi tiết tại tệp [LICENSE](LICENSE).

Navisworks và Autodesk là thương hiệu đã đăng ký của Autodesk, Inc. bimwright là dự án open-source độc lập, không liên kết, không được tài trợ và không được bảo chứng bởi Autodesk, Inc.
