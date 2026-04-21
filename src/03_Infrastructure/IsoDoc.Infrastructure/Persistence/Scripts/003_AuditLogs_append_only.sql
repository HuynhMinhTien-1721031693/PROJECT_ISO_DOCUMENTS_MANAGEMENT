/*
  ISO 27001 / vận hành: khóa thao tác UPDATE/DELETE trên AuditLogs ở tầng SQL Server.
  Thay [IsoDocAppLogin] bằng login/ user thực tế mà ứng dụng dùng để kết nối (không dùng dbo/sa).

  Lưu ý: EF Core chỉ INSERT; ứng dụng đã chặn Modified/Deleted trong SaveChanges.
  Script này là lớp bảo vệ bổ sung khi ai đó truy cập DB trực tiếp.
*/

-- DENY UPDATE, DELETE ON dbo.AuditLogs TO [IsoDocAppLogin];
-- GO
