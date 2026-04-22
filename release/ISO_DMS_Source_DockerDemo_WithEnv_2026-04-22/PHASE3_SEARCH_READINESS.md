# Giai đoạn 3 — Tìm kiếm & độ sẵn sàng (checklist)

## Fallback SQL khi Elasticsearch không dùng được

- [x] `ResilientSearchService` bọc `ElasticsearchService`: lỗi HTTP / phản hồi ES lỗi → gọi `IDocumentRepository.SearchDocumentsAsync`.
- [x] Lọc SQL: mã (`Code`), tiêu đề, mô tả, tag, `Standard`, `Status`, `Category`, `OwnerId`, khoảng `UpdatedAt` (FromDate/ToDate), sắp xếp theo `SortBy`/`SortDesc`.
- [ ] Kiểm tra tay: tắt ES hoặc sai URI → API `GET .../documents/search` vẫn trả kết quả từ SQL.

## Đồng bộ index Elasticsearch

| Sự kiện / luồng | Cập nhật ES |
|-----------------|------------|
| `DocumentCreatedEvent` | `IndexDocumentAsync` (handler hiện có) |
| `DocumentIndexSyncEvent` | `UpdateDocumentIndexAsync` (metadata, submit, bước duyệt, reject, archive, publish, …) |
| `DeleteDocumentCommand` | `RemoveDocumentAsync` (đã có) |

- [ ] Sau **Publish**: kiểm tra trạng thái trên ES khớp SQL.
- [ ] Sau **Submit for review** / **Advance to final** / **Reject** / **Archive**: kiểm tra `status` trên index.
- [ ] Sau **Update metadata** (title/tags): kiểm tra full-text trên ES (hoặc SQL fallback).

## Giám sát

- [x] Log lỗi ES: `DebugInformation` / exception cho index, update, delete, search.
- [x] Health check tùy chọn: `IsoDoc:Elasticsearch:ParticipateInHealthChecks` = `true` → check `elasticsearch` (trạng thái **Degraded** nếu ES down, không làm fail toàn bộ `/health` theo cấu hình mặc định của ASP.NET Core).

## Kiểm thử tự động

- Chạy: `dotnet test src/tests/IsoDoc.Infrastructure.Tests/IsoDoc.Infrastructure.Tests.csproj`
- Bao phủ: `InMemoryDocumentRepository.SearchDocumentsAsync` (keyword + standard + status).
