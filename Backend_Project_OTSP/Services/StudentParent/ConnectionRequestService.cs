using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos;
using Backend_Project_OTSP.Dtos.StudentParent;
using Backend_Project_OTSP.Hubs;
using Backend_Project_OTSP.Models;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.StudentParent;
using Backend_Project_OTSP.Services.Tutor;

namespace Backend_Project_OTSP.Services;

public class ConnectionRequestService : IConnectionRequestService
{
    private const int VietnamUtcOffsetHours = 7;
    private const string OneToOneUniqueIndexName = "IX_ConnectionRequests_Student_Subject_Grade_OneToOne_Active";
    private readonly ApplicationDbContext _db;
    private readonly IOpenGroupClassroomService _openGroupClassroomService;
    private readonly IAvailabilitySlotService _availabilitySlotService;
    private readonly IHubContext<ConnectionRequestHub> _hubContext;
    private readonly ILogger<ConnectionRequestService> _logger;

    public ConnectionRequestService(
        ApplicationDbContext db,
        IOpenGroupClassroomService openGroupClassroomService,
        IAvailabilitySlotService availabilitySlotService,
        IHubContext<ConnectionRequestHub> hubContext,
        ILogger<ConnectionRequestService> logger)
    {
        _db = db;
        _openGroupClassroomService = openGroupClassroomService;
        _availabilitySlotService = availabilitySlotService;
        _hubContext = hubContext;
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

        var todayVn = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
        var groupClassrooms = await _db.Classrooms.AsNoTracking()
            .Where(c => c.TutorId == tutorId
                     && c.Type == ClassroomType.Group
                     && c.Status == ClassroomStatus.Active)
            .OrderBy(c => c.NameClass)
            .Select(c => new GroupBookingEligibilityDto
            {
                ClassroomId = c.Id,
                SubjectId = c.SubjectId,
                GradeLevelId = c.GradeLevelId,
                ClassroomName = c.NameClass,
                SubjectName = c.Subject.Name,
                GradeLevelName = c.GradeLevel.Name,
                Fee = c.Fee,
                StudentCount = c.ClassroomStudents.Count,
                MaxStudents = c.MaxStudents,
                AvailableSeats = c.MaxStudents - c.ClassroomStudents.Count,
                IsEnrolled = c.ClassroomStudents.Any(cs => cs.StudentId == student.Id),
                HasPendingRequest = c.ConnectionRequests.Any(cr =>
                    cr.StudentId == student.Id && cr.Status == RequestStatus.Pending),
                CanJoin = !c.ClassroomStudents.Any(cs => cs.StudentId == student.Id)
                    && !c.ConnectionRequests.Any(cr => cr.StudentId == student.Id && cr.Status == RequestStatus.Pending)
                    && c.ClassroomStudents.Count < c.MaxStudents
                    && (!c.StartDate.HasValue || c.StartDate.Value > todayVn),
                Reason = c.ClassroomStudents.Any(cs => cs.StudentId == student.Id)
                    ? "Bạn đã tham gia lớp này."
                    : c.ConnectionRequests.Any(cr => cr.StudentId == student.Id && cr.Status == RequestStatus.Pending)
                        ? "Yêu cầu tham gia lớp này đang chờ gia sư xác nhận."
                        : c.ClassroomStudents.Count >= c.MaxStudents
                            ? "Lớp đã hết chỗ."
                            : (c.StartDate.HasValue && c.StartDate.Value <= todayVn)
                                ? "Lớp học đã khai giảng và đóng tuyển sinh đợt này."
                                : null
            })
            .ToListAsync(cancellationToken);

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

        var student = await _db.StudentProfiles
            .Include(s => s.User)
            .FirstOrDefaultAsync(s => s.UserId == studentUserId, cancellationToken);

        if (student == null)
            return new ConnectionRequestOperationResult { Success = false, Message = "Không tìm thấy hồ sơ học viên." };

        var tutor = await _db.TutorProfiles
            .Include(t => t.User)
            .Include(t => t.TutorSubjects)
            .FirstOrDefaultAsync(t => t.Id == dto.TutorId
                                   && t.VerificationStatus == VerificationStatus.Approved
                                   && t.User.Status == AccountStatus.Active, cancellationToken);

        if (tutor == null)
            return new ConnectionRequestOperationResult { Success = false, Message = "Gia sư không tồn tại hoặc tài khoản hiện bị khóa." };

        // Xử lý theo loại lớp (Nhóm hay 1-1)
        if (dto.PreferredType == ClassroomType.Group)
        {
            if (!dto.ClassroomId.HasValue || dto.ClassroomId.Value <= 0)
                return new ConnectionRequestOperationResult { Success = false, Message = "Vui lòng chọn lớp nhóm đang mở tuyển sinh của gia sư." };

            var groupClassroom = await _db.Classrooms
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Id == dto.ClassroomId.Value && c.TutorId == tutor.Id, cancellationToken);

            if (groupClassroom == null)
            {
                return new ConnectionRequestOperationResult { Success = false, Message = "Lớp nhóm đã chọn không tồn tại hoặc không thuộc gia sư này." };
            }

            // [Phương án 1]: Nếu lớp nhóm đã qua ngày khai giảng (StartDate <= Today), đóng tuyển sinh
            var todayVnDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(7));
            if (groupClassroom.StartDate.HasValue && groupClassroom.StartDate.Value <= todayVnDate)
            {
                return new ConnectionRequestOperationResult
                {
                    Success = false,
                    Message = $"Lớp học nhóm này đã bắt đầu từ ngày {groupClassroom.StartDate.Value:dd/MM/yyyy} và đã đóng tuyển sinh đợt hiện tại. Vui lòng liên hệ gia sư để đăng ký đợt kế tiếp."
                };
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
        }
        else if (dto.PreferredType != ClassroomType.OneToOne)
        {
            return new ConnectionRequestOperationResult { Success = false, Message = "Hình thức học không hợp lệ." };
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

                if (selectedSlots.Count == 0 && string.IsNullOrWhiteSpace(dto.Message))
                {
                    return new ConnectionRequestOperationResult
                    {
                        Success = false,
                        Message = "Vui lòng bấm chọn 1 khung giờ học thử trên lịch rảnh của gia sư hoặc nhập lời nhắn nêu rõ thời gian bạn mong muốn."
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

                if (selectedSlots.Count == 0 && string.IsNullOrWhiteSpace(dto.Message))
                {
                    return new ConnectionRequestOperationResult
                    {
                        Success = false,
                        Message = "Vui lòng chọn các khung giờ học cố định trong tuần (tối đa 3 buổi = 6 tiếng/tuần) từ lịch rảnh của gia sư."
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
            }

            // Kiểm tra tránh trùng lịch với các lớp học chính thức đang có của gia sư
            if (selectedSlots.Count > 0)
            {
                var activeClassroomWeeklySchedules = await _db.ClassroomWeeklySchedules
                    .AsNoTracking()
                    .Where(w => w.Classroom.TutorId == tutor.Id
                             && w.Classroom.Status == ClassroomStatus.Active)
                    .ToListAsync(cancellationToken);

                foreach (var slotUtc in selectedSlots)
                {
                    var slotVn = slotUtc.AddHours(VietnamUtcOffsetHours);
                    var slotDay = slotVn.DayOfWeek;
                    var slotStartTime = TimeOnly.FromTimeSpan(slotVn.TimeOfDay);
                    var slotEndTime = slotStartTime.AddHours(2);

                    var conflictSchedule = activeClassroomWeeklySchedules.FirstOrDefault(w =>
                        w.DayOfWeek == slotDay
                        && slotStartTime < w.EndTime
                        && w.StartTime < slotEndTime);

                    if (conflictSchedule != null)
                    {
                        var dayVn = ToVnDay(slotDay);
                        return new ConnectionRequestOperationResult
                        {
                            Success = false,
                            Message = $"Khung giờ {dayVn} {slotStartTime:HH\\:mm} – {slotEndTime:HH\\:mm} đã có lịch học lớp chính thức. Vui lòng chọn khung giờ khác."
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
                : "[Mục đích: Vào học chính thức luôn]";

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

        var notification = new Notification
        {
            UserId = tutor.UserId,
            Title = "Yêu cầu kết nối mới",
            Content = $"Học viên {studentName} vừa gửi cho bạn một yêu cầu {actionLabel} môn {subjectName}. [url:/Tutor/TutorConnectionRequest]",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        };

        _db.Notifications.Add(notification);
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

        // Phát realtime qua SignalR tới gia sư
        try
        {
            await _hubContext.Clients.User(tutor.UserId).SendAsync(
                "ConnectionRequestCreated",
                new
                {
                    requestId = connectionRequest.Id,
                    studentName = studentName,
                    subjectName = subjectName,
                    preferredType = dto.PreferredType.ToString(),
                    isTrial = isTrialRequest
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi SignalR ConnectionRequestCreated tới gia sư {TutorUserId}", tutor.UserId);
        }

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

        var strategy = _db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

            var studentUserId = request.Student?.UserId ?? request.Student?.User?.Id ?? "";
            var studentName = request.Student?.User?.FullName ?? "Học viên";
            var tutorName = request.Tutor?.User?.FullName ?? "Gia sư";
            var subjectName = request.Subject?.Name ?? "Môn học";

            int? finalClassroomId = null;
            int? finalSessionId = null;
            bool isTrial = false;
            bool enrolled = false;

            if (request.PreferredType == ClassroomType.Group)
            {
                // 1. LỚP NHÓM
                if (!request.ClassroomId.HasValue || request.Classroom == null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new AcceptConnectionRequestResult { InvalidClassroom = true, Message = "Lớp nhóm không còn tồn tại hoặc thông tin không khớp." };
                }

                var groupClass = request.Classroom;
                if (groupClass.Status != ClassroomStatus.Active || groupClass.TutorId != request.TutorId)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return new AcceptConnectionRequestResult { InvalidClassroom = true, Message = "Lớp nhóm hiện không hoạt động." };
                }

                var currentCount = await _db.ClassroomStudents.CountAsync(cs => cs.ClassroomId == groupClass.Id, cancellationToken);
                var alreadyInClass = await _db.ClassroomStudents.AnyAsync(cs => cs.ClassroomId == groupClass.Id && cs.StudentId == request.StudentId, cancellationToken);

                if (!alreadyInClass)
                {
                    if (currentCount >= groupClass.MaxStudents)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return new AcceptConnectionRequestResult { ClassroomFull = true, Message = "Lớp nhóm đã đủ sĩ số tối đa." };
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
                    ? $" Học phí tháng là {groupClass.Fee:N0} đ. Vui lòng quét mã QR SePay để hoàn tất kích hoạt quyền vào lớp."
                    : "";

                _db.Notifications.Add(new Notification
                {
                    UserId = studentUserId,
                    Title = "Đã được nhận vào lớp nhóm",
                    Content = $"Gia sư {tutorName} đã chấp nhận yêu cầu của bạn vào lớp {groupClass.NameClass}.{groupFeeNotice} [url:/Student_Parent/StudentClassroom]",
                    CreatedAt = DateTime.UtcNow,
                    IsRead = false
                });
            }
            else
            {
                // 2. LỚP 1 KÈM 1 (Phân định: Học thử 1 buổi VS Vào học chính thức)
                // Xác định xem yêu cầu là học thử hay học chính thức
                var message = request.Message ?? "";
                var isExplicitTrial = message.Contains("Đăng ký học thử") || message.Contains("học thử 1 buổi");
                var isExplicitOfficial = message.Contains("Vào học chính thức") || request.TrialSchedules.Count > 1;

                // Nếu có từ khóa học chính thức hoặc chọn nhiều hơn 1 buổi -> Vào thẳng lớp chính thức (không học thử)
                isTrial = !isExplicitOfficial && (isExplicitTrial || request.TrialSchedules.Count <= 1);

                if (isTrial)
                {
                    // 2.A: TẠO LỚP VÀ BUỔI HỌC THỬ 1 BUỔI TỰ ĐỘNG
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
                            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(VietnamUtcOffsetHours))
                        };
                        _db.Classrooms.Add(trialClassroom);
                        request.Classroom = trialClassroom;
                    }
                    else
                    {
                        trialClassroom.Status = ClassroomStatus.Active;
                    }

                    // Gán học viên vào lớp học thử
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

                    // Tạo buổi học thử (ClassSession) với phòng video call
                    var trialSlotUtc = request.ProposedTrialSlotUtc
                        ?? request.TrialSchedules.FirstOrDefault()?.ScheduledAt
                        ?? DateTime.UtcNow.AddDays(1);

                    var videoRoomId = $"OTSP-Trial-{request.Id}-{Guid.NewGuid():N}"[..28];
                    var trialSession = new ClassSession
                    {
                        Classroom = trialClassroom,
                        ScheduledAt = trialSlotUtc,
                        Status = SessionStatus.Scheduled,
                        VideoRoom = videoRoomId
                    };
                    _db.ClassSessions.Add(trialSession);

                    // Cập nhật trạng thái TrialSchedule
                    foreach (var ts in request.TrialSchedules)
                    {
                        ts.Status = ScheduleStatus.Confirmed;
                        ts.ClassSession = trialSession;
                    }

                    request.Status = RequestStatus.Accepted;
                    request.RespondedAt = DateTime.UtcNow;

                    finalClassroomId = trialClassroom.Id;

                    var vnTrialTime = trialSlotUtc.AddHours(VietnamUtcOffsetHours);
                    var timeStr = vnTrialTime.ToString("HH:mm, dddd dd/MM/yyyy", new System.Globalization.CultureInfo("vi-VN"));

                    _db.Notifications.Add(new Notification
                    {
                        UserId = studentUserId,
                        Title = "Gia sư đã đồng ý lịch học thử",
                        Content = $"Gia sư {tutorName} đã xác nhận buổi học thử môn {subjectName} vào lúc {timeStr}. Phòng học trực tuyến đã sẵn sàng! [url:/Student_Parent/StudentSchedule]",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
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

                    var sessionsCount = Math.Clamp(request.TrialSchedules.Count, 1, 3);
                    var monthlyFee = hourlyFee * 2.0m * sessionsCount * 4;

                    var officialClassroom = request.Classroom;
                    if (officialClassroom == null)
                    {
                        officialClassroom = new Classroom
                        {
                            NameClass = $"Lớp 1-1: {subjectName} - {studentName}",
                            Fee = monthlyFee,
                            Status = ClassroomStatus.Active,
                            Type = ClassroomType.OneToOne,
                            MaxStudents = 1,
                            SessionsPerWeek = sessionsCount,
                            HoursPerSession = 2.0,
                            TutorId = request.TutorId,
                            SubjectId = request.SubjectId,
                            GradeLevelId = request.GradeLevelId,
                            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddHours(VietnamUtcOffsetHours))
                        };
                        _db.Classrooms.Add(officialClassroom);
                        request.Classroom = officialClassroom;
                    }
                    else
                    {
                        officialClassroom.NameClass = $"Lớp 1-1: {subjectName} - {studentName}";
                        officialClassroom.Status = ClassroomStatus.Active;
                        officialClassroom.Fee = monthlyFee;
                        officialClassroom.SessionsPerWeek = sessionsCount;
                    }

                    // Thiết lập lịch cố định hàng tuần (ClassroomWeeklySchedule) từ các buổi học viên đã chọn
                    if (!officialClassroom.WeeklySchedules.Any())
                    {
                        int sessionIdx = 1;
                        foreach (var sched in request.TrialSchedules)
                        {
                            var vn = sched.ScheduledAt.AddHours(VietnamUtcOffsetHours);
                            var start = TimeOnly.FromTimeSpan(vn.TimeOfDay);
                            var end = start.AddHours(2);
                            officialClassroom.WeeklySchedules.Add(new ClassroomWeeklySchedule
                            {
                                Classroom = officialClassroom,
                                SessionIndex = sessionIdx++,
                                DayOfWeek = vn.DayOfWeek,
                                StartTime = start,
                                EndTime = end
                            });
                        }
                    }

                    // Đảm bảo học viên đã được ghi danh
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

                    // Tự động tạo bản ghi Payment cho lớp 1-1 chính thức
                    if (monthlyFee > 0)
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
                                Amount = monthlyFee,
                                Method = "VietQR",
                                Status = PaymentStatus.Pending
                            });
                        }
                    }

                    // Cập nhật trạng thái TrialSchedule thành Confirmed cho lớp chính thức
                    foreach (var ts in request.TrialSchedules)
                    {
                        ts.Status = ScheduleStatus.Confirmed;
                    }

                    request.Status = RequestStatus.Accepted;
                    request.RespondedAt = DateTime.UtcNow;

                    finalClassroomId = officialClassroom.Id;

                    _db.Notifications.Add(new Notification
                    {
                        UserId = studentUserId,
                        Title = "Yêu cầu học chính thức đã được chấp nhận",
                        Content = $"Gia sư {tutorName} đã chấp nhận vào học chính thức lớp {officialClassroom.NameClass}. Học phí tháng ({sessionsCount} buổi/tuần = {sessionsCount * 2 * 4} tiếng) là {monthlyFee:N0} đ. Vui lòng quét mã QR SePay để thanh toán kích hoạt quyền vào lớp! [url:/Student_Parent/StudentClassroom]",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
            }

            await _db.SaveChangesAsync(cancellationToken);

            // Tự động sinh lịch ClassSessions nếu là lớp chính thức có WeeklySchedules
            if (!isTrial && finalClassroomId.HasValue)
            {
                await GenerateUpcomingSessionsAsync(finalClassroomId.Value, 28, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);

            // Gửi SignalR thông báo trạng thái tức thì cho học viên
            try
            {
                await _hubContext.Clients.User(studentUserId).SendAsync(
                    "ConnectionRequestStatusChanged",
                    new
                    {
                        requestId = request.Id,
                        status = RequestStatus.Accepted.ToString(),
                        classroomId = finalClassroomId,
                        isTrial = isTrial
                    },
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Lỗi gửi SignalR ConnectionRequestStatusChanged tới học viên {StudentUserId}", studentUserId);
            }

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

        var reasonText = string.IsNullOrWhiteSpace(reason) ? "" : $" Lý do: {reason.Trim()}";

        _db.Notifications.Add(new Notification
        {
            UserId = studentUserId,
            Title = "Yêu cầu kết nối chưa được chấp nhận",
            Content = $"Gia sư {tutorName} hiện chưa thể nhận yêu cầu môn {subjectName} của bạn.{reasonText} [url:/Student_Parent/Send_Details_Tutor/MyRequests]",
            CreatedAt = DateTime.UtcNow,
            IsRead = false
        });

        await _db.SaveChangesAsync(cancellationToken);

        try
        {
            await _hubContext.Clients.User(studentUserId).SendAsync(
                "ConnectionRequestStatusChanged",
                new
                {
                    requestId = request.Id,
                    status = RequestStatus.Rejected.ToString()
                },
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Lỗi gửi SignalR ConnectionRequestStatusChanged (Rejected) tới học viên {StudentUserId}", studentUserId);
        }

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
            .FirstOrDefaultAsync(c => c.Id == classroomId, cancellationToken);

        if (classroom == null || !classroom.WeeklySchedules.Any())
            return;

        var nowUtc = DateTime.UtcNow;
        var todayVn = DateOnly.FromDateTime(nowUtc.AddHours(VietnamUtcOffsetHours));

        for (var dayOffset = 1; dayOffset <= daysAhead; dayOffset++)
        {
            var targetDateVn = todayVn.AddDays(dayOffset);
            var matchingSchedules = classroom.WeeklySchedules
                .Where(s => s.DayOfWeek == targetDateVn.DayOfWeek)
                .ToList();

            foreach (var sched in matchingSchedules)
            {
                var sessionVn = targetDateVn.ToDateTime(sched.StartTime);
                var sessionUtc = DateTime.SpecifyKind(sessionVn.AddHours(-VietnamUtcOffsetHours), DateTimeKind.Utc);

                var windowStart = sessionUtc.AddMinutes(-30);
                var windowEnd = sessionUtc.AddMinutes(30);
                var exists = await _db.ClassSessions.AnyAsync(s =>
                    s.ClassroomId == classroomId && s.ScheduledAt >= windowStart && s.ScheduledAt <= windowEnd, cancellationToken);

                if (!exists)
                {
                    _db.ClassSessions.Add(new ClassSession
                    {
                        ClassroomId = classroomId,
                        ScheduledAt = sessionUtc,
                        Status = SessionStatus.Scheduled,
                        VideoRoom = $"OTSP-Room-{classroomId}-{Guid.NewGuid():N}"[..28]
                    });
                }
            }
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
