# Quản lý BookStore bằng Docker Compose

Compose dành cho môi trường phát triển local, tên project `bookstore-btl`.
Không dùng chung container/volume/DB với dự án khác.

## Thành phần

| Service | Chức năng | Khởi chạy |
|---|---|---|
| `sqlserver` | SQL Server 2022 Developer, healthcheck bằng sqlcmd | Mặc định |
| `web` | ASP.NET Core MVC, cổng localhost 5087 | Mặc định |
| `migrate` | EF CLI áp dụng migration, thoát khi xong | Chủ động, profile tools |
| `initialize` | Tạo vai trò/Admin/ngưỡng tồn, thoát khi xong | Chủ động, profile tools |
| `test` | Kiểm thử SQLite và SQL Server tách biệt | Chủ động, profile test |

- Dockerfile multi-stage: Node build tài nguyên local; SDK restore/publish;
  runtime ASP.NET chạy user `app`; target `migration` và `test` riêng.
- SQL Server không publish cổng host. Web bind `127.0.0.1`, mặc định 5087 để
  không đụng web chạy bằng .NET CLI ở cổng 5086.
- Named volumes: `sql-data` (DB), `covers` (ảnh bìa), `keys` (Data Protection).
  Restart/recreate container không làm mất dữ liệu và khóa cookie.
- `ASPNETCORE_ENVIRONMENT=Development` cho HTTP local. Không phải cấu hình
  production; SQL dùng chứng chỉ local và `TrustServerCertificate=True`.

## Khởi tạo cấu hình local

```bash
pnpm compose:env
docker compose config --quiet
docker compose build web migrate test
docker compose up -d sqlserver
docker compose ps
```

`pnpm compose:env` sinh `.env` với mật khẩu SQL/Admin ngẫu nhiên, quyền 0600;
không in mật khẩu, không ghi đè `.env` hiện có. Hoặc tự chép `.env.example`
thành `.env` và điền mật khẩu. .env bị loại khỏi Git và Docker build context.
Thông tin đăng nhập Admin xem trực tiếp trong .env; không gửi vào log/tài liệu.

Không cần cài .NET SDK trên host khi dùng các service Compose.

## Schema và tài khoản hệ thống

**Hai lệnh sau ghi schema và dữ liệu vào DB local của project `bookstore-btl`.
Chỉ chạy chủ động sau khi đã xác nhận cấu hình và SQL Server healthy. Web không
tự chạy hai bước này khi khởi động.**

```bash
docker compose --profile tools run --rm migrate
docker compose --profile tools run --rm initialize
docker compose up -d web
```

Mở <http://localhost:5087>. Dùng `BOOKSTORE_ADMIN_USERNAME` và
`BOOKSTORE_ADMIN_PASSWORD` trong `.env`. Đăng nhập quản trị, tạo thể loại, tạo
sách (tồn 0), vào Kho nhập thêm hàng. Không có dữ liệu sách mẫu tự động.

Migration chỉ chạy khi gọi service migrate. Khởi tạo lặp không đổi mật khẩu
Admin có sẵn, không tự nâng quyền Customer trùng tên.

## Lệnh quản lý hằng ngày

```bash
docker compose up -d
docker compose ps
docker compose logs --tail=100 web
docker compose logs --tail=100 sqlserver
docker compose restart web
docker compose stop
docker compose start
```

Cập nhật mã:

```bash
docker compose build web
docker compose up -d --no-deps web
```

Nếu có migration mới, build lại `migrate` và chủ động áp dụng trước theo
hướng dẫn thay đổi tương ứng. Không tự seed hay reset DB.

`docker compose down` gỡ container/network nhưng giữ named volumes. Không dùng
`down -v` hoặc xóa volumes nếu muốn giữ dữ liệu. Không chạy lệnh dọn hệ thống
Docker toàn máy để quản lý riêng dự án này.

## Kiểm thử SQL Server riêng

```bash
docker compose --profile test run --rm --build test
```

- Các bài HTTP dùng SQLite in-memory như trước.
- Bài SQL Server tạo database `BookStoreTest_<GUID>` mới trên service sqlserver;
  chỉ DB test này được tạo/xóa. Không chạm DB `BookStore`.
- Bài này kiểm tra hai yêu cầu nhập cùng rowversion: chỉ một thành công; tồn
  và lịch sử nhất quán; lỗi ghi lịch sử phải rollback tồn.
- Có thêm kiểm thử SQL Server tranh cuốn cuối, cùng xác nhận không tạo hai đơn,
  rollback khi lỗi đặt/giao/hủy và báo cáo theo ngày Việt Nam. Tổng nghiệm thu
  30/10/2026: 69/69 PASS, gồm 7 bài SQL Server tách biệt.
- Test mặc định `pnpm test` trên host bỏ qua bài SQL Server nếu thiếu
  `BOOKSTORE_TEST_SQLSERVER`; không tự lấy kết nối ứng dụng.

## Khắc phục lỗi cụ thể

- `BOOKSTORE_SQL_PASSWORD` thiếu: chạy `pnpm compose:env` hoặc điền .env.
- SQL không healthy: xem log sqlserver; mật khẩu phải đủ độ mạnh, Docker đủ
  tài nguyên, image SQL Server cần CPU x86_64. Không xóa volume để thử lại.
- Web báo `DATABASE_OPERATION_FAILED`/thiếu bảng: kiểm tra service sqlserver,
  áp dụng migration chủ động đúng DB local; gửi traceId khi cần tra lỗi.
- `CUSTOMER_ROLE_NOT_INITIALIZED`: chạy service initialize với đủ cấu hình
  BootstrapAdmin trong .env.
- Đổi mật khẩu SQL trong .env không tự đổi mật khẩu tài khoản đã có trong volume.
  Dùng cấu hình đúng với volume hiện tại hoặc đổi mật khẩu qua SQL Server bằng
  quy trình quản trị chủ động; không reset dữ liệu.
- Lỗi tải image/build do mạng: chạy lại build; không ảnh hưởng dữ liệu volumes.
