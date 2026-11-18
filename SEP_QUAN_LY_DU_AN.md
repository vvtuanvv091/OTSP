# TÀI LIỆU QUẢN LÝ DỰ ÁN PHẦN MỀM (chuẩn SEP)

**Website kết nối Gia sư và Học viên Trực tuyến (OTSP)**  
*(Các actor trong hệ thống: `Admin` / `Gia sư (Tutor)` / `Học viên & Phụ huynh (Student_Parent)` — áp dụng thay cho bộ actor mẫu Admin / Lễ tân / Bác sĩ / Bệnh nhân)*

---

## 1. PROJECT MANAGEMENT — Quản lý dự án

### 1.1 Project Planning (lập kế hoạch đầu kỳ)
| File | Ý nghĩa | Khi nào làm |
|---|---|---|
| TeamCharter | Danh sách thành viên, vai trò, nội quy nhóm | Tuần 1 |
| ProjectPlan | Tài liệu tổng: mục tiêu, phạm vi, stakeholder, phương pháp làm | Tuần 1–2 |
| ConfigurationManagementPlan | Quy ước đặt tên file, version, nơi lưu trữ (OneDrive/Git: nhánh `main`, `developer`, `feature/*`) | Tuần 1, áp dụng xuyên suốt |
| RiskManagementPlan | Quy trình nhận diện & xử lý rủi ro | Tuần 1–2 |
| RiskList | Danh sách rủi ro cụ thể + xác suất/tác động | Cập nhật mỗi sprint |
| Milestone | Timeline tổng các sprint | Tuần 1, đối chiếu Plan vs Actual |
| ProductBacklog | Toàn bộ tính năng của nền tảng OTSP dạng user story, có ưu tiên | Cập nhật liên tục |
| Sprint1Plan (mẫu) | Backlog **riêng của 1 sprint**: việc, story point, người làm | Đầu mỗi sprint — nhân bản cho Sprint 0/2/3 |

### 1.2 Project Tracking (theo dõi trong kỳ)
| File | Ý nghĩa | Khi nào làm |
|---|---|---|
| RetrospectiveSummary (mẫu Sprint1) | Họp cuối sprint: cái tốt / cần cải thiện / bài học | Cuối mỗi sprint — nhân bản cho từng sprint |
| Sprint1_BacklogItems (mẫu) | Task chi tiết + trạng thái trong sprint | Cập nhật hàng ngày trong sprint |

### 1.3 Meeting (họp)
| File | Ý nghĩa | Khi nào làm |
|---|---|---|
| MeetingReport (1 mẫu) | Biên bản họp: người dự, nội dung, action list | Sau mỗi buổi họp — nhân bản theo tuần/thứ |

---

## 2. CONTEXT AND SYSTEM REQUIREMENT — Yêu cầu hệ thống

| File | Ý nghĩa | Khi nào làm |
|---|---|---|
| ConceptOfOperation | Mô tả quy trình hiện tại (VD: quy trình tìm kiếm, kiểm định bằng cấp gia sư và đặt lịch học thủ công) và quy trình mới sau khi có nền tảng OTSP | Trước khi thiết kế, thống nhất với các bên liên quan |
| FunctionList | Danh sách chức năng, mã hoá `F.x.x`, độ ưu tiên | Là nguồn gốc cho mọi tài liệu sau (test case, kiến trúc...) |
| TraceabilityMatrix | Đối chiếu Requirement ↔ Test Case ↔ Thiết kế | Review cuối mỗi sprint |
| BusinessRule | Các ràng buộc nghiệp vụ của OTSP (VD: gia sư phải được Admin duyệt mới được mở lớp; học viên chỉ có 1 lớp/yêu cầu 1-1 hoạt động cho cùng cặp môn – cấp học; lịch học không được trùng giờ; tài liệu công khai cần Admin duyệt trước khi hiển thị công khai) | Dev tra cứu khi code |

---

## 3. SOFTWARE ARCHITECTURE & DESIGN — Thiết kế kiến trúc

| File | Ý nghĩa | Khi nào làm |
|---|---|---|
| ArchitectureDriver | Functional Requirement (FR), Entity (`AdminProfile`, `TutorProfile`, `StudentProfile`, `Classroom`, `ClassSession`, `ConnectionRequest`, `Payment`, `LearningDocument`...), Use Case, Quality Attribute | Sau khi có Function List |
| ArchitectureDesign | Kiến trúc hệ thống OTSP: sơ đồ ngữ cảnh, vật lý (Vercel / Render / Neon Cloud), luồng động (ASP.NET Core 9 Web API + React 19 SPA + Python FastAPI AI Agent + SignalR + Design Patterns) | Trước khi code |
| DatabaseAnalysisAndDesign | ERD + mô tả từng bảng, cột dữ liệu (PostgreSQL) | Trước khi code, dev dùng để tạo DB |

---

## 4. TESTING — Kiểm thử

| File | Ý nghĩa | Khi nào làm |
|---|---|---|
| TestPlan | Chiến lược test tổng: loại test (Unit Test xUnit, API/Integration, UI), ai làm, khi nào | Đầu dự án |
| TestCase (mẫu "Add-User") | Kịch bản test cụ thể cho 1 chức năng | Viết song song lúc code chức năng đó |
| TestExecute (mẫu "Add-User") | Chính file Test Case, điền thêm kết quả thực tế Pass/Fail | Sau khi code xong, chạy test |
| DefectReport (mẫu Sprint1) | Ghi nhận lỗi tìm thấy khi test | Khi test Fail |
| TestReport (mẫu Sprint1) | Tổng hợp % Pass/Fail của cả sprint | Cuối mỗi sprint, báo cáo mentor |
| UserAcceptance (mẫu Sprint1, PDF) | Người đại diện / giảng viên ký xác nhận từng chức năng đạt | Cuối mỗi sprint, bước "chốt" sprint |

---

## Cách dùng bộ mẫu này

1. **Áp dụng đúng cấu trúc bảng/mục** theo dữ liệu thực tế của đề tài **Website kết nối Gia sư và Học viên Trực tuyến (OTSP)** (actor, chức năng, bảng CSDL, luồng nghiệp vụ, API và test của nhóm).
2. Với các file có ghi "(mẫu)" — đây là **1 bản đại diện**, nhóm tự nhân bản theo số sprint / số chức năng / số tuần của mình (không cần hỏi thêm mẫu khác, cấu trúc giống hệt).
3. Thứ tự làm đúng: `Concept of Operation → Function List → Architecture → Sprint Plan → (code + Test Case song song) → Test Execute → Defect Report → Test Report → User Acceptance → Retrospective`.
4. Kiểm tra lại các lỗi thường gặp trước khi nộp: định dạng ngày tháng Excel đồng nhất, không để trống các cột bắt buộc, version file tăng đúng quy ước (`Ver1.0` → `Ver1.1`...).
