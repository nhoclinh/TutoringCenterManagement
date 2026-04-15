using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Student
{
    /// <summary>
    /// Trang lịch học dùng chung cho Student và Parent.
    /// Parent xem lịch của tất cả con em.
    /// </summary>
    public class ScheduleModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public ScheduleModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ──────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string? ClassFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? ToDate { get; set; }
        /// <summary>Dành cho Parent: lọc theo StudentId cụ thể (0 = tất cả con)</summary>
        [BindProperty(SupportsGet = true)] public int? StudentIdFilter { get; set; }

        // ── Thông tin người dùng ─────────────────────────────────────
        public string ViewerName { get; set; } = string.Empty;
        public string ViewerRole { get; set; } = string.Empty; // "Student" | "Parent"
        public bool IsParent => ViewerRole == "Parent";

        // ── KPI ─────────────────────────────────────────────────────
        public int TodaySessions { get; set; }
        public int ThisWeekSessions { get; set; }
        public int UpcomingSessions { get; set; }
        public int TotalClasses { get; set; }
        public int CompletedSessions { get; set; }
        public int CancelledSessions { get; set; }
        public int AttendedSessions { get; set; }   // Buổi đã có điểm danh Present
        public int AbsentSessions { get; set; }   // Buổi vắng mặt

        // ── Danh sách con em (Parent only) ──────────────────────────
        public List<ChildItem> MyChildren { get; set; } = new();

        // ── Danh sách lớp để filter ──────────────────────────────────
        public List<ClassItem> MyClasses { get; set; } = new();

        // ── Danh sách buổi học ───────────────────────────────────────
        public List<SessionItem> Sessions { get; set; } = new();

        // ── Thống kê điểm danh (dùng cho Student profile sidebar) ───
        public double AttendanceRate { get; set; }

        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            var role = HttpContext.Session.GetString("Role");
            var accountId = HttpContext.Session.GetInt32("AccountId");

            if (accountId == null || (role != "Student" && role != "Parent"))
                return RedirectToPage("/Account/Login");

            ViewerRole = role!;
            var aid = accountId.Value;

            // ── Xác định danh sách studentId cần xem ─────────────────
            List<int> studentIds = new();

            if (role == "Student")
            {
                var student = await _context.Students.FindAsync(aid);
                ViewerName = student?.Fullname ?? string.Empty;
                studentIds.Add(aid);
            }
            else // Parent
            {
                var parent = await _context.Parents
                    .Include(p => p.Students)
                    .FirstOrDefaultAsync(p => p.AccountId == aid);

                ViewerName = parent?.Fullname ?? string.Empty;
                MyChildren = parent?.Students.Select(s => new ChildItem
                {
                    StudentId = s.AccountId,
                    Fullname = s.Fullname,
                    School = s.CurrentSchool
                }).ToList() ?? new();

                studentIds = StudentIdFilter.HasValue && StudentIdFilter > 0
                    ? new List<int> { StudentIdFilter.Value }
                    : MyChildren.Select(c => c.StudentId).ToList();
            }

            if (!studentIds.Any()) return Page();

            var today = DateOnly.FromDateTime(DateTime.Today);
            var startOfWeek = today.AddDays(-(int)today.DayOfWeek + 1);
            var endOfWeek = startOfWeek.AddDays(6);

            // ── Base query: sessions có học sinh trong danh sách ───────
            var baseQ = _context.Sessions
                .Where(s => s.Class.ClassStudents.Any(cs =>
                    studentIds.Contains(cs.StudentId) &&
                    cs.Status == StudentClassStatus.Active));

            // ── KPI ──────────────────────────────────────────────────
            TodaySessions = await baseQ.CountAsync(s => s.SessionDate == today);
            ThisWeekSessions = await baseQ.CountAsync(s => s.SessionDate >= startOfWeek && s.SessionDate <= endOfWeek);
            UpcomingSessions = await baseQ.CountAsync(s => s.SessionDate > today && s.Status == SessionStatus.Scheduled);
            TotalClasses = await baseQ.Select(s => s.ClassId).Distinct().CountAsync();
            CompletedSessions = await baseQ.CountAsync(s => s.Status == SessionStatus.Completed);
            CancelledSessions = await baseQ.CountAsync(s => s.Status == SessionStatus.Cancelled);

            // Điểm danh
            AttendedSessions = await _context.Attendances
                .CountAsync(a => studentIds.Contains(a.StudentId) && a.Status == AttendanceStatus.Present);
            AbsentSessions = await _context.Attendances
                .CountAsync(a => studentIds.Contains(a.StudentId) && a.Status == AttendanceStatus.Absent);

            var totalRecorded = AttendedSessions + AbsentSessions;
            AttendanceRate = totalRecorded > 0 ? (double)AttendedSessions / totalRecorded * 100 : 0;

            // ── Danh sách lớp ─────────────────────────────────────────
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
                .Include(s => s.Attendances.Where(a => studentIds.Contains(a.StudentId)))
                .AsQueryable();

            if (!string.IsNullOrEmpty(ClassFilter) && int.TryParse(ClassFilter, out var cid))
                query = query.Where(s => s.ClassId == cid);

            if (!string.IsNullOrEmpty(StatusFilter) && int.TryParse(StatusFilter, out var st))
                query = query.Where(s => (int)s.Status == st);

            if (!string.IsNullOrEmpty(FromDate) && DateOnly.TryParse(FromDate, out var fd))
                query = query.Where(s => s.SessionDate >= fd);

            if (!string.IsNullOrEmpty(ToDate) && DateOnly.TryParse(ToDate, out var td2))
                query = query.Where(s => s.SessionDate <= td2);

            var raw = await query
                .OrderByDescending(s => s.SessionDate)
                .ThenBy(s => s.Shift.StartTime)
                .Take(100)
                .ToListAsync();

            Sessions = raw.Select(s =>
            {
                // Lấy attendance của học sinh được filter
                var att = s.Attendances.FirstOrDefault(a => studentIds.Contains(a.StudentId));
                AttendanceStatus? attStatus = att?.Status;

                return new SessionItem
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
                    Status = s.Status,
                    IsFromTemplate = s.TemplateId.HasValue,
                    AttendanceStatus = attStatus,
                    Note = s.Note,
                };
            }).ToList();

            return Page();
        }

        // ── Calendar events handler ───────────────────────────────────
        public async Task<JsonResult> OnGetGetEventsAsync(
            string start, string end,
            string? classFilter, string? statusFilter, int? studentIdFilter)
        {
            var role = HttpContext.Session.GetString("Role");
            var accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null) return new JsonResult(Array.Empty<object>());

            var aid = accountId.Value;
            List<int> studentIds = new();

            if (role == "Student")
            {
                studentIds.Add(aid);
            }
            else
            {
                var children = await _context.Students
                    .Where(s => s.ParentId == aid)
                    .Select(s => s.AccountId)
                    .ToListAsync();

                studentIds = studentIdFilter.HasValue && studentIdFilter > 0
                    ? new List<int> { studentIdFilter.Value }
                    : children;
            }

            if (!studentIds.Any()) return new JsonResult(Array.Empty<object>());

            var startDate = DateOnly.Parse(start.Split('T')[0]);
            var endDate = DateOnly.Parse(end.Split('T')[0]);

            var query = _context.Sessions
                .Where(s =>
                    s.Class.ClassStudents.Any(cs => studentIds.Contains(cs.StudentId) && cs.Status == StudentClassStatus.Active)
                    && s.SessionDate >= startDate
                    && s.SessionDate <= endDate)
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .Include(s => s.Teacher)
                .Include(s => s.Attendances.Where(a => studentIds.Contains(a.StudentId)))
                .AsQueryable();

            if (!string.IsNullOrEmpty(classFilter) && int.TryParse(classFilter, out var cid))
                query = query.Where(s => s.ClassId == cid);

            if (!string.IsNullOrEmpty(statusFilter) && int.TryParse(statusFilter, out var st))
                query = query.Where(s => (int)s.Status == st);

            var sessions = await query.ToListAsync();

            var events = sessions.Select(s =>
            {
                var att = s.Attendances.FirstOrDefault(a => studentIds.Contains(a.StudentId));
                var attCls = att == null ? "not-recorded"
                    : att.Status == AttendanceStatus.Present ? "present"
                    : "absent";

                var statusCls = s.Status switch
                {
                    SessionStatus.Scheduled => "scheduled",
                    SessionStatus.Ongoing => "ongoing",
                    SessionStatus.Completed => "completed",
                    SessionStatus.Cancelled => "cancelled",
                    _ => "scheduled"
                };

                return new
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
                        statusCls,
                        attCls,
                        note = s.Note ?? "",
                    }
                };
            });

            return new JsonResult(events);
        }

        // ── View Models ───────────────────────────────────────────────
        public class ChildItem
        {
            public int StudentId { get; set; }
            public string Fullname { get; set; } = string.Empty;
            public string? School { get; set; }
        }

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
            public SessionStatus Status { get; set; }
            public bool IsFromTemplate { get; set; }
            public AttendanceStatus? AttendanceStatus { get; set; }
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

            /// <summary>CSS class cho trạng thái điểm danh</summary>
            public string AttendanceCls => AttendanceStatus switch
            {
                Data.Enums.AttendanceStatus.Present => "present",
                Data.Enums.AttendanceStatus.Absent => "absent",
                _ => "not-recorded"
            };

            public string AttendanceLabel => AttendanceStatus switch
            {
                Data.Enums.AttendanceStatus.Present => "Có mặt",
                Data.Enums.AttendanceStatus.Absent => "Vắng mặt",
                _ => Status == SessionStatus.Completed ? "Chưa GN" : "—"
            };
        }
    }
}