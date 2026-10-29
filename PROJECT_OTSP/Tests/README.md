# HỆ THỐNG KIỂM THỬ DỰ ÁN OTSP (TEST SUITE & DOCUMENTATION)
> Phục vụ chuyên đề: **Bài 08 — Kiểm thử phục vụ thiết kế: Unit Test, TDD, Testing Patterns**  
> Phục vụ phân mục: **📝 B — Kiểm thử & sửa lỗi: Test Case, Unit Test, Bug Report, Test Report**

---

## 📁 CẤU TRÚC THƯ MỤC KIỂM THỬ

```
Tests/
│
├── PROJECT_OTSP.Tests.csproj   # File dự án xUnit (.NET 10, Moq, FluentAssertions, EF Core In-Memory)
│
├── Helpers/                    # Các bộ bổ trợ kiểm thử độc lập
│   └── TestDbContextFactory.cs # Factory tạo CSDL In-Memory độc lập cho từng test (Tránh xung đột dữ liệu)
│
├── Builders/                   # Testing Pattern: Test Data Builder
│   ├── TutorProfileBuilder.cs       # Fluent builder tạo hồ sơ gia sư & user linh hoạt
│   ├── ClassSessionBuilder.cs       # Fluent builder tạo buổi học & lớp học
│   └── ConnectionRequestBuilder.cs  # Fluent builder tạo yêu cầu kết nối
│
├── UnitTests/                  # Mã nguồn các bài Unit Test tự động (Áp dụng AAA Pattern & TDD)
│   ├── SearchServiceTests.cs          # Kiểm thử tìm kiếm, lọc học phí, trạng thái duyệt, phân trang
│   ├── ChatServiceTests.cs            # Kiểm thử phân quyền chat, validation độ dài, lưu tin nhắn
│   ├── ClassSessionTests.cs          # Kiểm thử chuyển đổi trạng thái buổi học, tạo mã phòng video
│   └── AvailabilitySlotServiceTests.cs # Kiểm thử tính toán slot rảnh và loại trừ lịch bận
│
└── Docs/                       # 📝 B — Bộ hồ sơ kiểm thử & sửa lỗi chuẩn quy cách
    ├── B1_TEST_CASES.md               # 16 Test Cases chi tiết bao phủ toàn bộ phân hệ
    ├── B2_UNIT_TEST_TDD_PATTERNS.md   # Phân tích nguyên lý Design for Testability, TDD & Design Patterns
    ├── B3_BUG_REPORT.md               # Nhật ký 5 lỗi thực tế kèm phân tích nguyên nhân & giải pháp
    └── B4_TEST_REPORT.md              # Báo cáo tổng kết nghiệm thu chất lượng phần mềm
```

---

## 🚀 HƯỚNG DẪN CHẠY TEST

### Cách 1: Sử dụng Visual Studio 2022 / 2026
1. Mở solution `PROJECT_OTSP.slnx`. Dự án kiểm thử `PROJECT_OTSP.Tests` đã được tích hợp sẵn.
2. Mở cửa sổ **Test Explorer** (`Test` -> `Test Explorer` hoặc nhấn tổ hợp phím `Ctrl + E, T`).
3. Nhấn nút **Run All Tests In View** (hoặc `Ctrl + R, A`).
4. Toàn bộ các bài test sẽ chạy và hiển thị dấu tích xanh lá (**Passed**).

### Cách 2: Sử dụng dòng lệnh (.NET CLI)
Mở terminal tại thư mục gốc của dự án và chạy:
```bash
dotnet test Tests/PROJECT_OTSP.Tests.csproj
```

