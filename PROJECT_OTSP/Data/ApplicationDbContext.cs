<<<<<<< HEAD
using Microsoft.AspNetCore.Identity;
=======
<<<<<<< HEAD
﻿using Microsoft.AspNetCore.Identity;
=======
using Microsoft.AspNetCore.Identity;
>>>>>>> eb30c869e0525d8bf651bfa9c0aee3191384d225
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
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
<<<<<<< HEAD
        public DbSet<TutorAvailability> TutorAvailabilities => Set<TutorAvailability>();
        public DbSet<AdminProfile> AdminProfiles => Set<AdminProfile>();
        public DbSet<Qualification> Qualifications => Set<Qualification>();
=======
<<<<<<< HEAD
        public DbSet<AdminProfile> AdminProfiles => Set<AdminProfile>();
        public DbSet<Qualification> Qualifications => Set<Qualification>();
        public DbSet<SubjectLevel> SubjectLevels => Set<SubjectLevel>();
        public DbSet<TutorSubjectLevel> TutorSubjectLevels => Set<TutorSubjectLevel>();
=======
        public DbSet<TutorAvailability> TutorAvailabilities => Set<TutorAvailability>();
        public DbSet<AdminProfile> AdminProfiles => Set<AdminProfile>();
        public DbSet<Qualification> Qualifications => Set<Qualification>();
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
        public DbSet<Subject> Subjects => Set<Subject>();
        public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
        public DbSet<TutorSubject> TutorSubjects => Set<TutorSubject>();

<<<<<<< HEAD
=======
>>>>>>> eb30c869e0525d8bf651bfa9c0aee3191384d225
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
        public DbSet<ProfileVerification> ProfileVerifications => Set<ProfileVerification>();
        public DbSet<ConnectionRequest> ConnectionRequests => Set<ConnectionRequest>();
        public DbSet<TrialSchedule> TrialSchedules => Set<TrialSchedule>();
        public DbSet<Classroom> Classrooms => Set<Classroom>();
<<<<<<< HEAD
        public DbSet<ClassroomStudent> ClassroomStudents => Set<ClassroomStudent>();
        public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
<<<<<<< HEAD
        public DbSet<ClassSessionAttendance> ClassSessionAttendances => Set<ClassSessionAttendance>();
=======
=======
        public DbSet<ClassroomWeeklySchedule> ClassroomWeeklySchedules => Set<ClassroomWeeklySchedule>();
        public DbSet<ClassroomStudent> ClassroomStudents => Set<ClassroomStudent>();
        public DbSet<ClassSession> ClassSessions => Set<ClassSession>();
        public DbSet<ClassSessionAttendance> ClassSessionAttendances => Set<ClassSessionAttendance>();
>>>>>>> eb30c869e0525d8bf651bfa9c0aee3191384d225
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
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
<<<<<<< HEAD
=======
<<<<<<< HEAD
            b.Entity<TutorSubjectLevel>().HasKey(x => new { x.TutorId, x.SubjectLevelId });
=======
>>>>>>> eb30c869e0525d8bf651bfa9c0aee3191384d225
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
            b.Entity<ClassroomStudent>().HasKey(x => new { x.ClassroomId, x.StudentId });

            // ===== 3. Quan hệ 1-1 với tài khoản: UserId không được trùng =====
            b.Entity<StudentProfile>().HasIndex(x => x.UserId).IsUnique();
            b.Entity<TutorProfile>().HasIndex(x => x.UserId).IsUnique();
            b.Entity<AdminProfile>().HasIndex(x => x.UserId).IsUnique();
<<<<<<< HEAD
            b.Entity<TutorProfile>()
                .Property(x => x.TeachingFormat)
                .HasDefaultValue(Enums.TeachingFormat.Hybrid)
                .HasSentinel(Enums.TeachingFormat.Hybrid);
