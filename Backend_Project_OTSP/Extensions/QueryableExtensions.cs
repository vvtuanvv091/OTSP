using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Backend_Project_OTSP.Dtos;

namespace Backend_Project_OTSP.Extensions;

public static class QueryableExtensions
{
    /// <summary>
    /// Phương thức mở rộng phân trang tái sử dụng cho mọi IQueryable trong dự án OTSP.
    /// Tự động đếm Total và áp dụng Skip/Take chuẩn SQL Server.
    /// </summary>
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var p = Math.Max(1, page);
        var ps = Math.Clamp(pageSize, 1, 100);
        var total = await query.CountAsync(cancellationToken);
        var skip = (p - 1) * ps;

        var items = await query
            .Skip(skip)
            .Take(ps)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>
        {
            Page = p,
            PageSize = ps,
            Total = total,
            Items = items
        };
    }
}

