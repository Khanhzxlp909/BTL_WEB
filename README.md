# BookStore — Bài tập lớn Lập trình Web

Website quản lý và bán sách, xây dựng bằng **ASP.NET Core MVC và SQL Server**.
Hỗ trợ khách tra cứu sách, quản trị danh mục và nhập kho; giao diện responsive,
tiếng Việt/tiếng Anh. Môi trường phát triển được quản lý bằng **Docker Compose**.

Yêu cầu nghiệp vụ lấy từ [đề bài BTL](BaiTapLon.doc),
[tài liệu BA](Mau_BTL_Cong_nghe_Java.docx) và [sơ đồ UML](UML/).
Nội dung BA xác định công nghệ C#/ASP.NET Core, dù tên tệp mẫu có chữ Java.

## Mục lục

- [Chức năng hiện có](#chức-năng-hiện-có)
- [Công nghệ](#công-nghệ)
- [Chạy bằng Docker Compose](#chạy-bằng-docker-compose)
- [Quản lý môi trường](#quản-lý-môi-trường)
- [Phát triển bằng .NET CLI](#phát-triển-bằng-net-cli)
- [Kiểm thử](#kiểm-thử)
- [API](#api)
- [Cấu trúc thư mục](#cấu-trúc-thư-mục)
- [Xử lý lỗi thường gặp](#xử-lý-lỗi-thường-gặp)
- [Tiến độ và tài liệu](#tiến-độ-và-tài-liệu)

## Chức năng hiện có

| Nhóm | Chức năng |
|---|---|
| Tài khoản | Đăng ký Customer, đăng nhập/đăng xuất, ghi nhớ đăng nhập, cập nhật hồ sơ, đổi mật khẩu |
| Tra cứu sách | Tìm tên/tác giả/SKU/ISBN; lọc thể loại, giá bán, còn hàng; sắp xếp, phân trang |
| Chi tiết sách | Thông tin, bìa, tồn khả dụng, đánh giá công khai và điểm trung bình |
| Quản trị sách | Thêm/sửa, ngừng bán/khôi phục, tải ảnh JPEG/PNG/WEBP tối đa 5 MB |
| Thể loại | Thêm/sửa/xóa mềm; chặn xóa khi còn sách tham chiếu |
| Kho | Tồn ghi sổ, giữ chỗ, khả dụng; nhập hàng, lịch sử biến động, ngưỡng cảnh báo chung |
| Giỏ hàng | Theo tài khoản; thêm/cộng dồn, sửa, xóa, làm trống bằng jQuery AJAX; không giữ chỗ |
| Đơn COD | Báo giá có chữ ký, xác nhận lại khi đổi giá, chống tạo trùng; lịch sử và hủy đơn chính chủ |
| Xử lý đơn | Admin lọc và chuyển trạng thái đúng bước; giao và thu COD, hủy hoàn giữ chỗ, lịch sử |
| Đánh giá | Customer đã nhận sách viết/sửa; Admin ẩn/hiện, giữ trạng thái ẩn khi sửa nội dung |
| Khách hàng | Tìm/lọc Customer, khóa có lý do, mở khóa; vô hiệu hóa phiên cũ |
| Báo cáo | Doanh thu, số giao/hủy theo ngày Việt Nam; biểu đồ, bảng ngày, top 5 sách |
| Tổng quan Admin | Số đơn đã giao/chờ xác nhận/đã hủy, sách đang bán, khách hàng |
| Giao diện | Responsive, VI/EN, giá VND nguyên đồng, hiển thị giờ UTC+7 |

Sáu module nghiệp vụ đã có mã và giao diện. Kiểm thử dùng dữ liệu tách biệt;
DB ứng dụng cần được người vận hành khởi tạo theo hướng dẫn bên dưới.
Xem [phạm vi nghiệm thu và phần còn lại](docs/IMPLEMENTATION_PLAN.md).

Các quy tắc chính:

- Tài khoản tự đăng ký chỉ có vai trò Customer; chức năng quản trị yêu cầu Admin.
- Sách mới có tồn kho bằng 0; form sách không cho chỉnh số lượng tồn.
- Khả dụng = tồn ghi sổ − giữ chỗ; nhập kho và lịch sử được lưu cùng transaction.
- Nhập bằng phiên bản tồn cũ bị từ chối; cần tải lại form và xác nhận số liệu mới.
- SKU và tên thể loại vẫn được giữ duy nhất sau khi ngừng bán/xóa mềm.
- Ảnh bìa được kiểm tra nội dung, giải mã rồi chuyển WEBP với tên tệp hệ thống sinh.
- Đặt đơn tăng giữ chỗ; giao giảm cả tồn và giữ chỗ; hủy chỉ hoàn giữ chỗ một lần.
- Chỉ COD, phí giao hàng 0đ. Giá/giỏ đổi sau khi mở checkout phải xác nhận báo giá mới.
- Một token xác nhận chỉ tạo một đơn, kể cả gửi lặp; token báo giá có thời hạn 24 giờ.
- Doanh thu tính từ đơn DELIVERED theo DeliveredAt; số hủy theo CancelledAt.

## Công nghệ

| Thành phần | Công nghệ |
|---|---|
| Backend | .NET 10 LTS, ASP.NET Core MVC, C# |
| Dữ liệu | Entity Framework Core 10, SQL Server 2022 Developer |
| Xác thực | ASP.NET Core Identity, cookie, phân vai Customer/Admin |
| Giao diện | Razor Views, Bootstrap 5.3.8, jQuery 3.7.1, CSS/JS |
| Xử lý ảnh | SkiaSharp |
| Kiểm thử | xUnit, ASP.NET Core MVC Testing, SQLite in-memory, SQL Server test |
| Môi trường | Docker Compose, Dockerfile multi-stage |

Bootstrap/jQuery được phục vụ local; trình duyệt không cần CDN.

## Chạy bằng Docker Compose

### 1. Chuẩn bị

- Docker Engine hoặc Docker Desktop đang chạy, có lệnh `docker compose`.
- Máy x86_64 chạy được image SQL Server Linux.
- Node.js 22+ để sinh `.env` bằng script; có thể tự sao chép `.env.example`
  thành `.env` nếu không dùng Node.js. Không cần .NET SDK trên máy host.

Chạy các lệnh tại thư mục chứa `compose.yaml`.

### 2. Tạo cấu hình

```bash
node scripts/compose-env.mjs
docker compose config --quiet
```

Script tạo `.env` với mật khẩu ngẫu nhiên, không in mật khẩu và không ghi đè
tệp có sẵn. Nếu đã có `.env`, dùng cấu hình hiện tại, bỏ qua bước sinh tệp.

| Biến trong `.env` | Ý nghĩa |
|---|---|
| `BOOKSTORE_SQL_PASSWORD` | Mật khẩu SQL Server; bắt buộc |
| `BOOKSTORE_ADMIN_USERNAME` | Tên đăng nhập Admin; mặc định `bookstore_admin` |
| `BOOKSTORE_ADMIN_EMAIL` | Email tài khoản Admin |
| `BOOKSTORE_ADMIN_PASSWORD` | Mật khẩu dùng khi tạo Admin lần đầu |
| `BOOKSTORE_HTTP_PORT` | Cổng web local; mặc định `5087` |

Không đưa `.env` vào Git, ảnh chụp hoặc tài liệu. Không có mật khẩu dùng chung
được ghi trong mã nguồn. Tên Admin cần 3–50 ký tự chữ không dấu/số/gạch dưới;
mật khẩu 10–64 ký tự, giữ nguyên khoảng trắng.

### 3. Build và mở SQL Server

```bash
docker compose build web migrate
docker compose up -d sqlserver
docker compose ps
```

Chờ `sqlserver` ở trạng thái `healthy`.

### 4. Khởi tạo DB và Admin lần đầu

**Hai lệnh `migrate` và `initialize` dưới đây ghi schema và dữ liệu vào DB local
`BookStore`. Kiểm tra đúng cấu hình trước khi chạy. Web không tự thực hiện các
bước này khi khởi động.**

```bash
docker compose --profile tools run --rm migrate
docker compose --profile tools run --rm initialize
docker compose up -d web
```

- `migrate`: áp dụng EF Core migrations.
- `initialize`: tạo vai trò Customer/Admin, Admin theo `.env`, ngưỡng tồn thấp 5.
  Chạy lại không đổi mật khẩu Admin có sẵn, không nâng quyền Customer trùng tên.
- Không tự tạo sách hoặc dữ liệu bán hàng mẫu.

### 5. Sử dụng

Mở **<http://localhost:5087>**, hoặc cổng đã đặt trong `.env`.

1. Đăng nhập tại `/tai-khoan/dang-nhap` bằng thông tin Admin trong `.env`.
2. Tạo thể loại tại `/admin/the-loai`.
3. Tạo sách tại `/admin/sach`; tồn ban đầu bằng 0.
4. Nhập thêm hàng tại `/admin/kho`, cung cấp mã phiếu và lý do.
5. Mở `/sach` để kiểm tra sách, giá bán và trạng thái còn hàng.

Customer tự đăng ký tại `/tai-khoan/dang-ky`. Hồ sơ và đổi mật khẩu nằm tại
`/tai-khoan/ho-so`, `/tai-khoan/doi-mat-khau`.

Mua hàng: `/sach` thêm giỏ, `/gio-hang` sửa số lượng, `/dat-hang` xác nhận báo giá
và COD, `/don-hang` theo dõi/hủy đơn PENDING hoặc CONFIRMED. Admin xử lý tại
`/admin/don-hang`; bắt buộc xác nhận đã giao/thu tiền trước khi DELIVERED.
Admin hủy đơn phải có lý do, kể cả hủy đơn của chính mình.

Đánh giá: `/sach/{bookId}/danh-gia` chỉ Customer đã có đơn DELIVERED chứa sách.
Admin kiểm duyệt tại `/admin/danh-gia`, quản lý khách tại `/admin/khach-hang`,
báo cáo tại `/admin/bao-cao` (mặc định 30 ngày).

## Quản lý môi trường

| Service | Vai trò |
|---|---|
| `web` | Website MVC, cổng `127.0.0.1:5087` mặc định; chạy user `app` |
| `sqlserver` | SQL Server có healthcheck; chỉ truy cập trong network Compose |
| `migrate` | Công cụ migration chủ động, profile `tools` |
| `initialize` | Công cụ khởi tạo vai trò/Admin/ngưỡng chủ động, profile `tools` |
| `test` | Kiểm thử tách biệt, profile `test` |

```bash
docker compose up -d                       # Mở web và SQL Server đã cấu hình
docker compose ps                          # Xem trạng thái
docker compose logs --tail=100 web         # Xem log web
docker compose logs --tail=100 sqlserver   # Xem log SQL Server
docker compose restart web                 # Khởi động lại web
docker compose stop                        # Dừng dịch vụ, giữ container/dữ liệu
docker compose start                       # Mở lại container đã dừng
```

Sau khi sửa mã:

```bash
docker compose build web
docker compose up -d --no-deps web
```

Nếu có migration mới, build lại service `migrate`, áp dụng chủ động theo hướng
dẫn thay đổi tương ứng. SQL Server, ảnh bìa và khóa cookie được lưu bằng named
volumes `sql-data`, `covers`, `keys`.

`docker compose down` gỡ container/network nhưng giữ volumes. **`down -v` xóa
volumes và dữ liệu; không dùng nếu cần giữ DB, ảnh và khóa phiên.**
Stack hiện tại dành cho phát triển local, sử dụng HTTP localhost.

Chi tiết: [hướng dẫn Docker Compose](docs/DOCKER_COMPOSE.md).

## Phát triển bằng .NET CLI

Cần .NET SDK 10, Node.js 22+, pnpm 11.8.0; SQL Server riêng nếu thao tác dữ liệu.

```bash
pnpm install
dotnet tool restore
pnpm build
pnpm test
```

`pnpm install` chép Bootstrap/jQuery và giấy phép vào `wwwroot/lib`.
Chạy `pnpm vendor` nếu thư viện local bị thiếu.

**Bash — chạy giao diện tại cổng 5086:**

```bash
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://localhost:5086 pnpm dev
```

**PowerShell:**

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = "http://localhost:5086"
pnpm dev
```

Chưa cấu hình DB: trang chủ hiển thị hướng dẫn; thao tác dữ liệu trả
`DATABASE_NOT_CONFIGURED`. Chế độ chạy host không tự đọc `.env` của Compose,
SQL Server trong Compose cũng không publish cổng ra host.

### Kết nối SQL Server riêng

Đặt biến `ConnectionStrings__BookStore` trong môi trường của terminal/IDE trước
khi chạy EF CLI hoặc web. Ví dụ Bash, thay toàn bộ giá trị giữ chỗ:

```bash
export ConnectionStrings__BookStore='Server=<host>,1433;Database=BookStore;User Id=<user>;Password=<password>;Encrypt=True;TrustServerCertificate=False;'
```

Web cũng hỗ trợ User Secrets cho `ConnectionStrings:BookStore`. Tuy nhiên,
design-time factory của EF CLI hiện đọc biến môi trường; chỉ đặt User Secrets
không đủ cho lệnh migration. Với SQL local dùng chứng chỉ tự ký, cấu hình
`TrustServerCertificate=True` riêng cho môi trường phát triển nếu cần.

Để khởi tạo DB phát triển đã chọn, chủ động chạy:

```bash
dotnet ef database update --project src/BookStore.Web
```

Cấu hình `BootstrapAdmin:UserName`, `BootstrapAdmin:Email`,
`BootstrapAdmin:Password` qua User Secrets; hoặc các biến môi trường
`BootstrapAdmin__UserName`, `BootstrapAdmin__Email`, `BootstrapAdmin__Password`.
Sau đó tạo tài khoản hệ thống:

```bash
dotnet run --project src/BookStore.Web -- --initialize
```

Chạy trong môi trường `Development` nếu dùng User Secrets. Không lưu chuỗi kết
nối/mật khẩu vào `appsettings.json`. Có thể dùng [script SQL](docs/database.sql)
thay cho EF migration trên DB phát triển đã chọn, bằng công cụ hỗ trợ batch `GO`.

## Kiểm thử

**Toàn bộ bộ kiểm thử trong Compose:**

```bash
docker compose --profile test run --rm --build test
```

**Kiểm thử trên host:**

```bash
pnpm build
pnpm test
```

Kết quả nghiệm thu ngày **30/10/2026**:

- Build: **0 warning, 0 error**.
- Compose: **69/69 PASS**, gồm 7 bài SQL Server thật.
- Host: 62 bài độc lập SQL Server; 7 bài SQL opt-in bỏ qua khi chưa đặt
  `BOOKSTORE_TEST_SQLSERVER`.

Các bài HTTP dùng SQLite in-memory riêng. Bài SQL Server chỉ tạo/xóa DB ngẫu
nhiên `BookStoreTest_<GUID>`; không dùng DB ứng dụng. Đã kiểm tra migration,
cạnh tranh nhập cùng rowversion, tranh cuốn cuối, đặt trùng, rollback tạo/giao/
hủy đơn, báo cáo SQL, quyền Admin, CSRF, validation, upload, xóa mềm và phiên.

Chi tiết: [test case và giới hạn xác minh](docs/TEST_CASES.md).

## API

| Phương thức | Đường dẫn | Quyền |
|---|---|---|
| `GET` | `/api/books` | Công khai |
| `GET` | `/api/books/{id}` | Công khai |
| `GET` | `/api/categories` | Công khai |
| `POST` | `/api/admin/books` | Admin |
| `PUT` | `/api/admin/books/{id}` | Admin |
| `DELETE` | `/api/admin/books/{id}` | Admin; ngừng bán, không xóa cứng |
| `GET` | `/api/cart` | Đã đăng nhập; giỏ chính chủ |
| `POST` | `/api/cart/items` | Đã đăng nhập; body `bookId`, `quantity` |
| `PUT` / `DELETE` | `/api/cart/items/{id}` | Dòng giỏ chính chủ; PUT `quantity=0` để xóa |
| `DELETE` | `/api/cart` | Làm trống giỏ chính chủ |
| `GET` | `/api/orders/quote` | Cart + Input có token báo giá do server ký |
| `POST` | `/api/orders` | Đặt COD; CheckoutInput và token đã nhận |
| `GET` | `/api/orders`, `/api/orders/{id}` | Đơn chính chủ |
| `POST` | `/api/orders/{id}/cancel` | Hủy đơn chính chủ, body `reason` |
| `GET` | `/api/admin/orders` | Admin; lọc mọi đơn |
| `PUT` | `/api/admin/orders/{id}/status` | Admin; `status`, `confirmCodCollected` |
| `POST` | `/api/admin/orders/{id}/cancel` | Admin; `reason` bắt buộc |

Ví dụ tìm sách:

```text
/api/books?Search=lap%20trinh&InStock=true&Sort=price-asc&Page=1&PageSize=12
```

- Bộ lọc: `Search`, `CategoryId`, `MinPrice`, `MaxPrice`, `InStock`.
- Sắp xếp `Sort`: `newest`, `price-asc`, `price-desc`, `title`.
- `Page` bắt đầu 1, `PageSize` mặc định 12, tối đa 100; trang quá lớn được đưa
  về trang cuối hợp lệ. Giá lọc là giá giảm nếu có, số nguyên đồng.
- Chi tiết trả tối đa 20 đánh giá công khai mới nhất; tổng số/điểm trung bình
  tính trên toàn bộ đánh giá công khai.
- Body tạo/sửa dùng các trường `BookInput` trong `Models/AdminModels.cs`.
  Không nhận tồn kho hoặc đường dẫn ảnh từ client; upload qua form MVC.
- API Admin dùng cookie đăng nhập và anti-forgery header `X-CSRF-TOKEN` lấy từ
  form cùng phiên. POST/PUT/DELETE đều cần token; chưa có xác thực bằng JWT.
- Lỗi JSON thống nhất: `code`, `message`, `details` theo trường nếu có,
  `traceId`; HTTP đúng ngữ nghĩa. API cần đăng nhập trả 401 JSON.
- Giỏ trả thêm `unitPriceText`, `lineTotalText`, `subtotalText`, `totalAmountText`
  để AJAX hiển thị nguyên đồng chính xác cả khi vượt giới hạn số nguyên JavaScript.
- CheckoutInput gồm `recipientName`, `phone`, `shippingAddress`, `note`,
  `quoteToken`, `confirmQuote=true`. Server tính tiền; không nhận giá/tổng từ client.

## Cấu trúc thư mục

```text
.
├── src/BookStore.Web/
│   ├── Controllers/       # MVC và REST API
│   ├── Data/              # DbContext, migrations, khởi tạo hệ thống
│   ├── Domain/            # Thực thể và trạng thái nghiệp vụ
│   ├── Infrastructure/    # Identity, validation, lỗi, định dạng/ngôn ngữ
│   ├── Models/            # DTO, form, query, hợp đồng service
│   ├── Services/          # Danh mục, ảnh bìa, kho
│   ├── Views/             # Razor Views và layout
│   └── wwwroot/           # CSS/JS, thư viện local, ảnh tải lên
├── tests/BookStore.Web.Tests/
├── scripts/               # Sinh .env và chép thư viện giao diện
├── docs/                  # Hướng dẫn, tiến độ, test case, SQL
├── UML/                   # Use case, ERD, sitemap, wireframe
├── BookStore.slnx
├── Dockerfile
└── compose.yaml
```

## Xử lý lỗi thường gặp

| Hiện tượng | Nguyên nhân / cách xử lý |
|---|---|
| Thiếu `BOOKSTORE_SQL_PASSWORD` | Tạo `.env` hoặc điền biến bắt buộc, kiểm tra lại `docker compose config --quiet` |
| `COMPOSE_ENV_EXISTS` | `.env` đã tồn tại; script giữ nguyên nội dung. Dùng cấu hình hiện có |
| SQL Server chưa healthy | Xem log `sqlserver`; kiểm tra mật khẩu đủ mạnh, kiến trúc CPU và tài nguyên Docker |
| Web không mở được cổng | Kiểm tra cổng 5087 có bị chiếm; đổi `BOOKSTORE_HTTP_PORT`, chạy lại `docker compose up -d web` |
| `DATABASE_NOT_CONFIGURED` | Chưa đặt kết nối cho web chạy host; cấu hình `ConnectionStrings__BookStore` |
| `DATABASE_OPERATION_FAILED` | Kiểm tra SQL Server, kết nối, schema; DB mới cần chạy `migrate` chủ động. Dùng `traceId` tra lỗi |
| `CUSTOMER_ROLE_NOT_INITIALIZED` | Chạy `initialize` chủ động sau migration, với đủ cấu hình Admin |
| `INVENTORY_CONCURRENCY_CONFLICT` | Tồn đã thay đổi; lần nhập này chưa lưu. Tải lại form, kiểm tra tồn mới rồi xác nhận |
| `COVER_IMAGE_INVALID` | Chọn JPEG/PNG/WEBP thật, tối đa 5 MB; ảnh không vượt 8000 px mỗi chiều hoặc 24 triệu điểm ảnh |
| Thiếu CSS/JS khi chạy host | Chạy `pnpm install` hoặc `pnpm vendor` |

Đổi mật khẩu SQL trong `.env` không tự đổi mật khẩu tài khoản trong volume đã
tồn tại. Dùng cấu hình khớp với DB hoặc đổi bằng thao tác quản trị SQL chủ động;
không xóa volume để xử lý lỗi kết nối.

## Tiến độ và tài liệu

Đã có đủ sáu module theo BA. Cần người vận hành khởi tạo DB ứng dụng trước khi
dùng; báo cáo BTL trong tệp Word và ảnh minh họa bàn giao chưa được cập nhật tự động.

- [Tiến độ đối chiếu BA và lộ trình](docs/IMPLEMENTATION_PLAN.md)
- [Vận hành Docker Compose](docs/DOCKER_COMPOSE.md)
- [Test case và kết quả nghiệm thu](docs/TEST_CASES.md)
- [Script schema SQL Server](docs/database.sql)
