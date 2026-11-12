using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Data;
using Backend_Project_OTSP.Dtos.Auth;
using Backend_Project_OTSP.Dtos.Common.Notification;
using Backend_Project_OTSP.Models;
using Backend_Project_OTSP.Services.Common;
using Backend_Project_OTSP.Services.Notifications;
using static Backend_Project_OTSP.Models.Enums;
using Backend_Project_OTSP.Services.Auth;

namespace Backend_Project_OTSP.Services.Auth;

public class AuthService : IAuthService
{
    private const string RoleStudent = "Student_Parent";
    private const string RoleTutor = "Tutor";

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<ApplicationRole> _roleManager;
    private readonly ApplicationDbContext _db;
    private readonly IJwtService _jwtService;
    private readonly IEmailSenderService _emailSender;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthService> _logger;
    private readonly GoogleIdTokenValidator _googleIdTokenValidator;
    private readonly IHttpContextAccessor? _httpContextAccessor;
    private readonly IEmailQueue? _emailQueue;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<ApplicationRole> roleManager,
        ApplicationDbContext db,
        IJwtService jwtService,
        IEmailSenderService emailSender,
        IConfiguration configuration,
        GoogleIdTokenValidator googleIdTokenValidator,
        ILogger<AuthService> logger,
        IHttpContextAccessor? httpContextAccessor = null,
        IEmailQueue? emailQueue = null)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
        _jwtService = jwtService;
        _emailSender = emailSender;
        _configuration = configuration;
        _googleIdTokenValidator = googleIdTokenValidator;
        _logger = logger;
        _httpContextAccessor = httpContextAccessor;
        _emailQueue = emailQueue;
    }

    private async Task RecordAuthAuditAsync(string userId, string action, string detail)
    {
        try
        {
            var http = _httpContextAccessor?.HttpContext;
            var ua = http?.Request.Headers.UserAgent.ToString();
            if (!string.IsNullOrEmpty(ua) && ua.Length > 480) ua = ua[..480];
            var ip = http?.Connection.RemoteIpAddress?.ToString();
            if (!string.IsNullOrEmpty(ip) && ip.Length > 480) ip = ip[..480];

            _db.AuditLogs.Add(new AuditLog
            {
                UserId = userId,
                Category = "Auth",
                Action = action,
                Detail = detail,
                IpAddress = ip,
                UserAgent = ua,
                Timestamp = DateTime.UtcNow
            });
            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _db.ChangeTracker.Clear();
            _logger.LogWarning(ex, "Không thể ghi Auth AuditLog cho user {UserId}", userId);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, AuthResponse? Data)> LoginAsync(LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return (false, "Vui lòng nhập đầy đủ Email và Mật khẩu.", null);
        }

        var emailOrName = request.Email.Trim();
        var user = await _userManager.FindByEmailAsync(emailOrName)
                   ?? await _userManager.FindByNameAsync(emailOrName);
        if (user == null)
        {
            return (false, "Email hoặc mật khẩu không chính xác.", null);
        }

        if (user.Status == AccountStatus.Locked)
        {
            return (false, "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.", null);
        }

        var checkPassword = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (checkPassword.IsLockedOut)
        {
            return (false, "Tài khoản tạm thời bị khóa do đăng nhập sai quá nhiều lần. Vui lòng thử lại sau ít phút.", null);
        }
        if (!checkPassword.Succeeded)
        {
            return (false, "Email hoặc mật khẩu không chính xác.", null);
        }

        var authResponse = await _jwtService.GenerateAuthResponseAsync(user, request.RememberMe);
        await RecordAuthAuditAsync(user.Id, "Đăng nhập hệ thống", $"Đăng nhập bằng Email: {user.Email}");
        return (true, null, authResponse);
    }

    public async Task<(bool Success, string? ErrorMessage, AuthResponse? Data)> GoogleLoginAsync(GoogleLoginRequest request)
    {
        GoogleIdentity? googleIdentity;
        try
        {
            googleIdentity = await _googleIdTokenValidator.ValidateAsync(request.Credential);
        }
        catch (InvalidOperationException exception)
        {
            return (false, exception.Message, null);
        }
        catch (HttpRequestException)
        {
            return (false, "Không thể xác minh với Google lúc này. Vui lòng thử lại.", null);
        }
        if (googleIdentity == null) return (false, "Thông tin xác thực Google không hợp lệ hoặc đã hết hạn.", null);

        var email = googleIdentity.Email.Trim();
        var user = await _userManager.FindByEmailAsync(email)
                   ?? await _userManager.FindByNameAsync(email);

        if (user != null)
        {
            if (user.Status == AccountStatus.Locked)
            {
                return (false, "Tài khoản của bạn đã bị khóa. Vui lòng liên hệ quản trị viên.", null);
            }

            if (string.IsNullOrWhiteSpace(user.AvatarUrl) && !string.IsNullOrWhiteSpace(googleIdentity.AvatarUrl))
            {
                user.AvatarUrl = googleIdentity.AvatarUrl;
                await _userManager.UpdateAsync(user);
            }

            var authResp = await _jwtService.GenerateAuthResponseAsync(user, rememberMe: true);
            await RecordAuthAuditAsync(user.Id, "Đăng nhập Google", $"Đăng nhập Google: {user.Email}");
            return (true, null, authResp);
        }

        // Người dùng Google mới: mặc định Học viên / Phụ huynh nếu chưa truyền role
        var role = string.IsNullOrWhiteSpace(request.Role) ? RoleStudent : request.Role.Trim();
        if (role != RoleStudent && role != RoleTutor)
        {
            return (false, "Vui lòng chọn vai trò: Học viên / Phụ huynh hoặc Gia sư để tiếp tục.", null);
        }

        user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            EmailConfirmed = true,
            FullName = googleIdentity.FullName,
            AvatarUrl = googleIdentity.AvatarUrl,
            Status = AccountStatus.Active
        };

        var tempPassword = $"Gg!{Guid.NewGuid():N}A1#";
        var createResult = await _userManager.CreateAsync(user, tempPassword);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return (false, errors, null);
        }

        try
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole { Name = role });
            }
            await _userManager.AddToRoleAsync(user, role);

            if (role == RoleTutor)
            {
                _db.TutorProfiles.Add(new TutorProfile
                {
                    UserId = user.Id,
                    Introduction = "Gia sư mới gia nhập qua Google",
                    HourlyFee = 0,
                    TeachingFormat = TeachingFormat.Online,
                    VerificationStatus = VerificationStatus.Pending
                });
            }
            else
            {
                _db.StudentProfiles.Add(new StudentProfile
                {
                    UserId = user.Id,
                    Area = "Hệ thống trực tuyến"
                });
            }

            await _db.SaveChangesAsync();

            try
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                foreach (var admin in adminUsers)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserId = admin.Id,
                        Title = role == RoleTutor ? "Gia sư mới đăng ký (Google)" : "Học viên mới đăng ký (Google)",
                        Content = role == RoleTutor
                            ? $"Gia sư {user.FullName} ({user.Email}) vừa tạo tài khoản qua Google và đang chờ duyệt hồ sơ."
                            : $"Học viên {user.FullName} ({user.Email}) vừa tạo tài khoản qua Google.",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
                if (adminUsers.Count > 0)
                {
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception notifEx)
            {
                _db.ChangeTracker.Clear();
                _logger.LogWarning(notifEx, "Không thể tạo thông báo Admin khi đăng ký Google cho {Email}", email);
            }

            var authResponse = await _jwtService.GenerateAuthResponseAsync(user, rememberMe: true);
            await RecordAuthAuditAsync(user.Id, "Đăng ký Google", $"Tạo tài khoản mới qua Google ({role}): {user.Email}");
            return (true, null, authResponse);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi tạo hồ sơ cho người dùng Google: {Email}", email);
            await _userManager.DeleteAsync(user);
            return (false, "Lỗi hệ thống khi thiết lập tài khoản Google.", null);
        }
    }

    public async Task<(bool Success, string? ErrorMessage, AuthResponse? Data)> RegisterAsync(RegisterRequest request)
    {
        var role = string.IsNullOrWhiteSpace(request.Role) ? RoleStudent : request.Role.Trim();
        if (role != RoleStudent && role != RoleTutor)
        {
            return (false, "Vai trò không hợp lệ. Chỉ chấp nhận 'Student_Parent' hoặc 'Tutor'.", null);
        }

        if (!request.AcceptTerms)
        {
            return (false, "Bạn cần đồng ý với Điều khoản sử dụng.", null);
        }

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return (false, "Vui lòng nhập đầy đủ Email và Mật khẩu.", null);
        }

        if (request.Password.Length < 6)
        {
            return (false, "Mật khẩu phải có ít nhất 6 ký tự.", null);
        }

        if (!string.IsNullOrWhiteSpace(request.ConfirmPassword) && request.Password != request.ConfirmPassword)
        {
            return (false, "Mật khẩu và xác nhận mật khẩu không khớp.", null);
        }

        var citizenId = string.IsNullOrWhiteSpace(request.CitizenIdNumber) ? null : request.CitizenIdNumber.Trim();
        if (citizenId != null && !System.Text.RegularExpressions.Regex.IsMatch(citizenId, "^[0-9]{12}$"))
        {
            return (false, "Số CCCD phải gồm đúng 12 chữ số (hoặc để trống nếu chưa có).", null);
        }

        DateOnly? dob = null;
        if (!string.IsNullOrWhiteSpace(request.DateOfBirth))
        {
            if (DateOnly.TryParse(request.DateOfBirth, out var parsedDob))
            {
                dob = parsedDob;
            }
            else
            {
                return (false, "Ngày sinh không đúng định dạng (yyyy-MM-dd).", null);
            }
        }

        int? age = null;
        if (dob.HasValue)
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var a = today.Year - dob.Value.Year;
            if (dob.Value > today.AddYears(-a)) a--;
            age = a;
        }

        var isMinor = age.HasValue && age.Value < 18;

        if (role == RoleTutor && isMinor)
        {
            return (false, "Gia sư phải từ 18 tuổi trở lên.", null);
        }

        var guardianFullName = request.GuardianFullName?.Trim();
        var guardianContact = request.GuardianContact?.Trim();
        if (role == RoleStudent && isMinor)
        {
            if (string.IsNullOrWhiteSpace(guardianFullName))
                guardianFullName = $"Phụ huynh của {request.FullName?.Trim()}";
            if (string.IsNullOrWhiteSpace(guardianContact))
                guardianContact = !string.IsNullOrWhiteSpace(request.PhoneNumber) ? request.PhoneNumber.Trim() : request.Email.Trim();
        }

        var normalizedEmail = request.Email.Trim();
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail)
                           ?? await _userManager.FindByNameAsync(normalizedEmail);
        if (existingUser != null)
        {
            return (false, "Email này đã được sử dụng. Vui lòng đăng nhập hoặc dùng email khác.", null);
        }

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = request.FullName?.Trim() ?? string.Empty,
            PhoneNumber = string.IsNullOrWhiteSpace(request.PhoneNumber) ? null : request.PhoneNumber.Trim(),
            DateOfBirth = dob,
            Address = request.Area?.Trim() ?? request.ResidenceAddress?.Trim(),
            CitizenIdNumber = citizenId,
            ResidenceAddress = string.IsNullOrWhiteSpace(request.ResidenceAddress) ? null : request.ResidenceAddress.Trim()
        };

        var createResult = await _userManager.CreateAsync(user, request.Password);
        if (!createResult.Succeeded)
        {
            var errors = string.Join("; ", createResult.Errors.Select(e => e.Description));
            return (false, errors, null);
        }

        try
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                await _roleManager.CreateAsync(new ApplicationRole { Name = role });
            }

            await _userManager.AddToRoleAsync(user, role);

            if (role == RoleTutor)
            {
                _db.TutorProfiles.Add(new TutorProfile
                {
                    UserId = user.Id,
                    Introduction = request.Introduction,
                    HourlyFee = request.HourlyFee ?? 0,
                    TeachingFormat = request.TeachingFormat ?? TeachingFormat.Online,
                    VerificationStatus = VerificationStatus.Pending
                });
            }
            else
            {
                var student = new StudentProfile
                {
                    UserId = user.Id,
                    InterestedSubjects = request.InterestedSubjects,
                    Area = request.Area ?? request.ResidenceAddress
                };

                if (isMinor)
                {
                    student.Guardians.Add(new Guardian
                    {
                        FullName = guardianFullName ?? string.Empty,
                        ContactInfo = guardianContact ?? string.Empty,
                        CitizenIdNumber = string.IsNullOrWhiteSpace(request.GuardianCitizenIdNumber) ? null : request.GuardianCitizenIdNumber.Trim(),
                        ResidenceAddress = string.IsNullOrWhiteSpace(request.GuardianResidenceAddress) ? null : request.GuardianResidenceAddress.Trim()
                    });
                }

                _db.StudentProfiles.Add(student);
            }

            await _db.SaveChangesAsync();

            // Gửi thông báo cho toàn bộ Admin
            try
            {
                var adminUsers = await _userManager.GetUsersInRoleAsync("Admin");
                foreach (var admin in adminUsers)
                {
                    _db.Notifications.Add(new Notification
                    {
                        UserId = admin.Id,
                        Title = role == RoleTutor ? "Gia sư mới đăng ký cần duyệt" : "Học viên mới đăng ký",
                        Content = role == RoleTutor
                            ? $"Gia sư {user.FullName} ({user.Email}) vừa hoàn tất đăng ký và đang chờ xác nhận hồ sơ."
                            : $"Học viên {user.FullName} ({user.Email}) vừa tạo tài khoản trên hệ thống.",
                        CreatedAt = DateTime.UtcNow,
                        IsRead = false
                    });
                }
                if (adminUsers.Count > 0)
                {
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception notifEx)
            {
                _db.ChangeTracker.Clear();
                _logger.LogWarning(notifEx, "Không thể gửi thông báo Admin sau khi đăng ký");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi khởi tạo profile người dùng sau đăng ký: {Email}", request.Email);
            await _userManager.DeleteAsync(user);
            return (false, "Có lỗi xảy ra khi tạo hồ sơ người dùng.", null);
        }

        var authResponse = await _jwtService.GenerateAuthResponseAsync(user, rememberMe: true);
        await RecordAuthAuditAsync(user.Id, "Đăng ký tài khoản", $"Tạo tài khoản mới ({role}): {user.Email}");
        return (true, null, authResponse);
    }

    public async Task<(bool Success, string? ErrorMessage, string? ResetToken)> ForgotPasswordAsync(ForgotPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email))
        {
            return (false, "Vui lòng nhập địa chỉ email.", null);
        }

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user == null)
        {
            // Trả kết quả đồng nhất để tránh rò rỉ email tồn tại (user enumeration)
            return (true, null, null);
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        _logger.LogInformation("Password reset requested for user {UserId}", user.Id);

        try
        {
            var baseUrl = _configuration["App:FrontendUrl"]?.TrimEnd('/') ?? "http://localhost:5173";
            var resetUrl = $"{baseUrl}/reset-password?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";
            if (_emailQueue != null)
            {
                var (subject, body) = NotificationEmailTemplates.ForPasswordReset(user.Email!, token, resetUrl);
                _emailQueue.Enqueue(new EmailQueueItemDto(user.Email!, subject, body));
            }
            else
            {
                await _emailSender.SendPasswordResetEmailAsync(user.Email!, token, resetUrl);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi gửi email đặt lại mật khẩu cho user {UserId}", user.Id);
        }

        return (true, null, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> ResetPasswordAsync(ResetPasswordRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Token) || string.IsNullOrWhiteSpace(request.NewPassword))
        {
            return (false, "Vui lòng nhập đầy đủ thông tin: email, mã xác thực và mật khẩu mới.");
        }

        if (!string.IsNullOrWhiteSpace(request.ConfirmPassword) && request.NewPassword != request.ConfirmPassword)
        {
            return (false, "Mật khẩu mới và xác nhận mật khẩu không khớp.");
        }

        var user = await _userManager.FindByEmailAsync(request.Email.Trim());
        if (user == null)
        {
            return (false, "Không tìm thấy người dùng.");
        }

        var result = await _userManager.ResetPasswordAsync(user, request.Token, request.NewPassword);
        if (!result.Succeeded)
        {
            var err = string.Join("; ", result.Errors.Select(e => e.Description));
            return (false, err);
        }

        return (true, null);
    }

    public async Task<(bool Success, string? ErrorMessage)> ChangePasswordAsync(string userId, ChangePasswordRequest request)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return (false, "Người dùng chưa được xác thực.");
        }

        if (string.IsNullOrWhiteSpace(request.CurrentPassword) ||
            string.IsNullOrWhiteSpace(request.NewPassword) ||
            string.IsNullOrWhiteSpace(request.ConfirmPassword))
        {
            return (false, "Vui lòng nhập mật khẩu hiện tại, mật khẩu mới và xác nhận mật khẩu mới.");
        }

        if (!string.Equals(request.NewPassword, request.ConfirmPassword, StringComparison.Ordinal))
        {
            return (false, "Mật khẩu mới và xác nhận mật khẩu không khớp.");
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return (false, "Không tìm thấy tài khoản.");
        }

        var result = await _userManager.ChangePasswordAsync(user, request.CurrentPassword, request.NewPassword);
        if (!result.Succeeded)
        {
            var err = string.Join("; ", result.Errors.Select(e => e.Description));
            return (false, err);
        }

        return (true, null);
    }

    public async Task<UserInfoDto?> GetCurrentUserAsync(string userId)
    {
        if (string.IsNullOrEmpty(userId)) return null;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return null;

        var roles = await _userManager.GetRolesAsync(user);

        int? tutorId = null;
        string? verificationStatus = null;
        if (roles.Contains(RoleTutor))
        {
            var tutor = await _db.TutorProfiles
                .AsNoTracking()
                .Where(t => t.UserId == user.Id)
                .Select(t => new { t.Id, t.VerificationStatus })
                .FirstOrDefaultAsync();
            if (tutor != null)
            {
                tutorId = tutor.Id;
                verificationStatus = tutor.VerificationStatus.ToString();
            }
        }

        int? studentProfileId = null;
        if (roles.Contains(RoleStudent))
        {
            studentProfileId = await _db.StudentProfiles
                .AsNoTracking()
                .Where(s => s.UserId == user.Id)
                .Select(s => (int?)s.Id)
                .FirstOrDefaultAsync();
        }

        return new UserInfoDto
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName ?? string.Empty,
            AvatarUrl = user.AvatarUrl,
            PhoneNumber = user.PhoneNumber,
            CitizenIdNumber = user.CitizenIdNumber,
            ResidenceAddress = user.ResidenceAddress,
            Roles = roles,
            TutorId = tutorId,
            StudentProfileId = studentProfileId,
            VerificationStatus = verificationStatus
        };
    }

    public async Task LogoutAsync()
    {
        await _signInManager.SignOutAsync();
    }
}

