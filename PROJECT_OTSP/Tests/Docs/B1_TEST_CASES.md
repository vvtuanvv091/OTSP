# BẢNG KỊCH BẢN KIỂM THỬ CHI TIẾT (TEST CASES)
## DỰ ÁN: NỀN TẢNG KẾT NỐI GIA SƯ VÀ HỌC VIÊN (OTSP - ONLINE TUTOR-STUDENT PLATFORM)

---

### 1. QUY ƯỚC MÃ ĐỊNH DANH (TEST CASE ID)
- `TC_AUTH_xx`: Kiểm thử Xác thực & Phân quyền (Authentication & Authorization).
- `TC_SEARCH_xx`: Kiểm thử Tìm kiếm & Bộ lọc (Search & Filter).
- `TC_CONN_xx`: Kiểm thử Yêu cầu kết nối & Lịch học thử (Connection Request).
- `TC_SESSION_xx`: Kiểm thử Quản lý Buổi học & Phòng học (Class Session).
- `TC_CHAT_xx`: Kiểm thử Nhắn tin thời gian thực (Real-time Chat).
- `TC_PAY_xx`: Kiểm thử Thanh toán & Hóa đơn (Payment & Invoice).

---

### 2. DANH SÁCH TEST CASES CHI TIẾT

| Mã TC | Tên chức năng / Nghiệp vụ | Tiền điều kiện (Pre-condition) | Các bước thực hiện (Test Steps) | Dữ liệu kiểm thử (Test Data) | Kết quả mong đợi (Expected Result) | Kết quả thực tế | Trạng thái |
| :--- | :--- | :--- | :--- | :--- | :--- | :--- | :---: |
| **TC_AUTH_01** | Đăng nhập tài khoản Học viên thành công | Tài khoản đã tồn tại, trạng thái Active | 1. Truy cập `/Identity/Account/Login`<br>2. Nhập Email, Password hợp lệ<br>3. Nhấn "Đăng nhập" | Email: `student@otsp.vn`<br>Pass: `P@ssword123` | Đăng nhập thành công, điều hướng về Dashboard Học viên (`/Student_Parent/StudentDashboard`) | Chuyển đúng trang Dashboard | **PASS** |
| **TC_AUTH_02** | Chặn đăng nhập tài khoản bị khóa | Tài khoản có `Status = Locked` | 1. Nhập Email tài khoản bị khóa<br>2. Nhập đúng Password<br>3. Nhấn "Đăng nhập" | Email: `locked@otsp.vn`<br>Pass: `P@ssword123` | Hệ thống từ chối đăng nhập, hiển thị thông báo "Tài khoản của bạn đã bị tạm khóa" | Báo lỗi đúng thông điệp | **PASS** |
| **TC_AUTH_03** | Phân quyền truy cập Area Gia sư | Đang đăng nhập tài khoản vai trò Student | Truy cập URL trực tiếp: `/Tutor/TutorDashboard` | Role: `Student_Parent` | Hệ thống chặn truy cập, trả về mã lỗi HTTP 403 Forbidden hoặc điều hướng về AccessDenied | Bị chặn, điều hướng về AccessDenied | **PASS** |
| **TC_SEARCH_01** | Tìm kiếm gia sư theo từ khóa môn học | Có ít nhất 3 gia sư dạy môn Toán | 1. Mở trang `/Search`<br>2. Chọn tab "Gia sư"<br>3. Nhập từ khóa "Toán" vào ô tìm kiếm | Keyword: `Toán` | Danh sách hiển thị đúng các gia sư có đăng ký môn Toán, số lượng kết quả khớp CSDL | Trả về đúng 3 gia sư môn Toán | **PASS** |
| **TC_SEARCH_02** | Lọc gia sư theo mức học phí tối đa | Có các gia sư với học phí từ 100k đến 500k | 1. Tại trang Tìm kiếm, chọn dropdown "Học phí tối đa"<br>2. Chọn mức "≤ 200.000đ"<br>3. Nhấn Lọc | MaxFee: `200000` | 100% gia sư hiển thị đều có `HourlyFee <= 200,000 VNĐ`. Không có gia sư nào vượt giá | Tất cả gia sư đều có phí ≤ 200k | **PASS** |
| **TC_SEARCH_03** | Ẩn gia sư chưa được Admin duyệt hồ sơ | Có gia sư A có `VerificationStatus = Pending` | 1. Tìm kiếm danh sách gia sư trên hệ thống công khai | Tutor: Gia sư A (`Pending`) | Gia sư A không xuất hiện trong bất kỳ kết quả tìm kiếm nào | Không hiển thị gia sư A | **PASS** |
| **TC_SEARCH_04** | Phân trang danh sách lớp học | Có tổng cộng 25 lớp học mở | 1. Chọn tab "Lớp học"<br>2. Chọn kích thước trang = 12<br>3. Nhấn trang 2 | Page: `2`, PageSize: `12` | Trang 2 hiển thị đúng 12 lớp tiếp theo (từ 13 đến 24), thanh phân trang thể hiện trang 2 active | Hiển thị chính xác 12 item | **PASS** |
| **TC_CONN_01** | Gửi yêu cầu kết nối gia sư thành công | Học viên đã đăng nhập, Gia sư đã Active | 1. Vào chi tiết Gia sư B<br>2. Nhập lời nhắn kết nối<br>3. Chọn khung giờ học thử<br>4. Nhấn "Gửi yêu cầu" | Message: "Em muốn học thử Toán 12"<br>Slot: 19:00 Thứ 4 | Yêu cầu được lưu với trạng thái `Pending`, thông báo SignalR gửi đến Gia sư B | Yêu cầu tạo thành công | **PASS** |
| **TC_CONN_02** | Chặn gửi yêu cầu kết nối khi thiếu thông tin | Học viên bỏ trống lời nhắn hoặc chưa chọn môn | 1. Nhấn nút Gửi mà không điền thông tin | Form rỗng | Hệ thống báo lỗi validation tại client và server, không tạo bản ghi mới trong DB | Form báo lỗi đỏ, chặn submit | **PASS** |
| **TC_SESSION_01** | Gia sư kích hoạt buổi học trực tuyến | Buổi học có trạng thái `Scheduled` | 1. Gia sư vào Lịch dạy<br>2. Nhấn nút "Vào phòng học / Bắt đầu" | SessionId: `1`<br>Status: `Scheduled` | Trạng thái chuyển sang `InProgress`, hệ thống tự sinh mã phòng `VideoRoom (OTSP-...)`, học viên nhận thông báo realtime | Trạng thái đổi thành InProgress, phòng mở | **PASS** |
| **TC_SESSION_02** | Chặn kết thúc buổi học chưa diễn ra | Buổi học đang `Scheduled` hoặc đã `Done` | Gia sư cố tình gửi request POST `/ClassSession/End` | SessionId: `2`<br>Status: `Done` | Hệ thống từ chối thao tác, báo lỗi "Chỉ có thể kết thúc buổi học đang diễn ra" | Báo lỗi TempData chính xác | **PASS** |
| **TC_SESSION_03** | Ghi nhận thời gian điểm danh học viên | Buổi học đang diễn ra | 1. Học viên nhấn "Vào học"<br>2. Học viên nhấn "Rời phòng" | UserId: `student_01` | Bản ghi `ClassSessionAttendance` ghi đúng `JoinedAtUtc` và cập nhật `LeftAtUtc` khi rời | Lưu đúng thời gian UTC | **PASS** |
| **TC_CHAT_01** | Gửi tin nhắn hợp lệ giữa 2 bên đã kết nối | Yêu cầu kết nối có trạng thái `Accepted` | 1. Học viên nhập "Chào thầy"<br>2. Nhấn nút Gửi | Content: "Chào thầy" | Tin nhắn lưu vào DB, hiển thị ngay trên giao diện của cả 2 phía qua WebSocket | Tin nhắn đến tức thì | **PASS** |
| **TC_CHAT_02** | Chặn tin nhắn rác / tin nhắn rỗng | Đang ở cửa sổ chat | 1. Nhập toàn khoảng trắng<br>2. Nhấn Enter | Content: `"     "` | Báo lỗi validation, không lưu vào DB, không phát event | Không gửi được, báo lỗi | **PASS** |
| **TC_CHAT_03** | Chặn người lạ đọc tin nhắn hội thoại | Người dùng C không phải học viên/gia sư của request | Người dùng C gọi API lấy tin nhắn với RequestId = 5 | UserId: `user_intruder_C` | API trả về 404 Not Found hoặc null, không để lộ dữ liệu | Trả về HTTP 404 | **PASS** |
| **TC_PAY_01** | Tạo giao dịch thanh toán học phí thành công | Lớp học đã được thỏa thuận | 1. Học viên nhấn "Thanh toán học phí"<br>2. Chọn cổng thanh toán<br>3. Xác nhận giao dịch | Số tiền: `1.200.000đ` | Bản ghi `Payment` chuyển sang `Completed`, tự động sinh `Invoice` tương ứng | Thanh toán hoàn tất, có hóa đơn | **PASS** |

