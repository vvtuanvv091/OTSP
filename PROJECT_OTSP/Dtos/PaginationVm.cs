namespace PROJECT_OTSP.Dtos;

/// <summary>
/// ViewModel hỗ trợ phân trang đồng bộ toàn hệ thống OTSP.
/// </summary>
public class PaginationVm
{
    public int CurrentPage { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; } = 0;
    public int PageSize { get; set; } = 8;
    public string BaseUrl { get; set; } = "";
    public Dictionary<string, string> QueryParams { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string GetPageUrl(int page)
    {
        var dict = new Dictionary<string, string>(QueryParams, StringComparer.OrdinalIgnoreCase);
        dict["page"] = page.ToString();
        var qs = string.Join("&", dict.Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(kv.Value)}"));
        return string.IsNullOrEmpty(qs) ? BaseUrl : $"{BaseUrl}?{qs}";
    }

    /// <summary>
    /// Tạo danh sách các số trang hiển thị và dấu ba chấm (null đại diện cho ...)
    /// </summary>
    public List<int?> GetVisiblePages()
    {
        var pages = new List<int?>();
        if (TotalPages <= 7)
        {
            for (int i = 1; i <= TotalPages; i++) pages.Add(i);
            return pages;
        }

        pages.Add(1);
        if (CurrentPage > 3) pages.Add(null); // Dấu ...

        int start = Math.Max(2, CurrentPage - 1);
        int end = Math.Min(TotalPages - 1, CurrentPage + 1);

        for (int i = start; i <= end; i++)
        {
            if (!pages.Contains(i)) pages.Add(i);
        }

        if (CurrentPage < TotalPages - 2) pages.Add(null); // Dấu ...
        if (!pages.Contains(TotalPages)) pages.Add(TotalPages);

        return pages;
    }
}