=======
<<<<<<< HEAD
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc

            // Gia sư không lưu trùng cùng một khung giờ trong tuần
            b.Entity<TutorAvailability>()
                .HasIndex(x => new { x.TutorId, x.DayOfWeek, x.StartTime, x.EndTime })
                .IsUnique();

            b.Entity<ClassSessionAttendance>()
                .HasIndex(x => new { x.ClassSessionId, x.UserId })
                .IsUnique();
            b.Entity<ClassSessionAttendance>()
                .Property(x => x.JoinCount)
                .HasDefaultValue(1);
            b.Entity<ClassSessionAttendance>()
                .HasOne(x => x.ClassSession).WithMany(x => x.Attendances)
                .HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<ClassSessionAttendance>()
                .HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

            b.Entity<TrialSchedule>()
                .HasIndex(x => x.ClassSessionId)
                .IsUnique();
            b.Entity<TrialSchedule>()
                .HasOne(x => x.ClassSession).WithOne()
                .HasForeignKey<TrialSchedule>(x => x.ClassSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== 4. Gia sư không dạy trùng môn-cấp =====
            b.Entity<TutorSubject>().HasIndex(x => new { x.TutorId, x.SubjectId, x.GradeLevelId }).IsUnique();

            b.Entity<Classroom>()
                .HasIndex(x => new { x.TutorId, x.Type, x.Status })
                .HasDatabaseName("IX_Classrooms_TutorId_Type_Status");

            // PostgreSQL system column: concurrency token shadow property, không thêm cột vào Model.
            b.Entity<Classroom>()
                .Property<uint>("xmin")
                .HasColumnName("xmin")
                .IsRowVersion();

            // ===== 5. Chống ghi nhận tiền hai lần =====
            b.Entity<Payment>().HasIndex(x => x.GatewayTransactionCode).IsUnique();
            b.Entity<Invoice>().HasIndex(x => x.PaymentId).IsUnique();

<<<<<<< HEAD
            // ===== 6. Messager: hai khóa ngoại cùng trỏ tới ApplicationUser =====
=======
            // ===== 5. Message: hai khóa ngoại cùng trỏ tới ApplicationUser =====
=======
            b.Entity<TutorProfile>()
                .Property(x => x.TeachingFormat)
                .HasDefaultValue(Enums.TeachingFormat.Hybrid)
                .HasSentinel(Enums.TeachingFormat.Hybrid);

            // Gia sư không lưu trùng cùng một khung giờ trong tuần
            b.Entity<TutorAvailability>()
                .HasIndex(x => new { x.TutorId, x.DayOfWeek, x.StartTime, x.EndTime })
                .IsUnique();

            b.Entity<ClassSessionAttendance>()
                .HasIndex(x => new { x.ClassSessionId, x.UserId })
                .IsUnique();
            b.Entity<ClassSessionAttendance>()
                .Property(x => x.JoinCount)
                .HasDefaultValue(1);
            b.Entity<ClassSessionAttendance>()
                .HasOne(x => x.ClassSession).WithMany(x => x.Attendances)
                .HasForeignKey(x => x.ClassSessionId).OnDelete(DeleteBehavior.Cascade);
            b.Entity<ClassSessionAttendance>()
                .HasOne(x => x.User).WithMany()
                .HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);

            b.Entity<TrialSchedule>()
                .HasIndex(x => x.ClassSessionId)
                .IsUnique();
            b.Entity<TrialSchedule>()
                .HasOne(x => x.ClassSession).WithOne()
                .HasForeignKey<TrialSchedule>(x => x.ClassSessionId)
                .OnDelete(DeleteBehavior.Restrict);

            // ===== 4. Gia sư không dạy trùng môn-cấp =====
            b.Entity<TutorSubject>().HasIndex(x => new { x.TutorId, x.SubjectId, x.GradeLevelId }).IsUnique();

            b.Entity<Classroom>()
                .HasIndex(x => new { x.TutorId, x.Type, x.Status })
                .HasDatabaseName("IX_Classrooms_TutorId_Type_Status");

            // PostgreSQL system column: concurrency token shadow property, không thêm cột vào Model.
            b.Entity<Classroom>()
                .Property<uint>("xmin")
                .HasColumnName("xmin")
                .IsRowVersion();

            b.Entity<ClassroomWeeklySchedule>()
                .HasIndex(x => new { x.ClassroomId, x.DayOfWeek, x.StartTime });
            b.Entity<ClassroomWeeklySchedule>()
                .HasOne(x => x.Classroom)
                .WithMany(c => c.WeeklySchedules)
                .HasForeignKey(x => x.ClassroomId)
                .OnDelete(DeleteBehavior.Cascade);

            // ===== 5. Chống ghi nhận tiền hai lần =====
            b.Entity<Payment>().HasIndex(x => x.GatewayTransactionCode).IsUnique();
            b.Entity<Invoice>().HasIndex(x => x.PaymentId).IsUnique();

            // ===== 6. Messager: hai khóa ngoại cùng trỏ tới ApplicationUser =====
