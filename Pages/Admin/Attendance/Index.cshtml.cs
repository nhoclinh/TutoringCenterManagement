using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Attendance
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<ClassInfo> Classes { get; set; } = new();
        public List<SessionInfo> Sessions { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public int? ClassId { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? FromDate { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? ToDate { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var role = HttpContext.Session.GetString("Role");
            if (role != "Admin")
                return RedirectToPage("/Account/Login");

            // Luon load danh sach lop cho dropdown
            Classes = await _context.Classes
                .Select(c => new ClassInfo { ClassId = c.ClassId, ClassCode = c.ClassCode })
                .OrderBy(c => c.ClassCode)
                .ToListAsync();

            // Chi query khi co it nhat 1 bo loc (tranh load toan bo khi vao trang lan dau)
            bool hasFilter = ClassId.HasValue
                || !string.IsNullOrEmpty(FromDate)
                || !string.IsNullOrEmpty(ToDate);

            if (hasFilter)
            {
                var query = _context.Sessions
                    .Include(s => s.Class)
                    .Include(s => s.Shift)
                    .Include(s => s.Room)
                    .Include(s => s.Teacher)
                    .Include(s => s.TeacherAssistant)
                    .Include(s => s.Attendances)
                    .AsQueryable();

                // Loc theo lop (neu co)
                if (ClassId.HasValue)
                    query = query.Where(s => s.ClassId == ClassId.Value);

                // Loc theo khoang ngay
                if (!string.IsNullOrEmpty(FromDate) && DateOnly.TryParse(FromDate, out var from))
                    query = query.Where(s => s.SessionDate >= from);

                if (!string.IsNullOrEmpty(ToDate) && DateOnly.TryParse(ToDate, out var to))
                    query = query.Where(s => s.SessionDate <= to);

                var sessions = await query
                    .OrderByDescending(s => s.SessionDate)
                    .ToListAsync();

                // Dem hoc sinh active theo tung lop (cache tranh N+1)
                var classIds = sessions.Select(s => s.ClassId).Distinct().ToList();
                var studentCounts = await _context.ClassStudents
                    .Where(cs => classIds.Contains(cs.ClassId) && cs.Status == StudentClassStatus.Active)
                    .GroupBy(cs => cs.ClassId)
                    .Select(g => new { ClassId = g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.ClassId, x => x.Count);

                Sessions = sessions.Select(s => new SessionInfo
                {
                    SessionId = s.SessionId,
                    ClassId = s.ClassId,
                    ClassName = s.Class.ClassCode,
                    SessionDate = s.SessionDate,
                    ShiftName = s.Shift.ShiftName,
                    RoomCode = s.Room.RoomCode,
                    TeacherName = s.Teacher.Fullname +
                                      (s.TeacherAssistant != null ? $", {s.TeacherAssistant.Fullname}" : ""),
                    AttendanceCount = s.Attendances.Count,
                    TotalStudents = studentCounts.GetValueOrDefault(s.ClassId, 0)
                }).ToList();
            }

            return Page();
        }

        public class ClassInfo
        {
            public int ClassId { get; set; }
            public string ClassCode { get; set; } = string.Empty;
        }

        public class SessionInfo
        {
            public int SessionId { get; set; }
            public int ClassId { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public DateOnly SessionDate { get; set; }
            public string ShiftName { get; set; } = string.Empty;
            public string RoomCode { get; set; } = string.Empty;
            public string TeacherName { get; set; } = string.Empty;
            public int AttendanceCount { get; set; }
            public int TotalStudents { get; set; }
        }
    }
}