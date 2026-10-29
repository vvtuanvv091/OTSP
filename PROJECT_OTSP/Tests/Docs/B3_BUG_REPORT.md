# BÁO CÁO LỖI VÀ SỬA LỖI (DEFECT / BUG REPORT)
## DỰ ÁN: OTSP (ONLINE TUTOR-STUDENT PLATFORM)

---

### 1. QUY ƯỚC PHÂN LOẠI MỨC ĐỘ LỖI (SEVERITY & PRIORITY)
- **Mức độ nghiêm trọng (Severity)**:
  - `Critical`: Lỗi sập hệ thống, mất mát dữ liệu hoặc rò rỉ bảo mật nghiêm trọng.
  - `Major`: Chức năng chính không hoạt động, không có cách giải quyết thay thế.
  - `Minor`: Chức năng hoạt động sai trong điều kiện biên, có giải pháp tạm thời.
  - `Trivial`: Lỗi hiển thị giao diện, chính tả, khoảng cách.
- **Mức độ ưu tiên (Priority)**:
  - `High`: Cần xử lý ngay lập tức trong phiên làm việc hiện tại.
  - `Medium`: Cần xử lý trước khi đóng đợt kiểm thử.
  - `Low`: Có thể xem xét khắc phục ở bản phát hành sau.

---

### 2. DANH SÁCH BÁO CÁO LỖI CHI TIẾT ĐÃ XỬ LÝ (BUG LOG)

#### 🐛 BUG-001: Lỗi tràn truy vấn tìm kiếm khi người dùng gõ phím liên tục (DDoS vô ý)
- **Module**: Frontend / Search Module (`wwwroot/js/search.js`)
- **Mức độ**: `Major` | **Ưu tiên**: `High`
- **Mô tả**: Khi người dùng gõ từ khóa vào ô tìm kiếm, mỗi phím gõ đều gửi ngay một request `fetch()` tới `/Search/Tutors`. Nếu gõ 10 chữ cái nhanh, hệ thống bắn 10 truy vấn liên tiếp về server gây quá tải CPU và xung đột thứ tự kết quả trả về (Race Condition).
- **Nguyên nhân gốc rễ (Root Cause)**: Sự kiện `input` chưa được cài đặt cơ chế trì hoãn (Debounce).
- **Giải pháp khắc phục (Fix)**:
  Bổ sung hàm `debounce` 300ms trong `wwwroot/js/search.js`:
  ```javascript
  clearTimeout(debounceTimer);
  debounceTimer = setTimeout(() => load(1), 300);
  ```
- **Kết quả xác minh**: Kiểm tra lại, người dùng gõ cả câu chỉ phát sinh 1 request duy nhất sau khi dừng gõ.
- **Trạng thái**: `CLOSED / VERIFIED`

---

#### 🐛 BUG-002: Lỗi rò rỉ tin nhắn bảo mật giữa Học viên và Gia sư
- **Module**: Backend / Chat Service (`Services/ChatService.cs`)
- **Mức độ**: `Critical` | **Ưu tiên**: `High`
- **Mô tả**: Bất kỳ người dùng đã đăng nhập nào chỉ cần biết `requestId` là có thể gọi phương thức lấy toàn bộ lịch sử tin nhắn của người khác.
- **Nguyên nhân gốc rễ**: Phương thức truy vấn chưa kiểm tra xem `currentUserId` có thuộc danh sách `StudentUserId` hoặc `TutorUserId` của `ConnectionRequest` đó hay không.
- **Giải pháp khắc phục**:
  Bổ sung hàm kiểm tra quyền truy cập nghiêm ngặt trong `ChatService.cs`:
  ```csharp
  var participants = await GetAcceptedParticipantsAsync(requestId, currentUserId, cancellationToken);
  if (participants is null)
      return null; // Từ chối truy cập và trả về null
  ```
- **Kết quả xác minh**: Unit test `GetConversationAsync_WhenUserIsNotParticipant_ShouldReturnNull` chạy thành công; người ngoài cuộc bị chặn 100%.
- **Trạng thái**: `CLOSED / VERIFIED`

---

