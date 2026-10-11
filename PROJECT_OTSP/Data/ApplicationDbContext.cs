using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Models;

namespace PROJECT_OTSP.Data
{
    // Cầu nối giữa code và database. Kế thừa IdentityDbContext để có sẵn bảng tài khoản, vai trò.
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, string>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        // ===== Mỗi DbSet tương ứng một bảng =====
        // Không khai báo ApplicationUser và ApplicationRole vì Identity đã có sẵn (Users, Roles)
        public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
        public DbSet<Notification> Notifications => Set<Notification>();
        public DbSet<Messager> Messagers => Set<Messager>();
        public DbSet<Complaint> Complaints => Set<Complaint>();
        public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
        public DbSet<Guardian> Guardians => Set<Guardian>();
        public DbSet<TutorProfile> TutorProfiles => Set<TutorProfile>();
        public DbSet<AdminProfile> AdminProfiles => Set<AdminProfile>();
        public DbSet<Qualification> Qualifications => Set<Qualification>();
        public DbSet<SubjectLevel> SubjectLevels => Set<SubjectLevel>();
        public DbSet<TutorSubjectLevel> TutorSubjectLevels => Set<TutorSubjectLevel>();
        public DbSet<ProfileVerification> ProfileVerifications => Set<ProfileVerification>();
        public DbSet<ConnectionRequest> ConnectionRequests => Set<ConnectionRequest>();
        public DbSet<TrialSchedule> TrialSchedules => Set<TrialSchedule>();
        public DbSet<Classroom> Classrooms => Set<Classroom>();
        public DbSet<ClassroomStudent> ClassroomStudents => Set<ClassroomStudent>();
        public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
        public DbSet<Review> Reviews => Set<Review>();
        public DbSet<ReviewAlert> ReviewAlerts => Set<ReviewAlert>();
        public DbSet<Payment> Payments => Set<Payment>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<Reconciliation> Reconciliations => Set<Reconciliation>();
        public DbSet<LearningDocument> LearningDocuments => Set<LearningDocument>();
        public DbSet<AiStudySession> AiStudySessions => Set<AiStudySession>();
        public DbSet<NewsArticle> NewsArticles => Set<NewsArticle>();
        public DbSet<PartnerCenter> PartnerCenters => Set<PartnerCenter>();
        public DbSet<GroupClass> GroupClasses => Set<GroupClass>();
        public DbSet<StatisticReport> StatisticReports => Set<StatisticReport>();

        protected override void OnModelCreating(ModelBuilder b)
        {
            // Bắt buộc: để Identity tạo bảng tài khoản, vai trò, claims...
            base.OnModelCreating(b);

            // ===== 1. Đổi tên bảng Identity cho khớp tài liệu thiết kế DB =====
            b.Entity<ApplicationUser>().ToTable("Users");
            b.Entity<ApplicationRole>().ToTable("Roles");
            b.Entity<IdentityUserRole<string>>().ToTable("UserRoles");

            // ===== 2. Khóa chính ghép cho bảng trung gian (không có cột Id riêng) =====
            b.Entity<TutorSubjectLevel>().HasKey(x => new { x.TutorId, x.SubjectLevelId });
            b.Entity<ClassroomStudent>().HasKey(x => new { x.ClassroomId, x.StudentId });

            // ===== 3. Quan hệ 1-1 với tài khoản: UserId không được trùng =====
            b.Entity<StudentProfile>().HasIndex(x => x.UserId).IsUnique();
            b.Entity<TutorProfile>().HasIndex(x => x.UserId).IsUnique();
            b.Entity<AdminProfile>().HasIndex(x => x.UserId).IsUnique();

            // ===== 4. Chống ghi nhận tiền hai lần =====
            // Mã giao dịch cổng thanh toán là duy nhất, callback gọi lại không tạo bản ghi mới
            b.Entity<Payment>().HasIndex(x => x.GatewayTransactionCode).IsUnique();
            // Mỗi giao dịch chỉ có một hóa đơn (quan hệ 1-1)
            b.Entity<Invoice>().HasIndex(x => x.PaymentId).IsUnique();

            // ===== 5. Message: hai khóa ngoại cùng trỏ tới ApplicationUser =====
            b.Entity<Messager>()
                .HasOne(m => m.Sender).WithMany(u => u.SentMessagers)
                .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Messager>()
                .HasOne(m => m.Receiver).WithMany(u => u.ReceivedMessagers)
                .HasForeignKey(m => m.ReceiverId).OnDelete(DeleteBehavior.Restrict);

            // ===== 6. Complaint: tương tự Message =====
            b.Entity<Complaint>()
                .HasOne(c => c.Sender).WithMany(u => u.SentComplaints)
                .HasForeignKey(c => c.SenderId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Complaint>()
                .HasOne(c => c.TargetUser).WithMany(u => u.ReceivedComplaints)
                .HasForeignKey(c => c.TargetUserId).OnDelete(DeleteBehavior.Restrict);

            // ===== 7. Không xóa dây chuyền dữ liệu tiền và đánh giá =====
            b.Entity<Payment>()
                .HasOne(x => x.Classroom).WithMany(c => c.Payments)
                .HasForeignKey(x => x.ClassroomId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Review>()
                .HasOne(x => x.Student).WithMany(s => s.Reviews)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);

            // ===== 8. Yêu cầu kết nối: không xóa dây chuyền khi xóa lớp hoặc môn =====
            b.Entity<ConnectionRequest>()
                .HasOne(x => x.Classroom).WithMany(c => c.ConnectionRequests)
                .HasForeignKey(x => x.ClassroomId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<ConnectionRequest>()
                .HasOne(x => x.SubjectLevel).WithMany(s => s.ConnectionRequests)
                .HasForeignKey(x => x.SubjectLevelId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}