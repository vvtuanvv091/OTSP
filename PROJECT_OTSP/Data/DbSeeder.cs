using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Data;

// Tạo dữ liệu mẫu khi chạy ở môi trường phát triển.
// Chạy an toàn nhiều lần: mỗi phần kiểm tra trước khi thêm.
public static class DbSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        // 1. Vai trò (phải có trước khi gán cho tài khoản)
        foreach (var ten in new[] { "Admin", "Tutor", "Student_Parent" })
        {
            if (!await roleManager.RoleExistsAsync(ten))
                await roleManager.CreateAsync(new ApplicationRole { Name = ten });
        }

        // 2. Danh mục môn-cấp học (không có khóa ngoại nên seed trước)
        if (!await db.SubjectLevels.AnyAsync())
        {
            db.SubjectLevels.AddRange(
                new SubjectLevel { SubjectName = "Toán", GradeLevel = "THCS" },
                new SubjectLevel { SubjectName = "Toán", GradeLevel = "THPT" },
                new SubjectLevel { SubjectName = "Tiếng Anh", GradeLevel = "THPT" }
            );
            await db.SaveChangesAsync();
        }

        // 3. Tài khoản Admin
        const string adminEmail = "admin@otsp.local";
        if (await userManager.FindByEmailAsync(adminEmail) == null)
        {
            var admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "Quản trị viên"
            };
            var kq = await userManager.CreateAsync(admin, "Admin@12345");
            if (kq.Succeeded)
            {
                await userManager.AddToRoleAsync(admin, "Admin");
                db.AdminProfiles.Add(new AdminProfile { UserId = admin.Id, Rank = "Quản trị cấp cao" });
                await db.SaveChangesAsync();
            }
        }

        // 4. Gia sư mẫu (đã duyệt để hiện trong tìm kiếm)
        const string tutorEmail = "tutor1@otsp.local";
        if (await userManager.FindByEmailAsync(tutorEmail) == null)
        {
            var tutorUser = new ApplicationUser
            {
                UserName = tutorEmail,
                Email = tutorEmail,
                EmailConfirmed = true,
                FullName = "Nguyễn Văn Gia Sư",
                DateOfBirth = new DateOnly(1995, 5, 10)
            };
            var kq = await userManager.CreateAsync(tutorUser, "Tutor@12345");
            if (kq.Succeeded)
            {
                await userManager.AddToRoleAsync(tutorUser, "Tutor");

                var tutor = new TutorProfile
                {
                    UserId = tutorUser.Id,
                    Introduction = "Giáo viên Toán 5 năm kinh nghiệm luyện thi THCS",
                    HourlyFee = 150000,
                    VerificationStatus = VerificationStatus.Approved,
                    AverageRating = 4.8
                };
                db.TutorProfiles.Add(tutor);
                await db.SaveChangesAsync();

                // Gán môn dạy: Toán THCS
                var toanThcs = await db.SubjectLevels
                    .FirstAsync(s => s.SubjectName == "Toán" && s.GradeLevel == "THCS");
                db.TutorSubjectLevels.Add(new TutorSubjectLevel
                {
                    TutorId = tutor.Id,
                    SubjectLevelId = toanThcs.Id
                });
                await db.SaveChangesAsync();
            }
        }

        // 5. Học viên mẫu dưới 18 tuổi, có người giám hộ
        const string studentEmail = "hocvien1@otsp.local";
        if (await userManager.FindByEmailAsync(studentEmail) == null)
        {
            var studentUser = new ApplicationUser
            {
                UserName = studentEmail,
                Email = studentEmail,
                EmailConfirmed = true,
                FullName = "Trần Thị Học Viên",
                DateOfBirth = new DateOnly(2010, 3, 15)
            };
            var kq = await userManager.CreateAsync(studentUser, "Student@12345");
            if (kq.Succeeded)
            {
                await userManager.AddToRoleAsync(studentUser, "Student_Parent");

                var student = new StudentProfile
                {
                    UserId = studentUser.Id,
                    Area = "Biên Hòa",
                    FreeSchedule = "Thứ 3, Thứ 5 buổi tối"
                };
                db.StudentProfiles.Add(student);
                await db.SaveChangesAsync();

                db.Guardians.Add(new Guardian
                {
                    FullName = "Trần Văn Phụ Huynh",
                    ContactInfo = "0900000000",
                    StudentProfileId = student.Id
                });
                await db.SaveChangesAsync();
            }
        }
    }
}