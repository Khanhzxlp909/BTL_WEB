# Kiểm thử BookStore

Nghiệm thu 2026-10-30: `docker compose --profile test run --rm --build test`:
**69/69 PASS**, không bỏ qua; build **0 warning, 0 error**.
Host không có `BOOKSTORE_TEST_SQLSERVER`: 62 bài dùng SQLite/kiểm tra độc lập,
7 bài SQL Server opt-in được bỏ qua.
Đầu vào giả lập, SQLite in-memory riêng mỗi bài; SQL Server dùng DB
`BookStoreTest_<GUID>` mới, xóa đúng DB test sau bài. Không dữ liệu thật.

## Giai đoạn 1 — 20 bài

| Mã | Mục tiêu / đầu vào | Kết quả mong đợi | Thực tế |
|---|---|---|---|
| TC01 | Lọc giá 75.000–85.000, còn hàng, trang 999; sách giảm 100.000 còn 80.000, tồn 10 giữ 3 | Một sách; giá bán 80.000; khả dụng 7; trang 1; sách ngừng bán không công khai; điểm chỉ tính đánh giá hiện; UTC có Z | PASS |
| TC02 | MinPrice=90000, MaxPrice=1 | 400 VALIDATION_FAILED, lỗi MaxPrice | PASS |
| TC03 | PageSize=101 | 400, lỗi PageSize | PASS |
| TC04 | Page=0 | 400, lỗi Page | PASS |
| TC05 | MinPrice=12.5 | 400, lỗi MinPrice, không tự làm tròn | PASS |
| TC06 | Page=abc | 400, lỗi kiểu dữ liệu Page | PASS |
| TC07 | Sort=invalid | 400, lỗi Sort | PASS |
| TC08 | Đăng ký, gửi thêm Role=Admin; mật khẩu chứa khoảng trắng; đăng ký lại tên khác hoa/thường | Chỉ Customer hoạt động; password hash, giữ khoảng trắng; từ chối trùng; không render lại mật khẩu | PASS |
| TC09 | POST đăng ký thiếu anti-forgery, Accept JSON | 400 ANTIFORGERY_TOKEN_INVALID | PASS |
| TC10 | Guest vào Admin, Customer vào Admin, đăng nhập sai với tên tồn tại/không tồn tại, ReturnUrl ngoài | MVC chuyển đăng nhập; JSON 401; Customer 403; lỗi đăng nhập đồng nhất; không redirect ngoài | PASS |
| TC11 | Sửa hồ sơ, gửi thêm email/username/role/id người khác | Chỉ họ tên/điện thoại chính chủ được sửa | PASS |
| TC12 | Hai phiên cùng tài khoản; một phiên đổi mật khẩu | Phiên hiện tại kết thúc; phiên kia bị từ chối ngay request kế tiếp | PASS |
| TC13 | Tài khoản đang đăng nhập chuyển IsActive=false có đủ thông tin khóa | Phiên hết hiệu lực request kế tiếp, không đăng nhập lại được | PASS |
| TC14 | Chuyển en, URL có query; gửi culture=xx | Cookie en, URL/query giữ nguyên, HTML lang en; culture không hỗ trợ trả 400 | PASS |
| TC15 | Description chứa `<script>unsafe()</script>` | HTML encode, không thực thi script | PASS |
| TC16 | Kiểm tra SQL Server model và GenerateCreateScript, không mở kết nối | FK Restrict; decimal(18,0); rowversion; check tồn/thanh toán; không cascade | PASS |
| TC17 | Chưa cấu hình DB; trang chủ, GET API, trang không tồn tại | Trang hướng dẫn; API 503 DATABASE_NOT_CONFIGURED; 404 có thông tin cụ thể | PASS |
| TC18 | Khởi tạo hệ thống hai lần; chọn tên Customer làm Admin | Không trùng quản trị/ngưỡng; từ chối nâng quyền Customer | PASS |
| TC19 | ReservedQuantity > Quantity; sửa/xóa lịch sử kho | Ràng buộc tồn từ chối; lịch sử chỉ thêm | PASS |
| TC20 | MVC trang chủ/danh mục có dữ liệu; bộ lọc sai; Admin đăng nhập và mở tổng quan | Trang hợp lệ trả 200; bộ lọc sai 400; Admin đích `/admin`, tổng quan render 200 | PASS |

## Kiểm tra bổ sung

- `pnpm build`: 0 warning, 0 error.
- `dotnet ef migrations has-pending-model-changes --project src/BookStore.Web`:
  không có thay đổi mô hình chưa đưa vào migration.
- Trình duyệt localhost: trang chủ/đăng ký mobile 390px không tràn ngang;
  đổi EN thành công; biểu mẫu có anti-forgery; gửi trống trả 400 và lỗi từng trường.
