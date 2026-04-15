using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Teacher
{
    public class ScheduleModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public ScheduleModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ──────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string? ClassFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? ToDate { get; set; }

        // ── KPI ─────────────────────────────────────────────────────
        public int TodaySessions { get; set; }
        public int ThisWeekSessions { get; set; }
        public int ThisMonthSessions { get; set; }
        public int TotalClasses { get; set; }
        public int CompletedSessions { get; set; }
        public int CancelledSessions { get; set; }
        public int ScheduledSessions { get; set; }

        // ── Danh sách lớp để filter ──────────────────────────────────
        public List<ClassItem> MyClasses { get; set; } = new();

        // ── Danh sách buổi học (table view) ──────────────────────────
        public List<SessionItem> Sessions { get; set; } = new();

        // ── Tên giáo viên (hiển thị welcome) ─────────────────────────
        public string TeacherName { get; set; } = string.Empty;

        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            var role = HttpContext.Session.GetString("Role");
            var teacherId = HttpContext.Session.GetInt32("AccountId");

            if (role != "Teacher" || !teacherId.HasValue)
                return RedirectToPage("/Account/Login");

            var tid = teacherId.Value;

            // Lấy tên giáo viên
            var teacher = await _context.Teachers.FindAsync(tid);
            TeacherName = teacher?.Fullname ?? string.Empty;

            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1);
            var endOfWeek = startOfWeek.AddDays(6);

            // ── KPI ──────────────────────────────────────────────────
            var baseQ = _context.Sessions.Where(s =>
                s.TeacherId == tid || s.TeacherAssistantId == tid);

            TodaySessions = await baseQ.CountAsync(s => s.SessionDate == today);
            ThisWeekSessions = await baseQ.CountAsync(s => s.SessionDate >= startOfWeek && s.SessionDate <= endOfWeek);
            ThisMonthSessions = await baseQ.CountAsync(s => s.SessionDate.Month == today.Month && s.SessionDate.Year == today.Year);
            TotalClasses = await baseQ.Select(s => s.ClassId).Distinct().CountAsync();
            CompletedSessions = await baseQ.CountAsync(s => s.Status == SessionStatus.Completed);
            CancelledSessions = await baseQ.CountAsync(s => s.Status == SessionStatus.Cancelled);
            ScheduledSessions = await baseQ.CountAsync(s => s.Status == SessionStatus.Scheduled);

            // ── Danh sách lớp của giáo viên ──────────────────────────
            MyClasses = await baseQ
                .Select(s => new { s.ClassId, s.Class.ClassCode })
                .Distinct()
                .Select(x => new ClassItem { ClassId = x.ClassId, ClassCode = x.ClassCode })
                .OrderBy(c => c.ClassCode)
                .ToListAsync();

            // ── Query có filter ───────────────────────────────────────
            var query = baseQ
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .Include(s => s.Teacher)
                .Include(s => s.TeacherAssistant)
                .Include(s => s.Attendances)
                .AsQueryable();

            if (!string.IsNullOrEmpty(ClassFilter) && int.TryParse(ClassFilter, out var cid))
                query = query.Where(s => s.ClassId == cid);

            if (!string.IsNullOrEmpty(StatusFilter) && int.TryParse(StatusFilter, out var st))
                query = query.Where(s => (int)s.Status == st);

            if (!string.IsNullOrEmpty(FromDate) && DateOnly.TryParse(FromDate, out var fd))
                query = query.Where(s => s.SessionDate >= fd);

            if (!string.IsNullOrEmpty(ToDate) && DateOnly.TryParse(ToDate, out var td2))
                query = query.Where(s => s.SessionDate <= td2);

            var raw = await query.OrderByDescending(s => s.SessionDate)
                                 .ThenBy(s => s.Shift.StartTime)
                                 .ToListAsync();

            Sessions = raw.Select(s => new SessionItem
            {
                SessionId = s.SessionId,
                ClassId = s.ClassId,
                ClassCode = s.Class.ClassCode,
                ClassName = s.Class.ClassName ?? s.Class.ClassCode,
                Subject = s.Class.Subject,
                SessionDate = s.SessionDate,
                ShiftName = s.Shift.ShiftName,
                ShiftTime = $"{s.Shift.StartTime:HH:mm} – {s.Shift.EndTime:HH:mm}",
                RoomCode = s.Room.RoomCode,
                RoomName = s.Room.RoomName,
                TeacherName = s.Teacher.Fullname,
                AssistantName = s.TeacherAssistant?.Fullname,
                IsMainTeacher = s.TeacherId == tid,
                Status = s.Status,
                IsFromTemplate = s.TemplateId.HasValue,
                HasAttendance = s.Attendances.Any(),
                Note = s.Note,
            }).ToList();

            return Page();
        }

        // ── Calendar events handler ───────────────────────────────────
        public async Task<JsonResult> OnGetGetEventsAsync(string start, string end,
            string? classFilter, string? statusFilter)
        {
            var teacherId = HttpContext.Session.GetInt32("AccountId");
            if (!teacherId.HasValue) return new JsonResult(Array.Empty<object>());

            var tid = teacherId.Value;
            var startDate = DateOnly.Parse(start.Split('T')[0]);
            var endDate = DateOnly.Parse(end.Split('T')[0]);

            var query = _context.Sessions
                .Where(s => (s.TeacherId == tid || s.TeacherAssistantId == tid)
                         && s.SessionDate >= startDate
                         && s.SessionDate <= endDate)
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .Include(s => s.Teacher)
                .Include(s => s.TeacherAssistant)
                .Include(s => s.Attendances)
                .AsQueryable();

            if (!string.IsNullOrEmpty(classFilter) && int.TryParse(classFilter, out var cid))
                query = query.Where(s => s.ClassId == cid);

            if (!string.IsNullOrEmpty(statusFilter) && int.TryParse(statusFilter, out var st))
                query = query.Where(s => (int)s.Status == st);

            var sessions = await query.ToListAsync();

            var statusCssMap = new Dictionary<SessionStatus, string>
            {
                [SessionStatus.Scheduled] = "scheduled",
                [SessionStatus.Ongoing] = "ongoing",
                [SessionStatus.Completed] = "completed",
                [SessionStatus.Cancelled] = "cancelled",
            };

            var events = sessions.Select(s => new
            {
                title = s.Class.ClassCode,
                start = s.SessionDate.ToString("yyyy-MM-dd") + "T" + s.Shift.StartTime.ToString("HH:mm"),
                end = s.SessionDate.ToString("yyyy-MM-dd") + "T" + s.Shift.EndTime.ToString("HH:mm"),
                extendedProps = new
                {
                    sessionId = s.SessionId,
                    className = s.Class.ClassCode,
                    classFullName = s.Class.ClassName ?? s.Class.ClassCode,
                    date = s.SessionDate.ToString("dd/MM/yyyy"),
                    dow = s.SessionDate.DayOfWeek switch
                    {
                        DayOfWeek.Monday => "Thứ hai",
                        DayOfWeek.Tuesday => "Thứ ba",
                        DayOfWeek.Wednesday => "Thứ tư",
                        DayOfWeek.Thursday => "Thứ năm",
                        DayOfWeek.Friday => "Thứ sáu",
                        DayOfWeek.Saturday => "Thứ bảy",
                        _ => "Chủ nhật"
                    },
                    shift = s.Shift.ShiftName,
                    shiftTime = $"{s.Shift.StartTime:HH:mm} – {s.Shift.EndTime:HH:mm}",
                    room = s.Room.RoomCode,
                    roomName = s.Room.RoomName,
                    teacher = s.Teacher.Fullname,
                    assistant = s.TeacherAssistant?.Fullname ?? "",
                    isMain = s.TeacherId == tid,
                    statusCls = statusCssMap.GetValueOrDefault(s.Status, "scheduled"),
                    hasAttendance = s.Attendances.Any(),
                    note = s.Note ?? "",
                }
            });

            return new JsonResult(events);
        }

        // ── View Models ───────────────────────────────────────────────
        public class ClassItem
        {
            public int ClassId { get; set; }
            public string ClassCode { get; set; } = string.Empty;
        }

        public class SessionItem
        {
            public int SessionId { get; set; }
            public int ClassId { get; set; }
            public string ClassCode { get; set; } = string.Empty;
            public string ClassName { get; set; } = string.Empty;
            public Subject Subject { get; set; }
            public DateOnly SessionDate { get; set; }
            public string ShiftName { get; set; } = string.Empty;
            public string ShiftTime { get; set; } = string.Empty;
            public string RoomCode { get; set; } = string.Empty;
            public string RoomName { get; set; } = string.Empty;
            public string TeacherName { get; set; } = string.Empty;
            public string? AssistantName { get; set; }
            public bool IsMainTeacher { get; set; }
            public SessionStatus Status { get; set; }
            public bool IsFromTemplate { get; set; }
            public bool HasAttendance { get; set; }
            public string? Note { get; set; }

            public string SubjectLabel => Subject switch
            {
                Subject.Math => "Toán",
                Subject.Vietnamese => "Tiếng Việt",
                Subject.English => "Tiếng Anh",
                Subject.Physics => "Vật lý",
                Subject.Biology => "Sinh học",
                Subject.Chemistry => "Hóa học",
                Subject.Geography => "Địa lý",
                Subject.History => "Lịch sử",
                _ => "Môn học"
            };
            public string AttendanceUrl => HasAttendance
                ? $"/Teacher/Attendance/Edit?sessionId={SessionId}"
                : $"/Teacher/Attendance/Create?sessionId={SessionId}";
        }
    }
}