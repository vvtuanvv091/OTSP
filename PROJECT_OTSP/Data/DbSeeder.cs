    using Microsoft.AspNetCore.Identity;
    using Microsoft.EntityFrameworkCore;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.DependencyInjection;
    using Microsoft.Extensions.Logging;
    using PROJECT_OTSP.Models;
    using static PROJECT_OTSP.Models.Enums;

    namespace PROJECT_OTSP.Data;

    /// <summary>
    /// Quản lý việc nạp dữ liệu mẫu và dữ liệu khởi tạo ban đầu cho hệ thống.
    /// - SeedCoreDataAsync: Dữ liệu tối thiểu (Vai trò, Danh mục cơ bản, 3 tài khoản mặc định đại diện cho các vai trò).
    /// - ExtraSampleDataSeeder: 30 bộ dữ liệu phong phú phục vụ kiểm thử, được điều khiển qua cờ cấu hình 'SeedData:EnableBulkSampleData'.
    /// </summary>
    public static class DbSeeder
    {
    public static async Task SeedAsync(IServiceProvider services, bool? forceBulk = null)
    {
        // forceBulk được giữ lại để không làm lỗi các nơi đang gọi SeedAsync(services, true/false).
        // Dữ liệu mẫu số lượng lớn (ExtraSampleDataSeeder) đã bị bỏ.
        var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
        var db = services.GetRequiredService<ApplicationDbContext>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

        await SeedCoreDataAsync(db, userManager, roleManager, logger);
    }

    private static async Task SeedCoreDataAsync(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger logger)
        {
            // 1. Vai trò cơ bản
            var roleNames = new[] { "Admin", "Tutor", "Student_Parent" };
            var existingRoleNames = await roleManager.Roles
                .Where(r => r.Name != null && roleNames.Contains(r.Name))
                .Select(r => r.Name!)
                .ToListAsync();
            foreach (var ten in roleNames.Except(existingRoleNames))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = ten });
            }

            // 2. Danh mục môn học cơ bản
            var monHoc = new[]
            {
                (Code: "TOAN", Name: "Toán"),
                (Code: "TIENGANH", Name: "Tiếng Anh")
            };
            var subjectCodes = monHoc.Select(m => m.Code).ToArray();
            var existingSubjectCodes = await db.Subjects
                .Where(s => subjectCodes.Contains(s.Code))
                .Select(s => s.Code)
                .ToListAsync();
            foreach (var m in monHoc.Where(m => !existingSubjectCodes.Contains(m.Code)))
            {
                db.Subjects.Add(new Subject { Code = m.Code, Name = m.Name });
            }

            // 3. Danh mục cấp học cơ bản
            var capHoc = new[]
            {
                (Code: "TH", Name: "Tiểu học"),
                (Code: "THCS", Name: "Trung học cơ sở"),
                (Code: "THPT", Name: "Trung học phổ thông")
            };
            var gradeLevelCodes = capHoc.Select(c => c.Code).ToArray();
            var existingGradeLevelCodes = await db.GradeLevels
                .Where(g => gradeLevelCodes.Contains(g.Code))
                .Select(g => g.Code)
                .ToListAsync();
            foreach (var c in capHoc.Where(c => !existingGradeLevelCodes.Contains(c.Code)))
            {
                db.GradeLevels.Add(new GradeLevel { Code = c.Code, Name = c.Name });
            }
            await db.SaveChangesAsync();

            // 4. Batch kiểm tra các tài khoản cốt lõi (Admin, Tutor, Student mặc định)
            const string adminEmail = "admin@otsp.local";
            const string tutorEmail = "tutor1@otsp.local";
            const string studentEmail = "hocvien1@otsp.local";
            const string adminAvatar = "https://randomuser.me/api/portraits/men/99.jpg";
            const string tutorAvatar = "https://randomuser.me/api/portraits/men/98.jpg";
            const string studentAvatar = "https://randomuser.me/api/portraits/women/98.jpg";
            var coreEmails = new[] { adminEmail, tutorEmail, studentEmail };

            var existingCoreUsers = await userManager.Users
                .Where(u => u.Email != null && coreEmails.Contains(u.Email))
                .ToDictionaryAsync(u => u.Email!, StringComparer.OrdinalIgnoreCase);

            // 4.1. Admin mặc định
            if (!existingCoreUsers.TryGetValue(adminEmail, out var admin))
            {
                admin = new ApplicationUser
                {
                    UserName = adminEmail,
                    Email = adminEmail,
                    EmailConfirmed = true,
                    FullName = "Quản trị viên",
                    AvatarUrl = adminAvatar
                };
                var kq = await userManager.CreateAsync(admin, "Admin@12345");
                if (kq.Succeeded)
                {
                    await userManager.AddToRoleAsync(admin, "Admin");
                    db.AdminProfiles.Add(new AdminProfile { UserId = admin.Id, Rank = "Quản trị cấp cao" });
                    await db.SaveChangesAsync();
                    logger.LogInformation("[DbSeeder] Đã khởi tạo tài khoản Admin mặc định: {Email}", adminEmail);
                }
            }
            else if (admin.AvatarUrl != adminAvatar)
            {
                admin.AvatarUrl = adminAvatar;
                await userManager.UpdateAsync(admin);
            }

            // 4.2. Gia sư mẫu mặc định (Toán THCS)
            if (!existingCoreUsers.TryGetValue(tutorEmail, out var tutorUser))
            {
                tutorUser = new ApplicationUser
                {
                    UserName = tutorEmail,
                    Email = tutorEmail,
                    EmailConfirmed = true,
                    FullName = "Nguyễn Văn Gia Sư",
                    AvatarUrl = tutorAvatar,
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

                    var toan = await db.Subjects.FirstAsync(s => s.Code == "TOAN");
                    var thcs = await db.GradeLevels.FirstAsync(g => g.Code == "THCS");
                    db.TutorSubjects.Add(new TutorSubject
                    {
                        TutorId = tutor.Id,
                        SubjectId = toan.Id,
                        GradeLevelId = thcs.Id
                    });
                    await db.SaveChangesAsync();
                    logger.LogInformation("[DbSeeder] Đã khởi tạo tài khoản Gia sư mặc định: {Email}", tutorEmail);
                }
            }
            else if (tutorUser.AvatarUrl != tutorAvatar)
            {
                tutorUser.AvatarUrl = tutorAvatar;
                await userManager.UpdateAsync(tutorUser);
            }

            // 4.3. Học viên mẫu mặc định
            if (!existingCoreUsers.TryGetValue(studentEmail, out var studentUser))
            {
                studentUser = new ApplicationUser
                {
                    UserName = studentEmail,
                    Email = studentEmail,
                    EmailConfirmed = true,
                    FullName = "Trần Thị Học Viên",
                    AvatarUrl = studentAvatar,
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
                    logger.LogInformation("[DbSeeder] Đã khởi tạo tài khoản Học viên mặc định: {Email}", studentEmail);
                }
            }
            else if (studentUser.AvatarUrl != studentAvatar)
            {
                studentUser.AvatarUrl = studentAvatar;
                await userManager.UpdateAsync(studentUser);
            }
        }
    }