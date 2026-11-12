using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Backend_Project_OTSP.Models;
// using Backend_Project_OTSP.Services;
using static Backend_Project_OTSP.Models.Enums;
namespace Backend_Project_OTSP.Data
{
    public class DbSeeder
    {
        public static async Task SeedAsync(IServiceProvider services, bool? forceBulk = null)
        {
            await SeedBootstrapAsync(services, syncDefaultAdminPasswords: true);
        }

        /// <summary>
        /// Khởi tạo tối thiểu dữ liệu gốc (Roles, Danh mục cơ bản, Admin mặc định nếu chưa có Admin nào, Tin tức mẫu khi rỗng).
        /// An toàn khi chạy ở mọi môi trường sau khi reset DB mà không ghi đè dữ liệu hoặc mật khẩu hiện có trên Production.
        /// </summary>
        public static async Task SeedBootstrapAsync(IServiceProvider services, bool syncDefaultAdminPasswords = false)
        {
            var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");
            var db = services.GetRequiredService<ApplicationDbContext>();
            var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
            var roleManager = services.GetRequiredService<RoleManager<ApplicationRole>>();

            await SeedCoreDataAsync(db, userManager, roleManager, logger, syncDefaultAdminPasswords);
            await SeedDefaultNewsArticlesAsync(db, logger, onlyWhenEmpty: !syncDefaultAdminPasswords);
        }
        private static async Task SeedCoreDataAsync(ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<ApplicationRole> roleManager,
            ILogger logger,
            bool syncDefaultAdminPasswords = true)
        {
            //1 lấy danh sách role
            var roleNames = new[] { "Admin", "Tutor", "Student_Parent" };
            var existingRoleNames = await roleManager.Roles
            .Where(r => r.Name != null && roleNames.Contains(r.Name))
            .Select(r => r.Name!)
            .ToListAsync();

            foreach (var ten in roleNames.Except(existingRoleNames))
            {
                await roleManager.CreateAsync(new ApplicationRole { Name = ten });
            }
            // 2. Danh mục môn học cơ bản (Production chỉ seed khi bảng rỗng để không hồi sinh mục Admin đã xóa)
            var monHoc = new[]
            {
                (Code: "TOAN", Name: "Toán"),
                (Code: "TIENGANH", Name: "Tiếng Anh")
            };
            if (syncDefaultAdminPasswords || !await db.Subjects.AnyAsync())
            {
                var subjectCodes = monHoc.Select(m => m.Code).ToArray();
                var existingSubjectCodes = await db.Subjects
                    .Where(s => subjectCodes.Contains(s.Code))
                    .Select(s => s.Code)
                    .ToListAsync();
                foreach (var m in monHoc.Where(m => !existingSubjectCodes.Contains(m.Code)))
                {
                    db.Subjects.Add(new Subject { Code = m.Code, Name = m.Name });
                }
            }
            // 3. Danh mục cấp học cơ bản
            var capHoc = new[]
            {
                (Code: "TH", Name: "Tiểu học"),
                (Code: "THCS", Name: "Trung học cơ sở"),
                (Code: "THPT", Name: "Trung học phổ thông")
            };
            if (syncDefaultAdminPasswords || !await db.GradeLevels.AnyAsync())
            {
                var gradeLevelCodes = capHoc.Select(c => c.Code).ToArray();
                var existingGradeLevelCodes = await db.GradeLevels
                    .Where(g => gradeLevelCodes.Contains(g.Code))
                    .Select(g => g.Code)
                    .ToListAsync();
                foreach (var c in capHoc.Where(c => !existingGradeLevelCodes.Contains(c.Code)))
                {
                    db.GradeLevels.Add(new GradeLevel { Code = c.Code, Name = c.Name });
                }
            }
            await db.SaveChangesAsync();

            if (!syncDefaultAdminPasswords && (await userManager.GetUsersInRoleAsync("Admin")).Count > 0)
            {
                return;
            }
            var adminAccounts = new[]
            {
                new { Email = "admin@otsp.com", Password = "Ngoctuan123@", FullName = "Quản trị viên OTSP", Rank = "Quản trị cấp cao" },
                new { Email = "admin@otsp.local", Password = "Admin@12345", FullName = "Quản trị viên hệ thống", Rank = "Quản trị cấp cao" }
            };

            const string tutorEmail = "tutor1@otsp.local";
            const string studentEmail = "hocvien1@otsp.local";
            const string adminAvatar = "https://res.cloudinary.com/dodglt55b/image/upload/v1793847115/otsp/avatars/b72bzktmq1rs8trlfvlf.jpg";
 
            var coreEmails = adminAccounts.Select(a => a.Email).Concat(new[] { tutorEmail, studentEmail }).ToArray();

            var existingCoreUsers = await userManager.Users
                .Where(u => u.Email != null && coreEmails.Contains(u.Email))
                .ToDictionaryAsync(u => u.Email!, StringComparer.OrdinalIgnoreCase);
            
            // 4.1. Admin mặc định & các tài khoản quản trị viên
            foreach (var adm in adminAccounts)
            {
                if (!existingCoreUsers.TryGetValue(adm.Email, out var admin))
                {
                    admin = new ApplicationUser
                    {
                        UserName = adm.Email,
                        Email = adm.Email,
                        EmailConfirmed = true,
                        FullName = adm.FullName,
                        AvatarUrl = adminAvatar
                    };
                    var kq = await userManager.CreateAsync(admin, adm.Password);
                    if (kq.Succeeded)
                    {
                        await userManager.AddToRoleAsync(admin, "Admin");
                        db.AdminProfiles.Add(new AdminProfile { UserId = admin.Id, Rank = adm.Rank });
                        await db.SaveChangesAsync();
                        logger.LogInformation("[DbSeeder] Đã khởi tạo tài khoản Admin: {Email}", adm.Email);
                    }
                    else
                    {
                        logger.LogError("[DbSeeder] Không thể tạo tài khoản Admin {Email}: {Errors}", adm.Email,
                            string.Join("; ", kq.Errors.Select(error => error.Description)));
                    }
                }
                else
                {
                    if (string.IsNullOrWhiteSpace(admin.AvatarUrl))
                    {
                        admin.AvatarUrl = adminAvatar;
                        await userManager.UpdateAsync(admin);
                    }

                    if (!await userManager.IsInRoleAsync(admin, "Admin"))
                        await userManager.AddToRoleAsync(admin, "Admin");

                    if (syncDefaultAdminPasswords && !await userManager.CheckPasswordAsync(admin, adm.Password))
                    {
                        var resetToken = await userManager.GeneratePasswordResetTokenAsync(admin);
                        var resetResult = await userManager.ResetPasswordAsync(admin, resetToken, adm.Password);
                        if (resetResult.Succeeded)
                            logger.LogInformation("[DbSeeder] Đã đồng bộ mật khẩu bootstrap cho tài khoản Admin {Email}", adm.Email);
                        else
                            logger.LogError("[DbSeeder] Không thể đồng bộ mật khẩu Admin {Email}: {Errors}", adm.Email,
                                string.Join("; ", resetResult.Errors.Select(error => error.Description)));
                    }

                    if (!await db.AdminProfiles.AnyAsync(profile => profile.UserId == admin.Id))
                    {
                        db.AdminProfiles.Add(new AdminProfile { UserId = admin.Id, Rank = adm.Rank });
                        await db.SaveChangesAsync();
                    }
                }
            }

        }
        private static async Task SeedDefaultNewsArticlesAsync(ApplicationDbContext db, ILogger logger, bool onlyWhenEmpty = false)
        {
            if (onlyWhenEmpty && await db.NewsArticles.AnyAsync()) return;
            var admin = await db.AdminProfiles.FirstOrDefaultAsync();
            if (admin == null) return;

            var existingTitles = await db.NewsArticles
                .Select(n => n.Title)
                .ToListAsync();

            var defaultArticles = new (string Category, string Title, string Content, DateTime PublishedAt)[]
            {
                (
                    "Tuyển sinh",
                    "Hà Nội công bố lịch thi tuyển sinh vào lớp 10 năm học 2027–2028",
                    "Sở Giáo dục và Đào tạo vừa công bố kế hoạch tuyển sinh vào lớp 10 THPT công lập năm học 2027–2028, thí sinh và phụ huynh cần lưu ý các mốc đăng ký nguyện vọng và cấu trúc đề thi đổi mới.\n\n" +
                    "1. Các mốc thời gian quan trọng cần ghi nhớ:\n" +
                    "- Từ đầu tháng 4: Học sinh lớp 9 nộp phiếu đăng ký dự tuyển tại trường THCS đang theo học.\n" +
                    "- Giữa tháng 5: Công bố tỷ lệ chọi của từng trường THPT để học sinh cân nhắc điều chỉnh nguyện vọng (nếu nằm trong diện được phép).\n" +
                    "- Tuần đầu tháng 6: Tổ chức kỳ thi chính thức với 3 môn Toán, Ngữ văn và Ngoại ngữ.\n\n" +
                    "2. Cấu trúc đề thi theo Chương trình Giáo dục phổ thông 2018:\n" +
                    "Đề thi năm nay tập trung đánh giá năng lực vận dụng thực tiễn, giảm các câu hỏi học thuộc lòng máy móc. Môn Toán tăng cường các bài toán thực tế và hình học không gian; môn Tiếng Anh kiểm tra toàn diện ngữ âm, từ vựng và kỹ năng đọc hiểu học thuật.\n\n" +
                    "3. Lời khuyên từ đội ngũ gia sư OTSP:\n" +
                    "Phụ huynh nên cùng con xây dựng lộ trình ôn tập từ sớm, kết hợp học bổ trợ 1-1 hoặc nhóm nhỏ (3-5 học viên) để lấp lỗ hổng kiến thức nền tảng và rèn tâm lý phòng thi vững vàng.",
                    new DateTime(2026, 9, 28, 8, 0, 0, DateTimeKind.Utc)
                ),
                (
                    "Kinh nghiệm chọn gia sư",
                    "Mẹo chọn gia sư phù hợp cho con lớp 9",
                    "Giai đoạn lớp 9 là bước ngoặt quan trọng chuẩn bị cho kỳ thi chuyển cấp. Việc chọn đúng gia sư đồng hành không chỉ giúp học sinh cải thiện điểm số mà còn tạo hứng thú học tập lâu dài.\n\n" +
                    "1. Xác định rõ mục tiêu học tập của con:\n" +
                    "Trước khi tìm gia sư, phụ huynh cần trao đổi thẳng thắn với con để biết con đang mất gốc kiến thức cơ bản, cần hệ thống hóa chuyên đề hay muốn luyện đề nâng cao vào trường chuyên, lớp chọn.\n\n" +
                    "2. Kiểm tra hồ sơ và văn bằng minh bạch:\n" +
                    "Tại nền tảng OTSP, 100% gia sư đều được đối soát CCCD, thẻ sinh viên/bằng tốt nghiệp và chứng chỉ chuyên môn (IELTS, giải thưởng học sinh giỏi) trước khi mở lớp.\n\n" +
                    "3. Tận dụng buổi học thử đánh giá mức độ tương tác:\n" +
                    "Một gia sư giỏi chuyên môn cần có phương pháp truyền đạt phù hợp với tính cách của học sinh. Qua buổi học thử trực tuyến trên OTSP, phụ huynh có thể quan sát cách gia sư đặt câu hỏi gợi mở và lộ trình cá nhân hóa cho từng tuần.",
                    new DateTime(2026, 9, 27, 8, 0, 0, DateTimeKind.Utc)
                ),
                (
                    "Phương pháp học",
                    "5 phương pháp ôn thi hiệu quả trong 3 tháng cuối",
                    "Ba tháng nước rút trước kỳ thi là giai đoạn quyết định để bứt phá điểm số. Thay vì học dồn ép gây quá tải, học sinh có thể áp dụng 5 phương pháp khoa học sau đây:\n\n" +
                    "1. Phương pháp Active Recall (Chủ động gợi nhớ):\n" +
                    "Sau khi học xong mỗi chuyên đề, hãy gấp sách lại và tự tóm tắt các công thức, định lý hoặc sơ đồ tư duy ra giấy trắng thay vì chỉ đọc lại thụ động.\n\n" +
                    "2. Kỹ thuật Spaced Repetition (Lặp lại ngắt quãng):\n" +
                    "Ôn lại kiến thức theo chu kỳ 1 ngày - 3 ngày - 7 ngày - 14 ngày để chuyển thông tin từ trí nhớ ngắn hạn sang trí nhớ dài hạn.\n\n" +
                    "3. Chia nhỏ thời gian theo Pomodoro (50 phút học - 10 phút nghỉ):\n" +
                    "Giúp não bộ duy trì sự tập trung cao độ mà không bị kiệt sức khi giải các đề thi tổng hợp.\n\n" +
                    "4. Sổ tay lỗi sai (Mistake Notebook):\n" +
                    "Ghi chép lại toàn bộ các câu làm sai khi luyện đề, phân tích nguyên nhân sai (do nhầm công thức, đọc thiếu dữ kiện hay tính toán ẩu) để không lặp lại trong phòng thi.\n\n" +
                    "5. Luyện đề bấm giờ thực chiến cùng gia sư:\n" +
                    "Mỗi tuần thực hiện 1-2 đề thi chuẩn cấu trúc dưới sự bấm giờ và chấm chữa chi tiết của gia sư OTSP để rèn kỹ năng phân bổ thời gian.",
                    new DateTime(2026, 9, 25, 8, 0, 0, DateTimeKind.Utc)
                ),
                (
                    "Xu hướng giáo dục",
                    "Học online hay offline: đâu là lựa chọn tối ưu?",
                    "Sự phát triển của công nghệ phòng học trực tuyến tương tác đã làm thay đổi hoàn toàn cách tiếp cận việc học kèm. Vậy học viên nên chọn học online 1-1 hay học trực tiếp truyền thống?\n\n" +
                    "1. Ưu điểm vượt trội của học trực tuyến trên OTSP:\n" +
                    "- Tiết kiệm 100% thời gian di chuyển, đặc biệt an toàn vào buổi tối hoặc mùa mưa bão.\n" +
                    "- Tiếp cận đội ngũ gia sư giỏi trên toàn quốc thay vì bị giới hạn trong phạm vi vài km quanh nhà.\n" +
                    "- Tích hợp phòng học Video HD (Jitsi Meet), chia sẻ màn hình, bảng trắng điện tử và kho tài liệu PDF lưu trữ trọn đời.\n\n" +
                    "2. Khi nào nên kết hợp học nhóm nhỏ?\n" +
                    "Các lớp nhóm từ 3-5 học viên trên OTSP giúp tăng tính thi đua, thảo luận sôi nổi với mức học phí tiết kiệm hơn 40% so với học kèm riêng nhưng vẫn đảm bảo gia sư theo sát từng em.",
                    new DateTime(2026, 9, 22, 8, 0, 0, DateTimeKind.Utc)
                ),
                (
                    "Thông báo OTSP",
                    "OTSP cập nhật tính năng kết nối gia sư theo lịch rảnh và phòng học trực tuyến",
                    "Nhằm mang lại trải nghiệm học tập thuận tiện và minh bạch nhất cho phụ huynh, học viên và gia sư, đội ngũ phát triển OTSP chính thức cập nhật loạt tính năng mới trên hệ thống:\n\n" +
                    "1. Ma trận lịch rảnh thông minh theo tuần:\n" +
                    "Học viên có thể xem trực tiếp các khung giờ trống của gia sư (Sáng - Chiều - Tối từ Thứ 2 đến Chủ Nhật) để đặt lịch học thử hoặc đăng ký lớp chỉ với vài thao tác.\n\n" +
                    "2. Phòng học Video tích hợp kiểm soát quyền riêng tư:\n" +
                    "Khi tham gia buổi học, hệ thống hỗ trợ màn hình kiểm tra thiết bị (Pre-join) và mặc định tắt Camera/Micro cho đến khi người học chủ động bật, đồng thời chống mở trùng tab gây vang âm thanh.\n\n" +
                    "3. Đối soát học phí tự động qua VietQR / SePay:\n" +
                    "Mọi giao dịch thanh toán học phí đều được xác nhận tức thì 24/7 và thống kê minh bạch trong bảng điều khiển của cả học viên lẫn gia sư.",
                    new DateTime(2026, 9, 20, 8, 0, 0, DateTimeKind.Utc)
                )
            };

             int added = 0;
            foreach (var item in defaultArticles)
            {
                if (!existingTitles.Any(t => string.Equals(t, item.Title, StringComparison.OrdinalIgnoreCase)))
                {
                    db.NewsArticles.Add(new NewsArticle
                    {
                        AdminId = admin.Id,
                        Category = item.Category,
                        Title = item.Title,
                        Content = item.Content,
                        PublishedAt = item.PublishedAt
                    });
                    added++;
                }
            }

            if (added > 0)
            {
                await db.SaveChangesAsync();
                logger.LogInformation("[DbSeeder] Đã khởi tạo bổ sung {Count} bài viết tin tức mẫu.", added);
            }
        }
    }
}