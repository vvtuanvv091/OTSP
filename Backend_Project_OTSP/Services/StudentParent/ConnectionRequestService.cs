using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.StudentParent;
using Backend_Project_OTSP.Services.Tutor;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;

namespace Backend_Project_OTSP.Services;

public class ConnectionRequestService : IConnectionRequestService
{
    private const int VietnamUtcOffsetHours = 7;
    private const string OneToOneUniqueIndexName = "IX_ConnectionRequests_Student_Subject_Grade_OneToOne_Active";
    private readonly ApplicationDbContext _db;
    private readonly IOpenGroupClassroomService _openGroupClassroomService;
    private readonly IAvailabilitySlotService _availabilitySlotService;
    private readonly IHubContext<ConnectionRequestHub> _hubContext;
    private readonly INotificationDispatcher _dispatcher;
    private readonly ILogger<ConnectionRequestService> _logger;

    public ConnectionRequestService(
        ApplicationDbContext db,
        IOpenGroupClassroomService openGroupClassroomService,
        IAvailabilitySlotService availabilitySlotService,
        IHubContext<ConnectionRequestHub> hubContext,
        INotificationDispatcher dispatcher,
        ILogger<ConnectionRequestService> logger)
    {
        _db = db;
        _openGroupClassroomService = openGroupClassroomService;
        _availabilitySlotService = availabilitySlotService;
        _hubContext = hubContext;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    public async Task<BookingEligibilityVm> GetBookingEligibilityAsync(
        string studentUserId,
        int tutorId,
        CancellationToken cancellationToken = default)
    {
        var student = await _db.StudentProfiles.AsNoTracking()
            .FirstOrDefaultAsync(s => s.UserId == studentUserId, cancellationToken);

        if (student == null)
            return new BookingEligibilityVm { TutorId = tutorId };

        var subjects = await _db.TutorSubjects.AsNoTracking()
            .Where(ts => ts.TutorId == tutorId)
            .OrderBy(ts => ts.Subject.Name)
            .ThenBy(ts => ts.GradeLevel.Name)
            .Select(ts => new OneToOneBookingEligibilityDto
            {
                SubjectId = ts.SubjectId,
                GradeLevelId = ts.GradeLevelId,
                SubjectName = ts.Subject.Name,
                GradeLevelName = ts.GradeLevel.Name,
                CanBook = true
            })
            .ToListAsync(cancellationToken);

        var activeClasses = await _db.ClassroomStudents.AsNoTracking()
            .Where(cs => cs.StudentId == student.Id
                      && cs.Classroom.Type == ClassroomType.OneToOne
                      && cs.Classroom.Status == ClassroomStatus.Active)
            .Select(cs => new
            {
                cs.Classroom.SubjectId,
                cs.Classroom.GradeLevelId,
                ClassroomId = (int?)cs.ClassroomId,
                TutorName = cs.Classroom.Tutor.User.FullName
            })
            .ToListAsync(cancellationToken);

        var activeRequests = await _db.ConnectionRequests.AsNoTracking()
            .Where(cr => cr.StudentId == student.Id
                      && cr.PreferredType == ClassroomType.OneToOne
                      && (cr.Status == RequestStatus.Pending
                          || (cr.Status == RequestStatus.Accepted
                              && (!cr.ClassroomId.HasValue || cr.Classroom!.Status == ClassroomStatus.Active))))
            .Select(cr => new
            {
                cr.SubjectId,
                cr.GradeLevelId,
                ClassroomId = cr.ClassroomId,
                TutorName = cr.Tutor.User.FullName
            })
            .ToListAsync(cancellationToken);

        for (var i = 0; i < subjects.Count; i++)
        {
            var subject = subjects[i];
            var activeClass = activeClasses.FirstOrDefault(c =>
                c.SubjectId == subject.SubjectId && c.GradeLevelId == subject.GradeLevelId);
            var activeRequest = activeRequests.FirstOrDefault(r =>
                r.SubjectId == subject.SubjectId && r.GradeLevelId == subject.GradeLevelId);

            if (activeClass != null)
            {
                subjects[i] = new OneToOneBookingEligibilityDto
                {
                    SubjectId = subject.SubjectId,
                    GradeLevelId = subject.GradeLevelId,
                    SubjectName = subject.SubjectName,
                    GradeLevelName = subject.GradeLevelName,
                    CanBook = false,
                    ExistingClassroomId = activeClass.ClassroomId,
                    ExistingTutorName = activeClass.TutorName,
                    Reason = $"Bạn đang học 1-1 môn {subject.SubjectName} - {subject.GradeLevelName} với gia sư {activeClass.TutorName}."
                };
            }
            else if (activeRequest != null)
            {
                subjects[i] = new OneToOneBookingEligibilityDto
                {
                    SubjectId = subject.SubjectId,
                    GradeLevelId = subject.GradeLevelId,
                    SubjectName = subject.SubjectName,
                    GradeLevelName = subject.GradeLevelName,
                    CanBook = false,
                    ExistingClassroomId = activeRequest.ClassroomId,
                    ExistingTutorName = activeRequest.TutorName,
                    Reason = $"Bạn đã có yêu cầu 1-1 môn {subject.SubjectName} - {subject.GradeLevelName} với gia sư {activeRequest.TutorName} đang chờ hoặc đã được chấp nhận."
                };
            }
        }

        var todayVn = ClassroomLifecycleHelper.GetVietnamToday();
        var rawGroupClassrooms = await _db.Classrooms.AsNoTracking()
            .Where(c => c.TutorId == tutorId
                     && c.Type == ClassroomType.Group
                     && c.Status == ClassroomStatus.Active)
            .OrderBy(c => c.NameClass)
            .Select(c => new
            {
                Classroom = c,
                SubjectName = c.Subject.Name,
                GradeLevelName = c.GradeLevel.Name,
                StudentCount = c.ClassroomStudents.Count,
                IsEnrolled = c.ClassroomStudents.Any(cs => cs.StudentId == student.Id),
                HasPendingRequest = c.ConnectionRequests.Any(cr =>
                    cr.StudentId == student.Id && cr.Status == RequestStatus.Pending),
                HasStartedOrCompletedSession = c.ClassSessions.Any(s =>
                    s.Status == SessionStatus.Done || s.Status == SessionStatus.InProgress),
                WeeklySchedules = c.WeeklySchedules.OrderBy(w => w.SessionIndex).ToList()
            })
            .ToListAsync(cancellationToken);

        var groupClassrooms = rawGroupClassrooms.Select(item =>
        {
            var c = item.Classroom;
            var isOpen = ClassroomLifecycleHelper.IsGroupEnrollmentOpen(c, todayVn, item.HasStartedOrCompletedSession);
            var canJoin = !item.IsEnrolled
                          && !item.HasPendingRequest
                          && item.StudentCount < c.MaxStudents
                          && isOpen;

            string? reason = item.IsEnrolled
                ? "Bạn đã tham gia lớp này."
                : item.HasPendingRequest
                    ? "Yêu cầu tham gia lớp này đang chờ gia sư xác nhận."
                    : item.StudentCount >= c.MaxStudents
                        ? "Lớp đã hết chỗ."
                        : item.HasStartedOrCompletedSession
                            ? "Lớp học đã bắt đầu học và đóng nhận học viên mới."
                            : c.RegistrationDeadline.HasValue && todayVn > c.RegistrationDeadline.Value
                                ? $"Lớp đã hết hạn đăng ký vào ngày {c.RegistrationDeadline.Value:dd/MM/yyyy}."
                                : !isOpen
                                    ? "Lớp học đã khai giảng và đóng tuyển sinh đợt này."
                                    : null;

            var expectedTotal = ClassroomLifecycleHelper.CalculateExpectedTotalSessions(
                c.StartDate, c.EndDate, c.TotalSessions, c.SessionsPerWeek, item.WeeklySchedules);

            return new GroupBookingEligibilityDto
            {
                ClassroomId = c.Id,
                SubjectId = c.SubjectId,
                GradeLevelId = c.GradeLevelId,
                ClassroomName = c.NameClass,
                SubjectName = item.SubjectName,
                GradeLevelName = item.GradeLevelName,
                Fee = c.Fee,
                StudentCount = item.StudentCount,
                MaxStudents = c.MaxStudents,
                AvailableSeats = Math.Max(0, c.MaxStudents - item.StudentCount),
                IsEnrolled = item.IsEnrolled,
                HasPendingRequest = item.HasPendingRequest,
                CanJoin = canJoin,
                Reason = reason,
                StartDate = c.StartDate,
                RegistrationDeadline = c.RegistrationDeadline,
                EndDate = c.EndDate,
                TotalSessions = expectedTotal
            };
        }).ToList();

        return new BookingEligibilityVm
        {
            TutorId = tutorId,
            Subjects = subjects,
            GroupClassrooms = groupClassrooms
        };
    }

    public async Task<ConnectionRequestOperationResult> CreateRequestAsync(
        SendConnectionRequestDto dto,
        string studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (dto.TutorId <= 0)
            return new ConnectionRequestOperationResult { Success = false, Message = "Gia sư không hợp lệ." };

        var enforceRole = await _db.Roles.AsNoTracking().AnyAsync(cancellationToken);

        var student = await _db.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == studentUserId
                                   && (!enforceRole || _db.UserRoles.Any(ur =>
                                       ur.UserId == s.UserId && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Student_Parent"))),
                cancellationToken);

        if (student == null)
            return new ConnectionRequestOperationResult { Success = false, Message = "Không tìm thấy hồ sơ học viên." };

        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects)
            .FirstOrDefaultAsync(t => t.Id == dto.TutorId
                                   && t.VerificationStatus == VerificationStatus.Approved
                                   && t.User.Status == AccountStatus.Active
                                   && (!enforceRole || _db.UserRoles.Any(ur =>
                                       ur.UserId == t.UserId && _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == "Tutor"))),
                cancellationToken);

        if (tutor == null)
            return new ConnectionRequestOperationResult { Success = false, Message = "Gia sư không tồn tại hoặc tài khoản hiện bị khóa." };

        // Xử lý theo loại lớp (Nhóm hay 1-1)
        var todayVnDate = ClassroomLifecycleHelper.GetVietnamToday();
        var durationWeeks = 4;
        DateOnly? requestedStartDate = dto.StartDate;
        DateOnly? requestedEndDate = dto.EndDate;

        if (dto.PreferredType == ClassroomType.Group)
        {
            if (!dto.ClassroomId.HasValue || dto.ClassroomId.Value <= 0)
                return new ConnectionRequestOperationResult { Success = false, Message = "Vui lòng chọn lớp nhóm đang mở tuyển sinh của gia sư." };

            var groupClassroom = await _db.Classrooms
                .AsNoTracking()
                .Include(c => c.WeeklySchedules)
                .Include(c => c.ClassSessions)
                .FirstOrDefaultAsync(c => c.Id == dto.ClassroomId.Value && c.TutorId == tutor.Id, cancellationToken);

            if (groupClassroom == null)
            {
                return new ConnectionRequestOperationResult { Success = false, Message = "Lớp nhóm đã chọn không tồn tại hoặc không thuộc gia sư này." };
            }

            var hasStartedSession = groupClassroom.ClassSessions.Any(s =>
                s.Status == SessionStatus.Done || s.Status == SessionStatus.InProgress);
            if (!ClassroomLifecycleHelper.IsGroupEnrollmentOpen(groupClassroom, todayVnDate, hasStartedSession))
            {
                var closedMsg = groupClassroom.RegistrationDeadline.HasValue && todayVnDate > groupClassroom.RegistrationDeadline.Value
                    ? $"Lớp học nhóm này đã hết hạn đăng ký vào ngày {groupClassroom.RegistrationDeadline.Value:dd/MM/yyyy}."
                    : hasStartedSession
                        ? "Lớp học nhóm này đã bắt đầu học nên đã đóng nhận học viên mới."
                        : "Lớp học nhóm này đã qua ngày khai giảng và đóng tuyển sinh đợt hiện tại.";
                return new ConnectionRequestOperationResult { Success = false, Message = closedMsg };
            }

            // Kiểm tra xem học viên đã là thành viên đang học trong lớp nhóm này chưa
            var isAlreadyEnrolled = await _db.ClassroomStudents.AnyAsync(cs =>
                cs.StudentId == student.Id &&
                cs.ClassroomId == dto.ClassroomId.Value &&
                cs.Classroom.Status == ClassroomStatus.Active, cancellationToken);

            if (isAlreadyEnrolled)
            {
                return new ConnectionRequestOperationResult
                {
                    Success = false,
                    Message = "Bạn hiện đang là học viên đang theo học trong lớp nhóm này của gia sư rồi. Không thể đăng ký lại!"
                };
            }

            // Kiểm tra xem đã có yêu cầu xin vào lớp nhóm này đang chờ phản hồi chưa
            var hasPendingClassroomRequest = await _db.ConnectionRequests.AnyAsync(cr =>
                cr.StudentId == student.Id &&
                cr.ClassroomId == dto.ClassroomId.Value &&
                cr.Status == RequestStatus.Pending, cancellationToken);

            if (hasPendingClassroomRequest)
            {
                return new ConnectionRequestOperationResult
                {
                    Success = false,
                    Message = "Bạn đã gửi yêu cầu đăng ký vào lớp nhóm này rồi và đang chờ gia sư phản hồi."
                };
            }

            // Tự động đồng bộ môn học và cấp học theo đúng lớp nhóm học viên đã chọn
            dto.SubjectId = groupClassroom.SubjectId;
            dto.GradeLevelId = groupClassroom.GradeLevelId;

            var isOpen = await _openGroupClassroomService.IsOpenGroupClassroomAsync(
                tutor.Id,
                dto.ClassroomId.Value,
                dto.SubjectId,
                dto.GradeLevelId,
                cancellationToken);

            if (!isOpen)
                return new ConnectionRequestOperationResult { Success = false, Message = "Lớp nhóm đã chọn không còn nhận học viên, đã đủ sĩ số hoặc không khớp môn học." };

            if (groupClassroom.WeeklySchedules.Any())
            {
                var groupSlots = groupClassroom.WeeklySchedules
                    .Select(w => new ScheduleConflictHelper.ScheduleSlotCandidate(w.DayOfWeek, w.StartTime, w.EndTime))
                    .ToList();
                var studentConflict = await ScheduleConflictHelper.FindStudentScheduleConflictAsync(
                    _db,
                    groupClassroom.Id,
                    groupClassroom.StartDate ?? todayVnDate,
                    groupClassroom.EndDate,
                    groupSlots,
                    new[] { student.Id },
                    Array.Empty<(DateTime, double)>(),
                    cancellationToken);
                if (studentConflict != null)
                    return new ConnectionRequestOperationResult { Success = false, Message = studentConflict };
            }
        }
        else if (dto.PreferredType != ClassroomType.OneToOne)
        {
            return new ConnectionRequestOperationResult { Success = false, Message = "Hình thức học không hợp lệ." };
        }
        else if (!dto.IsTrial)
        {
            if (requestedStartDate.HasValue && requestedStartDate.Value < todayVnDate)
            {
                return new ConnectionRequestOperationResult
                {
                    Success = false,
                    Message = "Ngày bắt đầu dự kiến không được nằm trong quá khứ."
                };
            }

            if (requestedStartDate.HasValue && requestedEndDate.HasValue)
            {
                if (requestedEndDate.Value < requestedStartDate.Value)
                {
                    return new ConnectionRequestOperationResult
                    {
                        Success = false,
                        Message = "Ngày kết thúc không được trước ngày bắt đầu."
                    };
                }

                durationWeeks = Math.Clamp(
                    (int)Math.Ceiling((requestedEndDate.Value.DayNumber - requestedStartDate.Value.DayNumber + 1) / 7.0),
                    1,
                    52);
            }
            else
            {
                durationWeeks = dto.DurationWeeks.HasValue
                    ? Math.Clamp(dto.DurationWeeks.Value, 1, 52)
                    : 4;
                if (requestedStartDate.HasValue && !requestedEndDate.HasValue)
                {
                    requestedEndDate = requestedStartDate.Value.AddDays(durationWeeks * 7 - 1);
                }
            }
        }

        // Kiểm tra môn học và cấp học có khớp với gia sư dạy không
        var teachesSubject = tutor.TutorSubjects.Any(ts => ts.SubjectId == dto.SubjectId && ts.GradeLevelId == dto.GradeLevelId);
        if (!teachesSubject)
            return new ConnectionRequestOperationResult { Success = false, Message = "Gia sư không giảng dạy môn học hoặc cấp học này." };

        if (dto.PreferredType == ClassroomType.OneToOne)
        {
            var eligibility = await GetBookingEligibilityAsync(studentUserId, tutor.Id, cancellationToken);
            var subjectEligibility = eligibility.Subjects.FirstOrDefault(s =>
                s.SubjectId == dto.SubjectId && s.GradeLevelId == dto.GradeLevelId);
            if (subjectEligibility == null)
            {
                return new ConnectionRequestOperationResult
                {
                    Success = false,
                    Message = "Môn học hoặc cấp học này không còn được gia sư cung cấp."
                };
            }
            if (!subjectEligibility.CanBook)
            {
                return new ConnectionRequestOperationResult
                {
                    Success = false,
                    Message = subjectEligibility.Reason ?? "Bạn đã có lớp hoặc yêu cầu 1-1 cho môn học và cấp học này."
                };
            }
        }

        // Xử lý các slot thời gian đề xuất
        var requestedSlots = (dto.ProposedTrialSlotsUtc ?? new List<DateTime>())
            .Distinct()
            .OrderBy(s => s)
            .ToList();

        if (requestedSlots.Count == 0 && dto.ProposedTrialSlotUtc.HasValue)
        {
            requestedSlots.Add(dto.ProposedTrialSlotUtc.Value);
        }

        if (requestedSlots.Any(slot => slot <= DateTime.UtcNow))
        {
            return new ConnectionRequestOperationResult
            {
                Success = false,
                Message = "Một hoặc nhiều khung giờ đã chọn đã qua. Vui lòng tải lại lịch và chọn thời gian khác."
            };
        }

        var selectedSlots = requestedSlots;

        if (dto.PreferredType == ClassroomType.OneToOne)
        {
            if (dto.IsTrial)
            {
                // Đối với học thử 1 buổi: Chỉ lấy tối đa 1 slot (2 tiếng)
                if (selectedSlots.Count > 1)
                {
                    selectedSlots = selectedSlots.Take(1).ToList();
                }

                if (selectedSlots.Count == 0)
                {
                    return new ConnectionRequestOperationResult
                    {
                        Success = false,
                        Message = "Vui lòng chọn 1 khung giờ học thử trên lịch rảnh của gia sư."
                    };
                }
            }
            else
            {
                // Đối với học chính thức: Giới hạn tối đa 3 buổi/tuần (tương ứng 6 tiếng/tuần)
                if (selectedSlots.Count > 3)
                {
                    return new ConnectionRequestOperationResult
                    {
                        Success = false,
                        Message = "Gói học tiêu chuẩn tối đa 3 buổi/tuần (tổng cộng 6 tiếng/tuần). Vui lòng chọn không quá 3 buổi."
                    };
                }

                if (selectedSlots.Count == 0)
                {
                    return new ConnectionRequestOperationResult
                    {
                        Success = false,
                        Message = "Vui lòng chọn từ 1 đến 3 khung giờ cố định/tuần từ lịch rảnh của gia sư."
                    };
                }
            }

            if (selectedSlots.Count > 0)
            {
                var availableSlots = await _availabilitySlotService.GetAvailableSlotsForStudentAsync(
                    tutor.Id,
                    student.Id,
                    14,
                    cancellationToken);
                var invalidSlot = selectedSlots.FirstOrDefault(slot =>
                    !availableSlots.Any(available => available.SlotUtc == slot));
                if (invalidSlot != default)
                {
                    return new ConnectionRequestOperationResult
                    {
                        Success = false,
                        Message = "Một hoặc nhiều khung giờ đã chọn không còn rảnh hoặc trùng lịch học của bạn. Vui lòng tải lại lịch và chọn khung giờ khác."
                    };
                }

                foreach (var slotUtc in selectedSlots)
                {
                    var conflict = await ScheduleConflictHelper.FindSingleSessionConflictAsync(
                        _db,
                        tutor.Id,
                        null,
                        null,
                        slotUtc,
                        2.0,
                        new[] { student.Id },
                        cancellationToken);
                    if (conflict != null)
                    {
                        return new ConnectionRequestOperationResult
                        {
                            Success = false,
                            Message = conflict
                        };
                    }
                }
            }
        }

        // Định dạng tin nhắn tóm tắt lịch học vào Message
        var baseMessage = dto.Message?.Trim();
        string? finalMessage = baseMessage;
        var isTrialRequest = dto.PreferredType == ClassroomType.OneToOne && dto.IsTrial;
        var purposeTag = dto.PreferredType == ClassroomType.Group
            ? "[Mục đích: Tham gia lớp nhóm]"
            : isTrialRequest
                ? "[Mục đích: Đăng ký học thử 1 buổi]"
                : $"[Mục đích: Vào học chính thức luôn | Thời lượng: {durationWeeks} tuần]";

        if (dto.PreferredType == ClassroomType.OneToOne && selectedSlots.Count > 0)
        {
            var slotStrings = selectedSlots.Select((slotUtc, idx) =>
            {
                var vn = slotUtc.AddHours(VietnamUtcOffsetHours);
                return $"Buổi {idx + 1}: {vn.ToString("dddd, dd/MM/yyyy HH:mm", new System.Globalization.CultureInfo("vi-VN"))}";
            });
            var scheduleHeader = $"{purposeTag}\n[Lịch học đề xuất ({selectedSlots.Count} buổi): {string.Join(" | ", slotStrings)}]";
            finalMessage = string.IsNullOrWhiteSpace(baseMessage)
                ? scheduleHeader
                : $"{scheduleHeader}\n{baseMessage}";
        }
        else
        {
            finalMessage = string.IsNullOrWhiteSpace(baseMessage)
                ? purposeTag
                : $"{purposeTag}\n{baseMessage}";
        }

        var connectionRequest = new ConnectionRequest
        {
            StudentId = student.Id,
            TutorId = tutor.Id,
            SubjectId = dto.SubjectId,
            GradeLevelId = dto.GradeLevelId,
            PreferredType = dto.PreferredType,
            IsTrial = isTrialRequest,
            DurationWeeks = isTrialRequest ? 1 : durationWeeks,
            RequestedStartDate = isTrialRequest ? null : requestedStartDate,
            RequestedEndDate = isTrialRequest ? null : requestedEndDate,
            ClassroomId = dto.PreferredType == ClassroomType.Group ? dto.ClassroomId : null,
            ProposedTrialSlotUtc = dto.PreferredType == ClassroomType.OneToOne ? selectedSlots.FirstOrDefault() : null,
            Message = finalMessage,
            Status = RequestStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        if (dto.PreferredType == ClassroomType.OneToOne)
        {
            foreach (var slotUtc in selectedSlots)
            {
                connectionRequest.TrialSchedules.Add(new TrialSchedule
                {
                    ScheduledAt = slotUtc,
                    Status = ScheduleStatus.Pending
                });
            }
        }

        _db.ConnectionRequests.Add(connectionRequest);

        // Tạo thông báo cho gia sư
        var studentName = student.User?.FullName ?? "Học viên";
        var subject = await _db.Subjects.FindAsync(new object[] { dto.SubjectId }, cancellationToken);
        var subjectName = subject?.Name ?? "môn học";
        var actionLabel = dto.PreferredType == ClassroomType.Group
            ? "tham gia lớp nhóm"
            : isTrialRequest
                ? "học thử 1 buổi"
                : "vào học chính thức";

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is PostgresException pg
            && pg.SqlState == PostgresErrorCodes.UniqueViolation
            && pg.ConstraintName == OneToOneUniqueIndexName)
        {
            return new ConnectionRequestOperationResult
            {
                Success = false,
                Message = "Bạn đã có yêu cầu 1-1 hoặc lớp đang học cho môn học và cấp học này. Vui lòng kiểm tra các yêu cầu đã gửi."
            };
        }

        // Điều phối thông báo đa kênh (In-App chuông, Realtime SignalR, Gmail SMTP) qua NotificationDispatcher
        await _dispatcher.DispatchBookingCreatedAsync(new BookingCreatedNotificationEvent(
            TutorUserId: tutor.UserId,
            TutorEmail: tutor.User?.Email,
            TutorName: tutor.User?.FullName ?? "thầy/cô",
            StudentName: studentName,
            SubjectName: subjectName,
            ActionLabel: actionLabel,
            Message: dto.Message,
            RequestId: connectionRequest.Id,
            PreferredType: dto.PreferredType.ToString(),
            IsTrial: isTrialRequest
        ), cancellationToken);

        return new ConnectionRequestOperationResult
        {
            Success = true,
            Message = "Gửi yêu cầu kết nối thành công! Vui lòng chờ gia sư phản hồi.",
            RequestId = connectionRequest.Id
        };
    }

