# ISO DMS Blazor FE Handover

## 1) Scope implemented
- Login + token storage (`/login`).
- Dashboard (`/`) with KPI and recent document/workflow blocks.
- Documents list + filter + paging (`/documents`).
- Document detail (`/documents/{id}`).
- New document upload (`/new-document`).
- Pending workflow + decision action (`/workflow`).
- Settings page (`/settings`).

## 2) Key folders
- `Components/Pages`: application pages.
- `Components/Documents`: reusable document components.
- `Components/Common`: shared UI pieces.
- `Services/Api`: API integration services.
- `Services/Auth`: auth state and token helpers.
- `Models`: DTO and request models.

## 3) Run instructions
1. Ensure backend WebAPI is up at `http://localhost:5075`.
2. Run Blazor app:
   - `dotnet run --project "src/05_Frontend/IsoDoc.Blazor/IsoDoc.Blazor.csproj"`
3. Open:
   - `http://localhost:5062/login`

## 4) Demo account
- Email: `admin@local`
- Password: `Admin@123`

## 5) Smoke checklist
- [ ] Open login page successfully.
- [ ] Login success and redirect to dashboard.
- [ ] Documents page loads list from API.
- [ ] Document detail opens from table.
- [ ] Upload page accepts valid file and creates document.
- [ ] Workflow page loads pending items and can approve/reject.
- [ ] Logout returns user to login page.

## 6) Unit tests
Run:
- `dotnet test "src/05_Frontend/IsoDoc.Blazor.Tests/IsoDoc.Blazor.Tests.csproj"`

Current tests cover:
- JWT principal parsing.
- Expired token detection.
- Valid token detection.
