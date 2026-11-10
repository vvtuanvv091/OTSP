# TỔNG QUAN HỆ THỐNG OTSP (ONLINE TUTORING SERVICES PLATFORM)
> **Tài liệu chuẩn bị bảo vệ & báo cáo môn học:** Mẫu Thiết Kế Phần Mềm (Software Design Patterns)  
> **Sinh viên thực hiện:** Tống Ngọc Tuấn  
> **Phiên bản hệ thống:** v1.0.0 (Production Ready)  
> *(Tài liệu tập trung 100% vào Kiến trúc, Nghiệp vụ, Design Patterns và Dữ liệu - Không chứa nội dung Unit Test)*

---

## MỤC LỤC TRA CỨU NHANH
1. [Giới thiệu tổng quan & Mục tiêu dự án](#1-giới-thiệu-tổng-quan--mục-tiêu-dự-án)
2. [Kiến trúc hệ thống tổng thể (System Architecture)](#2-kiến-trúc-hệ-thống-tổng-thể-system-architecture)
3. [Các Mẫu thiết kế phần mềm (Design Patterns) áp dụng](#3-các-mẫu-thiết-kế-phần-mềm-design-patterns-áp-dụng)
4. [Chi tiết Backend (ASP.NET Core 9 Web API)](#4-chi-tiết-backend-aspnet-core-9-web-api)
5. [Chi tiết AI Agent Service (Python FastAPI & Gemini)](#5-chi-tiết-ai-agent-service-python-fastapi--gemini)
6. [Chi tiết Frontend (React 19 + Vite + Bootstrap)](#6-chi-tiết-frontend-react-19--vite--bootstrap)
7. [Cơ sở dữ liệu & Các thực thể chính (Entities)](#7-cơ-sở-dữ-liệu--các-thực-thể-chính-entities)
8. [Các luồng nghiệp vụ trọng tâm (Core Workflows)](#8-các-luồng-nghiệp-vụ-trọng-tâm-core-workflows)
9. [Bộ câu hỏi "Bẫy" Mentor hay hỏi & Đáp án ăn điểm tuyệt đối](#9-bộ-câu-hỏi-bẫy-mentor-hay-hỏi--đáp-án-ăn-điểm-tuyệt-đối)

---

## 1. GIỚI THIỆU TỔNG QUAN & MỤC TIÊU DỰ ÁN

* **Tên dự án:** **OTSP** (*Online Tutoring Services Platform* - Nền tảng kết nối & hỗ trợ quản trị gia sư trực tuyến).
* **Bài toán thực tế:**
  * Giải quyết sự thiếu minh bạch và khó khăn trong việc tìm kiếm gia sư có năng lực được kiểm định.
  * Tự động hóa quá trình phê duyệt hồ sơ gia sư nhờ Trí tuệ nhân tạo (AI Vision OCR).
  * Cung cấp môi trường dạy - học khép kín: Tìm kiếm $\rightarrow$ Đặt lịch / Mở lớp $\rightarrow$ Thanh toán VietQR $\rightarrow$ Chat thời gian thực $\rightarrow$ Học trực tuyến (Jitsi Meet) $\rightarrow$ Đánh giá & Khiếu nại.
  * Hỗ trợ học tập thông minh 24/7 với Chatbot tự hành và Trợ lý giải bài tập sư phạm từng bước.

---

## 2. KIẾN TRÚC HỆ THỐNG TỔNG THỂ (SYSTEM ARCHITECTURE)

Hệ thống được thiết kế theo mô hình **Hybrid Microservices + Event-Driven Architecture**, chia tách rõ ràng giữa Core Business, Trí tuệ nhân tạo và Tầng trình diễn:

```
[ FRONTEND CLIENT ]
(React 19 + Vite SPA, React Router, Context API, SignalR Client, Jitsi Meet)
         │                                       ▲
         │ HTTPS / WSS                           │ HTTPS (Internal API)
         ▼                                       ▼
[ CORE BACKEND API ] ◄────── X-Internal-Key ────► [ AI AGENT MICROSERVICE ]
(ASP.NET Core 9 Web API,                          (Python FastAPI, Google GenAI SDK,
 EF Core 9, Identity,                             In-Memory RAM Cache, Gemini 3.5 Flash)
 SignalR Hubs, Channels)                                 │
         │                                               │
         │ Direct SQL (EF Core)                          │ Read-Only SQL (psycopg2)
         ▼                                               ▼
[ NEON POSTGRESQL CLOUD DATABASE ] ◄─────────────────────┘
```

### Các thành phần chính trong hệ thống:
1. **Frontend (`Fontend_Project_OTSP`):** React 19 Single Page Application, giao diện đáp ứng (responsive) Bootstrap 5, chia module rõ ràng theo Roles (Admin, Tutor, Student).
2. **Backend Web API (`Backend_Project_OTSP`):** ASP.NET Core 9, chịu trách nhiệm xác thực, phân quyền (RBAC), quản lý nghiệp vụ, giao dịch thanh toán, socket SignalR.
3. **AI Agent Service (`OTSP_WEIN_AI_AGENTS`):** Dịch vụ độc lập viết bằng Python FastAPI, tích hợp Google Gemini AI và Vision OCR, sử dụng cơ chế nạp sẵn dữ liệu DB vào RAM để phản hồi < 1ms.
4. **Cloud Database:** Neon Serverless PostgreSQL lưu trữ toàn bộ dữ liệu người dùng, lớp học, tài chính và lịch sử tin nhắn.
5. **Dịch vụ bên thứ ba (Third-party Services):**
   * **VietQR:** Sinh mã QR chuyển khoản ngân hàng chuẩn Napas 24/7.
   * **Cloudinary:** Lưu trữ và tối ưu hóa tài nguyên ảnh (avatar, ảnh chứng chỉ, ảnh tài liệu).
   * **Jitsi Meet:** Hệ thống hội nghị truyền hình WebRTC miễn phí phục vụ lớp học trực tuyến.
   * **Google OAuth2:** Đăng nhập một chạm tiện lợi.

---

## 3. CÁC MẪU THIẾT KẾ PHẦN MỀM (DESIGN PATTERNS) ÁP DỤNG
*(Trọng tâm ghi điểm khi Mentor / Giảng viên kiểm tra mã nguồn)*

### 1. Singleton Pattern (Mẫu cá thể duy nhất)
* **Vị trí áp dụng trong code:**
  * `ChatEventBus` (`IChatEventBus`): Đăng ký dạng `AddSingleton` trong `Program.cs`. Chỉ có duy nhất 1 luồng kênh (`Channel<ChatMessageCreatedEvent>`) tồn tại trong suốt vòng đời của server để phân phối tin nhắn.
  * `UserPresenceService` (`IUserPresenceService`): Sử dụng `ConcurrentDictionary` duy nhất lưu trữ trạng thái Online/Offline và các `ConnectionId` SignalR của mọi user.
  * `GoogleIdTokenValidator`: Singleton lưu cache public keys của Google để xác thực ID Token.
  * `VietQrService`: Singleton khởi tạo cấu hình ngân hàng 1 lần dùng chung cho mọi request tạo mã QR.
  * `get_gemini_client()` (trong Python AI): Khởi tạo một đối tượng `genai.Client` duy nhất, tái sử dụng kết nối HTTP Keep-Alive tới Google API.

### 2. Observer / Publisher - Subscriber Pattern (Mẫu người quan sát / Xuất bản - Đăng ký)
* **Vị trí áp dụng trong code:**
  * Triển khai qua `ChatEventBus.cs` và `ChatNotificationWorker.cs`:
    * **Publisher:** Khi người dùng gửi tin nhắn, `ChatService.SendMessageAsync()` ghi vào DB rồi gọi `_chatEventBus.PublishAsync(chatEvent)` và kết thúc request ngay (không để client chờ).
    * **Subscriber/Consumer:** `ChatNotificationWorker` (chạy ngầm kiểu `BackgroundService`) liên tục đọc từ `_chatEventBus.ReadEventsAsync()` để xử lý việc gửi thông báo tới người nhận, phát tín hiệu SignalR hoặc kích hoạt email nhắc nhở.

### 3. Repository & Unit of Work Pattern (Kho lưu trữ & Đơn vị công việc)
* **Vị trí áp dụng trong code:**
  * `ApplicationDbContext` của Entity Framework Core đóng vai trò là một **Unit of Work**, còn các `DbSet<T>` đóng vai trò là các **Repository**.
  * Cho phép gom nhiều thao tác thêm/sửa/xóa trên nhiều bảng khác nhau (`Classroom`, `ClassSession`, `Payment`, `AuditLog`) vào chung một giao dịch logic và commit bằng một lệnh gọi `await db.SaveChangesAsync()`.

### 4. Background Worker / Hosted Service Pattern (Dịch vụ xử lý nền)
* **Vị trí áp dụng trong code:**
  * `StaleSessionCleanupWorker.cs`: Chạy nền định kỳ mỗi 10 phút, tự động quét tìm các buổi học ở trạng thái `InProgress` hoặc `Scheduled` bị bỏ quên quá 6 giờ để đánh dấu hoàn tất (`Done`) và cập nhật thời gian rời phòng cho học viên.
  * `ChatNotificationWorker.cs`: Worker chạy ngầm tiêu thụ hàng đợi tin nhắn.

### 5. Strategy Pattern (Mẫu chiến lược)
* **Vị trí áp dụng trong code:**
  * **Chiến lược xác thực (Authentication Strategies):** Tách biệt chiến lược xác thực bằng tài khoản mật khẩu nội bộ (`Identity PasswordHasher`) và xác thực bằng Google ID Token (`GoogleIdTokenValidator`).
  * **Chiến lược thanh toán:** Xử lý thanh toán qua mã VietQR và kiểm tra đối soát tự động.

### 6. Adapter & Façade Pattern (Mẫu tương thích & Mặt bối)
* **Vị trí áp dụng trong code:**
  * `AiService.cs`: Đóng vai trò **Adapter & Façade** kết nối giữa Backend C# và Python AI Agent. C# Controller chỉ cần gọi `_aiService.ChatWithAgentAsync(...)`, tầng này tự động chuyển đổi DTO C#, đính kèm Header xác thực `X-Internal-Key`, gọi REST API HTTP, parse dữ liệu trả về.
  * `ChatService.cs`: Façade che giấu sự phức tạp của việc phối hợp 3 thành phần: Entity Framework DB, SignalR `IHubContext<ChatHub>` và `IChatEventBus`.

### 7. Middleware Pattern (Mẫu phần mềm trung gian)
* **Vị trí áp dụng trong code:**
  * `UserPresenceMiddleware` trong `Program.cs`: Bắt mọi request đi qua API, nếu có Claims người dùng hợp lệ thì gọi `presence.TouchUserActivity(userId)` để gia hạn trạng thái "Đang online" mà không cần gọi API riêng.
  * `add_utf8_charset` trong `main.py` (FastAPI): Tự động ép header `charset=utf-8` cho tất cả JSON response để tránh lỗi font chữ tiếng Việt.

### 8. Data Transfer Object (DTO) Pattern
* Tách biệt 100% giữa Entity (Database Model) và DTO (Payload gửi qua mạng).
* Ngăn chặn hoàn toàn lỗi bảo mật Over-Posting / Mass Assignment và giảm kích thước gói tin gửi về trình duyệt.

### 9. Dependency Injection (DI / Inversion of Control)
* Toàn bộ các dịch vụ từ Controller đến Service đều đăng ký qua IoC Container của .NET theo đúng vòng đời chuẩn: `AddScoped` (theo từng request), `AddSingleton` (toàn cục), `AddHostedService` (tiến trình nền).

---

## 4. CHI TIẾT BACKEND (ASP.NET CORE 9 WEB API)

### 4.1 Cấu hình & Pipeline khởi chạy (`Program.cs`)
* **Xác thực JWT:** Token ký bằng HMAC-SHA256, kiểm tra Issuer, Audience, hỗ trợ nhận Token qua Query String `?access_token=...` dành riêng cho kết nối WebSocket SignalR.
* **Xử lý lỗi chuẩn REST:** Trả về JSON có cấu trúc khi gặp lỗi `401 Unauthorized` hoặc `403 Forbidden` thay vì trả về HTML mặc định.
* **Tối ưu hóa hiệu năng:** Kích hoạt nén phản hồi Brotli và Gzip (`ResponseCompression`) cho các định dạng JSON/Text.

### 4.2 Danh sách Controller theo phân hệ nghiệp vụ
| Phân hệ | Controller | Nhiệm vụ chính |
| :--- | :--- | :--- |
| **Xác thực** | `AuthController.cs` | Đăng ký, Đăng nhập, Google Login, Đổi/Quên mật khẩu, Refresh/Logout. |
| **AI Tự hành** | `AiController.cs` | Chatbot điều khiển web, Trợ lý giải bài tập sư phạm từng bước. |
| **Gia sư (Tutor)** | `TutorProfileApiController.cs` | Cập nhật hồ sơ, tiểu sử, bằng cấp, video giới thiệu. |
| | `TutorAvailabilityApiController.cs` | Quản lý khung giờ rảnh trong tuần để nhận lịch dạy kèm. |
| | `TutorClassroomApiController.cs` | Tạo và quản lý lớp học nhóm, sĩ số, học phí. |
| | `TutorConnectionRequestApiController.cs` | Duyệt / Từ chối yêu cầu mời dạy kèm từ học viên. |
| | `TutorScheduleApiController.cs` | Xem thời khóa biểu cá nhân, lịch dạy theo tuần/tháng. |
| | `TutorIncomeApiController.cs` | Theo dõi tổng thu nhập, lịch sử đối soát, bảng kê tài chính. |
| | `TutorLearningDocumentApiController.cs` | Đăng tải và chia sẻ giáo trình, tài liệu ôn tập. |
| **Học viên (Student)** | `StudentProfileApiController.cs` | Quản lý hồ sơ học viên, thông tin phụ huynh bảo hộ. |
| | `StudentConnectionRequestApiController.cs`| Tìm gia sư, gửi yêu cầu kết nối học kèm 1-1. |
| | `StudentClassroomApiController.cs` | Xem danh sách lớp học nhóm đang mở, gửi đơn xin vào lớp. |
| | `StudentScheduleApiController.cs` | Xem lịch học, thời khóa biểu sắp diễn ra. |
| | `StudentPaymentApiController.cs` | Lấy mã thanh toán VietQR, kiểm tra trạng thái hoá đơn học phí. |
| | `TutorReviewApiController.cs` | Viết đánh giá, chấm sao (rating) sau khi hoàn thành buổi học. |
| **Admin** | `AdminTutorApprovalsController.cs` | Phê duyệt hồ sơ gia sư mới (xem phân tích OCR của AI). |
| | `AdminCatalogController.cs` | Quản lý danh mục Môn học (Subjects) và Khối lớp (GradeLevels). |
| | `AdminClassroomsController.cs` | Giám sát toàn bộ các lớp học trên nền tảng. |
| | `AdminOperationsController.cs` | Thống kê số liệu, đối soát tài chính giữa gia sư và nền tảng. |
| | `AdminComplaintsPage` / `ComplaintApiController.cs` | Tiếp nhận và phân xử khiếu nại từ học viên hoặc gia sư. |
| **Realtime** | `ChatApiController.cs` | Lấy danh sách hội thoại, lịch sử tin nhắn, thu hồi/xóa tin. |
| | `NotificationApiController.cs` | Quản lý thông báo hệ thống (chuông thông báo). |
| | `PaymentController.cs` | Tiếp nhận thanh toán và cập nhật trạng thái hóa đơn. |
| | `SearchApiController.cs` | Tìm kiếm tổng hợp (gia sư, lớp học, môn học, bộ lọc nâng cao). |
| | `ClassSessionController.cs` | Điểm danh, vào phòng học online, bắt đầu/kết thúc buổi học. |

### 4.3 SignalR Hubs (Giao tiếp thời gian thực)
* **`ChatHub.cs` (`/hubs/chat`):**
  * Gửi nhận tin nhắn tức thì giữa 2 người dùng.
  * Báo hiệu trạng thái đang gõ phím (`TypingIndicator`).
  * Đánh dấu đã xem (`MarkAsRead`), thông báo thu hồi tin nhắn.
  * Cập nhật trạng thái người dùng Online / Offline qua `UserPresenceService`.
* **`ConnectionRequestHub.cs` (`/hubs/connection-requests`):**
  * Bắn thông báo ngay lập tức cho gia sư khi có học viên vừa gửi yêu cầu đặt lịch học.

---

## 5. CHI TIẾT AI AGENT SERVICE (PYTHON FASTAPI & GEMINI)

Tọa lạc tại thư mục [`OTSP_WEIN_AI_AGENTS`](file:///c:/tai_lieu_hoc_thuat_cua_tuan/mauthietkephanmem/OTSP/OTSP_WEIN_AI_AGENTS), đóng vai trò là một Agent tự hành thông minh:

### 5.1 Các tính năng AI cốt lõi:
1. **Chatbot Tự hành Điều hướng Web (Autonomous Navigation Agent):**
   * Không chỉ trả lời văn bản, AI nhận diện ý định của người dùng để trả về `uiAction`:
     * Người dùng hỏi: *"Tôi muốn xem lịch học"* $\rightarrow$ Trả lời + Điều hướng sang `/student/schedule` hoặc `/tutor/schedule`.
     * Người dùng hỏi: *"Tìm gia sư Toán lớp 12 giá dưới 200k"* $\rightarrow$ Gọi tool tra cứu RAM cache + Trả về danh sách gia sư + Gợi ý chuyển tab.
2. **Bộ nhớ RAM Cache siêu tốc (< 1ms):**
   * File `memory.py` tải sẵn dữ liệu Gia sư, Lớp học, Tin tức, Tài liệu vào RAM khi khởi động.
   * Khi người dùng hỏi tìm kiếm, thuật toán lọc trên RAM trả về kết quả ngay lập tức mà không phải tốn thời gian truy vấn DB PostgreSQL hoặc làm nghẽn API.
3. **Trợ lý Giải bài tập Sư phạm (`student_chat.py`):**
   * Giải toán/khoa học tự nhiên theo từng bước mạch lạc.
   * Công thức toán học định dạng chuẩn KaTeX LaTeX (`$x = \frac{-b \pm \sqrt{\Delta}}{2a}$`).
   * Tự động tóm tắt lý thuyết trọng tâm và tạo thêm 1 bài tập tương tự đổi số để học viên tự luyện.
4. **Kiểm định Bằng cấp / Chứng chỉ Tự động (`certificate_ocr.py`):**
   * Sử dụng Google Gemini Multimodal Vision đọc ảnh bằng cấp đại học, chứng chỉ ngoại ngữ (IELTS, TOEIC...).
   * Trích xuất: Họ tên, trường cấp, loại bằng, điểm số, ngày cấp, ngày hết hạn.
   * So khớp tên trên bằng với tên tài khoản đăng ký, cảnh báo dấu hiệu bất thường, đưa ra khuyến nghị duyệt cho Admin.

---

## 6. CHI TIẾT FRONTEND (REACT 19 + VITE + BOOTSTRAP)

### 6.1 Cấu trúc mã nguồn Frontend
* **`src/app/` & `src/routes/`:** Cấu hình router tập trung trong `AppRoutes.jsx`, áp dụng kỹ thuật **Lazy-loading (`React.lazy` + `Suspense`)** giúp tối ưu tốc độ tải trang lần đầu.
* **`src/context/`:**
  * `AuthContext.jsx`: Lưu trữ phiên đăng nhập, JWT token, User Info và hàm kiểm tra vai trò (`isTutor`, `isStudent`, `isAdmin`).
  * `NotificationContext.jsx` & `ChatContext.jsx`: Quản lý kết nối SignalR, lưu trữ số lượng tin nhắn và thông báo chưa đọc.
* **`src/features/`:** Phân chia theo từng phân hệ nghiệp vụ:
  * `features/admin/`: Các màn hình quản trị của Admin.
  * `features/tutor/`: Màn hình lịch dạy, lớp học, thu nhập, tài liệu của Gia sư.
  * `features/student/`: Màn hình đặt lịch, thanh toán VietQR, lớp học của Học viên.
  * `features/chat/`: Giao diện chat thời gian thực như Messenger, có gửi ảnh và emoji.
  * `features/classroom/`: Phòng học trực tuyến tích hợp Jitsi Meet SDK.
* **Bảo vệ tuyến đường (`ProtectedRoute.jsx`):** Ngăn chặn người dùng truy cập trái phép vào các trang không đúng thẩm quyền (chuyển hướng về `/access-denied` hoặc `/login`).

---

## 7. CƠ SỞ DỮ LIỆU & CÁC THỰC THỂ CHÍNH (ENTITIES)

Hệ thống sử dụng **PostgreSQL** trên Neon Cloud với các thực thể quan hệ chặt chẽ:

```
[ ApplicationUser ] ── 1:1 ── [ StudentProfile ] ── 1:N ── [ ConnectionRequest ]
        │                                                          │
       1:1                                                        N:1
        │                                                          │
  [ TutorProfile ] ──── 1:N ── [ TutorAvailability ]               │
        │                                                          │
       1:N                                                         │
        ├──────── 1:N ── [ Classroom ] ── 1:N ── [ ClassroomStudent ]
        │                     │
       1:N                   1:N
        │                     │
  [ Qualification ]     [ ClassSession ] ── 1:N ── [ ClassSessionAttendance ]
        │                     │
  [ TutorSubject ]            │ (Phòng học Jitsi)
                              │
  [ Payment / Invoice ] ◄─────┘ (Thanh toán qua VietQR)
```

### Bảng tóm tắt các Entity quan trọng:
1. **`ApplicationUser`:** Tài khoản người dùng chung (kế thừa `IdentityUser<string>`), lưu họ tên, email, avatar, trạng thái khóa/mở.
2. **`TutorProfile`:** Hồ sơ gia sư (học phí theo giờ `HourlyFee`, đánh giá trung bình `AverageRating`, hình thức dạy Online/Offline, trạng thái duyệt `ApprovalStatus`).
3. **`StudentProfile`:** Hồ sơ học viên (lớp học hiện tại, trường học, thông tin người giám hộ/phụ huynh `Guardian`).
4. **`Qualification`:** Bằng cấp, chứng chỉ gia sư đính kèm ảnh bằng chứng để Admin duyệt.
5. **`TutorAvailability`:** Khung giờ rảnh cố định trong tuần của gia sư (Thứ, Giờ bắt đầu, Giờ kết thúc).
6. **`ConnectionRequest`:** Yêu cầu học kèm 1-1 giữa học viên và gia sư (chứa trạng thái: Chờ duyệt, Chấp nhận, Từ chối, Đã hủy).
7. **`Classroom` & `ClassroomStudent`:** Quản lý lớp học nhóm mở rộng, số lượng học sinh tối đa, ngày bắt đầu, học phí trọn gói.
8. **`ClassSession` & `ClassSessionAttendance`:** Buổi học thực tế (ngày giờ diễn ra, link phòng học trực tuyến, điểm danh học sinh, ghi chú bài học).
9. **`Payment` & `Invoice`:** Hóa đơn thanh toán học phí qua mã VietQR, ghi nhận trạng thái thanh toán (`Pending`, `Paid`, `Failed`).
10. **`Complaint`:** Đơn khiếu nại của người dùng khi có sự cố về buổi học hoặc chất lượng dạy để Admin can thiệp.
11. **`AiStudySession`:** Lịch sử các câu hỏi bài tập của học viên và lời giải của Trợ lý AI.

---

## 8. CÁC LUỒNG NGHIỆP VỤ TRỌNG TÂM (CORE WORKFLOWS)

### Luồng 1: Đăng ký & Kiểm định gia sư bằng AI OCR
1. Người dùng đăng ký tài khoản với vai trò `Tutor`.
2. Gia sư điền thông tin học vấn, tải ảnh chứng chỉ/bằng cấp lên hệ thống (ảnh lưu trữ an toàn trên Cloudinary).
3. Backend .NET gọi API `/v1/verify-certificate` của Python AI Service.
4. AI Vision OCR đọc dữ liệu trên ảnh, so sánh tên trên bằng với tên gia sư, kiểm tra độ tin cậy.
5. Admin vào màn hình `AdminApprovalsPage`, xem kết quả trích xuất tự động của AI và chỉ cần bấm **"Chấp thuận"** hoặc **"Từ chối"** kèm lý do.

### Luồng 2: Tìm kiếm gia sư & Đặt lịch học kèm (Booking 1-1)
1. Học viên vào trang tìm kiếm, lọc theo Môn học, Khối lớp, Khu vực, Khoảng giá và Đánh giá sao.
2. Học viên xem trang chi tiết gia sư, kiểm tra các khung giờ rảnh trong tuần (`TutorAvailability`).
3. Học viên chọn lịch phù hợp và bấm **"Gửi yêu cầu kết nối"**.
4. SignalR Hub đẩy thông báo tức thì đến tài khoản của Gia sư.
5. Gia sư bấm "Chấp nhận" $\rightarrow$ Hệ thống tự động tạo hóa đơn thanh toán và tạo các buổi học (`ClassSession`).

### Luồng 3: Thanh toán học phí tự động qua VietQR
1. Học viên vào chi tiết thanh toán (`PaymentDetailPage`).
2. Backend gọi `VietQrService` sinh URL mã QR động chứa sẵn: Số tiền, Số tài khoản ngân hàng thụ hưởng, Nội dung chuyển khoản chuẩn hóa (`OTSP_PAY_<ID>`).
3. Học viên mở App ngân hàng quét mã QR thanh toán nhanh 24/7.
4. Khi giao dịch hoàn tất, hệ thống kích hoạt trạng thái `Paid`, cấp quyền vào lớp cho học viên.

### Luồng 4: Phòng học trực tuyến & Tự động dọn dẹp buổi học treo
1. Đến giờ học, Gia sư và Học viên bấm nút **"Vào lớp học"** trên màn hình lịch trình.
2. Hệ thống nhúng phòng học **Jitsi Meet** ngay trong ứng dụng (hỗ trợ bật cam, mic, chia sẻ màn hình, chat trong phòng).
3. Hệ thống tự động điểm danh (`ClassSessionAttendance`) khi học viên tham gia.
4. Nếu gia sư quên bấm "Kết thúc buổi học", dịch vụ chạy ngầm `StaleSessionCleanupWorker` sẽ tự động đóng buổi học sau 6 tiếng để đảm bảo tính nhất quán dữ liệu.

---

## 9. BỘ CÂU HỎI "BẪY" MENTOR HAY HỎI & ĐÁP ÁN ĂN ĐIỂM TUYỆT ĐỐI

#### ❓ Câu 1: Tại sao không dùng trực tiếp SQL Server mà lại chọn PostgreSQL trên Neon Cloud?
> **Trả lời:** PostgreSQL có khả năng tương thích cao với kiểu dữ liệu chuỗi, mảng và hỗ trợ mở rộng Full-Text Search rất tốt. Neon Cloud cung cấp kiến trúc Serverless Postgres, tự động co giãn tài nguyên và có tính năng Branching dữ liệu, giúp đội ngũ phát triển dễ dàng test và deploy trên môi trường Cloud (như Render) mà không tốn chi phí máy chủ cố định.

#### ❓ Câu 2: Trong dự án, em đã hiện thực mẫu Observer Pattern ở đâu?
> **Trả lời:** Em hiện thực qua hệ thống Event-Driven Chat tại `ChatEventBus.cs` và `ChatNotificationWorker.cs`. Khi một tin nhắn mới được gửi, thay vì bắt người dùng chờ gửi thông báo và ghi log, `ChatService` đóng vai trò Publisher đẩy event vào `System.Threading.Channels.Channel`. Một Background Worker chạy ngầm đóng vai trò Subscriber liên tục lắng nghe kênh này để phát thông báo và tín hiệu SignalR. Việc này giúp giảm độ trễ (latency) của API chat xuống mức tối thiểu.

#### ❓ Câu 3: Vì sao lại tách Chatbot AI ra một service Python riêng mà không gọi trực tiếp Gemini SDK từ C# Backend?
> **Trả lời:** Có 3 lý do kiến trúc quan trọng:
> 1. **Khả năng mở rộng (Scalability):** Tác vụ xử lý ngôn ngữ và AI OCR tiêu tốn nhiều tài nguyên CPU/RAM và thời gian phản hồi phụ thuộc vào mô hình AI. Việc tách riêng giúp Backend C# không bao giờ bị nghẽn (blocking) các tác vụ giao dịch cốt lõi.
> 2. **Hệ sinh thái AI của Python:** Python là ngôn ngữ tiêu chuẩn cho AI/Data, có sẵn các thư viện xử lý ảnh, RAG, bộ nhớ cache và SDK Gemini cập nhật nhanh nhất.
> 3. **Cơ chế In-Memory RAM Cache:** Service Python nạp sẵn dữ liệu gia sư vào RAM để tìm kiếm và lọc chỉ mất dưới 1 mili-giây, sau đó mới tổng hợp kết quả trả về cho C#.

#### ❓ Câu 4: Làm thế nào để đảm bảo người dùng không hack được các API nhạy cảm của Admin hoặc Gia sư?
> **Trả lời:** Hệ thống áp dụng bảo mật 3 lớp:
> 1. **Xác thực JWT Bearer:** Mỗi request đều phải kèm theo Token hợp lệ có chữ ký điện tử HMAC-SHA256.
> 2. **Phân quyền theo vai trò (RBAC):** Mọi API đều gắn attribute `[Authorize(Roles = "Admin")]` hoặc `[Authorize(Roles = "Tutor")]`.
> 3. **Bảo vệ API nội bộ:** Giữa Backend C# và Python AI giao tiếp qua Header bảo mật `X-Internal-Key`, người ngoài không thể gọi trực tiếp vào dịch vụ AI mà phải đi qua cổng kiểm soát của Backend .NET.

#### ❓ Câu 5: Khi mất kết nối mạng hoặc người dùng tắt trình duyệt, làm sao hệ thống biết họ đã Offline?
> **Trả lời:** Nhờ `UserPresenceService` kết hợp với `ChatHub` trong SignalR:
> Khi kết nối WebSocket bị đứt, SignalR tự động kích hoạt sự kiện `OnDisconnectedAsync()`, service sẽ xóa `ConnectionId` tương ứng. Nếu người dùng không còn bất kỳ tab trình duyệt nào mở (tập hợp kết nối rỗng), hệ thống sẽ cập nhật trạng thái là **Offline** và phát thông báo qua WebSocket đến danh bạ của những người đang chat cùng.
