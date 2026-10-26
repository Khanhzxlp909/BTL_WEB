# Đối chiếu BA và lộ trình triển khai

## Nguồn yêu cầu

- `BaiTapLon.doc`: ASP.NET MVC; nhiều lớp người dùng; template responsive,
  paging/filtering/AJAX; CRUD, session/cookie, annotations, REST, bảo mật và
  quốc tế hóa; báo cáo, test case, hướng dẫn và script CSDL.
- `Mau_BTL_Cong_nghe_Java.docx`: BA BookStore, 6 module, BR01–BR40, ERD, API,
  sitemap. Nội dung xác định C#/ASP.NET Core/SQL Server, không dùng Java.
- `UML/`: 7 use case, 2 ERD, 2 sitemap, 9 wireframe desktop/mobile.

Thời điểm bắt đầu: chưa có mã nguồn và Git. Tài liệu/diagram hiện có được giữ
nguyên. Chỉ bổ sung phần dự án còn thiếu.

## A. Logic / Backend / API

| Hạng mục | Trạng thái | Bằng chứng / phần còn lại |
|---|---|---|
| Nền tảng MVC, Identity, EF Core SQL Server | Đã triển khai | `Program.cs`, `Data/BookStoreDbContext.cs` |
| Mô hình 6 module, ràng buộc tiền/tồn, FK không cascade, rowversion | Đã khai báo, kiểm thử SQL Server | Migration chạy trên DB test ngẫu nhiên; DB ứng dụng chưa khởi tạo |
| M1: đăng ký/nhập/xuất, hồ sơ, mật khẩu | Đã triển khai | Chỉ Customer tự đăng ký; bind hồ sơ giới hạn; vô hiệu hóa phiên cũ |
| M1: ngôn ngữ VI/EN, session/cookie | Đã triển khai | Cookie culture, session, nhãn và validation song ngữ |
| M2: tìm/lọc/sắp xếp/phân trang, chi tiết | Đã triển khai | Service dùng AsNoTracking, projection DTO, phân trang DB |
| M2: CRUD sách/thể loại, soft-delete/restore, upload bìa | Đã triển khai | Tạo sách + tồn 0; kiểm tra tham chiếu/trùng tên/SKU; bìa decode/re-encode WEBP |
| M3: giỏ theo tài khoản, AJAX | Chưa làm | Giai đoạn 3; không giữ chỗ, kiểm tra số khả dụng |
| M4: đơn COD, chốt giá, giữ chỗ, trạng thái/hủy | Chưa làm | Giai đoạn 4; transaction, cạnh tranh, chống tạo trùng |
| M5: nhập/tồn/lịch sử/ngưỡng | Đã triển khai phần kho | Transaction nhập/lịch sử, rowversion, ngưỡng chung; biến động đơn chờ M4 |
| M6: điểm và đánh giá công khai chỉ đọc | Đã triển khai | Loại đánh giá ẩn khỏi danh sách và trung bình |
| M6: tổng quan 5 chỉ số | Đã triển khai | Chỉ Admin, số đơn hàng chỉ gồm DELIVERED |
| M6: viết/sửa/kiểm duyệt, khóa khách, doanh thu | Chưa làm | Giai đoạn 5; chỉ đơn DELIVERED đủ điều kiện và tính doanh thu |
| REST công khai 3 endpoint | Đã triển khai | `/api/books`, `/api/books/{id}`, `/api/categories` |
| REST quản trị sách | Đã triển khai | POST/PUT/DELETE `/api/admin/books`, Admin + cookie/CSRF |
| REST giỏ, đơn | Chưa làm | Hoàn thiện theo module tương ứng |
| Docker Compose | Đã triển khai | Web/SQL Server, volumes bền, migrate/initialize/test chủ động |

## B. UI / Razor / Bootstrap

- Giao diện gắn kiểu mạnh (strongly typed) theo `Models/ViewModels.cs` và
  `Models/AdminModels.cs`.
- Đã có: layout responsive, trang chủ, tài khoản, danh sách/chi tiết sách,
  trang lỗi, tổng quan Admin; nhãn VI/EN, tiền VND, giờ UTC+7.
- Đã có UI quản trị sách, thể loại, kho/nhập/lịch sử/ngưỡng. Trình duyệt DB thử riêng
  xác minh nhập 2: tồn 10 thành 12, giữ 3, khả dụng 9; không tràn ngang 390px.
- Các màn giỏ/checkout/đơn/báo cáo sẽ được triển khai tiếp theo khi
  backend và types tương ứng sẵn sàng.

## Thứ tự tiếp theo và tiêu chí

1. **Danh mục quản trị và kho: đã xong.** CRUD thể loại/sách, xóa mềm/khôi phục,
   ảnh <=5 MB JPEG/PNG/WEBP, nhập kho/lịch sử/ngưỡng. Đã kiểm thử quyền,
   trùng tên/SKU, ràng buộc thể loại, rowversion cạnh tranh trên SQL Server.
2. **Giỏ AJAX:** thao tác chính chủ, một dòng mỗi sách, cộng dồn, sửa 0 là xóa;
   lưu theo tài khoản; cảnh báo tồn/ngừng bán, không giữ chỗ.
3. **Đơn COD:** xác nhận lại báo giá; một xác nhận tạo một đơn; snapshot, nhật
   ký, transaction; PENDING/CONFIRMED/SHIPPING/DELIVERED/CANCELLED đúng BR;
   kiểm thử tranh tồn cuối trên SQL Server thử nghiệm tách biệt.
4. **Đánh giá, khách, doanh thu:** điều kiện mua đã giao, kiểm duyệt idempotent,
   khóa phiên, báo cáo theo ngày Việt Nam dựa DeliveredAt/CancelledAt.
5. **Nghiệm thu BTL:** ma trận yêu cầu/test case đầy đủ, kiểm thử SQL Server,
   AJAX/responsive, ảnh chụp, cập nhật chương triển khai và kết luận báo cáo.

## Điểm cần giữ rõ khi triển khai tiếp

- Giữ nguyên tài liệu BA/UML gốc và các luồng nghiệp vụ đã nghiệm thu.
- Có SQL Server local qua Docker Compose; bài test chỉ tạo/xóa DB
  `BookStoreTest_<GUID>`. DB ứng dụng chờ người vận hành chủ động chạy
  migrate/initialize theo `docs/DOCKER_COMPOSE.md`.
- Không có dữ liệu sách mẫu trên DB ứng dụng; dữ liệu giả chỉ tồn tại trong test.
- Tài liệu có thuật ngữ “không có khuyến mãi” nhưng BR02 cho phép DiscountPrice.
  Đang bám BR02: giá giảm từng sách, không mã giảm giá/campaign/thời hạn.
- BA không định nghĩa danh sách sort cụ thể: công khai 4 giá trị được kiểm tra
  hợp lệ trong README. REST dùng tiền tố `/api`.
- Cảnh báo “giá thay đổi trong giỏ” cần so với giá đã hiển thị phía client;
  ERD quy định không lưu giá trong CartItems. Quyết định báo giá/xác nhận phải
  được triển khai ở bước giỏ/checkout, không thêm cột tùy tiện.
