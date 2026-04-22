# Giai đoạn 6 — Chất lượng & go-live

Tài liệu này tóm tắt những gì đã được đưa vào repo và việc còn lại cần làm trên môi trường thật (UAT, vận hành).

## 1. Kiểm thử tự động

### Integration tests (API)

- **Project:** `src/tests/IsoDoc.Integration.Tests`
- **Chạy:** `dotnet test src/tests/IsoDoc.Integration.Tests/IsoDoc.Integration.Tests.csproj`
- **Phạm vi:** môi trường `IntegrationTests`, không SQL/Redis, Elasticsearch URI cố ý không phục vụ (fallback SQL/in-memory), auth config-file, JWT test.
- **Kịch bản:** đăng nhập sai → 401; tài liệu không token → 401; luồng đăng nhập → upload PDF → submit workflow → hai bước phê duyệt → tìm kiếm (Elasticsearch lỗi → fallback repository).

### E2E Blazor (Playwright)

- **Thư mục:** `e2e/`
- **Cài đặt:** `cd e2e && npm install && npm run install:browsers`
- **Chạy:** nâng **WebAPI** và **Blazor** (profile `http`/`https` theo `launchSettings`), sau đó `npm test` trong `e2e/`.
- **Biến môi trường:** `ISODOC_BASE_URL` (mặc định `http://localhost:5062`).
- **Smoke hiện tại:** trang login hiển thị; truy cập `/` có hướng login hoặc shell ứng dụng (tùy cấu hình auth).

Mở rộng E2E (khuyến nghị): đăng nhập với tài khoản UAT, danh sách tài liệu, mở chi tiết, workflow pending (role QA).

### Unit / component

- `IsoDoc.Infrastructure.Tests` — tìm kiếm in-memory.
- `IsoDoc.Blazor.Tests` — kiểm thử Blazor hiện có.

## 2. Hiệu năng

| Hạng mục | Trạng thái |
|-----------|------------|
| Phân trang API (`page`, `pageSize`, giới hạn 1–100) | Đã có; kiểm tra tải với dữ liệu lớn trên staging |
| Chỉ mục SQL cho list/search | Migration `DocumentSearchIndexes`: `Documents` (IsDeleted+UpdatedAt, Status+UpdatedAt, OwnerId), `ApprovalWorkflows` (DocumentId, DocumentId+Status) |
| Lọc `!IsDeleted` trên truy vấn EF search | Đã bổ sung trong `DocumentSearchFilter.Apply` (IQueryable) |
| File lớn | `RequestSizeLimit` 100MB trên upload; rule ứng dụng 50MB (`DocumentFileUploadRules`); production: cấu hình **Kestrel** / reverse proxy body limit và timeout upload |

## 3. Bảo mật (rà soát nhanh)

| Chủ đề | Ghi chú |
|--------|---------|
| Secrets | Không commit JWT/Blob/Redis thật; dùng Key Vault / biến môi trường / User Secrets dev |
| HTTPS | Bật HTTPS termination (IIS/nginx/Azure); `UseHttpsRedirection` (tắt trong `IntegrationTests` chỉ cho test host) |
| CORS | `Cors:AllowedOrigins` — chỉ domain Blazor/production |
| Rate limit đăng nhập | Policy `login`: cửa sổ 1 phút, `RateLimiting:LoginPermitPerMinute` (mặc định 40; test host nới rộng) |
| Headers | `SecurityHeadersMiddleware`: `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy` |
| HSTS | Bật trên reverse proxy hoặc `app.UseHsts()` khi không phải Development |

## 4. UAT với người dùng thật

1. Chuẩn bị môi trường staging (DB + Blob + Elasticsearch tùy chọn), tài khoản theo vai trò (Controller, QA, ISO Manager, Viewer).
2. Kịch bản UAT tối thiểu: đăng nhập, tạo tài liệu, phiên bản, gửi duyệt, phê duyệt/từ chối, tìm kiếm, tải file, thông báo (nếu bật).
3. Ghi nhận lỗi vào backlog (mức độ, owner, mục tiêu phiên bản).
4. Sign-off UAT trước go-live.

## 5. Checklist go-live

- [ ] `dotnet test` (ít nhất Application + Infrastructure + Integration) pass trên build server
- [ ] Migration DB áp dụng: `dotnet ef database update` (hoặc pipeline tương đương)
- [ ] Blob container tồn tại và quyền đọc/ghi đúng
- [ ] Elasticsearch (nếu dùng): index và health; xác nhận fallback DB chấp nhận được khi ES down
- [ ] JWT signing key đủ dài, xoay key có kế hoạch
- [ ] CORS + URL Blazor/API khớp production
- [ ] Giám sát: log tập trung, cảnh báo 5xx / disk / SQL
- [ ] Runbook: ai liên hệ khi sự cố, RTO/RPO thống nhất

## 6. Backup & restore

### SQL Server

- **Backup định kỳ:** FULL hàng ngày + DIFF (tuỳ RPO) + LOG nếu FULL recovery.
- **Lệnh ví dụ (DB `IsoDocDms`):**  
  `BACKUP DATABASE [IsoDocDms] TO DISK = N'\\backup\IsoDocDms_full.bak' WITH INIT, CHECKSUM;`
- **Restore test:** restore lên instance ẩn, chạy smoke + kiểm tra row count / migration version.
- **Lưu ý:** lưu bản backup ngoài cùng site với server; mã hóa nếu chứa dữ liệu nhạy cảm.

### Azure Blob (nếu dùng)

- **Soft delete** + **versioning** trên storage account (khuyến nghị).
- **AzCopy / lifecycle:** sao chép định kỳ sang secondary region hoặc cool/archive tier theo chính sách lưu trữ.

### Local disk blob (`UseLocalDisk`)

- Sao chép thư mục `LocalDiskRootPath` theo lịch (robocopy/rsync) kèm metadata đường dẫn trong DB.

### Thứ tự restore tham khảo

1. Dừng traffic vào ứng dụng (maintenance).
2. Restore DB → kiểm tra `__EFMigrationsHistory`.
3. Restore Blob khớp `BlobPath` trong bảng phiên bản tài liệu.
4. Khởi động API, chạy health + smoke E2E.
5. Mở lại traffic.

---

**Lệnh tham chiếu nhanh**

```bash
dotnet test src/tests/IsoDoc.Integration.Tests/IsoDoc.Integration.Tests.csproj
cd e2e && npm install && npm run install:browsers && npm test
```