#### 🐛 BUG-003: Cho phép bấm "Kết thúc" buổi học khi buổi học chưa diễn ra
- **Module**: Controller / ClassSession (`Controllers/ClassSessionController.cs`)
- **Mức độ**: `Major` | **Ưu tiên**: `High`
- **Mô tả**: Gia sư có thể nhấn nút kết thúc một buổi học đang ở trạng thái `Scheduled` (lên lịch trong tương lai), làm thay đổi trạng thái sang `Done` và tính giờ dạy sai lệch.
- **Nguyên nhân gốc rễ**: Điều kiện kiểm tra trạng thái chỉ xét `session != null` mà không kiểm tra trạng thái hiện tại phải là `InProgress`.
- **Giải pháp khắc phục**:
  Bổ sung kiểm tra logic chặn chuyển đổi trạng thái phi lý:
  ```csharp
  if (session.Status != SessionStatus.InProgress)
  {
      TempData["Error"] = "Chỉ có thể kết thúc buổi học đang diễn ra.";
      return RedirectToAction("Index", "TutorSchedule", new { area = "Tutor" });
  }
  ```
- **Kết quả xác minh**: Unit test `EndSession_WhenSessionAlreadyDone_ShouldNotAllowTransition` vượt qua kiểm tra.
- **Trạng thái**: `CLOSED / VERIFIED`

---

#### 🐛 BUG-004: Gián đoạn kết nối CSDL Cloud Neon khi mạng chập chờn
- **Module**: Database / Infrastructure (`Program.cs`)
- **Mức độ**: `Critical` | **Ưu tiên**: `High`
- **Mô tả**: Do CSDL PostgreSQL được đặt trên nền tảng Cloud (Neon serverless), khi có độ trễ mạng nhất thời hoặc kết nối bị đóng nhàn rỗi (idle connection), ứng dụng ném lỗi `NpgsqlException: Connection refused / Timeout` làm sập trang web.
- **Nguyên nhân gốc rễ**: `DbContext` chưa được cấu hình cơ chế tự động thử lại khi gặp lỗi tạm thời (Transient Faults).
- **Giải pháp khắc phục**:
  Cấu hình `EnableRetryOnFailure` trong `Program.cs`:
  ```csharp
  builder.Services.AddDbContext<ApplicationDbContext>(options =>
      options.UseNpgsql(connectionString, npgsqlOptions =>
      {
          npgsqlOptions.EnableRetryOnFailure(
              maxRetryCount: 5,
              maxRetryDelay: TimeSpan.FromSeconds(10),
              errorCodesToAdd: null);
      }));
  ```
- **Kết quả xác minh**: Ứng dụng tự động tái kết nối thành công khi mạng chập chờn mà người dùng không bị thấy trang lỗi 500.
- **Trạng thái**: `CLOSED / VERIFIED`

---

#### 🐛 BUG-005: Cho phép gửi tin nhắn chat rỗng hoặc toàn ký tự trắng
- **Module**: Service / Chat (`Services/ChatService.cs`)
- **Mức độ**: `Minor` | **Ưu tiên**: `Medium`
- **Mô tả**: Người dùng có thể nhấn phím cách nhiều lần và bấm gửi, tạo ra các ô chat rỗng làm xấu giao diện.
- **Nguyên nhân gốc rễ**: Chưa áp dụng `string.IsNullOrWhiteSpace()` và `Trim()` trước khi kiểm tra dữ liệu.
- **Giải pháp khắc phục**:
  Bổ sung kiểm tra và ném `ArgumentException` nếu chuỗi rỗng:
  ```csharp
  var normalizedContent = content?.Trim();
  if (string.IsNullOrWhiteSpace(normalizedContent))
      throw new ArgumentException("Nội dung tin nhắn không được để trống.", nameof(content));
  ```
- **Kết quả xác minh**: Test Case `TC_CHAT_02` và Unit test tương ứng đạt kết quả PASS.
- **Trạng thái**: `CLOSED / VERIFIED`

---

### 3. TỔNG KẾT TÌNH TRẠNG LỖI (DEFECT SUMMARY)
| Tổng số lỗi ghi nhận | Đã sửa và xác minh (Closed) | Đang xử lý (Open) | Tỷ lệ khắc phục |
| :---: | :---: | :---: | :---: |
| **5** | **5** | **0** | **100%** |

