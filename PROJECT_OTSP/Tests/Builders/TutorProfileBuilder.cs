using PROJECT_OTSP.Models;
using static PROJECT_OTSP.Models.Enums;

namespace PROJECT_OTSP.Tests.Builders;

/// <summary>
/// Testing Pattern: Test Data Builder
/// Giúp tạo dữ liệu TutorProfile mẫu với cú pháp Fluent dễ đọc,
/// tránh bị vỡ bài test khi thêm bớt trường trong Model.
/// </summary>
public class TutorProfileBuilder
{
    private int _id = 1;
    private string _userId = Guid.NewGuid().ToString();
    private string _fullName = "Gia Sư Mẫu";
    private string _introduction = "Kinh nghiệm 5 năm luyện thi đại học môn Toán";
    private decimal _hourlyFee = 200000m;
    private TeachingFormat _format = TeachingFormat.Hybrid;
    private VerificationStatus _verificationStatus = VerificationStatus.Approved;
    private AccountStatus _accountStatus = AccountStatus.Active;
    private double _averageRating = 4.8;
    private readonly List<TutorSubject> _subjects = new();

    public TutorProfileBuilder WithId(int id)
    {
        _id = id;
        return this;
    }

    public TutorProfileBuilder WithName(string name)
    {
        _fullName = name;
        return this;
    }

    public TutorProfileBuilder WithFee(decimal fee)
    {
        _hourlyFee = fee;
        return this;
    }

    public TutorProfileBuilder WithFormat(TeachingFormat format)
    {
        _format = format;
        return this;
    }

    public TutorProfileBuilder WithVerificationStatus(VerificationStatus status)
    {
        _verificationStatus = status;
        return this;
    }

    public TutorProfileBuilder WithAccountStatus(AccountStatus status)
    {
        _accountStatus = status;
        return this;
    }

    public TutorProfileBuilder WithSubject(int subjectId, int gradeLevelId)
    {
        _subjects.Add(new TutorSubject
        {
            SubjectId = subjectId,
            GradeLevelId = gradeLevelId
        });
        return this;
    }

    public TutorProfile Build()
    {
        var user = new ApplicationUser
        {
            Id = _userId,
            UserName = $"tutor_{_id}@otsp.edu.vn",
            Email = $"tutor_{_id}@otsp.edu.vn",
            FullName = _fullName,
            Status = _accountStatus,
            CreatedAt = DateTime.UtcNow
        };

        var tutor = new TutorProfile
        {
            Id = _id,
            UserId = _userId,
            User = user,
            Introduction = _introduction,
            HourlyFee = _hourlyFee,
            TeachingFormat = _format,
            VerificationStatus = _verificationStatus,
            AverageRating = _averageRating,
            TutorSubjects = _subjects
        };

        foreach (var s in _subjects)
        {
            s.TutorId = _id;
            s.Tutor = tutor;
        }

        return tutor;
    }
}