>>>>>>> eb30c869e0525d8bf651bfa9c0aee3191384d225
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
            b.Entity<Messager>()
                .HasOne(m => m.Sender).WithMany(u => u.SentMessagers)
                .HasForeignKey(m => m.SenderId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Messager>()
                .HasOne(m => m.Receiver).WithMany(u => u.ReceivedMessagers)
                .HasForeignKey(m => m.ReceiverId).OnDelete(DeleteBehavior.Restrict);

<<<<<<< HEAD
            // ===== 7. Complaint: tương tự Message =====
=======
<<<<<<< HEAD
            // ===== 6. Complaint: tương tự Message =====
=======
            // ===== 7. Complaint: tương tự Message =====
>>>>>>> eb30c869e0525d8bf651bfa9c0aee3191384d225
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
            b.Entity<Complaint>()
                .HasOne(c => c.Sender).WithMany(u => u.SentComplaints)
                .HasForeignKey(c => c.SenderId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Complaint>()
                .HasOne(c => c.TargetUser).WithMany(u => u.ReceivedComplaints)
                .HasForeignKey(c => c.TargetUserId).OnDelete(DeleteBehavior.Restrict);

<<<<<<< HEAD
            // ===== 8. Không xóa dây chuyền dữ liệu tiền và đánh giá =====
=======
<<<<<<< HEAD
            // ===== 7. Không xóa dây chuyền dữ liệu tiền và đánh giá =====
=======
            // ===== 8. Không xóa dây chuyền dữ liệu tiền và đánh giá =====
>>>>>>> eb30c869e0525d8bf651bfa9c0aee3191384d225
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
            b.Entity<Payment>()
                .HasOne(x => x.Classroom).WithMany(c => c.Payments)
                .HasForeignKey(x => x.ClassroomId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Review>()
                .HasOne(x => x.Student).WithMany(s => s.Reviews)
                .HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);

<<<<<<< HEAD
            // ===== 9. Không xóa dây chuyền khi xóa môn hoặc cấp học =====
            b.Entity<TutorSubject>()
                .HasOne(x => x.Subject).WithMany(s => s.TutorSubjects)
                .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<TutorSubject>()
                .HasOne(x => x.GradeLevel).WithMany(g => g.TutorSubjects)
                .HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);

            b.Entity<ConnectionRequest>()
                .HasOne(x => x.Subject).WithMany(s => s.ConnectionRequests)
                .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<ConnectionRequest>()
                .HasOne(x => x.GradeLevel).WithMany(g => g.ConnectionRequests)
                .HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);
=======
<<<<<<< HEAD
            // ===== 8. Yêu cầu kết nối: không xóa dây chuyền khi xóa lớp hoặc môn =====
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
            b.Entity<ConnectionRequest>()
                .HasOne(x => x.Classroom).WithMany(c => c.ConnectionRequests)
                .HasForeignKey(x => x.ClassroomId).OnDelete(DeleteBehavior.Restrict);

            b.Entity<LearningDocument>()
                .HasOne(x => x.Subject).WithMany(s => s.LearningDocuments)
                .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<LearningDocument>()
                .HasOne(x => x.GradeLevel).WithMany(g => g.LearningDocuments)
                .HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);

            b.Entity<Classroom>()
                .HasOne(x => x.Subject).WithMany(s => s.Classrooms)
                .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Classroom>()
                .HasOne(x => x.GradeLevel).WithMany(g => g.Classrooms)
                .HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);
        }
    }
<<<<<<< HEAD

}
=======
}
=======
            // ===== 9. Không xóa dây chuyền khi xóa môn hoặc cấp học =====
            b.Entity<TutorSubject>()
                .HasOne(x => x.Subject).WithMany(s => s.TutorSubjects)
                .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<TutorSubject>()
                .HasOne(x => x.GradeLevel).WithMany(g => g.TutorSubjects)
                .HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);

            b.Entity<ConnectionRequest>()
                .HasOne(x => x.Subject).WithMany(s => s.ConnectionRequests)
                .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<ConnectionRequest>()
                .HasOne(x => x.GradeLevel).WithMany(g => g.ConnectionRequests)
                .HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<ConnectionRequest>()
                .HasOne(x => x.Classroom).WithMany(c => c.ConnectionRequests)
                .HasForeignKey(x => x.ClassroomId).OnDelete(DeleteBehavior.Restrict);

            b.Entity<LearningDocument>()
                .HasOne(x => x.Subject).WithMany(s => s.LearningDocuments)
                .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<LearningDocument>()
                .HasOne(x => x.GradeLevel).WithMany(g => g.LearningDocuments)
                .HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);

            b.Entity<Classroom>()
                .HasOne(x => x.Subject).WithMany(s => s.Classrooms)
                .HasForeignKey(x => x.SubjectId).OnDelete(DeleteBehavior.Restrict);
            b.Entity<Classroom>()
                .HasOne(x => x.GradeLevel).WithMany(g => g.Classrooms)
                .HasForeignKey(x => x.GradeLevelId).OnDelete(DeleteBehavior.Restrict);
        }
    }

}
>>>>>>> eb30c869e0525d8bf651bfa9c0aee3191384d225
>>>>>>> 9910af6bd9154ada3623a89ce0eeb13217dcc6cc
