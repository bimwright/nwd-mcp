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
- **Điều hướng Nhiều Phiên chạy:** Tự động phát hiện nhiều tiến trình Navisworks đang chạy đồng thời và cho phép chuyển đổi mục tiêu điều khiển linh hoạt.

Navisworks hiện một thẻ cho các lệnh MCP. Thẻ bật sẵn và ở lại 20 giây sau kết quả mới nhất (chọn 10, 20, 30 hoặc 60). Đưa chuột vào thì tạm dừng; rời thẻ thì đếm lại đủ khoảng chờ. Wordmark BIMwright tắt cho đến khi bật Show branding. `nwd_health_check` không lên thẻ. Tab ribbon **Bimwright** có **Toasts**, **Toast Brand**, **Record** và **Status**. **Record** tắt cho đến khi bạn bật; khi bật, mọi lệnh được ghi thêm vào `%LOCALAPPDATA%\Bimwright\nwd-mcp\mcp-calls.jsonl`. `send_code` chỉ lưu độ dài và SHA-256, không lưu mã nguồn. Cấu hình nằm ở `%LOCALAPPDATA%\Bimwright\nwd-mcp\nwdmcp.config.json`. `BIMWRIGHT_NWD_ENABLE_TOAST` và `BIMWRIGHT_NWD_RECORD_CALLS` ghi đè hai công tắc đó ở lần mở sau.

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

Tải [GitHub Releases](https://github.com/bimwright/nwd-mcp/releases/latest) (`NwdMcp.Setup-*-win-x64.zip`). v0.1.2 gồm plugin Manage **2025**. `install.ps1` trong ZIP; trỏ MCP vào `nwd-mcp.exe` đã cài. Không `dotnet tool install -g Bimwright.Nwd.Server`.

Cờ: `--read-only` / `BIMWRIGHT_NWD_READ_ONLY=1`. `nwd_send_code` bật sẵn. Tắt bằng `--disable-send-code` (hoặc `BIMWRIGHT_NWD_ENABLE_SEND_CODE=0`) và `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0` trên tiến trình Navisworks — xem [Cấu hình an toàn](#cấu-hình-an-toàn).

---

## Danh Sách Công Cụ (Tool Surface)

Một lần chạy mặc định đăng ký đủ mọi nhóm: **33 công cụ**, gồm `nwd_send_code`. `--disable-send-code` còn **32**. `--read-only` còn **21**. Mỗi công cụ dùng tiền tố `nwd_*`.

### 1. Công cụ Quản lý / Meta (3)
* `nwd_list_available_targets` — Liệt kê tất cả các phiên chạy Navisworks đang hoạt động.
* `nwd_get_current_target` — Báo cáo phiên chạy hiện tại mà máy chủ đang kết nối.
* `nwd_switch_target` — Chuyển đổi cổng điều khiển sang một phiên chạy tích cực khác.

### 2. Công cụ tệp (3)
* `nwd_list_recent_files` — Liệt kê file gần đây của năm Manage đang chạy, theo thứ tự menu File.
* `nwd_open_file` *(Ghi)* — Mở một file. Từ chối khi file hiện tại còn thay đổi chưa lưu, trừ khi `discard_changes` là true.
* `nwd_import_model` *(Ghi)* — Đưa một mô hình vào tài liệu đang mở. `append` (mặc định) thêm thành model riêng; `merge` gộp vào tài liệu.

### 3. Công cụ Truy vấn / Đọc (8)
* `nwd_health_check` — Kiểm tra trạng thái và tín hiệu nhịp tim (heartbeat) của phiên chạy.
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
* `nwd_send_code` *(Ghi, bật sẵn)* — Chạy script C# trên UI thread của Navisworks, với `doc` là tài liệu đang mở. Trả biểu thức cuối trong `result` và nội dung Console trong `stdout`; Navisworks chờ trong lúc script chạy.

### 9. Công cụ Đóng gói ToolBaker (6)
* `nwd_list_baked_tools` — Liệt kê danh sách các công cụ tự viết đã được xác thực, biên dịch và đăng ký.
* `nwd_run_baked_tool` *(Ghi)* — Chạy một công cụ đã đóng gói theo tên kèm theo tham số.
* `nwd_list_bake_suggestions` — Liệt kê các gợi ý tự động hóa quy trình lặp đi lặp lại.
* `nwd_accept_bake_suggestion` *(Ghi)* — Xác thực, biên dịch, đóng gói và triển khai một gợi ý thành công cụ Governed.
* `nwd_dismiss_bake_suggestion` *(Ghi)* — Bác bỏ hoặc tạm ẩn một gợi ý tích cực.
* `nwd_create_bake_issue_draft` — Tạo bản nháp GitHub issue cho các công cụ được yêu cầu.

---

## Cấu Hình An Toàn

### Chế độ Chỉ Đọc (Read-Only Mode)
Có thể kích hoạt chế độ chỉ đọc nghiêm ngặt bằng cờ `--read-only` hoặc biến môi trường `BIMWRIGHT_NWD_READ_ONLY=1`.
- Các công cụ có khả năng ghi hoặc thay đổi mô hình sẽ bị ẩn hoàn toàn khỏi danh sách đăng ký MCP.
- Các công cụ hỗn hợp (như `nwd_execute_search_set`) sẽ bị ép buộc tham số an toàn (`select=false`) và trả về cờ đánh dấu `read_only_enforced` trong phản hồi.
- Tổng số lượng công cụ khả dụng ở chế độ chỉ đọc là đúng **21 công cụ**. `nwd_list_recent_files` vẫn còn; `nwd_open_file` và `nwd_import_model` bị gỡ.

### send_code
`nwd_send_code` **bật sẵn**. `--read-only` vẫn gỡ công cụ này. Tắt bằng `--disable-send-code` hoặc `BIMWRIGHT_NWD_ENABLE_SEND_CODE=0` trên máy chủ, và `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=0` trong tiến trình Navisworks. Một trong hai phía tắt là lệnh không chạy. `--enable-send-code` hoặc `=1` bật lại. Giá trị không nhận ra được coi là tắt.

### Ghi lệnh (Record)
**Record** **tắt sẵn**. Nút trên ribbon, ô trong cửa sổ Status, khóa `recordCalls` trong `nwdmcp.config.json` và `BIMWRIGHT_NWD_RECORD_CALLS` là cùng một công tắc cho mọi công cụ. Khi bật, mỗi lệnh được ghi thêm vào `mcp-calls.jsonl` cạnh file cấu hình đó. Mã `send_code` không được ghi; dòng nhật ký lưu `code_length` và `code_sha256`.

### Quyền (permissions) & auto mode
Mọi tool trừ `nwd_send_code` đều khai báo annotation MCP (`readOnlyHint`, `destructiveHint`, `idempotentHint`, `openWorldHint=false`). Claude Desktop dựa vào đó: tool đọc chỉ cần cho phép một lần, còn tool destructive (`nwd_open_file`, `nwd_hide_items`, `nwd_unhide_all`, `nwd_run_baked_tool`, `nwd_dismiss_bake_suggestion`) luôn hỏi.

Auto mode của Claude Code kiểm tra từng lời gọi MCP bằng classifier. Muốn bỏ qua bước này cho các tool chỉ đọc, thêm chúng vào `permissions.allow` trong `.claude/settings.json` (đổi `nwd-mcp` nếu mục MCP của bạn dùng tên khác):

```json
{
  "permissions": {
    "allow": [
      "mcp__nwd-mcp__nwd_list_recent_files",
      "mcp__nwd-mcp__nwd_list_available_targets",
      "mcp__nwd-mcp__nwd_get_current_target",
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
