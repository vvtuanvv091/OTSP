namespace Backend_Project_OTSP.Dtos;

/// <summary>
/// Kết quả phân trang dùng chung cho các API tìm kiếm và danh sách.
/// TotalPages được tự động tính dựa trên Total và PageSize.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của các phần tử trong danh sách.</typeparam>
public class PagedResult<T>
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 12;
    public int Total { get; set; } = 0;
    public int TotalPages
    {
        get => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
        set { }
    }
    public bool HasPreviousPage => Page > 1;
    public bool HasNextPage => Page < TotalPages;
    public List<T> Items { get; set; } = new();
}

