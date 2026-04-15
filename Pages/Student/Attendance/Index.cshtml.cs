using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Student.Attendance
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public IndexModel(ApplicationDbContext context) => _context = context;

        [BindProperty(SupportsGet = true)] public string? FilterYear { get; set; }
        [BindProperty(SupportsGet = true)] public string? FilterMonth { get; set; }
        [BindProperty(SupportsGet = true)] public string? ClassFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? ToDate { get; set; }
        [BindProperty(SupportsGet = true)] public int? StudentIdFilter { get; set; }

        public string ViewerName { get; set; } = string.Empty;
        public string ViewerRole { get; set; } = string.Empty;
        public bool IsParent => ViewerRole == "Parent";
        public bool HasDateRange { get; set; }
        public int ActiveYear { get; set; }
        public int ActiveMonth { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;

        public List<ChildInfo> MyChildren { get; set; } = new();
        public List<ClassInfo> Classes { get; set; } = new();
        public List<AttendanceInfo> AttendanceList { get; set; } = new();
        public StatisticsInfo Statistics { get; set; } = new();
        public List<ClassStats> ClassStatsList { get; set; } = new();
        public List<AttendanceInfo> RecentList { get; set; } = new();

        public string ChartLabelsJson { get; set; } = "[]";
        public string ChartPresentJson { get; set; } = "[]";
        public string ChartAbsentJson { get; set; } = "[]";
        public string ChartMode { get; set; } = "days";

        public async Task<IActionResult> OnGetAsync()
        {
            var role = HttpContext.Session.GetString("Role");
            var accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null || (role != "Student" && role != "Parent"))
                return RedirectToPage("/Account/Login");
            ViewerRole = role!;
            var aid = accountId.Value;

            List<int> studentIds = new();
            if (role == "Student")
            {
                var s = await _context.Students.FindAsync(aid);
                ViewerName = s?.Fullname ?? string.Empty;
                studentIds.Add(aid);
            }
            else
            {
                var p = await _context.Parents.Include(x => x.Students)
                    .FirstOrDefaultAsync(x => x.AccountId == aid);
                ViewerName = p?.Fullname ?? string.Empty;
                MyChildren = p?.Students.Select(s => new ChildInfo
                { StudentId = s.AccountId, Fullname = s.Fullname, School = s.CurrentSchool })
                    .ToList() ?? new();
                studentIds = StudentIdFilter.HasValue && StudentIdFilter > 0
                    ? new List<int> { StudentIdFilter.Value }
                    : MyChildren.Select(c => c.StudentId).ToList();
            }
            if (!studentIds.Any()) return Page();

            // Mutual exclusion: date range xóa năm/tháng
            HasDateRange = !string.IsNullOrEmpty(FromDate) || !string.IsNullOrEmpty(ToDate);
            if (HasDateRange) { FilterYear = null; FilterMonth = null; }

            ActiveYear = !string.IsNullOrEmpty(FilterYear) && int.TryParse(FilterYear, out int fy) ? fy : DateTime.Today.Year;
            ActiveMonth = !string.IsNullOrEmpty(FilterMonth) && int.TryParse(FilterMonth, out int fm) ? fm : 0;
            if (HasDateRange) { ActiveYear = 0; ActiveMonth = 0; }

            if (HasDateRange)
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(FromDate)) parts.Add("Từ " + FromDate);
                if (!string.IsNullOrEmpty(ToDate)) parts.Add("đến " + ToDate);
                PeriodLabel = string.Join(" ", parts);
            }
            else
                PeriodLabel = ActiveMonth == 0 ? $"Cả năm {ActiveYear}" : $"Tháng {ActiveMonth}/{ActiveYear}";

            Classes = await _context.ClassStudents
                .Where(cs => studentIds.Contains(cs.StudentId) && cs.Status == StudentClassStatus.Active)
                .Include(cs => cs.Class)
                .Select(cs => new ClassInfo { ClassId = cs.ClassId, ClassCode = cs.Class.ClassCode })
                .Distinct().OrderBy(c => c.ClassCode).ToListAsync();

            var allRaw = await _context.Attendances
                .Where(a => studentIds.Contains(a.StudentId))
                .Include(a => a.Student)
                .Include(a => a.Session).ThenInclude(s => s.Class)
                .Include(a => a.Session).ThenInclude(s => s.Shift)
                .Include(a => a.Session).ThenInclude(s => s.Room)
                .Include(a => a.Session).ThenInclude(s => s.Teacher)
                .Include(a => a.Session).ThenInclude(s => s.TeacherAssistant)
                .ToListAsync();

            // Một pipeline filter duy nhất
            var filtered = allRaw.AsEnumerable();
            if (!HasDateRange)
            {
                filtered = filtered.Where(a => a.Session.SessionDate.Year == ActiveYear);
                if (ActiveMonth > 0) filtered = filtered.Where(a => a.Session.SessionDate.Month == ActiveMonth);
            }
            else
            {
                if (!string.IsNullOrEmpty(FromDate) && DateOnly.TryParse(FromDate, out var fdDate))
                    filtered = filtered.Where(a => a.Session.SessionDate >= fdDate);
                if (!string.IsNullOrEmpty(ToDate) && DateOnly.TryParse(ToDate, out var tdDate))
                    filtered = filtered.Where(a => a.Session.SessionDate <= tdDate);
            }
            if (!string.IsNullOrEmpty(ClassFilter) && int.TryParse(ClassFilter, out int cid))
                filtered = filtered.Where(a => a.Session.ClassId == cid);
            if (StatusFilter == "present") filtered = filtered.Where(a => a.Status == AttendanceStatus.Present);
            else if (StatusFilter == "absent") filtered = filtered.Where(a => a.Status == AttendanceStatus.Absent);

            var fl = filtered.ToList();

            // Tất cả thống kê theo bộ lọc
            Statistics.TotalSessions = fl.Count;
            Statistics.PresentCount = fl.Count(a => a.Status == AttendanceStatus.Present);
            Statistics.AbsentCount = fl.Count(a => a.Status == AttendanceStatus.Absent);
            Statistics.AttendanceRate = fl.Count > 0
                ? Math.Round((double)Statistics.PresentCount / fl.Count * 100, 1) : 0;

            AttendanceList = fl.OrderByDescending(a => a.Session.SessionDate)
                .ThenByDescending(a => a.Session.Shift.StartTime)
                .Take(200).Select(a => MapToInfo(a, IsParent)).ToList();

            RecentList = fl.OrderByDescending(a => a.Session.SessionDate)
                .ThenByDescending(a => a.Session.Shift.StartTime)
                .Take(10).Select(a => MapToInfo(a, IsParent)).ToList();

            ClassStatsList = fl.GroupBy(a => new { a.Session.ClassId, a.Session.Class.ClassCode })
                .Select(g => {
                    var p2 = g.Count(a => a.Status == AttendanceStatus.Present);
                    var t2 = g.Count();
                    return new ClassStats
                    {
                        ClassCode = g.Key.ClassCode,
                        TotalSessions = t2,
                        PresentCount = p2,
                        AbsentCount = t2 - p2,
                        Rate = t2 > 0 ? Math.Round((double)p2 / t2 * 100, 1) : 0
                    };
                }).OrderByDescending(c => c.TotalSessions).ToList();

            // Chart: cả năm → theo tháng, còn lại → theo ngày
            if (!HasDateRange && ActiveMonth == 0)
            {
                ChartMode = "months";
                var pByM = fl.Where(a => a.Status == AttendanceStatus.Present)
                    .GroupBy(a => a.Session.SessionDate.Month).ToDictionary(g => g.Key, g => g.Count());
                var aByM = fl.Where(a => a.Status == AttendanceStatus.Absent)
                    .GroupBy(a => a.Session.SessionDate.Month).ToDictionary(g => g.Key, g => g.Count());
                ChartLabelsJson = JsonSerializer.Serialize(Enumerable.Range(1, 12).Select(m => "Tháng " + m));
                ChartPresentJson = JsonSerializer.Serialize(Enumerable.Range(1, 12).Select(m => pByM.GetValueOrDefault(m, 0)));
                ChartAbsentJson = JsonSerializer.Serialize(Enumerable.Range(1, 12).Select(m => aByM.GetValueOrDefault(m, 0)));
            }
            else
            {
                ChartMode = "days";
                List<DateOnly> dayList;
                if (fl.Any())
                {
                    var minD = fl.Min(a => a.Session.SessionDate);
                    var maxD = fl.Max(a => a.Session.SessionDate);
                    var span = (maxD.ToDateTime(TimeOnly.MinValue) - minD.ToDateTime(TimeOnly.MinValue)).Days + 1;
                    dayList = span <= 62
                        ? Enumerable.Range(0, span).Select(i => minD.AddDays(i)).ToList()
                        : Enumerable.Range(0, 30).Select(i => DateOnly.FromDateTime(DateTime.Today.AddDays(-29 + i))).ToList();
                }
                else
                    dayList = Enumerable.Range(0, 30).Select(i => DateOnly.FromDateTime(DateTime.Today.AddDays(-29 + i))).ToList();

                var pByD = fl.Where(a => a.Status == AttendanceStatus.Present)
                    .GroupBy(a => a.Session.SessionDate).ToDictionary(g => g.Key, g => g.Count());
                var aByD = fl.Where(a => a.Status == AttendanceStatus.Absent)
                    .GroupBy(a => a.Session.SessionDate).ToDictionary(g => g.Key, g => g.Count());
                ChartLabelsJson = JsonSerializer.Serialize(dayList.Select(d => d.ToString("dd/MM")));
                ChartPresentJson = JsonSerializer.Serialize(dayList.Select(d => pByD.GetValueOrDefault(d, 0)));
                ChartAbsentJson = JsonSerializer.Serialize(dayList.Select(d => aByD.GetValueOrDefault(d, 0)));
            }

            return Page();
        }

        private static AttendanceInfo MapToInfo(TutoringCenterManagement.Data.Entities.Attendance a, bool isParent) => new()
        {
            SessionDate = a.Session.SessionDate,
            ClassName = a.Session.Class.ClassCode,
            ShiftName = a.Session.Shift.ShiftName,
            ShiftTime = $"{a.Session.Shift.StartTime:HH:mm}–{a.Session.Shift.EndTime:HH:mm}",
            RoomCode = a.Session.Room?.RoomCode ?? "—",
            TeacherName = a.Session.Teacher.Fullname,
            AssistantName = a.Session.TeacherAssistant?.Fullname,
            IsPresent = a.Status == AttendanceStatus.Present,
            StudentName = isParent ? a.Student?.Fullname : null
        };

        public class ChildInfo { public int StudentId { get; set; } public string Fullname { get; set; } = string.Empty; public string? School { get; set; } }
        public class ClassInfo { public int ClassId { get; set; } public string ClassCode { get; set; } = string.Empty; }
        public class AttendanceInfo
        {
            public DateOnly SessionDate { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public string ShiftName { get; set; } = string.Empty; public string ShiftTime { get; set; } = string.Empty;
            public string RoomCode { get; set; } = string.Empty; public string TeacherName { get; set; } = string.Empty;
            public string? AssistantName { get; set; }
            public bool IsPresent { get; set; }
            public string? StudentName { get; set; }
        }
        public class StatisticsInfo { public int TotalSessions { get; set; } public int PresentCount { get; set; } public int AbsentCount { get; set; } public double AttendanceRate { get; set; } }
        public class ClassStats { public string ClassCode { get; set; } = string.Empty; public int TotalSessions { get; set; } public int PresentCount { get; set; } public int AbsentCount { get; set; } public double Rate { get; set; } }
    }
}