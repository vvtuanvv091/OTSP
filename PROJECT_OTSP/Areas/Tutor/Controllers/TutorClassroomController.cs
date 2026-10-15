using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using PROJECT_OTSP.Data;
using PROJECT_OTSP.Dtos;
using PROJECT_OTSP.Models;
using PROJECT_OTSP.Services;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Areas.Tutor.Controllers;

[Area("Tutor")]
[Authorize(Roles = "Tutor")]
public sealed class TutorClassroomController : TutorAreaController
{
    private const long MaxImageBytes = 5 * 1024 * 1024;
    private const long MaxRequestBytes = MaxImageBytes + 1024 * 1024;
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<TutorClassroomController> _logger;
    private readonly IImageStorageService _imageStorage;

    public TutorClassroomController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IWebHostEnvironment environment,
        ILogger<TutorClassroomController> logger,
        IImageStorageService imageStorage)
    {
        _db = db;
        _userManager = userManager;
        _environment = environment;
        _logger = logger;
        _imageStorage = imageStorage;
    }

    [HttpGet]
    public async Task<IActionResult> Index(ClassroomStatus? status, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId is null)
            return Forbid();

        if (status.HasValue && !Enum.IsDefined(typeof(ClassroomStatus), status.Value))
            status = null;

        var query = _db.Classrooms
            .AsNoTracking()
            .Where(classroom => classroom.TutorId == tutorId.Value && classroom.Type == ClassroomType.Group);

        if (status.HasValue)
            query = query.Where(classroom => classroom.Status == status.Value);

        var classrooms = await query
            .OrderByDescending(classroom => classroom.Status == ClassroomStatus.Active)
            .ThenByDescending(classroom => classroom.Id)
            .Select(classroom => new ClassroomListItemVm
            {
                Id = classroom.Id,
                Name = classroom.Name,
                SubjectName = classroom.Subject.Name,
                GradeLevelName = classroom.GradeLevel.Name,
                CoverImageUrl = classroom.CoverImageUrl,
                Fee = classroom.Fee,
                StudentCount = classroom.ClassroomStudents.Count,
                MaxStudents = classroom.MaxStudents,
                Status = classroom.Status,
                StartDate = classroom.StartDate,
                CanHardDelete = !classroom.ClassroomStudents.Any()
                    && !classroom.ConnectionRequests.Any()
                    && !classroom.ClassSessions.Any()
                    && !classroom.Payments.Any(),
                HasSuccessfulPayment = classroom.Payments.Any(payment => payment.Status == PaymentStatus.Success)
            })
            .ToListAsync(cancellationToken);

        ViewBag.SelectedStatus = status;
        return RenderIndex(classrooms);
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId is null)
            return Forbid();

        var model = new ClassroomFormVm();
        await PopulateSubjectGradeOptionsAsync(model, tutorId.Value, cancellationToken);
        if (model.SubjectGradeOptions.Count == 0)
        {
            TempData["Error"] = "Bạn cần khai báo môn học và cấp học giảng dạy trước khi tạo lớp.";
            return RedirectToAction("Index", "TutorSubject");
        }

        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Create(ClassroomFormVm model, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId is null)
            return Forbid();

        await PopulateSubjectGradeOptionsAsync(model, tutorId.Value, cancellationToken);
        if (model.SubjectGradeOptions.Count == 0)
        {
            TempData["Error"] = "Bạn cần khai báo môn học và cấp học giảng dạy trước khi tạo lớp.";
            return RedirectToAction("Index", "TutorSubject");
        }

        if (!TryParseSubjectGradeKey(model.SubjectGradeKey, out var subjectId, out var gradeLevelId)
            || !await TutorSubjectPairExistsAsync(tutorId.Value, subjectId, gradeLevelId, cancellationToken))
        {
            ModelState.AddModelError(nameof(model.SubjectGradeKey), "Môn học hoặc cấp học không thuộc danh sách bạn đăng ký giảng dạy.");
        }

        await ValidateCoverImageInputAsync(model, cancellationToken);

        if (!ModelState.IsValid)
            return View(model);

        var coverImageUrl = model.CoverImageUrl?.Trim();
        StoredImage? uploadedImage = null;
        if (model.CoverImageFile is { Length: > 0 } imageFile)
        {
            uploadedImage = await _imageStorage.UploadClassroomCoverAsync(imageFile, cancellationToken);
            coverImageUrl = uploadedImage.Url;
        }

        var classroom = new Classroom
        {
            Name = model.Name.Trim(),
            Description = model.Description?.Trim(),
            Fee = model.Fee,
            MaxStudents = model.MaxStudents,
            StartDate = model.StartDate,
            CoverImageUrl = coverImageUrl,
            CoverImagePublicId = uploadedImage?.PublicId,
            SubjectId = subjectId,
            GradeLevelId = gradeLevelId,
            Type = ClassroomType.Group,
            Status = ClassroomStatus.Active,
            TutorId = tutorId.Value
        };

        try
        {
            _db.Classrooms.Add(classroom);
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch
        {
            if (uploadedImage is not null)
                await TryDeleteUploadedImageAsync(uploadedImage.PublicId);
            throw;
        }

        TempData["Success"] = "Đã tạo lớp nhóm.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId is null)
            return Forbid();

        var classroom = await GetOwnedGroupClassroomAsync(id, tutorId.Value, cancellationToken);
        if (classroom is null)
            return NotFound();

        if (IsImmutableStatus(classroom.Status))
        {
            TempData["Error"] = "Không thể sửa lớp đã hủy hoặc hoàn thành.";
            return RedirectToAction(nameof(Index));
        }

        var model = new ClassroomFormVm
        {
            Id = classroom.Id,
            Name = classroom.Name,
            Description = classroom.Description,
            Fee = classroom.Fee,
            MaxStudents = classroom.MaxStudents,
            StartDate = classroom.StartDate,
            CoverImageUrl = GetRemoteImageUrl(classroom.CoverImageUrl),
            CurrentCoverImageUrl = classroom.CoverImageUrl,
            SubjectGradeKey = $"{classroom.SubjectId}_{classroom.GradeLevelId}"
        };
        await PopulateEditStateAsync(model, classroom, tutorId.Value, cancellationToken);
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Edit(int id, ClassroomFormVm model, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId is null)
            return Forbid();

        if (id != model.Id)
            return NotFound();

        var classroom = await GetOwnedGroupClassroomAsync(id, tutorId.Value, cancellationToken);
        if (classroom is null)
            return NotFound();

        if (IsImmutableStatus(classroom.Status))
        {
            TempData["Error"] = "Không thể sửa lớp đã hủy hoặc hoàn thành.";
            return RedirectToAction(nameof(Index));
        }

        var currentXmin = _db.Entry(classroom).Property<uint>("xmin").CurrentValue;
        if (currentXmin != model.RowVersion)
        {
            TempData["Error"] = "Dữ liệu lớp đã thay đổi. Vui lòng mở lại trang sửa và thử lại.";
            return RedirectToAction(nameof(Edit), new { id });
        }

        await PopulateSubjectGradeOptionsAsync(model, tutorId.Value, cancellationToken);

        var pairParsed = TryParseSubjectGradeKey(model.SubjectGradeKey, out var subjectId, out var gradeLevelId);
        var pairOwned = pairParsed && await TutorSubjectPairExistsAsync(
            tutorId.Value, subjectId, gradeLevelId, cancellationToken);
        if (!pairOwned)
            ModelState.AddModelError(nameof(model.SubjectGradeKey), "Môn học hoặc cấp học không thuộc danh sách bạn đăng ký giảng dạy.");

        var studentCount = await _db.ClassroomStudents.CountAsync(
            student => student.ClassroomId == classroom.Id, cancellationToken);
        if (model.MaxStudents < studentCount)
            ModelState.AddModelError(nameof(model.MaxStudents), $"Sĩ số tối đa không được nhỏ hơn {studentCount} học viên hiện có.");

        var hasSuccessfulPayment = await _db.Payments.AnyAsync(
            payment => payment.ClassroomId == classroom.Id && payment.Status == PaymentStatus.Success,
            cancellationToken);
        if (hasSuccessfulPayment && model.Fee != classroom.Fee)
            ModelState.AddModelError(nameof(model.Fee), "Không thể sửa học phí vì lớp đã có giao dịch thanh toán thành công.");

        var hasStudents = studentCount > 0;
        var hasConnectionRequests = await _db.ConnectionRequests.AnyAsync(
            request => request.ClassroomId == classroom.Id, cancellationToken);
        var subjectGradeChanged = pairParsed
            && (subjectId != classroom.SubjectId || gradeLevelId != classroom.GradeLevelId);
        if (subjectGradeChanged && (hasStudents || hasConnectionRequests))
            ModelState.AddModelError(nameof(model.SubjectGradeKey), "Không thể đổi môn/cấp học khi lớp đã có học viên hoặc yêu cầu kết nối.");

        await ValidateCoverImageInputAsync(model, cancellationToken);

        if (!ModelState.IsValid)
        {
            await PopulateEditStateAsync(model, classroom, tutorId.Value, cancellationToken);
            return View(model);
        }

        var previousCoverImageUrl = classroom.CoverImageUrl;
        var previousCoverImagePublicId = classroom.CoverImagePublicId;
        StoredImage? uploadedImage = null;
        if (model.CoverImageFile is { Length: > 0 } imageFile)
            uploadedImage = await _imageStorage.UploadClassroomCoverAsync(imageFile, cancellationToken);

        classroom.Name = model.Name.Trim();
        classroom.Description = model.Description?.Trim();
        classroom.StartDate = model.StartDate;
        classroom.MaxStudents = model.MaxStudents;
        classroom.Fee = model.Fee;
        classroom.CoverImageUrl = uploadedImage?.Url
            ?? (model.RemoveCoverImage
                ? null
                : string.IsNullOrWhiteSpace(model.CoverImageUrl)
                    ? previousCoverImageUrl
                    : model.CoverImageUrl.Trim());
        classroom.CoverImagePublicId = uploadedImage?.PublicId
            ?? (model.RemoveCoverImage
                || !string.Equals(classroom.CoverImageUrl, previousCoverImageUrl, StringComparison.Ordinal)
                    ? null
                    : previousCoverImagePublicId);
        if (subjectGradeChanged)
        {
            classroom.SubjectId = subjectId;
            classroom.GradeLevelId = gradeLevelId;
        }

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            if (uploadedImage is not null)
                await TryDeleteUploadedImageAsync(uploadedImage.PublicId);
            TempData["Error"] = "Dữ liệu lớp vừa được thay đổi ở nơi khác. Vui lòng tải lại trước khi lưu.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        catch
        {
            if (uploadedImage is not null)
                await TryDeleteUploadedImageAsync(uploadedImage.PublicId);
            throw;
        }

        if (!string.Equals(previousCoverImageUrl, classroom.CoverImageUrl, StringComparison.Ordinal)
            || !string.Equals(previousCoverImagePublicId, classroom.CoverImagePublicId, StringComparison.Ordinal))
        {
            await DeleteCoverImageAsync(previousCoverImageUrl, previousCoverImagePublicId);
        }

        TempData["Success"] = "Đã cập nhật lớp nhóm.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id, CancellationToken cancellationToken)
    {
        var tutorId = await GetApprovedTutorIdAsync(cancellationToken);
        if (tutorId is null)
            return Forbid();

        var result = new CancelResult();
        try
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            await strategy.ExecuteAsync(async () =>
            {
                _db.ChangeTracker.Clear();
                await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
                var classroom = await _db.Classrooms.FirstOrDefaultAsync(item =>
                    item.Id == id && item.TutorId == tutorId.Value && item.Type == ClassroomType.Group,
                    cancellationToken);

                if (classroom is null)
                {
                    result.NotFound = !result.Deleted;
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                if (classroom.Status == ClassroomStatus.Completed)
                {
                    result.Completed = true;
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                if (classroom.Status == ClassroomStatus.Cancelled)
                {
                    result.Cancelled = true;
                    result.HasSuccessfulPayment = await _db.Payments.AnyAsync(
                        payment => payment.ClassroomId == classroom.Id && payment.Status == PaymentStatus.Success,
                        cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                var hasStudents = await _db.ClassroomStudents.AnyAsync(
                    student => student.ClassroomId == classroom.Id, cancellationToken);
                var hasRequests = await _db.ConnectionRequests.AnyAsync(
                    request => request.ClassroomId == classroom.Id, cancellationToken);
                var hasSessions = await _db.ClassSessions.AnyAsync(
                    session => session.ClassroomId == classroom.Id, cancellationToken);
                var hasPayments = await _db.Payments.AnyAsync(
                    payment => payment.ClassroomId == classroom.Id, cancellationToken);
                result.HasSuccessfulPayment = await _db.Payments.AnyAsync(
                    payment => payment.ClassroomId == classroom.Id && payment.Status == PaymentStatus.Success,
                    cancellationToken);

                if (!hasStudents && !hasRequests && !hasSessions && !hasPayments)
                {
                    result.DeletedCoverImageUrl = classroom.CoverImageUrl;
                    result.DeletedCoverImagePublicId = classroom.CoverImagePublicId;
                    _db.Classrooms.Remove(classroom);
                    await _db.SaveChangesAsync(cancellationToken);
                    result.Deleted = true;
                    await transaction.CommitAsync(cancellationToken);
                    return;
                }

                var nowUtc = DateTime.UtcNow;
                classroom.Status = ClassroomStatus.Cancelled;
                await _db.SaveChangesAsync(cancellationToken);

                await _db.ConnectionRequests
                    .Where(request => request.ClassroomId == classroom.Id && request.Status == RequestStatus.Pending)
                    .ExecuteUpdateAsync(update => update
                        .SetProperty(request => request.Status, RequestStatus.Cancelled)
                        .SetProperty(request => request.RespondedAt, nowUtc), cancellationToken);

                var studentUserIds = await _db.ClassroomStudents
                    .AsNoTracking()
                    .Where(student => student.ClassroomId == classroom.Id)
                    .Select(student => student.Student.UserId)
                    .Distinct()
                    .ToListAsync(cancellationToken);

                foreach (var studentUserId in studentUserIds)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserId = studentUserId,
                        Title = "Lớp học đã bị hủy",
                        Content = $"Lớp \"{classroom.Name}\" đã được gia sư hủy. Vui lòng liên hệ gia sư để biết thêm thông tin.",
                        CreatedAt = nowUtc,
                        IsRead = false
                    });
                }

                await _db.SaveChangesAsync(cancellationToken);
                result.Cancelled = true;
                await transaction.CommitAsync(cancellationToken);
            });
        }
        catch (DbUpdateConcurrencyException)
        {
            TempData["Error"] = "Lớp vừa được thay đổi ở nơi khác. Hãy tải lại danh sách trước khi thao tác.";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            TempData["Error"] = "Không thể xóa lớp vì dữ liệu liên quan vừa được thêm. Vui lòng thử hủy lớp thay vì xóa.";
            return RedirectToAction(nameof(Index));
        }

        if (result.NotFound)
            return NotFound();
        if (result.Deleted)
            await DeleteCoverImageAsync(result.DeletedCoverImageUrl, result.DeletedCoverImagePublicId);
        if (result.Completed)
            TempData["Error"] = "Không thể hủy lớp đã hoàn thành.";
        else if (result.Deleted)
            TempData["Success"] = "Đã xóa lớp nhóm vì chưa có dữ liệu liên quan.";
        else if (result.Cancelled && result.HasSuccessfulPayment)
            TempData["Warning"] = "Đã hủy lớp. Lớp có giao dịch thanh toán thành công; hệ thống không tự hoàn tiền, vui lòng xử lý hoàn tiền thủ công.";
        else if (result.Cancelled)
            TempData["Success"] = "Đã hủy lớp nhóm và thông báo cho học viên.";

        return RedirectToAction(nameof(Index));
    }

    private async Task<int?> GetApprovedTutorIdAsync(CancellationToken cancellationToken)
    {
        var userId = _userManager.GetUserId(User);
        if (string.IsNullOrWhiteSpace(userId))
            return null;

        return await _db.TutorProfiles
            .AsNoTracking()
            .Where(tutor => tutor.UserId == userId && tutor.VerificationStatus == VerificationStatus.Approved)
            .Select(tutor => (int?)tutor.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    private Task<Classroom?> GetOwnedGroupClassroomAsync(
        int classroomId,
        int tutorId,
        CancellationToken cancellationToken)
    {
        return _db.Classrooms
            .Include(classroom => classroom.Subject)
            .Include(classroom => classroom.GradeLevel)
            .FirstOrDefaultAsync(classroom =>
                classroom.Id == classroomId
                && classroom.TutorId == tutorId
                && classroom.Type == ClassroomType.Group,
                cancellationToken);
    }

    private Task<List<SelectListItem>> GetSubjectGradeOptionsAsync(int tutorId, CancellationToken cancellationToken)
    {
        return _db.TutorSubjects
            .AsNoTracking()
            .Where(pair => pair.TutorId == tutorId)
            .OrderBy(pair => pair.Subject.Name)
            .ThenBy(pair => pair.GradeLevel.Name)
            .Select(pair => new SelectListItem
            {
                Value = pair.SubjectId + "_" + pair.GradeLevelId,
                Text = pair.Subject.Name + " - " + pair.GradeLevel.Name
            })
            .ToListAsync(cancellationToken);
    }

    private async Task PopulateSubjectGradeOptionsAsync(
        ClassroomFormVm model,
        int tutorId,
        CancellationToken cancellationToken)
    {
        model.SubjectGradeOptions = await GetSubjectGradeOptionsAsync(tutorId, cancellationToken);
    }

    private async Task PopulateEditStateAsync(
        ClassroomFormVm model,
        Classroom classroom,
        int tutorId,
        CancellationToken cancellationToken)
    {
        await PopulateSubjectGradeOptionsAsync(model, tutorId, cancellationToken);
        var hasStudents = await _db.ClassroomStudents.AnyAsync(
            student => student.ClassroomId == classroom.Id, cancellationToken);
        var hasRequests = await _db.ConnectionRequests.AnyAsync(
            request => request.ClassroomId == classroom.Id, cancellationToken);
        model.CanChangeSubjectGrade = !hasStudents && !hasRequests;
        model.CanChangeFee = !await _db.Payments.AnyAsync(
            payment => payment.ClassroomId == classroom.Id && payment.Status == PaymentStatus.Success,
            cancellationToken);
        model.SubjectGradeLabel = $"{classroom.Subject.Name} - {classroom.GradeLevel.Name}";
        model.CurrentCoverImageUrl = classroom.CoverImageUrl;
        if (string.IsNullOrWhiteSpace(model.CoverImageUrl))
            model.CoverImageUrl = GetRemoteImageUrl(classroom.CoverImageUrl);
        model.RowVersion = _db.Entry(classroom).Property<uint>("xmin").CurrentValue;
    }

    private async Task ValidateCoverImageInputAsync(ClassroomFormVm model, CancellationToken cancellationToken)
    {
        if (model.CoverImageFile is { } file)
        {
            if (file.Length == 0 || file.Length > MaxImageBytes
                || !await HasSupportedImageSignatureAsync(file, cancellationToken))
            {
                ModelState.AddModelError(nameof(model.CoverImageFile), "Ảnh phải là JPG, PNG hoặc WebP và không vượt quá 5 MB.");
            }

            return;
        }

        if (!model.RemoveCoverImage
            && !string.IsNullOrWhiteSpace(model.CoverImageUrl)
            && !IsRemoteImageUrl(model.CoverImageUrl))
        {
            ModelState.AddModelError(nameof(model.CoverImageUrl), "Vui lòng nhập URL ảnh bắt đầu bằng http:// hoặc https://.");
        }
    }

    private static async Task<bool> HasSupportedImageSignatureAsync(IFormFile file, CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var header = new byte[12];
        await using var stream = file.OpenReadStream();
        var read = 0;
        while (read < header.Length)
        {
            var count = await stream.ReadAsync(header.AsMemory(read), cancellationToken);
            if (count == 0)
                break;
            read += count;
        }

        var isJpeg = (extension is ".jpg" or ".jpeg")
            && read >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
        var isPng = extension == ".png"
            && read >= 8
            && header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47
            && header[4] == 0x0D && header[5] == 0x0A && header[6] == 0x1A && header[7] == 0x0A;
        var isWebp = extension == ".webp"
            && read >= 12
            && header[0] == (byte)'R' && header[1] == (byte)'I' && header[2] == (byte)'F' && header[3] == (byte)'F'
            && header[8] == (byte)'W' && header[9] == (byte)'E' && header[10] == (byte)'B' && header[11] == (byte)'P';

        return isJpeg || isPng || isWebp;
    }

    private string GetCoverImageFolder()
    {
        var webRoot = _environment.WebRootPath
            ?? Path.Combine(_environment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "uploads", "classrooms");
    }

    private void DeleteManagedCoverImage(string? imageUrl)
    {
        const string managedPrefix = "/uploads/classrooms/";
        if (string.IsNullOrWhiteSpace(imageUrl) || !imageUrl.StartsWith(managedPrefix, StringComparison.Ordinal))
            return;

        var storedName = imageUrl[managedPrefix.Length..];
        if (string.IsNullOrWhiteSpace(storedName) || Path.GetFileName(storedName) != storedName)
            return;

        var fullPath = Path.Combine(GetCoverImageFolder(), storedName);
        try
        {
            if (System.IO.File.Exists(fullPath))
                System.IO.File.Delete(fullPath);
        }
        catch (IOException exception)
        {
            _logger.LogWarning(exception, "Unable to delete managed classroom image {ImagePath}", fullPath);
        }
        catch (UnauthorizedAccessException exception)
        {
            _logger.LogWarning(exception, "Unable to delete managed classroom image {ImagePath}", fullPath);
        }
    }

    private async Task DeleteCoverImageAsync(string? imageUrl, string? publicId)
    {
        if (!string.IsNullOrWhiteSpace(publicId))
        {
            await TryDeleteUploadedImageAsync(publicId);
            return;
        }

        DeleteManagedCoverImage(imageUrl);
    }

    private async Task TryDeleteUploadedImageAsync(string publicId)
    {
        try
        {
            await _imageStorage.DeleteAsync(publicId, CancellationToken.None);
        }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Unable to delete classroom image {PublicId} from image storage", publicId);
        }
    }

    private static bool IsRemoteImageUrl(string value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static string? GetRemoteImageUrl(string? value)
        => !string.IsNullOrWhiteSpace(value) && IsRemoteImageUrl(value) ? value : null;

    private Task<bool> TutorSubjectPairExistsAsync(
        int tutorId,
        int subjectId,
        int gradeLevelId,
        CancellationToken cancellationToken)
    {
        return _db.TutorSubjects.AsNoTracking().AnyAsync(pair =>
            pair.TutorId == tutorId
            && pair.SubjectId == subjectId
            && pair.GradeLevelId == gradeLevelId,
            cancellationToken);
    }

    private static bool TryParseSubjectGradeKey(string? value, out int subjectId, out int gradeLevelId)
    {
        subjectId = 0;
        gradeLevelId = 0;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var parts = value.Split('_', StringSplitOptions.TrimEntries);
        return parts.Length == 2
            && int.TryParse(parts[0], out subjectId)
            && int.TryParse(parts[1], out gradeLevelId)
            && subjectId > 0
            && gradeLevelId > 0;
    }

    private static bool IsImmutableStatus(ClassroomStatus status)
        => status is ClassroomStatus.Cancelled or ClassroomStatus.Completed;

    private sealed class CancelResult
    {
        public bool NotFound { get; set; }
        public bool Deleted { get; set; }
        public bool Cancelled { get; set; }
        public bool Completed { get; set; }
        public bool HasSuccessfulPayment { get; set; }
        public string? DeletedCoverImageUrl { get; set; }
        public string? DeletedCoverImagePublicId { get; set; }
    }
}
