using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;
using TutoringCenterManagement.Services.Interfaces;

namespace TutoringCenterManagement.Pages.Teacher.Attendance
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EditModel> _logger;
        private readonly IAttendanceService _attendanceService;

        public EditModel(
            ApplicationDbContext context,
            ILogger<EditModel> logger,
            IAttendanceService attendanceService)
        {
            _context = context;
            _logger = logger;
            _attendanceService = attendanceService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public SessionInfoModel SessionInfo { get; set; } = new();
        public List<AttendanceInfo> AttendanceList { get; set; } = new();
        public List<SuspendedInfo> SuspendedStudents { get; set; } = new();
        public string CreatedByInfo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? LastUpdatedByInfo { get; set; }
        public DateTime? LastUpdatedAt { get; set; }
        public double? HoursRemaining { get; set; }

        public class InputModel
        {
            public int SessionId { get; set; }
            public List<int> AttendanceIds { get; set; } = new();
            public List<int> StudentIds { get; set; } = new();
            public List<string> AttendanceStatuses { get; set; } = new();
        }

        public async Task<IActionResult> OnGetAsync(int sessionId)
        {
            var role = HttpContext.Session.GetString("Role");
            var teacherId = HttpContext.Session.GetInt32("AccountId");

            if (role != "Teacher" || !teacherId.HasValue)
                return RedirectToPage("/Account/Login");

            var session = await _context.Sessions
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return NotFound();

            if (session.TeacherId != teacherId.Value
                && session.TeacherAssistantId != teacherId.Value)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền xem điểm danh buổi học này!";
                return RedirectToPage("./Index");
            }

            // Dùng Service
            HoursRemaining = _attendanceService
                .GetHoursRemaining(session.SessionDate, session.Shift.EndTime);

            var attendances = await _context.Attendances
                .Where(a => a.SessionId == sessionId)
                .Include(a => a.Student)
                .Include(a => a.CreatedByAccount).ThenInclude(acc => acc.Staff)
                .Include(a => a.CreatedByAccount).ThenInclude(acc => acc.Teacher)
                .Include(a => a.LastUpdatedByAccount).ThenInclude(acc => acc.Staff)
                .Include(a => a.LastUpdatedByAccount).ThenInclude(acc => acc.Teacher)
                .OrderBy(a => a.Student.Fullname)
                .ToListAsync();

            if (!attendances.Any())
            {
                TempData["ErrorMessage"] = "Chưa có điểm danh cho buổi học này!";
                return RedirectToPage("./Index");
            }

            var firstAtt = attendances.First();

            // Dùng Service thay vì private method
            CreatedByInfo = _attendanceService.GetUserFullname(firstAtt.CreatedByAccount);
            CreatedAt = firstAtt.CreatedAt;

            if (firstAtt.LastUpdatedBy.HasValue)
            {
                LastUpdatedByInfo = _attendanceService
                    .GetUserFullname(firstAtt.LastUpdatedByAccount);
                LastUpdatedAt = firstAtt.LastUpdatedAt;
            }

            var activeStudentIds = await _context.ClassStudents
                .Where(cs => cs.ClassId == session.ClassId
                          && cs.Status == StudentClassStatus.Active
                          && (cs.LeftAt == null || cs.LeftAt > session.SessionDate))
                .Select(cs => cs.StudentId)
                .ToListAsync();

            AttendanceList = attendances.Select(a => new AttendanceInfo
            {
                AttendanceId = a.AttendanceId,
                StudentId = a.StudentId,
                StudentName = a.Student.Fullname,
                CurrentSchool = a.Student.CurrentSchool ?? "N/A",
                CurrentStatus = a.Status == AttendanceStatus.Present ? "present"
                              : a.Status == AttendanceStatus.Absent ? "absent"
                              : "not_recorded",
                IsNew = !activeStudentIds.Contains(a.StudentId)
            }).ToList();

            SuspendedStudents = await _context.ClassStudents
                .Where(cs => cs.ClassId == session.ClassId
                          && cs.Status == StudentClassStatus.Suspended)
                .Include(cs => cs.Student)
                .Select(cs => new SuspendedInfo
                {
                    StudentId = cs.StudentId,
                    StudentName = cs.Student.Fullname,
                    CurrentSchool = cs.Student.CurrentSchool ?? "N/A"
                })
                .OrderBy(s => s.StudentName)
                .ToListAsync();

            SessionInfo = new SessionInfoModel
            {
                SessionId = session.SessionId,
                ClassId = session.ClassId,
                ClassName = session.Class.ClassCode,
                SessionDate = session.SessionDate,
                ShiftName = $"{session.Shift.ShiftName} ({session.Shift.StartTime:HH:mm}–{session.Shift.EndTime:HH:mm})"
            };

            Input.SessionId = sessionId;
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var teacherId = HttpContext.Session.GetInt32("AccountId");
            if (!teacherId.HasValue) return RedirectToPage("/Account/Login");

            var session = await _context.Sessions
                .Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.SessionId == Input.SessionId);

            if (session == null) return NotFound();

            if (session.TeacherId != teacherId.Value
                && session.TeacherAssistantId != teacherId.Value)
            {
                TempData["ErrorMessage"] = "Bạn không có quyền sửa điểm danh buổi học này!";
                return RedirectToPage("./Index");
            }

            // Dùng Service kiểm tra giờ
            if (!_attendanceService.GetHoursRemaining(
                    session.SessionDate, session.Shift.EndTime).HasValue)
            {
                TempData["ErrorMessage"] =
                    "Không thể lưu! Đã quá 3 giờ kể từ khi buổi học kết thúc.";
                return RedirectToPage("./Index");
            }

            for (int i = 0; i < Input.AttendanceIds.Count; i++)
            {
                var statusStr = i < Input.AttendanceStatuses.Count
                    ? Input.AttendanceStatuses[i] : "not_recorded";

                var att = await _context.Attendances.FindAsync(Input.AttendanceIds[i]);
                if (att == null) continue;

                att.Status = statusStr == "present"
                    ? AttendanceStatus.Present
                    : AttendanceStatus.Absent;
                att.LastUpdatedBy = teacherId.Value;
                att.LastUpdatedAt = DateTime.Now;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật điểm danh thành công!";
            return RedirectToPage("./Index");
        }

        public async Task<IActionResult> OnPostDeleteAsync()
        {
            var teacherId = HttpContext.Session.GetInt32("AccountId");
            if (!teacherId.HasValue) return RedirectToPage("/Account/Login");

            var session = await _context.Sessions
                .Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.SessionId == Input.SessionId);

            if (session == null) return NotFound();

            // Dùng Service kiểm tra giờ
            if (!_attendanceService.GetHoursRemaining(
                    session.SessionDate, session.Shift.EndTime).HasValue)
            {
                TempData["ErrorMessage"] =
                    "Không thể xóa! Đã quá 3 giờ kể từ khi buổi học kết thúc.";
                return RedirectToPage("./Index");
            }

            var attendances = await _context.Attendances
                .Where(a => a.SessionId == Input.SessionId)
                .ToListAsync();

            _context.Attendances.RemoveRange(attendances);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Đã xóa toàn bộ điểm danh ({attendances.Count} bản ghi).";
            return RedirectToPage("./Index");
        }

        public class SessionInfoModel
        {
            public int SessionId { get; set; }
            public int ClassId { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public DateOnly SessionDate { get; set; }
            public string ShiftName { get; set; } = string.Empty;
        }

        public class AttendanceInfo
        {
            public int AttendanceId { get; set; }
            public int StudentId { get; set; }
            public string StudentName { get; set; } = string.Empty;
            public string CurrentSchool { get; set; } = string.Empty;
            public string CurrentStatus { get; set; } = "absent";
            public bool IsNew { get; set; }
        }

        public class SuspendedInfo
        {
            public int StudentId { get; set; }
            public string StudentName { get; set; } = string.Empty;
            public string CurrentSchool { get; set; } = string.Empty;
        }
    }
}