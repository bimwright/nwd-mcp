# Cổng Kết Nối Bimwright Navisworks MCP

`nwd-mcp` là cổng kết nối Model Context Protocol (MCP) chuyên nghiệp dành cho việc tự động hóa **Autodesk Navisworks Manage**. Giải pháp này cho phép các trợ lý AI truy vấn, kiểm tra, điều hướng và viết mã kịch bản trực tiếp lên các phiên chạy Navisworks Manage desktop thông qua đầu vào/đầu ra tiêu chuẩn (stdin/stdout).

---

## Tính Năng & Kiến Trúc
- **Ứng dụng Máy chủ Hỗ trợ:** Chỉ hỗ trợ Autodesk Navisworks Manage (Không hỗ trợ Freedom và Simulate).
- **Các Phiên bản Hỗ trợ:** Từ 2022 đến 2027.
- **Mô hình Hai Tiến trình:** Máy chủ điều phối chạy trên `.NET 8` gọn nhẹ, giao tiếp với plug-in chạy trực tiếp (`net48`) bên trong Navisworks thông qua giao thức TCP NDJSON (localhost).
- **Bảo mật Tối đa:** Xác thực qua mã token ngẫu nhiên tạo theo từng phiên, chỉ liên kết với loopback TCP (`127.0.0.1`), và tự động ẩn/lọc đường dẫn tệp tuyệt đối trong phản hồi gửi về cho mô hình AI.
- **Điều hướng Nhiều Phiên chạy:** Tự động phát hiện nhiều tiến trình Navisworks đang chạy đồng thời và cho phép chuyển đổi mục tiêu điều khiển linh hoạt.

---

## Danh Sách Công Cụ (Tool Surface)

Phiên bản đầu tiên cung cấp chính xác **30 công cụ** khi tất cả bộ công cụ được bật. Mỗi công cụ đều sử dụng tiền tố `nwd_*`.

### 1. Công cụ Quản lý / Meta (3)
* `nwd_list_available_targets` — Liệt kê tất cả các phiên chạy Navisworks đang hoạt động.
* `nwd_get_current_target` — Báo cáo phiên chạy hiện tại mà máy chủ đang kết nối.
* `nwd_switch_target` — Chuyển đổi cổng điều khiển sang một phiên chạy tích cực khác.

### 2. Công cụ Truy vấn / Đọc (8)
* `nwd_health_check` — Kiểm tra trạng thái và tín hiệu nhịp tim (heartbeat) của phiên chạy.
* `nwd_get_document_info` — Lấy thông tin tài liệu hoạt động: tên, đường dẫn tệp, và số lượng mô hình liên kết.
* `nwd_get_model_statistics` — Lấy số liệu thống kê: số lượng phần tử, số lượng mô hình, và số đối tượng đang được chọn.
* `nwd_get_model_tree` — Lấy cấu trúc cây phân cấp mô hình giới hạn.
* `nwd_get_item_properties` — Lấy các danh mục và danh sách thuộc tính của một phần tử cụ thể.
* `nwd_batch_get_properties` — Lấy hàng loạt thuộc tính của nhiều phần tử cùng lúc.
* `nwd_find_items` — Tìm kiếm phần tử thông qua các bộ lọc nâng cao (thuộc tính/danh mục).
* `nwd_find_items_by_name` — Tìm kiếm nhanh phần tử theo tên hiển thị.

### 3. Công cụ Lựa chọn / Selection (3)
* `nwd_get_current_selection` — Lấy danh sách Element ID của các phần tử đang được chọn trong UI.
* `nwd_clear_selection` *(Ghi)* — Xóa bỏ các lựa chọn hiện tại.
* `nwd_select_items_by_search` *(Ghi)* — Tự động chọn các phần tử khớp với bộ lọc thuộc tính/tên.

### 4. Công cụ Tập hợp Lựa chọn / Sets (3)
* `nwd_list_sets` — Liệt kê các tập hợp chọn (selection sets) và tìm kiếm (search sets), đệ quy qua các thư mục.
* `nwd_get_selection_set_items` — Lấy danh sách phần tử thuộc một tập hợp cụ thể.
* `nwd_execute_search_set` *(Hỗn hợp)* — Thực thi tìm kiếm của một tập hợp tìm kiếm; tùy chọn chọn các phần tử khớp.

### 5. Công cụ Điểm nhìn / Viewpoints (4)
* `nwd_list_viewpoints` — Liệt kê các điểm nhìn đã lưu và các thư mục tương ứng.
* `nwd_get_current_viewpoint` — Lấy trạng thái máy ảnh và góc nhìn hiện tại.
* `nwd_goto_viewpoint` *(Ghi)* — Điều hướng máy ảnh đến một điểm nhìn đã lưu.
* `nwd_save_viewpoint` *(Ghi)* — Lưu góc nhìn hoạt động thành một điểm nhìn có tên.

### 6. Công cụ Hiển thị / Visibility (2)
* `nwd_hide_items` *(Ghi)* — Ẩn/hiển thị các phần tử được chỉ định.
* `nwd_unhide_all` *(Ghi)* — Khôi phục trạng thái hiển thị của tất cả các phần tử bị ẩn.

### 7. Viết mã Kịch bản / Escape Hatch (1)
* `nwd_send_code` *(Ghi, Kích hoạt tùy chọn)* — Biên dịch và thực thi mã C# trực tiếp đối với Navisworks API.

### 8. Công cụ Đóng gói ToolBaker (6)
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
- Tổng số lượng công cụ khả dụng ở chế độ chỉ đọc là đúng **20 công cụ**.

### Cơ chế Xác thực Hai Bên của send_code
Viết kịch bản C# động (`nwd_send_code`) bị **tắt theo mặc định**. Nó chỉ được kích hoạt khi:
1. Máy chủ được chạy kèm cờ `--enable-send-code` hoặc biến môi trường `BIMWRIGHT_NWD_ENABLE_SEND_CODE=1`.
2. Plug-in phát hiện biến môi trường `BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=1` trong môi trường chạy của nó.

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

## Giấy phép
Apache-2.0. Xem chi tiết tại tệp [LICENSE](LICENSE).
