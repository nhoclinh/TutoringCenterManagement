using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;
using TutoringCenterManagement.Services.Interfaces;

namespace TutoringCenterManagement.Pages.Teacher.Attendance
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IAttendanceService _attendanceService;

        public IndexModel(
            ApplicationDbContext context,
            IAttendanceService attendanceService)
        {
            _context = context;
            _attendanceService = attendanceService;
        }

        public List<SessionInfo> Sessions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ToDate { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var role = HttpContext.Session.GetString("Role");
            var teacherId = HttpContext.Session.GetInt32("AccountId");

            if (role != "Teacher" || !teacherId.HasValue)
                return RedirectToPage("/Account/Login");

            var query = _context.Sessions
                .Where(s => s.TeacherId == teacherId.Value
                         || s.TeacherAssistantId == teacherId.Value)
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .Include(s => s.Attendances)
                .AsQueryable();

            if (!string.IsNullOrEmpty(FromDate) && DateOnly.TryParse(FromDate, out var from))
                query = query.Where(s => s.SessionDate >= from);

            if (!string.IsNullOrEmpty(ToDate) && DateOnly.TryParse(ToDate, out var to))
                query = query.Where(s => s.SessionDate <= to);

            var sessions = await query
                .OrderByDescending(s => s.SessionDate)
                .Take(50)
                .ToListAsync();

            Sessions = new List<SessionInfo>();

            foreach (var s in sessions)
            {
                var totalStudents = await _context.ClassStudents
                    .Where(cs => cs.ClassId == s.ClassId
                              && cs.Status == StudentClassStatus.Active)
                    .CountAsync();

                // Dùng Service thay vì tính thủ công
                var hoursRemaining = _attendanceService
                    .GetHoursRemaining(s.SessionDate, s.Shift.EndTime);

                var isFuture = DateTime.Now <
                    s.SessionDate.ToDateTime(s.Shift.StartTime);

                Sessions.Add(new SessionInfo
                {
                    SessionId = s.SessionId,
                    SessionDate = s.SessionDate,
                    ClassName = s.Class.ClassCode,
                    ShiftName = s.Shift.ShiftName,
                    RoomCode = s.Room.RoomCode,
                    AttendanceCount = s.Attendances.Count,
                    TotalStudents = totalStudents,
                    CanEdit = s.Attendances.Any() && hoursRemaining.HasValue,
                    HoursRemaining = hoursRemaining,
                    IsFuture = isFuture
                });
            }

            return Page();
        }

        public class SessionInfo
        {
            public int SessionId { get; set; }
            public DateOnly SessionDate { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public string ShiftName { get; set; } = string.Empty;
            public string RoomCode { get; set; } = string.Empty;
            public int AttendanceCount { get; set; }
            public int TotalStudents { get; set; }
            public bool CanEdit { get; set; }
            public double? HoursRemaining { get; set; }
            public bool IsFuture { get; set; }
        }
    }
}