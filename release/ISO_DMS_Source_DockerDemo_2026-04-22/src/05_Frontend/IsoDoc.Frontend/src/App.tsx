import { useEffect, useMemo, useState } from "react";
import type { FormEvent } from "react";
import {
  Link,
  Navigate,
  Outlet,
  Route,
  BrowserRouter as Router,
  Routes,
  useLocation,
  useNavigate,
} from "react-router-dom";

type ApiResponse<T> = {
  isSuccess: boolean;
  data: T;
  pagination?: {
    page: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
    hasNext: boolean;
    hasPrev: boolean;
  };
};

type LoginPayload = {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  tokenType: string;
};

type DocumentSummary = {
  id: string;
  title: string;
  documentCode: string;
  isoStandard: string;
  category: string;
  status: string;
  currentVersion: string;
  ownerName: string;
  updatedAt: string;
};

const TOKEN_KEY = "isodoc_access_token";
const API_BASE = import.meta.env.VITE_API_BASE_URL ?? "http://localhost:5075/api/v1";

function emitDebugLog(hypothesisId: string, location: string, message: string, data: Record<string, unknown>) {
  fetch("http://127.0.0.1:7647/ingest/ef3db311-c361-4346-a69d-cb044499f12e", {
    method: "POST",
    headers: { "Content-Type": "application/json", "X-Debug-Session-Id": "b9f138" },
    body: JSON.stringify({
      sessionId: "b9f138",
      runId: "pre-fix-3",
      hypothesisId,
      location,
      message,
      data,
      timestamp: Date.now(),
    }),
  }).catch(() => {});
}

const statusClass: Record<string, string> = {
  Draft: "status-draft",
  UnderReview: "status-review",
  PendingFinalApproval: "status-review",
  Published: "status-published",
  Archived: "status-archived",
  Rejected: "status-rejected",
};

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const url = `${API_BASE}${path}`;
  // #region agent log
  emitDebugLog("h14", "IsoDoc.Frontend/App.tsx:request:entry", "Frontend request started", {
    path,
    url,
    method: init?.method ?? "GET",
    hasAuthHeader: Boolean((init?.headers as Record<string, string> | undefined)?.Authorization),
  });
  // #endregion
  let res: Response;
  try {
    res = await fetch(url, init);
  } catch (error) {
    // #region agent log
    emitDebugLog("h15", "IsoDoc.Frontend/App.tsx:request:network-error", "Frontend network error", {
      path,
      url,
      method: init?.method ?? "GET",
      errorMessage: error instanceof Error ? error.message : "Unknown network error",
    });
    // #endregion
    throw error;
  }
  // #region agent log
  emitDebugLog("h16", "IsoDoc.Frontend/App.tsx:request:response", "Frontend response received", {
    path,
    url,
    method: init?.method ?? "GET",
    status: res.status,
    ok: res.ok,
  });
  // #endregion
  if (!res.ok) {
    const text = await res.text();
    throw new Error(text || `Request failed (${res.status})`);
  }
  const data = (await res.json()) as ApiResponse<T>;
  return data.data;
}

function isLoggedIn() {
  return Boolean(localStorage.getItem(TOKEN_KEY));
}

function PrivateRoute() {
  if (!isLoggedIn()) return <Navigate to="/login" replace />;
  return <Outlet />;
}

function AppLayout() {
  const navigate = useNavigate();
  const location = useLocation();

  return (
    <div className="app-shell">
      <aside className="sidebar">
        <h2>ISO DMS</h2>
        <p className="muted">Document Management</p>
        <nav>
          <Link className={location.pathname === "/" ? "active" : ""} to="/">
            Dashboard
          </Link>
          <Link className={location.pathname.startsWith("/documents") ? "active" : ""} to="/documents">
            Documents
          </Link>
          <Link className={location.pathname === "/new-document" ? "active" : ""} to="/new-document">
            New Document
          </Link>
        </nav>
        <button
          className="btn btn-outline"
          onClick={() => {
            localStorage.removeItem(TOKEN_KEY);
            navigate("/login");
          }}
        >
          Logout
        </button>
      </aside>
      <main className="content">
        <Outlet />
      </main>
    </div>
  );
}

