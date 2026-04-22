# PROJECT ISO DOCUMENTS MANAGEMENT SYSTEM V3

Architecture & Design Specification  
ISO 9001 · ISO 45001 · ISO 27001 · .NET 8 · Blazor · SQL Server

- Version: `3.0.0`
- Date: `25/03/2026`
- Scope: `Enterprise 50-500 users`
- Platform: `.NET 8 / ASP.NET Core / Blazor`
- Status: `Architecture Draft`

## Table of Contents

1. [Bước 1 - Database Schema (ERD)](#buoc-1---database-schema-erd)
2. [Bước 2 - Solution Structure (.NET 8 Clean Architecture)](#buoc-2---solution-structure-net-8-clean-architecture)
3. [Bước 3 - REST API Endpoints](#buoc-3---rest-api-endpoints)
4. [Bước 4 - Blazor UI Component Architecture](#buoc-4---blazor-ui-component-architecture)
5. [Roadmap triển khai](#roadmap-trien-khai)

---

## Bước 1 - Database Schema (ERD)

Thiết kế cơ sở dữ liệu theo chuẩn normalized, hỗ trợ versioning tài liệu, phân quyền RBAC và audit trail đầy đủ cho ISO 27001.

### 1.1 Core Entities

#### Bảng Users

Quản lý người dùng hệ thống với tích hợp ASP.NET Core Identity.

```sql
-- Users (extends AspNetUsers)
CREATE TABLE Users (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Email         NVARCHAR(256)    NOT NULL UNIQUE,
    FullName      NVARCHAR(200)    NOT NULL,
    DepartmentId  UNIQUEIDENTIFIER NOT NULL REFERENCES Departments(Id),
    IsActive      BIT              NOT NULL DEFAULT 1,
    CreatedAt     DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    LastLoginAt   DATETIME2        NULL
);
```

#### Bảng Documents

Thực thể chính lưu metadata tài liệu ISO. File vật lý lưu tại Blob Storage.

```sql
CREATE TABLE Documents (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Title           NVARCHAR(500)    NOT NULL,
    DocumentCode    NVARCHAR(50)     NOT NULL UNIQUE,  -- e.g. QMS-PR-001
    IsoStandard     NVARCHAR(20)     NOT NULL,          -- 9001 | 45001 | 27001
    Category        NVARCHAR(100)    NOT NULL,          -- Procedure | Policy | Form
    Status          NVARCHAR(30)     NOT NULL DEFAULT 'Draft',
    CurrentVersion  NVARCHAR(20)     NOT NULL DEFAULT '1.0',
    OwnerId         UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    DepartmentId    UNIQUEIDENTIFIER NOT NULL REFERENCES Departments(Id),
    Tags            NVARCHAR(MAX)    NULL,              -- JSON array
    CreatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsDeleted       BIT              NOT NULL DEFAULT 0
);
```

#### Bảng DocumentVersions

Lưu toàn bộ lịch sử phiên bản. Mỗi lần chỉnh sửa tạo một record mới, không ghi đè.

```sql
CREATE TABLE DocumentVersions (
    Id            UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    DocumentId    UNIQUEIDENTIFIER NOT NULL REFERENCES Documents(Id),
    Version       NVARCHAR(20)     NOT NULL,  -- e.g. 1.0, 1.1, 2.0
    BlobPath      NVARCHAR(1000)   NOT NULL,  -- Azure Blob Storage path
    FileSize      BIGINT           NOT NULL,
    FileType      NVARCHAR(20)     NOT NULL,  -- pdf | docx | xlsx
    ChangeNote    NVARCHAR(2000)   NULL,
    UploadedBy    UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    UploadedAt    DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    IsCurrentVersion BIT           NOT NULL DEFAULT 0,
    Checksum      NVARCHAR(64)     NOT NULL   -- SHA-256 for integrity (ISO 27001)
);
```

#### Bảng ApprovalWorkflows + ApprovalSteps

State machine phê duyệt đa cấp. Mỗi tài liệu có một workflow độc lập theo từng phiên bản.

```sql
CREATE TABLE ApprovalWorkflows (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    DocumentId      UNIQUEIDENTIFIER NOT NULL REFERENCES Documents(Id),
    VersionId       UNIQUEIDENTIFIER NOT NULL REFERENCES DocumentVersions(Id),
    CurrentStep     INT              NOT NULL DEFAULT 1,
    TotalSteps      INT              NOT NULL DEFAULT 2,
    Status          NVARCHAR(30)     NOT NULL DEFAULT 'Pending',
    StartedAt       DATETIME2        NOT NULL DEFAULT GETUTCDATE(),
    CompletedAt     DATETIME2        NULL
);

CREATE TABLE ApprovalSteps (
    Id              UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    WorkflowId      UNIQUEIDENTIFIER NOT NULL REFERENCES ApprovalWorkflows(Id),
    StepOrder       INT              NOT NULL,
    ApproverId      UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    Decision        NVARCHAR(20)     NULL,   -- Approved | Rejected | Pending
    Comments        NVARCHAR(2000)   NULL,
    DecidedAt       DATETIME2        NULL
);
```

#### Bảng RBAC - Roles & Permissions

```sql
CREATE TABLE Roles (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Name        NVARCHAR(100) NOT NULL UNIQUE,  -- SystemAdmin | ISOManager | ...
    IsoScope    NVARCHAR(20)  NULL,              -- 9001 | 45001 | 27001 | ALL
    Description NVARCHAR(500) NULL
);

CREATE TABLE Permissions (
    Id          UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    Code        NVARCHAR(100) NOT NULL UNIQUE,  -- document:upload, document:approve
    Description NVARCHAR(300) NULL
);

CREATE TABLE RolePermissions (
    RoleId       UNIQUEIDENTIFIER NOT NULL REFERENCES Roles(Id),
    PermissionId UNIQUEIDENTIFIER NOT NULL REFERENCES Permissions(Id),
    PRIMARY KEY (RoleId, PermissionId)
);

CREATE TABLE UserRoles (
    UserId    UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    RoleId    UNIQUEIDENTIFIER NOT NULL REFERENCES Roles(Id),
    GrantedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    GrantedBy UNIQUEIDENTIFIER NOT NULL REFERENCES Users(Id),
    PRIMARY KEY (UserId, RoleId)
);
```

#### Bảng AuditLogs - ISO 27001

Immutable audit trail: không được phép UPDATE hoặc DELETE.

```sql
CREATE TABLE AuditLogs (
    Id           UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    UserId       UNIQUEIDENTIFIER NULL REFERENCES Users(Id),
    Action       NVARCHAR(100)    NOT NULL,  -- Document.Upload | Auth.Login | ...
    EntityType   NVARCHAR(100)    NULL,
    EntityId     NVARCHAR(200)    NULL,
    OldValues    NVARCHAR(MAX)    NULL,      -- JSON snapshot before
    NewValues    NVARCHAR(MAX)    NULL,      -- JSON snapshot after
    IpAddress    NVARCHAR(45)     NULL,
    UserAgent    NVARCHAR(500)    NULL,
    OccurredAt   DATETIME2        NOT NULL DEFAULT GETUTCDATE()
);

-- Ledger table for tamper-evident audit (SQL Server 2022+)
ALTER TABLE AuditLogs SET (SYSTEM_VERSIONING = OFF);
-- Recommend: SQL Ledger or append-only policy
```

### 1.2 Indexes & Performance

```sql
-- Full-text search index trên Documents
CREATE FULLTEXT CATALOG FtCatalog AS DEFAULT;
CREATE FULLTEXT INDEX ON Documents(Title, Tags) KEY INDEX PK_Documents;

-- Covering indexes cho queries phổ biến
CREATE INDEX IX_Documents_Status_ISO   ON Documents(Status, IsoStandard) INCLUDE (Title, DocumentCode);
CREATE INDEX IX_Documents_Owner        ON Documents(OwnerId) INCLUDE (Status, UpdatedAt);
CREATE INDEX IX_AuditLogs_OccurredAt   ON AuditLogs(OccurredAt DESC) INCLUDE (UserId, Action);
CREATE INDEX IX_Versions_Document      ON DocumentVersions(DocumentId, UploadedAt DESC);
```

---

## Bước 2 - Solution Structure (.NET 8 Clean Architecture)

Áp dụng Clean Architecture với 4 tầng tách biệt rõ ràng. Dependency rule: tầng trong không phụ thuộc tầng ngoài.

### 2.1 Solution Layout

```text
IsoDocumentManagement.sln
│
├── src/
│   ├── 01_Domain/
│   │   └── IsoDoc.Domain/                 ← Entities, ValueObjects, Enums
│   │       ├── Entities/
│   │       │   ├── Document.cs
│   │       │   ├── DocumentVersion.cs
│   │       │   ├── ApprovalWorkflow.cs
│   │       │   └── AuditLog.cs
│   │       ├── Enums/
│   │       │   ├── DocumentStatus.cs
│   │       │   └── IsoStandard.cs
│   │       └── Events/
│   │           └── DocumentApprovedEvent.cs
│   │
│   ├── 02_Application/
│   │   └── IsoDoc.Application/            ← Use cases (CQRS + MediatR)
│   │       ├── Documents/
│   │       │   ├── Commands/
│   │       │   │   ├── UploadDocument/
│   │       │   │   ├── SubmitForApproval/
│   │       │   │   └── ApproveDocument/
│   │       │   └── Queries/
│   │       │       ├── GetDocumentById/
│   │       │       └── SearchDocuments/
│   │       ├── Common/
│   │       │   ├── Interfaces/
│   │       │   ├── Behaviours/
│   │       │   └── Exceptions/
│   │       └── Mappings/
│   │           └── DocumentMappingProfile.cs
│   │
│   ├── 03_Infrastructure/
│   │   └── IsoDoc.Infrastructure/         ← EF Core, Blob, Redis, Email
│   │       ├── Persistence/
│   │       │   ├── AppDbContext.cs
│   │       │   ├── Repositories/
│   │       │   └── Migrations/
│   │       ├── Storage/
│   │       │   └── AzureBlobStorageService.cs
│   │       ├── Search/
│   │       │   └── ElasticsearchService.cs
│   │       ├── Identity/
│   │       │   └── JwtTokenService.cs
│   │       └── Audit/
│   │           └── AuditInterceptor.cs
│   │
│   ├── 04_WebAPI/
│   │   └── IsoDoc.WebAPI/
│   │       ├── Controllers/
│   │       │   ├── DocumentsController.cs
│   │       │   ├── WorkflowController.cs
│   │       │   └── SearchController.cs
│   │       ├── Middleware/
│   │       │   ├── ExceptionHandlingMiddleware.cs
│   │       │   └── AuditMiddleware.cs
│   │       └── Program.cs
│   │
│   └── 05_Web/
│       └── IsoDoc.BlazorApp/
│           ├── Pages/
│           │   ├── Documents/
│           │   ├── Workflows/
│           │   └── Admin/
│           └── Shared/
│               └── Components/
│
└── tests/
    ├── IsoDoc.Domain.Tests/
    ├── IsoDoc.Application.Tests/
    └── IsoDoc.Integration.Tests/
```

### 2.2 Key Domain Entity - `Document.cs`

```csharp
public class Document : BaseAuditableEntity
{
    public string Title          { get; private set; }
    public string DocumentCode   { get; private set; }  // QMS-PR-001
    public IsoStandard Standard  { get; private set; }  // Enum
    public DocumentStatus Status { get; private set; }  // Enum
    public string CurrentVersion { get; private set; }
    public Guid OwnerId          { get; private set; }

    private readonly List<DocumentVersion> _versions = new();
    public IReadOnlyCollection<DocumentVersion> Versions => _versions.AsReadOnly();

    public static Document Create(string title, string code, IsoStandard std, Guid ownerId)
    {
        Guard.Against.NullOrEmpty(title, nameof(title));
        Guard.Against.NullOrEmpty(code, nameof(code));
        var doc = new Document {
            Title = title, DocumentCode = code,
            Standard = std, OwnerId = ownerId,
            Status = DocumentStatus.Draft, CurrentVersion = "1.0"
        };
        doc.AddDomainEvent(new DocumentCreatedEvent(doc.Id));
        return doc;
    }

    public void SubmitForReview()
    {
        if (Status != DocumentStatus.Draft)
            throw new DomainException("Only Draft documents can be submitted.");
        Status = DocumentStatus.UnderReview;
    }

    public void Approve(string approvedBy)
    {
        if (Status != DocumentStatus.PendingFinal)
            throw new DomainException("Invalid state transition.");
        Status = DocumentStatus.Published;
        AddDomainEvent(new DocumentApprovedEvent(Id, approvedBy));
    }
}
```

### 2.3 NuGet Packages chính

| Package | Version | Mục đích |
|---|---|---|
| MediatR | 12.x | CQRS / Mediator pattern |
| FluentValidation | 11.x | Validation pipeline |
| Ardalis.GuardClauses | 4.x | Domain guard helpers |
| AutoMapper | 13.x | DTO mapping profiles |
| EF Core 8 | 8.x | ORM + Migrations |
| Azure.Storage.Blobs | 12.x | File storage |
| NEST (Elasticsearch) | 8.x | Full-text search |
| Serilog | 3.x | Structured logging |
| Microsoft.Identity.Web | 2.x | Auth / JWT |
| StackExchange.Redis | 2.x | Distributed cache |

---

## Bước 3 - REST API Endpoints

Thiết kế API RESTful theo chuẩn, có Swagger/OpenAPI. Tất cả endpoints yêu cầu Bearer JWT token.

### 3.1 Documents API

| Method | Endpoint | Role required | Mô tả |
|---|---|---|---|
| POST | `/api/v1/documents` | Controller+ | Upload tài liệu mới |
| GET | `/api/v1/documents` | Viewer+ | Danh sách (filter, page) |
| GET | `/api/v1/documents/{id}` | Viewer+ | Chi tiết tài liệu |
| PUT | `/api/v1/documents/{id}` | Controller+ | Cập nhật metadata |
| DELETE | `/api/v1/documents/{id}` | ISOManager+ | Soft delete |
| POST | `/api/v1/documents/{id}/versions` | Controller+ | Upload phiên bản mới |
| GET | `/api/v1/documents/{id}/versions` | Viewer+ | Lịch sử phiên bản |
| GET | `/api/v1/documents/{id}/download` | Viewer+ | Tải file (SAS URL) |

### 3.2 Workflow API

| Method | Endpoint | Role required | Mô tả |
|---|---|---|---|
| POST | `/api/v1/documents/{id}/submit` | Controller+ | Gửi phê duyệt |
| POST | `/api/v1/workflows/{id}/approve` | Approver+ | Phê duyệt bước hiện tại |
| POST | `/api/v1/workflows/{id}/reject` | Approver+ | Từ chối + comment |
| GET | `/api/v1/workflows/pending` | Approver+ | Danh sách cần duyệt |
| GET | `/api/v1/workflows/{id}` | Viewer+ | Trạng thái workflow |

### 3.3 Search API

| Method | Endpoint | Role required | Mô tả |
|---|---|---|---|
| GET | `/api/v1/search?q=&iso=&status=` | Viewer+ | Full-text search |
| GET | `/api/v1/search/suggestions?q=` | Viewer+ | Autocomplete |
| POST | `/api/v1/search/advanced` | Viewer+ | Filter phức hợp |

### 3.4 Mẫu `DocumentsController.cs`

```csharp
[ApiController]
[Route("api/v1/[controller]")]
[Authorize]
public class DocumentsController : ControllerBase
{
    private readonly IMediator _mediator;
    public DocumentsController(IMediator mediator) => _mediator = mediator;

    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<DocumentDto>), 200)]
    public async Task<IActionResult> GetAll([FromQuery] GetDocumentsQuery query)
        => Ok(await _mediator.Send(query));

    [HttpPost]
    [Authorize(Policy = "RequireController")]
    [ProducesResponseType(typeof(DocumentDto), 201)]
    public async Task<IActionResult> Upload([FromForm] UploadDocumentCommand cmd)
    {
        var result = await _mediator.Send(cmd);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPost("{id}/submit")]
    [Authorize(Policy = "RequireController")]
    public async Task<IActionResult> Submit(Guid id)
        => Ok(await _mediator.Send(new SubmitForApprovalCommand(id)));
}
```

### 3.5 Response & Error Format

```json
{
  "success": true,
  "data": {},
  "pagination": { "page": 1, "pageSize": 20, "total": 150 }
}
```

```json
{
  "type": "https://isodms.internal/errors/validation",
  "title": "Validation failed",
  "status": 400,
  "detail": "Document code QMS-PR-001 already exists.",
  "traceId": "0HN5K2Q4J7J3:00000001"
}
```

---

## Bước 4 - Blazor UI Component Architecture

Blazor Server được chọn cho môi trường intranet (50-500 users) vì latency thấp, SignalR realtime, và không cần compile WASM phía client.

### 4.1 Component Hierarchy

```text
App.razor
└── MainLayout.razor
    ├── NavMenu.razor              ← Sidebar với RBAC-driven menu
    └── [Page components]
        ├── Dashboard.razor
        │   ├── KpiCard.razor
        │   ├── PendingApprovalList.razor
        │   └── RecentDocuments.razor
        ├── Documents/
        │   ├── DocumentList.razor
        │   ├── DocumentDetail.razor
        │   ├── DocumentUpload.razor
        │   └── DocumentViewer.razor
        ├── Workflow/
        │   ├── ApprovalQueue.razor
        │   └── ApprovalDetail.razor
        └── Admin/
            ├── UserManagement.razor
            └── RoleAssignment.razor
```

### 4.2 Mẫu Component - `DocumentList.razor`

```razor
@page "/documents"
@attribute [Authorize]
@inject IDocumentService DocumentService
@inject NavigationManager Nav

<PageTitle>Danh sách tài liệu ISO</PageTitle>

<DocumentFilter @bind-Filter="_filter" OnSearch="LoadDocuments" />

<MudDataGrid Items="@_documents" Loading="@_loading" Hover Dense
             SortMode="SortMode.Multiple" Filterable="false"
             RowClick="OnRowClick" T="DocumentDto">
    <Columns>
        <PropertyColumn Property="x => x.DocumentCode" Title="Mã tài liệu" />
        <PropertyColumn Property="x => x.Title"        Title="Tên tài liệu" />
        <PropertyColumn Property="x => x.IsoStandard"  Title="Tiêu chuẩn" />
        <TemplateColumn Title="Trạng thái">
            <CellTemplate>
                <MudChip Color="@GetStatusColor(context.Item.Status)">
                    @context.Item.Status
                </MudChip>
            </CellTemplate>
        </TemplateColumn>
        <PropertyColumn Property="x => x.CurrentVersion" Title="Phiên bản" />
        <PropertyColumn Property="x => x.UpdatedAt"      Title="Cập nhật" />
    </Columns>
    <PagerContent>
        <MudDataGridPager T="DocumentDto" PageSizeOptions="new[]{20,50,100}" />
    </PagerContent>
</MudDataGrid>
```

### 4.3 UI Libraries & Dependencies

| Library | Version | Sử dụng |
|---|---|---|
| MudBlazor | 7.x | Component library (DataGrid, Dialog, Snackbar) |
| Blazored.FluentValidation | 2.x | Form validation Blazor |
| PSPDFKit / Syncfusion | latest | PDF viewer inline |
| Microsoft.AspNetCore.SignalR | 8.x | Realtime notifications |
| Blazored.LocalStorage | 4.x | UI state persistence |

---

## Roadmap triển khai

| Sprint | Thời gian | Deliverables |
|---|---|---|
| Sprint 1 | Tuần 1-2 | Setup solution, DB migration, Identity + JWT, Role seeding |
| Sprint 2 | Tuần 3-4 | Document CRUD API + Blob Storage, EF Core repositories |
| Sprint 3 | Tuần 5-6 | Approval workflow state machine + Notifications (Email/SignalR) |
| Sprint 4 | Tuần 7-8 | Elasticsearch integration + Advanced search API |
| Sprint 5 | Tuần 9-10 | Blazor UI: Dashboard, Document List, Upload wizard |
| Sprint 6 | Tuần 11-12 | Blazor UI: Approval queue, Admin pages, PDF viewer |
| Sprint 7 | Tuần 13-14 | Audit log UI, Reports ISO, Testing & Performance tuning |
| Sprint 8 | Tuần 15-16 | UAT, Security review (ISO 27001 checklist), Go-live |

