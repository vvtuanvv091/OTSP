namespace PROJECT_OTSP.Services
{
    public readonly record struct TimeBlock(int Start, int End)
    {
        public int Hours => End - Start;
        public string Label => $"{Start:00}:00 – {End:00}:00";
    }

    public interface IScheduleBlockService
    {
        IReadOnlyList<TimeBlock> Blocks { get; }

        /// <summary>Tìm khối theo giờ BẮT ĐẦU (dùng khi đọc slot key).</summary>
        TimeBlock? FindByStart(int hour);

        /// <summary>Tìm khối CHỨA một giờ bất kỳ (dùng khi quy buổi học về khối).</summary>
        TimeBlock? Containing(int hour);
    }

    public class ScheduleBlockService : IScheduleBlockService
    {
        // Mỗi khối = 1 buổi học 2 tiếng. 12:00-13:00 là nghỉ trưa (không có khối).
        public IReadOnlyList<TimeBlock> Blocks { get; } = new[]
        {
            new TimeBlock(6, 8),
            new TimeBlock(8, 10),
            new TimeBlock(10, 12),
            new TimeBlock(13, 15),
            new TimeBlock(15, 17),
            new TimeBlock(17, 19),
            new TimeBlock(19, 21),
            new TimeBlock(21, 23)
        };

        // Bảng tra cứu trực tiếp O(1) theo giờ (0-23h) tránh lặp tuyến tính và cấp phát bộ nhớ
        private readonly TimeBlock?[] _byStart = new TimeBlock?[24];
        private readonly TimeBlock?[] _containing = new TimeBlock?[24];

        public ScheduleBlockService()
        {
            foreach (var b in Blocks)
            {
                if (b.Start >= 0 && b.Start < 24)
                {
                    _byStart[b.Start] = b;
                }

                for (var h = b.Start; h < b.End && h < 24; h++)
                {
                    if (h >= 0)
                    {
                        _containing[h] = b;
                    }
                }
            }
        }

        public TimeBlock? FindByStart(int hour) =>
            hour is >= 0 and < 24 ? _byStart[hour] : null;

        public TimeBlock? Containing(int hour) =>
            hour is >= 0 and < 24 ? _containing[hour] : null;
    }
}