- Kiểm tra tài nguyên local Bootstrap/jQuery/CSS/JS được phục vụ.
- Nhánh CLI `--initialize` khi thiếu DB trả mã thoát 1, thông báo cấu hình
  cụ thể; không kết nối hoặc ghi dữ liệu.

## Lỗi đã phát hiện và xử lý trong lần xây dựng

- Build đầu: sai namespace interface anti-forgery, header Referrer-Policy;
  đã sửa và build sạch.
- Dependency mới kéo Pkcs 9.0.11 có advisory; pin bản 10.0.14 của hạng mục
  đang xây dựng, restore/build không còn cảnh báo.
- Fixture SQLite ban đầu lưu decimal thành TEXT làm so sánh giá không tương
  đương SQL Server; fixture đổi VND sang INTEGER, không đổi schema ứng dụng.
- Thiếu UI tài khoản khi bài test chạy sớm; hoàn thiện Razor trước nghiệm thu.
- DateTime đọc từ datetime2 có Kind Unspecified; converter gán UTC để JSON
  giữ hậu tố Z theo BA; có assertion tích hợp bảo vệ.

## Giai đoạn 2 — 28 bài bổ sung

| Mã | Mục tiêu / đầu vào | Kết quả mong đợi | Thực tế |
|---|---|---|---|
| TC21–24 | Customer truy cập sách/thể loại/kho quản trị và POST API Admin | 403 ở cả bốn nhóm | PASS |
| TC25 | Tạo/sửa sách, gửi thêm Quantity/IsDeleted/CoverImage; ngừng bán/khôi phục | Một tồn 0; bỏ qua trường không được phép; giá cập nhật; soft-delete/restore không xóa dữ liệu | PASS |
| TC26–31 | Giá 0, giá lẻ, giá giảm vượt giá gốc, năm tương lai, tên toàn khoảng trắng, thể loại không có | 400; không tạo sách | PASS |
| TC32 | Xóa thể loại có sách; trùng tên khác hoa/thường; xóa mềm rồi dùng lại tên | 409 với tham chiếu/trùng kể cả tên đã xóa; không xóa cứng | PASS |
| TC33 | API sách thiếu CSRF; tạo, trùng SKU, sửa, DELETE | Thiếu CSRF 400; tạo 201, trùng 409; sửa/xóa mềm 200, tồn không đổi | PASS |
| TC34 | Nhập 4 vào tồn 10 giữ 3; gửi rowversion cũ | Tồn 14 giữ 3; lịch sử trước/sau đúng; stale 409, không thêm lịch sử | PASS |
| TC35 | Nhập với actor không tồn tại làm ghi lịch sử thất bại | Rollback cả tồn và lịch sử | PASS |
| TC36–41 | Nhập số 0/âm/lẻ, mã phiếu trống, lý do trống, phiên bản sai | 400; tồn 10 và lịch sử giữ nguyên | PASS |
| TC42 | Ngưỡng 5 thành 7 với khả dụng 7 | Cảnh báo cập nhật; tồn không đổi | PASS |
| TC43 | Ngưỡng âm | 400; giữ ngưỡng cũ | PASS |
| TC44–46 | Ảnh PNG/JPEG/WEBP thật, tên chứa đường dẫn | Decode/re-encode WEBP; tên GUID trong thư mục cố định | PASS |
| TC47 | Nội dung script giả PNG; file >5 MB | COVER_IMAGE_INVALID; không tạo sách | PASS |
| TC48 | SQL Server: migration DB test; hai nhập cùng rowversion; nhập lỗi FK lịch sử | Migration thành công; đúng một nhập thành công; rowversion đổi; rollback khi ghi lịch sử lỗi | PASS trong Compose |

### Nghiệm thu Compose/UI

- `docker compose config --quiet`: PASS; build web/migrate/test thành công.
- SQL Server healthy; web `127.0.0.1:5087` trả 200 cho trang đăng nhập và
  Bootstrap; runtime uid 1654 (`app`), volumes ảnh/khóa ghi được.
- Thư viện native SkiaSharp trong runtime có đủ dependency (`ldd` không thiếu).
- Trình duyệt SQLite test riêng: đăng nhập Admin, danh sách sách, xác nhận
  nhập kho, lịch sử 10 thành 12/giữ 3; viewport 390px không tràn ngang.
- GET `sys.databases` sau test không còn `BookStoreTest_*`, chưa có DB
  `BookStore`; không chạy migration/initialize vào DB ứng dụng.
- Không phát sinh thay đổi schema; EF báo không có model change ngoài migration.

## Giai đoạn 3 — 21 bài bổ sung

