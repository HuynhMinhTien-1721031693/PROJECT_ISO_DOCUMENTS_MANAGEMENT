# Triển khai Docker (demo cuối kỳ)

Stack gồm **SQL Server 2022**, **Redis 7** (cache), **Elasticsearch 8**, **Web API**, **Blazor Server**. Phù hợp chạy demo trên máy cá nhân có Docker Desktop (Windows/macOS) hoặc Docker Engine (Linux).

## Yêu cầu

- Docker Engine 24+ và plugin Compose V2  
- RAM khuyến nghị **tối thiểu 8 GB** (Elasticsearch + SQL Server)  
- Cổng host trống: **5062** (Blazor), **5075** (API), **9200** (Elasticsearch), **6379** (Redis), **14333** (SQL Server, tuỳ chọn truy cập ngoài)

## Chạy nhanh

Tại thư mục gốc repository (cùng cấp với `docker-compose.yml`):

```bash
copy .env.example .env
docker compose up --build
```

Trên Linux/macOS:

```bash
cp .env.example .env
docker compose up --build
```

Lần đầu SQL Server khởi tạo dữ liệu có thể mất **30–60 giây**. Nếu container `isodoc-api` thoát với lỗi kết nối SQL, chạy lại:

```bash
docker compose up
```

(API đã bật `restart: unless-stopped` để tự thử lại.)

## Địa chỉ sau khi chạy

| Dịch vụ | URL |
|--------|-----|
| Giao diện Blazor | http://localhost:5062 |
| Swagger API | http://localhost:5075/swagger |
| Health API | http://localhost:5075/health |
| Elasticsearch | http://localhost:9200 |
| Redis | localhost:6379 |

## Tài khoản demo (seed trong container)

- **Email:** `admin@local`  
- **Mật khẩu:** `Admin@123`  

Được seed qua `appsettings.Docker.json` + `IdentityDataSeeder` khi `ASPNETCORE_ENVIRONMENT=Docker`.

## Cấu hình

- Biến môi trường: file **`.env`** (mẫu `.env.example`).  
- Chuỗi SQL và JWT có thể ghi đè trong `docker-compose.yml` / `.env`.  
- File lưu cục bộ (blob demo): volume Docker **`isodoc-blobs`** gắn vào `/app/App_Data/blobs` trong container API.

## Dừng và xóa dữ liệu

```bash
docker compose down
```

Xóa cả volume (mất DB và ES local):

```bash
docker compose down -v
```

## Nén nộp bài

1. Xóa thư mục `**/bin`, `**/obj` (hoặc `dotnet clean`) để giảm dung lượng.  
2. **Không** đưa file `.env` chứa mật khẩu thật vào zip nếu nộp công khai — chỉ gửi `.env.example`.  
3. Nén toàn bộ thư mục project (trừ `.git` nếu giảng viên không cần lịch sử).

## Hạn chế (demo)

- Môi trường **Docker** bật Swagger + migrate + seed — **không** dùng làm production.  
- Mật khẩu SA và JWT trong file mẫu chỉ phục vụ demo.

Xem thêm phần tổng quan trong `README.md`.
