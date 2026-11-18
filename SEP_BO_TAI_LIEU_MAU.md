# Bộ tài liệu mẫu đã soạn sẵn nội dung — Đề tài minh hoạ

**Đề tài:** Xây dựng Website kết nối Gia sư và Học viên Trực tuyến (OTSP)  
**Nhóm thực hiện (giả định):** Nhóm 05 – Lớp 22DTH1 - 037019702 (5 thành viên)  
**Phạm vi chức năng:** Kết nối Gia sư – Học viên trực tuyến: Quản lý tài khoản & phân quyền (`Admin`, `Tutor`, `Student_Parent`), kiểm duyệt hồ sơ & bằng cấp gia sư (hỗ trợ AI OCR), tìm kiếm gia sư & lớp học, đặt lịch học thử / học chính thức 1-1 & lớp nhóm, thanh toán học phí VietQR (SePay), phòng học trực tuyến Jitsi Meet, nhắn tin thời gian thực (SignalR), kho tài liệu học tập, đánh giá, khiếu nại và trợ lý AI học tập.  
**Công nghệ:** ASP.NET Core 9 Web API + Entity Framework Core 9 + SignalR (Backend), React 19 + Vite + Bootstrap 5 + JavaScript (Frontend), Python FastAPI + Google Gemini SDK (AI Agent Service), PostgreSQL trên Neon Cloud (Cơ sở dữ liệu).

---

## Đây là gì?

Đây là bản **đã điền đầy đủ nội dung** của 24 loại tài liệu trong bộ
`SEP_Template_Kit_RutGon.zip` (bộ mẫu rút gọn còn trống) dùng cho đề tài **OTSP**.
Dùng file này như một **ví dụ mẫu hoàn chỉnh** để:

- Cho thành viên nhóm xem trước khi tự hoàn thiện bộ tài liệu của mình, giúp hình dung một bộ tài liệu SEP "đúng và đủ" trông như thế nào.
- Đối chiếu song song với bộ mẫu trống: cột nào cần điền gì, viết theo giọng văn ra sao, mức độ chi tiết bao nhiêu.
- Nhóm 05 (giả định) hoàn thành theo từng Sprint; ở Sprint 1 tập trung vào các tài liệu tổng quan (Function List, Traceability Matrix, Database Design thiết kế cho toàn bộ hệ thống OTSP), còn Test Case / Test Execute / Defect Report / Test Report / User Acceptance làm mẫu cho chức năng đại diện: **"Thêm tài khoản"** (tương đương chức năng "Add User" trong bộ tài liệu gốc và API quản trị tài khoản `AdminOperationsController`).

---

## Cấu trúc thư mục

Giữ nguyên đúng cấu trúc 4 phần như bộ mẫu rút gọn:

```
1. PROJECT MANAGEMENT/
   1.1 PROJECT PLANNING/      (TeamCharter, ProjectPlan, RiskManagementPlan,
                                RiskList, Milestone, ProductBacklog,
                                ConfigurationManagementPlan, Sprint1 Plan)
   1.2 PROJECT TRACKING/      (Retrospective Sprint1, Sprint1 Backlog Items)
   1.3 MEETING/               (1 biên bản họp mẫu)
2. CONTEXT AND SYSTEM REQUIREMENT/
                              (ConceptOfOperation, FunctionList,
                               TraceabilityMatrix, BusinessRule)
3. SOFTWARE ARCHITECTURE & DESIGN/
                              (ArchitectureDriver, ArchitectureDesign,
                               Database Analysis and Design)
4. TESTING/
   4.1 TestPlan
   4.3 TEST CASE (mau)/       Test Case "Thêm tài khoản"
   4.4 TEST EXECUTE (mau)/    Test Execute "Thêm tài khoản" (2 lần chạy:
                               lần 1 có 1 test Fail -> lần 2 Pass sau khi fix)
   4.5 DEFECT REPORT/         Defect liên kết với test Fail ở trên
   4.6 TEST REPORT/           Báo cáo tổng hợp kết quả test Sprint 1
   4.7 USER ACCEPTANCE/       Biên bản nghiệm thu Sprint 1 (PDF)
```

---

## Lưu ý khi sử dụng cho đề tài OTSP

- Các số liệu kế hoạch, ngày họp hoặc dữ liệu giả lập (nếu dùng minh hoạ) cần đồng bộ thống nhất xuyên suốt các tài liệu trong bộ SEP.
- Nội dung phải được xây dựng song song, nhất quán với mã nguồn thực tế của hệ thống **OTSP**: tên chức năng trong `Function List` khớp với `Traceability Matrix`, khớp với `Test Case`, khớp với các bảng thực thể (`ApplicationUser`, `TutorProfile`, `StudentProfile`, `Classroom`, `ClassSession`, `ConnectionRequest`, `Payment`, `LearningDocument`, `Review`, `Complaint`...) trong `Database Design` — mọi tài liệu phải "khớp" nhau, không viết rời rạc từng file.
- Khi mở rộng sang các Sprint tiếp theo (Đặt lịch 1-1 & lớp nhóm, Thanh toán VietQR SePay, Lớp học trực tuyến Jitsi Meet, Chat SignalR, Kho tài liệu & AI Agent), nhóm nhân bản các mẫu Sprint Plan, Test Case, Test Execute, Defect Report, Test Report, User Acceptance và Retrospective tương ứng cho từng Sprint.
