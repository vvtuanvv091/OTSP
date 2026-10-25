namespace PROJECT_OTSP.Dtos;

/// <summary>
/// Kết quả phân trang dùng chung cho các API tìm kiếm và danh sách.
/// TotalPages được tự động tính dựa trên Total và PageSize.
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của các phần tử trong danh sách.</typeparam>
public class PagedResult<T>
{
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int Total { get; set; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(Total / (double)PageSize);
    public List<T> Items { get; set; } = new();
}
