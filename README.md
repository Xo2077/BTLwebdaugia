# BidHub — Website đấu giá sản phẩm trực tuyến

Bài tập lớn môn **Lập trình web** của Trường Đại học Giao thông Vận tải, năm 2026. BidHub hỗ trợ đăng sản phẩm, kiểm duyệt, đặt giá, chốt phiên và xử lý đơn hàng. Ví và giao nhận được mô phỏng trong phạm vi bài tập.

## Chức năng

| Vai trò | Chức năng |
|---|---|
| Khách | Xem phiên công khai, tìm kiếm, lọc, phân trang, đăng ký và đăng nhập. |
| Thành viên | Cập nhật hồ sơ, đăng sản phẩm và ảnh, lập lịch, đặt giá, xem ví và xử lý đơn liên quan. |
| Quản trị viên | CRUD danh mục, duyệt hoặc từ chối sản phẩm và phiên, theo dõi dữ liệu nghiệp vụ. |

## Công nghệ

- ASP.NET Core MVC trên .NET 10, Razor và Entity Framework Core.
- SQL Server, CSS, jQuery AJAX và EasyMDE.
- Cookie xác thực, Session và tài nguyên giao diện EN/VN.

## Cài đặt và chạy

Cần .NET SDK 10, SQL Server đang chạy và công cụ chạy script T-SQL, chẳng hạn SQL Server Management Studio. Các lệnh dưới đây chạy từ thư mục chứa solution.

### 1. Tạo cơ sở dữ liệu

Kết nối vào SQL Server bằng tài khoản có quyền tạo cơ sở dữ liệu. Chạy `CSDL/BidHub_Schema_SQLServer.sql` trên instance chưa có database `CSDLWEBDAUGIA`. Script tự tạo database và 10 bảng, gồm cả bảng ảnh đại diện. Có thể bỏ qua bước này nếu instance đã có database với schema phù hợp.

Không cần chạy `dataseed1.sql`. Danh mục và các phiên demo có thể tạo từ giao diện.

### 2. Cấu hình kết nối

Cập nhật `ConnectionStrings:BidHubConnection` trong `BTLwebdaugia/appsettings.json` cho đúng instance SQL Server. Ví dụ cấu hình phát triển với xác thực Windows:

```json
{
  "ConnectionStrings": {
    "BidHubConnection": "Server=localhost;Database=CSDLWEBDAUGIA;Trusted_Connection=True;TrustServerCertificate=True"
  }
}
```

Đổi `localhost` thành tên instance thực tế nếu dùng named instance. Tài khoản Windows chạy ứng dụng cần có quyền đọc và ghi dữ liệu trong database. `TrustServerCertificate=True` trong ví dụ dùng cho môi trường phát triển cục bộ.

### 3. Khởi chạy

Mở `BTLwebdaugia.slnx` trong Visual Studio, chọn project `BTLwebdaugia` và chạy profile **https**. Hoặc dùng terminal:

```powershell
dotnet dev-certs https --trust
dotnet restore .\BTLwebdaugia\BTLwebdaugia.csproj
dotnet run --project .\BTLwebdaugia\BTLwebdaugia.csproj --launch-profile https
```

Profile hiện tại cấu hình địa chỉ **https://localhost:7141**. Nếu đã đổi cổng, dùng địa chỉ mà ứng dụng in ra khi khởi chạy.

### 4. Chuẩn bị tài khoản và danh mục

Đăng ký tài khoản qua giao diện. Tài khoản mới có role `User`. Với database demo, người có quyền quản trị SQL có thể cấp role `Admin` cho một tài khoản đã đăng ký:

```sql
USE CSDLWEBDAUGIA;

UPDATE dbo.Users
SET [Role] = 'Admin'
WHERE Email = N'admin-demo@example.com';
```

Thay email ví dụ bằng email tài khoản demo đã đăng ký. Đăng xuất rồi đăng nhập lại để cookie nhận role mới. Mở **Admin → Danh mục** và tạo ít nhất một danh mục trước khi đăng sản phẩm. Tạo các tài khoản thành viên riêng cho người bán và người đặt giá.

## Luồng demo

1. Thành viên đăng sản phẩm, tải ảnh và chọn lịch theo giờ Việt Nam. Dùng thời lượng từ **60 phút đến 30 ngày** để phù hợp schema.
2. Admin duyệt sản phẩm và phiên. Phiên hiển thị theo các mốc sắp diễn ra, đang diễn ra và đã kết thúc.
3. Thành viên khác đặt giá trong phiên đang mở. Lịch sử cập nhật qua AJAX.
4. Sau khi phiên chốt, người thắng mở **Đơn hàng của tôi** và thanh toán bằng ví ảo. Người bán xác nhận gửi, sau đó người mua xác nhận đã nhận.

Để demo liền mạch, chuẩn bị sẵn một phiên đang mở và một đơn đã phát sinh từ phiên kết thúc.

## Web API

| Method | URL | Chức năng |
|---|---|---|
| GET | `/api/auctions?page=1&pageSize=10` | Danh sách phiên công khai. Có thể truyền thêm `categoryId`. |
| GET | `/api/auctions/{id}` | Chi tiết một phiên công khai theo mã. |

## Cấu trúc dự án

```text
BTLwebdaugia.slnx
CSDL/
  BidHub_Schema_SQLServer.sql
BTLwebdaugia/
  Controllers/   Điều hướng và xử lý nghiệp vụ
  Data/          DbContext và ánh xạ SQL Server
  Models/        Entity
  ViewModels/    Dữ liệu biểu mẫu và màn hình
  Views/         Razor View, layout và partial
  Services/      Chốt phiên và dữ liệu dùng chung
  Resources/     Tài nguyên ngôn ngữ
  wwwroot/       CSS, JavaScript, ảnh và tệp tải lên
```

Khi chuyển database có dữ liệu sang máy khác, sao chép cả ảnh cần demo trong `BTLwebdaugia/wwwroot/uploads` để các đường dẫn đã lưu tiếp tục hiển thị đúng.