| Mã | Kiểm thử | Kết quả |
|---|---|---|
| TC49 | Giỏ cộng dồn một dòng, chính chủ, phiên khác cùng user, sửa 0 là xóa; không giữ chỗ | PASS |
| TC50–52 | Thêm số lượng 0/âm/vượt khả dụng | PASS; 400/409, giỏ giữ nguyên |
| TC53 | Giá đổi sau báo giá; token giả; replay xác nhận | PASS; yêu cầu giá mới, token giả bị từ chối, replay một đơn |
| TC54 | Điện thoại sai, chưa xác nhận COD, giỏ đổi sau báo giá | PASS; không tạo đơn |
| TC55 | Đúng trình tự trạng thái, COD bắt buộc, không trừ hai lần, snapshot khi sách sửa/ngừng bán | PASS |
| TC56 | Hủy chính chủ, lý do Admin, hủy lặp | PASS; hoàn giữ chỗ một lần |
| TC57 | MVC giỏ/checkout/lịch sử/Admin render; báo giá mới giữ người nhận và yêu cầu xác nhận lại | PASS |
| TC58 | Giá 9.007.199.254.740.993đ trong JSON | PASS; trường text giữ nguyên đồng ngoài Number.MAX_SAFE_INTEGER |
| TC59 | Sách ngừng bán sau khi xem báo giá | PASS; không đơn, giỏ và giữ chỗ không đổi |
| TC60 | Admin hủy đơn riêng qua endpoint chính chủ | PASS; vẫn bắt buộc lý do |
| TC61 | Đánh giá cần DELIVERED; upsert cùng đánh giá; sửa khi ẩn; kiểm duyệt lặp | PASS |
| TC62 | Khóa có lý do, không khóa Admin, vô hiệu hóa phiên, mở khóa đăng nhập lại | PASS |
| TC63 | Báo cáo theo DeliveredAt/CancelledAt UTC+7, ngày 0, top sách, khoảng sai không hiển thị số liệu | PASS |
| TC64 | SQL Server: hai khách tranh cuốn cuối | PASS; một đơn, tồn không âm |
| TC65 | SQL Server: đồng thời cùng xác nhận; hủy/giao lặp | PASS; một đơn/token, biến động đúng một lần |
| TC66 | SQL Server: trigger gây lỗi history khi đặt | PASS; rollback đơn, dòng đơn, timeline, giữ chỗ và giỏ |
| TC67 | SQL Server: query báo cáo dịch SQL, biên 00:00 Việt Nam | PASS |
| TC68 | SQL Server: trigger lỗi history giao | PASS; rollback trạng thái/thanh toán/ngày giao/tồn/giữ chỗ |
| TC69 | SQL Server: trigger lỗi history hủy | PASS; rollback trạng thái/ngày hủy/hoàn giữ chỗ |

### Trình duyệt giao dịch

- Kestrel + SQLite in-memory riêng: Customer thêm sách bằng jQuery AJAX tại
  chi tiết, mở giỏ và checkout COD; tạo một đơn, hiển thị snapshot/timeline.
- Admin xác nhận, chuyển đang giao, xác nhận thu COD rồi đánh dấu đã giao.
  Báo cáo ghi 80.000đ, 1 đơn, top 1 sách; giá trị từ dữ liệu test thật.
- Giỏ, chi tiết đơn, báo cáo viewport 390px không tràn ngang.
- Lỗi báo cáo ngày: 400, giữ ngày và thông báo; sau nghiệm thu UI ẩn toàn bộ
  metrics/chart/table để không hiểu nhầm là báo cáo bằng 0.
- AJAX tiền dùng chuỗi server format; badge giỏ lỗi mạng/401 hiện trạng thái
  chưa xác định và mô tả, không giả số lượng 0.
- Trình duyệt mô phỏng mất kết nối GET /api/cart: badge hiện `?` kèm lý do;
  kết nối phục hồi hiện 0 thực và gỡ trạng thái lỗi. Không ghi DB trong bài này.
- Giỏ AJAX browser: nhập -1/1.5/999 trong khi tồn khả dụng 7 đều giữ lại số 1
  đã lưu; đổi hợp lệ sang 2 rồi nhập sai trả về 2; nhập 0 xóa dòng không reload.

## Chưa xác minh / còn lại

- Luồng UI có dữ liệu dùng SQLite tách biệt; runtime SQL Server đã kiểm tra qua
  migration, cạnh tranh/rollback và báo cáo, chưa chạy bài browser trên DB ứng dụng.
- CSS/HTML ứng dụng chỉ dùng tài nguyên nội bộ, không nạp font ngoài và giữ nguyên chính sách CSP.
- Chưa benchmark tải lớn và chưa nghiệm thu triển khai production.