function LoginPage() {
  const [email, setEmail] = useState("admin@local");
  const [password, setPassword] = useState("Admin@123");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState("");
  const navigate = useNavigate();

  async function onSubmit(e: FormEvent) {
    e.preventDefault();
    setLoading(true);
    setError("");

    try {
      const data = await request<LoginPayload>("/Auth/login", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ email, password }),
      });
      localStorage.setItem(TOKEN_KEY, data.accessToken);
      navigate("/");
    } catch (err) {
      setError(err instanceof Error ? err.message : "Login failed");
    } finally {
      setLoading(false);
    }
  }

  return (
    <div className="auth-wrap">
      <form className="card auth-card" onSubmit={onSubmit}>
        <h1>ISO Documents Login</h1>
        <p className="muted">Demo account is prefilled for your group.</p>
        <label>Email</label>
        <input value={email} onChange={(e) => setEmail(e.target.value)} />
        <label>Password</label>
        <input type="password" value={password} onChange={(e) => setPassword(e.target.value)} />
        {error ? <p className="error">{error}</p> : null}
        <button className="btn" disabled={loading} type="submit">
          {loading ? "Signing in..." : "Sign in"}
        </button>
      </form>
    </div>
  );
}

function DashboardPage() {
  const [documents, setDocuments] = useState<DocumentSummary[]>([]);
  const [error, setError] = useState("");

  useEffect(() => {
    const token = localStorage.getItem(TOKEN_KEY) ?? "";
    request<DocumentSummary[]>("/Documents?page=1&pageSize=20", {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then(setDocuments)
      .catch((err) => setError(err instanceof Error ? err.message : "Cannot load dashboard"));
  }, []);

  const stats = useMemo(() => {
    const total = documents.length;
    const draft = documents.filter((x) => x.status === "Draft").length;
    const review = documents.filter(
      (x) => x.status === "UnderReview" || x.status === "PendingFinalApproval",
    ).length;
    const published = documents.filter((x) => x.status === "Published").length;
    return { total, draft, review, published };
  }, [documents]);

  return (
    <>
      <h1>Dashboard</h1>
      <p className="muted">Overview for ISO document operations.</p>
      {error ? <p className="error">{error}</p> : null}
      <div className="stats-grid">
        <article className="card stat"><h3>Total</h3><strong>{stats.total}</strong></article>
        <article className="card stat"><h3>Draft</h3><strong>{stats.draft}</strong></article>
        <article className="card stat"><h3>Under Review</h3><strong>{stats.review}</strong></article>
        <article className="card stat"><h3>Published</h3><strong>{stats.published}</strong></article>
      </div>

      <section className="card">
        <div className="section-head">
          <h2>Recent Documents</h2>
          <Link to="/documents">View all</Link>
        </div>
        <ul className="list">
          {documents.slice(0, 5).map((doc) => (
            <li key={doc.id}>
              <div>
                <p>{doc.title}</p>
                <small>{doc.documentCode} • {doc.ownerName || "N/A"}</small>
              </div>
              <span className={`badge ${statusClass[doc.status] ?? "status-draft"}`}>{doc.status}</span>
            </li>
          ))}
        </ul>
      </section>
    </>
  );
}

function DocumentsPage() {
  const [docs, setDocs] = useState<DocumentSummary[]>([]);
  const [q, setQ] = useState("");
  const [error, setError] = useState("");

  useEffect(() => {
    const token = localStorage.getItem(TOKEN_KEY) ?? "";
    const params = new URLSearchParams({ page: "1", pageSize: "20" });
    if (q.trim()) params.set("keyword", q.trim());
    request<DocumentSummary[]>(`/Documents?${params.toString()}`, {
      headers: { Authorization: `Bearer ${token}` },
    })
      .then(setDocs)
      .catch((err) => setError(err instanceof Error ? err.message : "Cannot load documents"));
  }, [q]);

  return (
    <>
      <h1>Document Library</h1>
      <div className="toolbar">
        <input
          placeholder="Search by title or code"
          value={q}
          onChange={(e) => setQ(e.target.value)}
        />
        <Link className="btn" to="/new-document">+ New Document</Link>
      </div>
      {error ? <p className="error">{error}</p> : null}
      <section className="card table-wrap">
        <table>
          <thead>
            <tr>
              <th>Code</th>
              <th>Title</th>
              <th>ISO</th>
              <th>Status</th>
              <th>Version</th>
              <th>Updated</th>
            </tr>
          </thead>
          <tbody>
            {docs.map((doc) => (
              <tr key={doc.id}>
                <td>{doc.documentCode}</td>
                <td>{doc.title}</td>
                <td>{doc.isoStandard}</td>
                <td>
                  <span className={`badge ${statusClass[doc.status] ?? "status-draft"}`}>{doc.status}</span>
                </td>
                <td>{doc.currentVersion || "-"}</td>
                <td>{new Date(doc.updatedAt).toLocaleDateString()}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </section>
    </>
  );
}

function NewDocumentPage() {
  const [title, setTitle] = useState("");
  const [documentCode, setDocumentCode] = useState("QMS-PR-001");
  const [standard, setStandard] = useState("ISO9001");
  const [category, setCategory] = useState("Procedure");
  const [changeNote, setChangeNote] = useState("Initial version");
  const [file, setFile] = useState<File | null>(null);
  const [message, setMessage] = useState("");
  const [submitting, setSubmitting] = useState(false);

  async function submitForm(e: FormEvent) {
    e.preventDefault();
    if (!file) {
      setMessage("Please choose a file first.");
      return;
    }

    setSubmitting(true);
    setMessage("");
    const token = localStorage.getItem(TOKEN_KEY) ?? "";
    const formData = new FormData();
    formData.append("title", title);
    formData.append("documentCode", documentCode);
    formData.append("standard", standard);
    formData.append("category", category);
    formData.append("changeNote", changeNote);
    formData.append("file", file);

    try {
      await request<string>("/Documents", {
        method: "POST",
        headers: { Authorization: `Bearer ${token}` },
        body: formData,
      });
      setMessage("Upload completed successfully.");
      setTitle("");
      setDocumentCode("QMS-PR-001");
      setFile(null);
    } catch (err) {
      setMessage(err instanceof Error ? err.message : "Upload failed.");
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <>
      <h1>New Document</h1>
      <p className="muted">Upload PDF, DOCX, or XLSX (max 50MB).</p>
      <form className="card form-grid" onSubmit={submitForm}>
        <label>
          Title
          <input required value={title} onChange={(e) => setTitle(e.target.value)} />
        </label>
        <label>
          Document Code
          <input required value={documentCode} onChange={(e) => setDocumentCode(e.target.value)} />
        </label>
        <label>
          ISO Standard
          <select value={standard} onChange={(e) => setStandard(e.target.value)}>
            <option>ISO9001</option>
            <option>ISO45001</option>
            <option>ISO27001</option>
          </select>
        </label>
        <label>
          Category
          <select value={category} onChange={(e) => setCategory(e.target.value)}>
            <option>Policy</option>
            <option>Procedure</option>
            <option>WorkInstruction</option>
            <option>Form</option>
            <option>Record</option>
            <option>Manual</option>
            <option>Specification</option>
          </select>
        </label>
        <label className="full">
          File
          <input
            type="file"
            accept=".pdf,.docx,.xlsx"
            onChange={(e) => setFile(e.target.files?.[0] ?? null)}
          />
        </label>
        <label className="full">
          Change Note
          <textarea value={changeNote} onChange={(e) => setChangeNote(e.target.value)} />
        </label>
        <button disabled={submitting} className="btn full" type="submit">
          {submitting ? "Uploading..." : "Upload Document"}
        </button>
        {message ? <p className="full">{message}</p> : null}
      </form>
    </>
  );
}

export default function App() {
  return (
    <Router>
      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route element={<PrivateRoute />}>
          <Route element={<AppLayout />}>
            <Route index element={<DashboardPage />} />
            <Route path="documents" element={<DocumentsPage />} />
            <Route path="new-document" element={<NewDocumentPage />} />
          </Route>
        </Route>
        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>
    </Router>
  );
}
