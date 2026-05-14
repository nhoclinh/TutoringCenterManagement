using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Sessions
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<SessionViewModel> Sessions { get; set; } = new();
        public List<ClassInfo> Classes { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ClassFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ToDate { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            Classes = await _context.Classes
                .Select(c => new ClassInfo { ClassId = c.ClassId, ClassCode = c.ClassCode })
                .ToListAsync();

            var query = _context.Sessions
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .Include(s => s.Teacher)
                .Include(s => s.TeacherAssistant)
                .AsQueryable();

            if (!string.IsNullOrEmpty(ClassFilter) && int.TryParse(ClassFilter, out int classId))
                query = query.Where(s => s.ClassId == classId);

            if (!string.IsNullOrEmpty(StatusFilter) && int.TryParse(StatusFilter, out int status))
                query = query.Where(s => s.Status == (SessionStatus)status);

            if (!string.IsNullOrEmpty(FromDate) && DateOnly.TryParse(FromDate, out var from))
                query = query.Where(s => s.SessionDate >= from);

            if (!string.IsNullOrEmpty(ToDate) && DateOnly.TryParse(ToDate, out var to))
                query = query.Where(s => s.SessionDate <= to);

            var sessions = await query
                .OrderByDescending(s => s.SessionDate)
                .Take(100)
                .ToListAsync();

            // Thu thập SessionId đã có điểm danh (1 query duy nhất)
            var sessionIds = sessions.Select(s => s.SessionId).ToList();
            var attendedIds = await _context.Attendances
                .Where(a => sessionIds.Contains(a.SessionId))
                .Select(a => a.SessionId)
                .Distinct()
                .ToListAsync(); // Lấy dữ liệu về client
                                // Chuyển sang HashSet ở bộ nhớ

            Sessions = sessions.Select(s => new SessionViewModel
            {
                SessionId = s.SessionId,
                SessionDate = s.SessionDate,
                ClassName = s.Class.ClassCode,
                ShiftName = s.Shift.ShiftName,
                ShiftTime = $"{s.Shift.StartTime:HH:mm}-{s.Shift.EndTime:HH:mm}",
                RoomCode = s.Room.RoomCode,
                TeacherName = s.Teacher.Fullname +
                                 (s.TeacherAssistant != null ? $", {s.TeacherAssistant.Fullname} (TG)" : ""),
                Status = s.Status,
                IsFromTemplate = s.TemplateId.HasValue,
                HasAttendance = attendedIds.Contains(s.SessionId)
            }).ToList();

            return Page();
        }

        /// <summary>
        /// API endpoint cho FullCalendar — trả về JSON events với đầy đủ thông tin chi tiết.
        /// Hỗ trợ lọc theo classFilter và statusFilter (giống OnGetAsync).
        /// </summary>
        public async Task<JsonResult> OnGetGetCalendarEventsAsync(
            string start, string end,
            string? classFilter = null,
            string? statusFilter = null)
        {
            var startDate = DateOnly.Parse(start.Split('T')[0]);
            var endDate = DateOnly.Parse(end.Split('T')[0]);

            var query = _context.Sessions
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .Include(s => s.Teacher)
                .Include(s => s.TeacherAssistant)
                .Where(s => s.SessionDate >= startDate && s.SessionDate <= endDate)
                .AsQueryable();

            if (!string.IsNullOrEmpty(classFilter) && int.TryParse(classFilter, out int classId))
                query = query.Where(s => s.ClassId == classId);

            if (!string.IsNullOrEmpty(statusFilter) && int.TryParse(statusFilter, out int status))
                query = query.Where(s => s.Status == (SessionStatus)status);

            // Lịch không hiển thị buổi đã hủy — chỉ hiện trong danh sách
            query = query.Where(s => s.Status != SessionStatus.Cancelled);

            var sessions = await query.ToListAsync();

            // Lấy danh sách sessionId đã có điểm danh
            var calSessionIds = sessions.Select(s => s.SessionId).ToList();
            var calAttendedIds = (await _context.Attendances
                .Where(a => calSessionIds.Contains(a.SessionId))
                .Select(a => a.SessionId)
                .Distinct()
                .ToListAsync()).ToHashSet();

            // Map status → CSS class name (phải khớp với JS statusLabels)
            static string StatusCls(SessionStatus st) => st switch
            {
                SessionStatus.Scheduled => "scheduled",
                SessionStatus.Ongoing => "ongoing",
                SessionStatus.Completed => "completed",
                SessionStatus.Cancelled => "cancelled",
                _ => ""
            };

            // Map DayOfWeek → tiếng Việt
            static string DowVi(DayOfWeek d) => d switch
            {
                DayOfWeek.Monday => "Thứ hai",
                DayOfWeek.Tuesday => "Thứ ba",
                DayOfWeek.Wednesday => "Thứ tư",
                DayOfWeek.Thursday => "Thứ năm",
                DayOfWeek.Friday => "Thứ sáu",
                DayOfWeek.Saturday => "Thứ bảy",
                _ => "Chủ nhật"
            };

            var events = sessions.Select(s => new
            {
                title = s.Class.ClassCode,
                start = s.SessionDate.ToString("yyyy-MM-dd") + "T" + s.Shift.StartTime.ToString("HH:mm"),
                end = s.SessionDate.ToString("yyyy-MM-dd") + "T" + s.Shift.EndTime.ToString("HH:mm"),
                extendedProps = new
                {
                    sessionId = s.SessionId,
                    date = s.SessionDate.ToString("dd/MM/yyyy"),
                    dow = DowVi(s.SessionDate.DayOfWeek),
                    shift = s.Shift.ShiftName,
                    shiftTime = $"{s.Shift.StartTime:HH:mm}-{s.Shift.EndTime:HH:mm}",
                    room = s.Room.RoomCode,
                    teacher = s.Teacher.Fullname +
                                 (s.TeacherAssistant != null ? $", {s.TeacherAssistant.Fullname} (TG)" : ""),
                    statusCls = StatusCls(s.Status),
                    isTemplate = s.TemplateId.HasValue,
                    hasAttendance = calAttendedIds.Contains(s.SessionId)
                }
            });

            return new JsonResult(events);
        }

        public async Task<IActionResult> OnPostDeleteAsync(int sessionId)
        {
            try
            {
                var session = await _context.Sessions.FindAsync(sessionId);
                if (session == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy buổi học!";
                    return RedirectToPage();
                }

                _context.Sessions.Remove(session);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Xóa buổi học thành công!";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = "Không thể xóa buổi học này vì đã có điểm danh!";
            }

            return RedirectToPage();
        }

        // ─── View models ───────────────────────────────────────────────────────

        public class SessionViewModel
        {
            public int SessionId { get; set; }
            public DateOnly SessionDate { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public string ShiftName { get; set; } = string.Empty;
            public string ShiftTime { get; set; } = string.Empty;
            public string RoomCode { get; set; } = string.Empty;
            public string TeacherName { get; set; } = string.Empty;
            public SessionStatus Status { get; set; }
            public bool IsFromTemplate { get; set; }

            /// <summary>
            /// True = buổi đã có ít nhất 1 bản ghi điểm danh → dẫn vào Edit.
            /// False = chưa điểm danh → dẫn vào Create.
            /// </summary>
            public bool HasAttendance { get; set; }

            /// <summary>Link điểm danh đúng (Create hoặc Edit) theo HasAttendance</summary>
            public string AttendanceUrl => HasAttendance
                ? $"/Admin/Attendance/Edit?sessionId={SessionId}"
                : $"/Admin/Attendance/Create?sessionId={SessionId}";
        }

        public class ClassInfo
        {
            public int ClassId { get; set; }
            public string ClassCode { get; set; } = string.Empty;
        }
    }
}