import { useState, useEffect, useRef } from "react";
import "./App.css";

const API_URL = "http://localhost:5000/api/books";

// Màu gáy sách, chọn theo _id nên mỗi cuốn luôn giữ cùng một màu
const SPINES = ["#14675f", "#8c2f39", "#c9922e", "#2b4a7e", "#6b4a7a", "#4e6b3a"];

function spineColor(id) {
  let sum = 0;
  for (const ch of id) sum += ch.charCodeAt(0);
  return SPINES[sum % SPINES.length];
}

const formatPrice = (n) => `${Number(n).toLocaleString("vi-VN")} đ`;

const EMPTY_FORM = { title: "", author: "", price: "" };

function App() {
  const [books, setBooks] = useState([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState("");
  const [form, setForm] = useState(EMPTY_FORM);
  const [saving, setSaving] = useState(false);
  const [notice, setNotice] = useState(null); // { type: "ok" | "err", text }
  const [newId, setNewId] = useState(null);
  const titleRef = useRef(null);

  // [GET] /api/books khi component vừa mount
  useEffect(() => {
    let cancelled = false;

    fetch(API_URL)
      .then((res) => {
        if (!res.ok) throw new Error("Lỗi server");
        return res.json();
      })
      .then((data) => {
        if (!cancelled) setBooks(data);
      })
      .catch(() => {
        if (!cancelled) {
          setLoadError(
            "Không tải được danh sách sách. Hãy kiểm tra backend đã chạy ở cổng 5000 chưa."
          );
        }
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });

    return () => {
      cancelled = true;
    };
  }, []);

  const handleChange = (e) => {
    setForm({ ...form, [e.target.name]: e.target.value });
  };

  // [POST] /api/books
  const handleSubmit = async (e) => {
    e.preventDefault();
    setSaving(true);
    setNotice(null);

    try {
      const res = await fetch(API_URL, {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({
          title: form.title.trim(),
          author: form.author.trim(),
          price: Number(form.price),
        }),
      });

      const data = await res.json();
      if (!res.ok) throw new Error(data.message || "Không thêm được sách");

      // Server trả về cuốn sách vừa tạo -> đưa thẳng vào danh sách
      setBooks((prev) => [...prev, data]);
      setNewId(data._id);
      setForm(EMPTY_FORM);
      setNotice({ type: "ok", text: `Đã thêm "${data.title}".` });
      titleRef.current?.focus();
    } catch (err) {
      setNotice({
        type: "err",
        text: err.message || "Không thêm được sách. Kiểm tra lại backend.",
      });
    } finally {
      setSaving(false);
    }
  };

  // Sách mới nhất hiển thị trước (ObjectId tăng dần theo thời gian thêm)
  const shown = [...books].reverse();

  return (
    <div className="app">
      <header className="masthead">
        <h1>Tiệm sách</h1>
        <p>{loading ? "Đang mở cửa…" : `${books.length} cuốn sách trong kho`}</p>
      </header>

      <div className="layout">
        <aside className="add-panel">
          <h2>Thêm sách mới</h2>
          <form onSubmit={handleSubmit}>
            <label className="field">
              <span>Tên sách</span>
              <input
                ref={titleRef}
                name="title"
                value={form.title}
                onChange={handleChange}
                placeholder="Ví dụ: Lập trình React"
                required
              />
            </label>

            <label className="field">
              <span>Tác giả</span>
              <input
                name="author"
                value={form.author}
                onChange={handleChange}
                placeholder="Ví dụ: Nguyễn Văn A"
                required
              />
            </label>

            <label className="field">
              <span>Giá tiền (đ)</span>
              <input
                name="price"
                type="number"
                min="0"
                step="1000"
                value={form.price}
                onChange={handleChange}
                placeholder="Ví dụ: 120000"
                required
              />
            </label>

            <button className="btn" type="submit" disabled={saving}>
              {saving ? "Đang lưu…" : "Thêm sách"}
            </button>

            {notice && (
              <p className={`notice ${notice.type}`} role="status">
                {notice.text}
              </p>
            )}
          </form>
        </aside>

        <main>
          {loading && <div className="state">Đang tải danh sách sách…</div>}

          {!loading && loadError && (
            <div className="state err" role="alert">
              {loadError}
            </div>
          )}

          {!loading && !loadError && books.length === 0 && (
            <div className="state">
              Kho đang trống. Thêm cuốn sách đầu tiên ở khung bên trái.
            </div>
          )}

          {!loading && !loadError && books.length > 0 && (
            <ul className="book-list">
              {shown.map((b) => (
                <li
                  key={b._id}
                  className={`book${b._id === newId ? " is-new" : ""}`}
                  style={{ "--spine": spineColor(b._id) }}
                >
                  <div className="spine" />
                  <div className="info">
                    <h3>{b.title}</h3>
                    <p className="author">{b.author}</p>
                    <div className="bottom">
                      <span className="price">{formatPrice(b.price)}</span>
                      {!b.isPublished && <span className="draft">Chưa xuất bản</span>}
                    </div>
                  </div>
                </li>
              ))}
            </ul>
          )}
        </main>
      </div>
    </div>
  );
}

export default App;