    public async Task<AcceptConnectionRequestResult> AcceptRequestAsync(
        int requestId,
        string tutorUserId,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.ConnectionRequests
            .Include(r => r.Student).ThenInclude(s => s.User)
            .Include(r => r.Tutor).ThenInclude(t => t.User)
            .Include(r => r.Subject)
            .Include(r => r.GradeLevel)
            .Include(r => r.Classroom!).ThenInclude(c => c.ClassroomStudents)
            .Include(r => r.Classroom!).ThenInclude(c => c.WeeklySchedules)
            .Include(r => r.TrialSchedules)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.Tutor.UserId == tutorUserId, cancellationToken);

        if (request == null)
            return new AcceptConnectionRequestResult { NotFound = true, Message = "Không tìm thấy yêu cầu kết nối." };

        if (request.Status != RequestStatus.Pending)
            return new AcceptConnectionRequestResult { AlreadyHandled = true, Message = "Yêu cầu này đã được phản hồi trước đó." };

        var isRelational = _db.Database.IsRelational();
        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = isRelational
                ? await _db.Database.BeginTransactionAsync(cancellationToken)
                : null;

            async Task<AcceptConnectionRequestResult> RollbackAndReturnAsync(AcceptConnectionRequestResult result)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                }
                return result;
            }

            var todayVn = ClassroomLifecycleHelper.GetVietnamToday();
            var studentUserId = request.Student?.UserId ?? request.Student?.User?.Id ?? "";
            var studentName = request.Student?.User?.FullName ?? "Học viên";
            var tutorName = request.Tutor?.User?.FullName ?? "Gia sư";
            var subjectName = request.Subject?.Name ?? "Môn học";

            int? finalClassroomId = null;
            int? finalSessionId = null;
            ClassSession? createdTrialSession = null;
            bool isTrial = false;
            bool enrolled = false;
            string? acceptNotifTitle = null;
            string? acceptNotifContent = null;

            if (request.PreferredType == ClassroomType.Group)
            {
                // 1. LỚP NHÓM
                if (!request.ClassroomId.HasValue || request.Classroom == null)
                {
                    return await RollbackAndReturnAsync(new AcceptConnectionRequestResult { InvalidClassroom = true, Message = "Lớp nhóm không còn tồn tại hoặc thông tin không khớp." });
                }

                var groupClass = request.Classroom;
                if (groupClass.Status != ClassroomStatus.Active || groupClass.TutorId != request.TutorId)
                {
                    return await RollbackAndReturnAsync(new AcceptConnectionRequestResult { InvalidClassroom = true, Message = "Lớp nhóm hiện không hoạt động." });
                }

                if (isRelational)
                {
                    // Khóa bi quan (Pessimistic Row Lock) trên lớp học để ngăn chặn race condition khi 2 yêu cầu duyệt cùng lúc
                    await _db.Database.ExecuteSqlRawAsync(
                        "SELECT \"Id\" FROM \"Classrooms\" WHERE \"Id\" = {0} FOR UPDATE",
                        new object[] { groupClass.Id },
                        cancellationToken);
                }

                var hasStartedSession = await _db.ClassSessions.AnyAsync(
                    s => s.ClassroomId == groupClass.Id && (s.Status == SessionStatus.Done || s.Status == SessionStatus.InProgress),
                    cancellationToken);

                var currentCount = await _db.ClassroomStudents.CountAsync(cs => cs.ClassroomId == groupClass.Id, cancellationToken);
                var alreadyInClass = await _db.ClassroomStudents.AnyAsync(cs => cs.ClassroomId == groupClass.Id && cs.StudentId == request.StudentId, cancellationToken);

                if (!alreadyInClass)
                {
                    if (!ClassroomLifecycleHelper.IsGroupEnrollmentOpen(groupClass, todayVn, hasStartedSession))
                    {
                        var closedReason = hasStartedSession
                            ? "Lớp nhóm đã có buổi học diễn ra nên không thể nhận thêm học viên trễ tiến độ."
                            : groupClass.RegistrationDeadline.HasValue && todayVn > groupClass.RegistrationDeadline.Value
                                ? $"Lớp nhóm đã hết hạn đăng ký vào ngày {groupClass.RegistrationDeadline.Value:dd/MM/yyyy}."
                                : "Lớp nhóm đã qua ngày khai giảng và đóng tuyển sinh.";
                        return await RollbackAndReturnAsync(new AcceptConnectionRequestResult { InvalidClassroom = true, Message = closedReason });
                    }

                    if (currentCount >= groupClass.MaxStudents)
                    {
                        return await RollbackAndReturnAsync(new AcceptConnectionRequestResult { ClassroomFull = true, Message = "Lớp nhóm đã đủ sĩ số tối đa. Không thể nhận thêm học viên!" });
                    }

                    _db.ClassroomStudents.Add(new ClassroomStudent
                    {
                        ClassroomId = groupClass.Id,
                        StudentId = request.StudentId,
                        JoinedAt = DateTime.UtcNow
                    });
                    enrolled = true;
                }

                // Tự động tạo bản ghi Payment cho học viên lớp nhóm nếu lớp có học phí
                if (groupClass.Fee > 0)
                {
                    var existingPayment = await _db.Payments.FirstOrDefaultAsync(
                        p => p.ClassroomId == groupClass.Id && p.StudentId == request.StudentId && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Success),
                        cancellationToken);

                    if (existingPayment == null)
                    {
                        _db.Payments.Add(new Payment
                        {
                            StudentId = request.StudentId,
                            ClassroomId = groupClass.Id,
                            Amount = groupClass.Fee,
                            Method = "VietQR",
                            Status = PaymentStatus.Pending
                        });
                    }
                }

                finalClassroomId = groupClass.Id;
                request.Status = RequestStatus.Accepted;
                request.RespondedAt = DateTime.UtcNow;

                var groupFeeNotice = groupClass.Fee > 0
                    ? $" Học phí là {groupClass.Fee:N0} đ. Vui lòng quét mã QR SePay để hoàn tất kích hoạt quyền vào lớp."
                    : "";

                acceptNotifTitle = "Đã được nhận vào lớp nhóm";
                acceptNotifContent = $"Gia sư {tutorName} đã chấp nhận yêu cầu của bạn vào lớp {groupClass.NameClass}.{groupFeeNotice} [url:/student/classrooms]";
            }
            else
            {
                // 2. LỚP 1 KÈM 1 (Phân định: Học thử 1 buổi VS Vào học chính thức)
                var message = request.Message ?? "";
                var hasOfficialTag = message.Contains("Vào học chính thức")
                    || request.RequestedStartDate.HasValue
                    || request.RequestedEndDate.HasValue
                    || request.DurationWeeks != 4
                    || request.TrialSchedules.Count > 1;
                var hasTrialTag = request.IsTrial
                    || message.Contains("Đăng ký học thử")
                    || message.Contains("học thử 1 buổi");

                isTrial = hasTrialTag || !hasOfficialTag;

                if (isTrial)
                {
                    var nullableTrialSlotUtc = request.ProposedTrialSlotUtc
                        ?? request.TrialSchedules.FirstOrDefault()?.ScheduledAt;
                    if (!nullableTrialSlotUtc.HasValue)
                    {
                        return await RollbackAndReturnAsync(new AcceptConnectionRequestResult
                        {
                            SlotConflict = true,
                            Message = "Yêu cầu học thử chưa có khung giờ cụ thể. Vui lòng từ chối hoặc đề nghị học viên chọn lịch rảnh."
                        });
                    }
                    var trialSlotUtc = DateTime.SpecifyKind(nullableTrialSlotUtc.Value, DateTimeKind.Utc);

                    if (trialSlotUtc <= DateTime.UtcNow)
                    {
                        return await RollbackAndReturnAsync(new AcceptConnectionRequestResult
                        {
                            SlotConflict = true,
                            Message = "Khung giờ học thử đã qua. Vui lòng từ chối hoặc đề nghị học viên chọn lịch mới."
                        });
                    }

                    var trialConflict = await ScheduleConflictHelper.FindSingleSessionConflictAsync(
                        _db,
                        request.TutorId,
                        request.ClassroomId,
                        null,
                        trialSlotUtc,
                        2.0,
                        new[] { request.StudentId },
                        cancellationToken);

                    if (trialConflict != null)
                    {
                        return await RollbackAndReturnAsync(new AcceptConnectionRequestResult
                        {
                            SlotConflict = true,
                            Message = trialConflict
                        });
                    }

                    var trialDateVn = DateOnly.FromDateTime(ClassroomLifecycleHelper.ToVietnamTime(trialSlotUtc));
                    var trialClassroom = request.Classroom;
                    if (trialClassroom == null)
                    {
                        trialClassroom = new Classroom
                        {
                            NameClass = $"Học thử: {subjectName} - {studentName}",
                            Fee = 0,
                            Status = ClassroomStatus.Active,
                            Type = ClassroomType.OneToOne,
                            MaxStudents = 1,
                            SessionsPerWeek = 1,
                            HoursPerSession = 2.0,
                            TutorId = request.TutorId,
                            SubjectId = request.SubjectId,
                            GradeLevelId = request.GradeLevelId,
                            StartDate = trialDateVn,
                            EndDate = trialDateVn,
                            TotalSessions = 1
                        };
                        _db.Classrooms.Add(trialClassroom);
                        request.Classroom = trialClassroom;
                    }
                    else
                    {
                        trialClassroom.Status = ClassroomStatus.Active;
                        trialClassroom.EndDate ??= trialDateVn;
                        trialClassroom.TotalSessions ??= 1;
                    }

                    var isEnrolled = await _db.ClassroomStudents.AnyAsync(
                        cs => cs.ClassroomId == trialClassroom.Id && cs.StudentId == request.StudentId, cancellationToken);
                    if (!isEnrolled)
                    {
                        _db.ClassroomStudents.Add(new ClassroomStudent
                        {
                            Classroom = trialClassroom,
                            StudentId = request.StudentId,
                            JoinedAt = DateTime.UtcNow
                        });
                        enrolled = true;
                    }

                    var videoRoomId = $"OTSP-Trial-{request.Id}-{Guid.NewGuid():N}"[..28];
                    createdTrialSession = new ClassSession
                    {
                        Classroom = trialClassroom,
                        ScheduledAt = trialSlotUtc,
                        DurationHours = 2.0,
                        Status = SessionStatus.Scheduled,
                        IsMakeup = false,
                        VideoRoom = videoRoomId
                    };
                    _db.ClassSessions.Add(createdTrialSession);

                    foreach (var ts in request.TrialSchedules)
                    {
                        ts.Status = ScheduleStatus.Confirmed;
                        ts.ClassSession = createdTrialSession;
                    }

                    request.Status = RequestStatus.Accepted;
                    request.RespondedAt = DateTime.UtcNow;
                    finalClassroomId = trialClassroom.Id;

                    var vnTrialTime = ClassroomLifecycleHelper.ToVietnamTime(trialSlotUtc);
                    var timeStr = vnTrialTime.ToString("HH:mm, dddd dd/MM/yyyy", new System.Globalization.CultureInfo("vi-VN"));

                    acceptNotifTitle = "Gia sư đã đồng ý lịch học thử";
                    acceptNotifContent = $"Gia sư {tutorName} đã xác nhận buổi học thử môn {subjectName} vào lúc {timeStr}. Phòng học trực tuyến đã sẵn sàng! [url:/student/schedule]";
                }
                else
                {
                    // 2.B: TẠO LỚP HỌC CHÍNH THỨC 1 KÈM 1
                    var tutorSubject = await _db.TutorSubjects.AsNoTracking()
                        .FirstOrDefaultAsync(ts => ts.TutorId == request.TutorId
                                                && ts.SubjectId == request.SubjectId
                                                && ts.GradeLevelId == request.GradeLevelId, cancellationToken);

                    var hourlyFee = (tutorSubject != null && tutorSubject.HourlyFee > 0) ? tutorSubject.HourlyFee : (request.Tutor?.HourlyFee ?? 150000);
                    if (hourlyFee <= 0) hourlyFee = 150000;

                    var durationWeeks = request.DurationWeeks is > 0 and <= 52 ? request.DurationWeeks : 4;
                    var startDateVn = request.RequestedStartDate
                        ?? (request.TrialSchedules.Any()
                            ? DateOnly.FromDateTime(request.TrialSchedules.Min(t => ClassroomLifecycleHelper.ToVietnamTime(t.ScheduledAt)))
                            : todayVn);
                    if (startDateVn < todayVn)
                        startDateVn = todayVn;

                    var distinctWeekly = new List<ClassroomWeeklySchedule>();
                    var seenKeys = new HashSet<(DayOfWeek, TimeOnly)>();
                    var slotIndex = 1;
                    foreach (var sched in request.TrialSchedules.OrderBy(t => t.ScheduledAt))
                    {
                        var vn = ClassroomLifecycleHelper.ToVietnamTime(sched.ScheduledAt);
                        var start = TimeOnly.FromTimeSpan(vn.TimeOfDay);
                        if (!seenKeys.Add((vn.DayOfWeek, start)))
                            continue;

                        distinctWeekly.Add(new ClassroomWeeklySchedule
                        {
                            SessionIndex = slotIndex++,
                            DayOfWeek = vn.DayOfWeek,
                            StartTime = start,
                            EndTime = start.AddHours(2)
                        });
                    }

                    if (distinctWeekly.Count == 0)
                    {
                        return await RollbackAndReturnAsync(new AcceptConnectionRequestResult
                        {
                            SlotConflict = true,
                            Message = "Yêu cầu học chính thức chưa có khung giờ cố định/tuần. Vui lòng từ chối hoặc yêu cầu học viên chọn lịch rảnh."
                        });
                    }

                    var sessionsCount = Math.Clamp(distinctWeekly.Count, 1, 3);
                    int expectedTotal;
                    DateOnly? resolvedEndDate;
                    if (request.RequestedEndDate.HasValue && request.RequestedEndDate.Value >= startDateVn)
                    {
                        resolvedEndDate = request.RequestedEndDate.Value;
                        expectedTotal = distinctWeekly.Count > 0
                            ? Math.Max(1, ClassroomLifecycleHelper.CountScheduledOccurrences(startDateVn, resolvedEndDate.Value, distinctWeekly))
                            : Math.Max(1, sessionsCount * durationWeeks);
                    }
                    else
                    {
                        expectedTotal = Math.Max(1, sessionsCount * durationWeeks);
                        resolvedEndDate = distinctWeekly.Count > 0
                            ? ClassroomLifecycleHelper.CalculateEndDateFromTotalSessions(startDateVn, expectedTotal, distinctWeekly)
                            : startDateVn.AddDays(durationWeeks * 7 - 1);
                    }

                    if (distinctWeekly.Count > 0)
                    {
                        var candidateSlots = distinctWeekly
                            .Select(w => new ScheduleConflictHelper.ScheduleSlotCandidate(w.DayOfWeek, w.StartTime, w.EndTime))
                            .ToList();
                        var conflictMsg = await ScheduleConflictHelper.FindClassroomScheduleConflictAsync(
                            _db,
                            request.TutorId,
                            request.ClassroomId,
                            startDateVn,
                            resolvedEndDate,
                            candidateSlots,
                            new[] { request.StudentId },
                            Array.Empty<(DateTime, double)>(),
                            cancellationToken);

                        if (conflictMsg != null)
                        {
                            return await RollbackAndReturnAsync(new AcceptConnectionRequestResult
                            {
                                SlotConflict = true,
                                Message = conflictMsg
                            });
                        }
                    }

                    var courseFee = hourlyFee * 2.0m * expectedTotal;

                    var officialClassroom = request.Classroom;
                    if (officialClassroom == null)
                    {
                        officialClassroom = new Classroom
                        {
                            NameClass = $"Lớp 1-1: {subjectName} - {studentName}",
                            Fee = courseFee,
                            Status = ClassroomStatus.Active,
                            Type = ClassroomType.OneToOne,
                            MaxStudents = 1,
                            SessionsPerWeek = sessionsCount,
                            HoursPerSession = 2.0,
                            TutorId = request.TutorId,
                            SubjectId = request.SubjectId,
                            GradeLevelId = request.GradeLevelId,
                            StartDate = startDateVn,
                            EndDate = resolvedEndDate,
                            TotalSessions = expectedTotal
                        };
                        _db.Classrooms.Add(officialClassroom);
                        request.Classroom = officialClassroom;
                    }
                    else
                    {
                        officialClassroom.NameClass = $"Lớp 1-1: {subjectName} - {studentName}";
                        officialClassroom.Status = ClassroomStatus.Active;
                        officialClassroom.Fee = courseFee;
                        officialClassroom.SessionsPerWeek = sessionsCount;
                        officialClassroom.StartDate = startDateVn;
                        officialClassroom.EndDate = resolvedEndDate;
                        officialClassroom.TotalSessions = expectedTotal;
                    }

                    if (!officialClassroom.WeeklySchedules.Any() && distinctWeekly.Count > 0)
                    {
                        foreach (var ws in distinctWeekly)
                        {
                            ws.Classroom = officialClassroom;
                            officialClassroom.WeeklySchedules.Add(ws);
                        }
                    }

                    var isEnrolled = await _db.ClassroomStudents.AnyAsync(
                        cs => cs.ClassroomId == officialClassroom.Id && cs.StudentId == request.StudentId, cancellationToken);
                    if (!isEnrolled)
                    {
                        _db.ClassroomStudents.Add(new ClassroomStudent
                        {
                            Classroom = officialClassroom,
                            StudentId = request.StudentId,
                            JoinedAt = DateTime.UtcNow
                        });
                        enrolled = true;
                    }

                    if (courseFee > 0)
                    {
                        var existingPayment = await _db.Payments.FirstOrDefaultAsync(
                            p => p.ClassroomId == officialClassroom.Id && p.StudentId == request.StudentId && (p.Status == PaymentStatus.Pending || p.Status == PaymentStatus.Success),
                            cancellationToken);

                        if (existingPayment == null)
                        {
                            _db.Payments.Add(new Payment
                            {
                                StudentId = request.StudentId,
                                Classroom = officialClassroom,
                                Amount = courseFee,
                                Method = "VietQR",
                                Status = PaymentStatus.Pending
                            });
                        }
                    }

                    foreach (var ts in request.TrialSchedules)
                    {
                        ts.Status = ScheduleStatus.Confirmed;
                    }

                    request.Status = RequestStatus.Accepted;
                    request.RespondedAt = DateTime.UtcNow;
                    finalClassroomId = officialClassroom.Id;

                    acceptNotifTitle = "Yêu cầu học chính thức đã được chấp nhận";
                    acceptNotifContent = $"Gia sư {tutorName} đã chấp nhận vào học chính thức lớp {officialClassroom.NameClass}. Học phí khóa {expectedTotal} buổi ({expectedTotal * 2} tiếng) là {courseFee:N0} đ. Vui lòng quét mã QR SePay để thanh toán kích hoạt quyền vào lớp! [url:/student/classrooms]";
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            if (createdTrialSession != null)
            {
                finalSessionId = createdTrialSession.Id;
            }
            if (finalClassroomId == null && request.ClassroomId.HasValue)
            {
                finalClassroomId = request.ClassroomId;
            }

            // Tự động sinh lịch ClassSessions nếu là lớp chính thức có WeeklySchedules (giới hạn theo StartDate/EndDate/TotalSessions)
            if (!isTrial && finalClassroomId.HasValue)
            {
                var horizonDays = request.RequestedEndDate.HasValue
                    ? Math.Clamp(request.RequestedEndDate.Value.DayNumber - todayVn.DayNumber + 7, 28, 365)
                    : Math.Clamp(request.DurationWeeks * 7 + 7, 28, 365);
                await GenerateUpcomingSessionsAsync(finalClassroomId.Value, horizonDays, cancellationToken);
            }

            if (transaction != null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            // Phân phối thông báo (In-App + SignalR + Gmail) qua Central Dispatcher
            await _dispatcher.DispatchBookingAcceptedAsync(new BookingAcceptedNotificationEvent(
                StudentUserId: studentUserId,
                StudentEmail: request.Student?.User?.Email,
                StudentName: studentName,
                TutorName: tutorName,
                SubjectName: subjectName,
                RequestId: request.Id,
                ClassroomId: finalClassroomId,
                IsTrial: isTrial,
                CustomTitle: acceptNotifTitle,
                CustomContent: acceptNotifContent
            ), cancellationToken);

            return new AcceptConnectionRequestResult
            {
                Success = true,
                Message = isTrial
                    ? $"Đã chấp nhận yêu cầu học thử của học viên {studentName}. Lịch học thử và phòng học đã được tạo thành công!"
                    : $"Đã chấp nhận học viên {studentName} vào lớp học. Lịch học đã được tự động xếp vào thời khóa biểu!",
                ClassroomId = finalClassroomId,
                SessionId = finalSessionId,
                IsTrial = isTrial,
                Enrolled = enrolled,
                StudentUserId = studentUserId
            };
        });
    }

    public async Task<RejectConnectionRequestResult> RejectRequestAsync(
        int requestId,
        string tutorUserId,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var request = await _db.ConnectionRequests
            .Include(r => r.Student).ThenInclude(s => s.User)
            .Include(r => r.Tutor).ThenInclude(t => t.User)
            .Include(r => r.Subject)
            .Include(r => r.TrialSchedules)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.Tutor.UserId == tutorUserId, cancellationToken);

        if (request == null)
            return new RejectConnectionRequestResult { NotFound = true, Message = "Không tìm thấy yêu cầu kết nối." };

        if (request.Status != RequestStatus.Pending)
            return new RejectConnectionRequestResult { AlreadyHandled = true, Message = "Yêu cầu này đã được xử lý trước đó." };

        request.Status = RequestStatus.Rejected;
        request.RespondedAt = DateTime.UtcNow;

        foreach (var ts in request.TrialSchedules)
        {
            ts.Status = ScheduleStatus.Rejected;
        }

        var studentUserId = request.Student?.UserId ?? request.Student?.User?.Id ?? "";
        var tutorName = request.Tutor?.User?.FullName ?? "Gia sư";
        var subjectName = request.Subject?.Name ?? "môn học";

        await _db.SaveChangesAsync(cancellationToken);

        // Phân phối thông báo từ chối (In-App + SignalR + Gmail) qua Central Dispatcher
        await _dispatcher.DispatchBookingRejectedAsync(new BookingRejectedNotificationEvent(
            StudentUserId: studentUserId,
            StudentEmail: request.Student?.User?.Email,
            StudentName: request.Student?.User?.FullName ?? "Học viên",
            TutorName: tutorName,
            SubjectName: subjectName,
            Reason: reason,
            RequestId: request.Id
        ), cancellationToken);

        return new RejectConnectionRequestResult
        {
            Success = true,
            Message = "Đã từ chối yêu cầu kết nối.",
            StudentUserId = studentUserId
        };
    }

    public async Task GenerateUpcomingSessionsAsync(
        int classroomId,
        int daysAhead = 28,
        CancellationToken cancellationToken = default)
    {
        var classroom = await _db.Classrooms
            .Include(c => c.WeeklySchedules)
            .Include(c => c.ClassSessions)
            .FirstOrDefaultAsync(c => c.Id == classroomId, cancellationToken);

        if (classroom == null || classroom.Status != ClassroomStatus.Active || !classroom.WeeklySchedules.Any())
            return;

        var nowUtc = DateTime.UtcNow;
        var existingNonCancelled = classroom.ClassSessions
            .Where(s => s.Status != SessionStatus.Cancelled)
            .Select(s => s.ScheduledAt)
            .ToList();

        var plannedSessions = ClassroomLifecycleHelper.BuildPlannedSessions(
            classroom,
            classroom.WeeklySchedules,
            nowUtc,
            existingNonCancelled,
            Math.Clamp(daysAhead, 14, 365));

        if (plannedSessions.Count == 0)
            return;

        foreach (var planned in plannedSessions)
        {
            _db.ClassSessions.Add(new ClassSession
            {
                ClassroomId = classroomId,
                ScheduledAt = planned.ScheduledAtUtc,
                DurationHours = planned.DurationHours,
                Status = SessionStatus.Scheduled,
                IsMakeup = false,
                VideoRoom = $"OTSP-Room-{classroomId}-{Guid.NewGuid():N}"[..28]
            });
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private static string ToVnDay(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => "Thứ Hai",
        DayOfWeek.Tuesday => "Thứ Ba",
        DayOfWeek.Wednesday => "Thứ Tư",
        DayOfWeek.Thursday => "Thứ Năm",
        DayOfWeek.Friday => "Thứ Sáu",
        DayOfWeek.Saturday => "Thứ Bảy",
        DayOfWeek.Sunday => "Chủ Nhật",
        _ => day.ToString()
    };
}
