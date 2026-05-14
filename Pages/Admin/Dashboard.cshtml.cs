using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin
{
    public class DashboardModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DashboardModel(ApplicationDbContext context)
        {
            _context = context;
        }

        // ── KPI ─────────────────────────────────────────────────────────────────
        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalClasses { get; set; }
        public int TodaySessions { get; set; }

        // ── Today sessions ───────────────────────────────────────────────────────
        public List<TodaySessionDto> TodaySessionList { get; set; } = new();

        // ── Attendance rate (7 days) — chỉ Present / Absent ─────────────────────
        public int AttendanceRatePresent { get; set; }
        public int AttendanceRateAbsent { get; set; }

        // ── Chart data ───────────────────────────────────────────────────────────
        public string Last7Days { get; set; } = "[]";
        public string AttendancePresentData { get; set; } = "[]";
        public string AttendanceAbsentData { get; set; } = "[]";
        public string ClassNames { get; set; } = "[]";
        public string ClassStudentCounts { get; set; } = "[]";

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var today = DateOnly.FromDateTime(DateTime.Today);

            // ── KPI — chỉ đếm đối tượng đang hoạt động ──────────────────────────
            TotalStudents = await _context.Students
                .Where(s => s.Account.IsActive == IsActive.Active)
                .CountAsync();

            TotalTeachers = await _context.Teachers
                .Where(t => t.Account.IsActive == IsActive.Active)
                .CountAsync();

            TotalClasses = await _context.Classes
                .Where(c => c.Status == ClassStatus.Active)
                .CountAsync();

            // ── Today sessions — không tính buổi đã hủy ─────────────────────────
            var todaySessions = await _context.Sessions
                .Where(s => s.SessionDate == today && s.Status != SessionStatus.Cancelled)
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .Include(s => s.Teacher)
                .OrderBy(s => s.Shift.StartTime)
                .ToListAsync();

            TodaySessions = todaySessions.Count;

            // HasAttendance — 1 query cho tất cả buổi hôm nay
            var todaySessionIds = todaySessions.Select(s => s.SessionId).ToList();
            var todayAttendedIds = (await _context.Attendances
                .Where(a => todaySessionIds.Contains(a.SessionId))
                .Select(a => a.SessionId)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            TodaySessionList = todaySessions.Select(s => new TodaySessionDto
            {
                SessionId    = s.SessionId,
                ClassName    = s.Class.ClassCode,
                ShiftName    = s.Shift.ShiftName,
                StartTime    = s.Shift.StartTime,
                EndTime      = s.Shift.EndTime,
                RoomCode     = s.Room.RoomCode,
                TeacherName  = s.Teacher.Fullname,
                Status       = s.Status,
                HasAttendance = todayAttendedIds.Contains(s.SessionId)
            }).ToList();

            // ── Attendance stats — 7 ngày, 1 batch query (thay vì N×3 queries) ───
            var sevenDaysAgo = DateOnly.FromDateTime(DateTime.Today.AddDays(-6));
            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-6 + i))
                .ToList();

            var attendanceByDate = await (
                from a in _context.Attendances
                join s in _context.Sessions on a.SessionId equals s.SessionId
                where s.SessionDate >= sevenDaysAgo && s.SessionDate <= today
                group a by new { s.SessionDate, a.Status } into g
                select new { g.Key.SessionDate, g.Key.Status, Count = g.Count() }
            ).ToListAsync();

            var attendanceStats = new List<int>();
            var absentStats    = new List<int>();

            foreach (var day in last7Days)
            {
                var dayOnly = DateOnly.FromDateTime(day);
                var present = attendanceByDate
                    .Where(x => x.SessionDate == dayOnly && x.Status == AttendanceStatus.Present)
                    .Sum(x => x.Count);
                var absent = attendanceByDate
                    .Where(x => x.SessionDate == dayOnly && x.Status == AttendanceStatus.Absent)
                    .Sum(x => x.Count);
                attendanceStats.Add(present);
                absentStats.Add(absent);
            }

            var totalPresent = attendanceStats.Sum();
            var totalAbsent  = absentStats.Sum();
            var totalRecords = totalPresent + totalAbsent;
            AttendanceRatePresent = totalRecords > 0 ? (int)Math.Round(100.0 * totalPresent / totalRecords) : 0;
            AttendanceRateAbsent  = totalRecords > 0 ? (int)Math.Round(100.0 * totalAbsent  / totalRecords) : 0;

            Last7Days             = JsonSerializer.Serialize(last7Days.Select(d => d.ToString("dd/MM")));
            AttendancePresentData = JsonSerializer.Serialize(attendanceStats);
            AttendanceAbsentData  = JsonSerializer.Serialize(absentStats);

            // ── Phân bổ học sinh theo lớp — chỉ lớp Active ──────────────────────
            var classStats = await _context.Classes
                .Where(c => c.Status == ClassStatus.Active)
                .Select(c => new
                {
                    c.ClassCode,
                    StudentCount = c.ClassStudents.Count(cs => cs.Status == StudentClassStatus.Active)
                })
                .Where(c => c.StudentCount > 0)
                .ToListAsync();

            ClassNames         = JsonSerializer.Serialize(classStats.Select(c => c.ClassCode));
            ClassStudentCounts = JsonSerializer.Serialize(classStats.Select(c => c.StudentCount));

            return Page();
        }

        /// <summary>
        /// FullCalendar API endpoint — trả về events với đầy đủ extendedProps
        /// (đồng bộ với Sessions/Index để modal hiển thị đúng thông tin).
        /// Không trả buổi đã hủy vì lịch tổng quan chỉ cần hiển thị buổi hoạt động.
        /// </summary>
        public async Task<JsonResult> OnGetGetEventsAsync(string start, string end)
        {
            var startDate = DateOnly.Parse(start.Split('T')[0]);
            var endDate   = DateOnly.Parse(end.Split('T')[0]);

            var sessions = await _context.Sessions
                .Where(s => s.SessionDate >= startDate && s.SessionDate <= endDate
                         && s.Status != SessionStatus.Cancelled)
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .Include(s => s.Teacher)
                .ToListAsync();

            var sessionIds  = sessions.Select(s => s.SessionId).ToList();
            var attendedIds = (await _context.Attendances
                .Where(a => sessionIds.Contains(a.SessionId))
                .Select(a => a.SessionId)
                .Distinct()
                .ToListAsync())
                .ToHashSet();

            static string StatusCls(SessionStatus st) => st switch
            {
                SessionStatus.Scheduled => "scheduled",
                SessionStatus.Ongoing   => "ongoing",
                SessionStatus.Completed => "completed",
                SessionStatus.Cancelled => "cancelled",
                _                       => ""
            };

            static string DowVi(DayOfWeek d) => d switch
            {
                DayOfWeek.Monday    => "Thứ hai",
                DayOfWeek.Tuesday   => "Thứ ba",
                DayOfWeek.Wednesday => "Thứ tư",
                DayOfWeek.Thursday  => "Thứ năm",
                DayOfWeek.Friday    => "Thứ sáu",
                DayOfWeek.Saturday  => "Thứ bảy",
                _                   => "Chủ nhật"
            };

            var events = sessions.Select(s => new
            {
                title = s.Class.ClassCode,
                start = s.SessionDate.ToString("yyyy-MM-dd") + "T" + s.Shift.StartTime.ToString("HH:mm"),
                end   = s.SessionDate.ToString("yyyy-MM-dd") + "T" + s.Shift.EndTime.ToString("HH:mm"),
                extendedProps = new
                {
                    sessionId     = s.SessionId,
                    date          = s.SessionDate.ToString("dd/MM/yyyy"),
                    dow           = DowVi(s.SessionDate.DayOfWeek),
                    shift         = s.Shift.ShiftName,
                    shiftTime     = $"{s.Shift.StartTime:HH:mm}-{s.Shift.EndTime:HH:mm}",
                    room          = s.Room.RoomCode,
                    teacher       = s.Teacher.Fullname,
                    statusCls     = StatusCls(s.Status),
                    isTemplate    = s.TemplateId.HasValue,
                    hasAttendance = attendedIds.Contains(s.SessionId)
                }
            });

            return new JsonResult(events);
        }

        // ── DTO ──────────────────────────────────────────────────────────────────

        public class TodaySessionDto
        {
            public int SessionId { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public string ShiftName { get; set; } = string.Empty;
            public TimeOnly StartTime { get; set; }
            public TimeOnly EndTime { get; set; }
            public string RoomCode { get; set; } = string.Empty;
            public string TeacherName { get; set; } = string.Empty;
            public SessionStatus Status { get; set; }
            public bool HasAttendance { get; set; }

            public string StatusCls => Status switch
            {
                SessionStatus.Scheduled => "scheduled",
                SessionStatus.Ongoing   => "ongoing",
                SessionStatus.Completed => "completed",
                SessionStatus.Cancelled => "cancelled",
                _                       => ""
            };

            public string StatusLabel => Status switch
            {
                SessionStatus.Scheduled => "Đã lên lịch",
                SessionStatus.Ongoing   => "Đang diễn ra",
                SessionStatus.Completed => "Hoàn thành",
                SessionStatus.Cancelled => "Đã hủy",
                _                       => ""
            };

            public string AttendanceUrl => HasAttendance
                ? $"/Admin/Attendance/Edit?sessionId={SessionId}"
                : $"/Admin/Attendance/Create?sessionId={SessionId}";
        }
    }
}
