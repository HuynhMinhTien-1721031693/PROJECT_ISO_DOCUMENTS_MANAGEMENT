# Hướng dẫn sử dụng — ISO Documents Management

## 1. Hai cách chạy

### A. Docker (khuyến nghị cho demo nộp bài)

1. Cài **Docker Desktop** (hoặc Docker Engine + Compose).  
2. Mở terminal tại thư mục gốc project (có file `docker-compose.yml`).  
3. Chạy:

```bash
docker compose up --build
```

4. Đợi SQL Server khởi động xong (khoảng 30–60 giây lần đầu). Nếu API restart vài lần là bình thường.  
5. Mở trình duyệt: **http://localhost:5062** (Blazor).  
6. API Swagger: **http://localhost:5075/swagger**.

Chi tiết biến môi trường, cổng, xóa volume: xem **`DOCKER.md`**.

### B. Chạy trực tiếp trên máy (.NET SDK 8)

1. Sao chép `src/04_WebAPI/IsoDoc.WebAPI/appsettings.Development.json.example` thành `appsettings.Development.json`, rồi điền SQL Server, Elasticsearch (nếu có) và tài khoản seed — file `.json` (không phải `.example`) được gitignore để tránh commit connection string.  
2. Chạy API:

```bash
dotnet run --project src/04_WebAPI/IsoDoc.WebAPI
```

3. Chạy Blazor (terminal khác), chỉnh `Api:BaseUrl` trong `src/05_Frontend/IsoDoc.Blazor/appsettings.json` trùng cổng API:

```bash
dotnet run --project src/05_Frontend/IsoDoc.Blazor
```

4. Mở URL Blazor trong `launchSettings.json` (thường http://localhost:5062).

---

## 2. Đăng nhập (Docker)

| Trường | Giá trị |
|--------|---------|
| Email | `admin@local` |
| Mật khẩu | `Admin@123` |

(Tài khoản được seed khi chạy với môi trường Docker — xem `appsettings.Docker.json`.)

---

## 3. Thao tác chính trên giao diện

1. **Đăng nhập** — trang Login.  
2. **Documents** — tra cứu, lọc danh sách tài liệu.  
3. **New document** — tạo / tải lên tài liệu (theo quyền).  
4. **Chi tiết tài liệu** — xem metadata và lịch sử phiên bản.  
5. **Workflow** — danh sách chờ phê duyệt (tài khoản có vai trò approver).  
6. **Settings** — xem URL API đang cấu hình.

---

## 4. API nhanh (Swagger)

- Vào `/swagger` trên cổng Web API.  
- Dùng **Authorize** và dán JWT sau khi gọi `POST /api/v1/auth/login`.

---

## 5. Nén nộp bài

- Chạy `dotnet clean` (tuỳ chọn) để giảm dung lượng.  
- Nén cả thư mục project (có `docker-compose.yml`, `DOCKER.md`, mã nguồn).  
- Không nộp file `.env` chứa mật khẩu thật; có thể chỉ nộp `.env.example`.
