namespace PROJECT_OTSP.Services;

/// <summary>Resolves a user's first page after sign-in without crossing role areas.</summary>
public static class RoleLandingPage
{
    public static string Resolve(
        string? returnUrl,
        IEnumerable<string> roles,
        Func<string?, bool> isLocalUrl,
        string homeUrl)
    {
        var roleSet = new HashSet<string>(roles, StringComparer.OrdinalIgnoreCase);
        var defaultUrl = roleSet.Contains("Tutor")
            ? "/Tutor/TutorProfile"
            : roleSet.Contains("Student_Parent")
                ? "/Student_Parent/StudentDashboard"
                : homeUrl;

        if (string.IsNullOrWhiteSpace(returnUrl)
            || returnUrl is "/" or "~/"
            || returnUrl == homeUrl
            || !isLocalUrl(returnUrl))
        {
            return defaultUrl;
        }

        var path = returnUrl.Split('?', '#')[0].TrimStart('~', '/');
        var firstSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        var canOpenArea = string.Equals(firstSegment, "Tutor", StringComparison.OrdinalIgnoreCase)
            ? roleSet.Contains("Tutor")
            : string.Equals(firstSegment, "Student_Parent", StringComparison.OrdinalIgnoreCase)
                ? roleSet.Contains("Student_Parent")
                : true;

        return canOpenArea ? returnUrl : defaultUrl;
    }
}
