namespace Backend_Project_OTSP.Security;

/// <summary>
/// Hằng số vai trò dùng thống nhất cho thuộc tính [Authorize(Roles = ...)].
/// </summary>
public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Tutor = "Tutor";
    public const string StudentParent = "Student_Parent";
    public const string StudentAccess = "Student_Parent,Student";
    public const string StudentOrTutor = "Student_Parent,Student,Tutor";
    public const string TutorOrAdmin = "Tutor,Admin";
    public const string AllAuthenticated = "Student_Parent,Student,Tutor,Admin";
}

