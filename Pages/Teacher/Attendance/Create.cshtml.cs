using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;
using TutoringCenterManagement.Services.Interfaces;

namespace TutoringCenterManagement.Pages.Teacher.Attendance
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CreateModel> _logger;
        private readonly IAttendanceService _attendanceService;

        public CreateModel(
            ApplicationDbContext context,
            ILogger<CreateModel> logger,
            IAttendanceService attendanceService)
        {
            _context = context;
            _logger = logger;
            _attendanceService = attendanceService;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public SessionInfoModel SessionInfo { get; set; } = new();
        public List<StudentInfo> ActiveStudents { get; set; } = new();
        public List<StudentInfo> SuspendedStudents { get; set; } = new();
        public bool IsWithinAllowedTime { get; set; }

        public class InputModel
        {
            public int SessionId { get; set; }
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
                TempData["ErrorMessage"] = "Bạn không có quyền điểm danh buổi học này!";
                return RedirectToPage("./Index");
            }

            var hasAttendance = await _context.Attendances
                .AnyAsync(a => a.SessionId == sessionId);

            if (hasAttendance)
            {
                TempData["ErrorMessage"] = "Buổi học này đã được điểm danh!";
                return RedirectToPage("./Index");
            }

            // Dùng Service
            IsWithinAllowedTime = _attendanceService.IsWithinAttendanceWindow(
                session.SessionDate,
                session.Shift.StartTime,
                session.Shift.EndTime);

            SessionInfo = new SessionInfoModel
            {
                SessionId = session.SessionId,
                ClassId = session.ClassId,
                ClassName = session.Class.ClassCode,
                SessionDate = session.SessionDate,
                ShiftName = $"{session.Shift.ShiftName} ({session.Shift.StartTime:HH:mm}–{session.Shift.EndTime:HH:mm})"
            };

            Input.SessionId = sessionId;

            var students = await _attendanceService
                .GetStudentsForAttendanceAsync(session.ClassId, session.SessionDate);

            ActiveStudents = students.Active
                .Select(s => new StudentInfo
                {
                    StudentId = s.StudentId,
                    Fullname = s.Fullname,
                    CurrentSchool = s.CurrentSchool,
                    StartedAt = s.StartedAt,
                    IsActive = true
                }).ToList();

            SuspendedStudents = students.Suspended
                .Select(s => new StudentInfo
                {
                    StudentId = s.StudentId,
                    Fullname = s.Fullname,
                    CurrentSchool = s.CurrentSchool,
                    StartedAt = s.StartedAt,
                    IsActive = false
                }).ToList();

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
                TempData["ErrorMessage"] = "Bạn không có quyền điểm danh buổi học này!";
                return RedirectToPage("./Index");
            }

            // Dùng Service kiểm tra giờ
            if (!_attendanceService.IsWithinAttendanceWindow(
                    session.SessionDate,
                    session.Shift.StartTime,
                    session.Shift.EndTime))
            {
                TempData["ErrorMessage"] = "Không thể điểm danh! Ngoài thời gian cho phép.";
                return RedirectToPage("./Index");
            }

            try
            {
                var attendances = new List<Data.Entities.Attendance>();

                for (int i = 0; i < Input.StudentIds.Count; i++)
                {
                    var statusStr = i < Input.AttendanceStatuses.Count
                        ? Input.AttendanceStatuses[i] : "not_recorded";

                    if (statusStr == "not_recorded") continue;

                    attendances.Add(new Data.Entities.Attendance
                    {
                        SessionId = Input.SessionId,
                        StudentId = Input.StudentIds[i],
                        Status = statusStr == "present"
                                        ? AttendanceStatus.Present
                                        : AttendanceStatus.Absent,
                        CheckInTime = DateTime.Now,
                        CreatedBy = teacherId.Value,
                        CreatedAt = DateTime.Now
                    });
                }

                _context.Attendances.AddRange(attendances);
                await _context.SaveChangesAsync();

                var p = attendances.Count(a => a.Status == AttendanceStatus.Present);
                var a2 = attendances.Count(a => a.Status == AttendanceStatus.Absent);
                TempData["SuccessMessage"] =
                    $"Điểm danh thành công! Có mặt: {p} · Vắng: {a2} · Chưa GN: {Input.StudentIds.Count - attendances.Count}";

                _logger.LogInformation(
                    "Teacher {T} created attendance for session {S}",
                    teacherId.Value, Input.SessionId);

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attendance for session {S}", Input.SessionId);
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi lưu điểm danh!";
                return Page();
            }
        }

        public class SessionInfoModel
        {
            public int SessionId { get; set; }
            public int ClassId { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public DateOnly SessionDate { get; set; }
            public string ShiftName { get; set; } = string.Empty;
        }

        public class StudentInfo
        {
            public int StudentId { get; set; }
            public string Fullname { get; set; } = string.Empty;
            public string CurrentSchool { get; set; } = string.Empty;
            public DateOnly StartedAt { get; set; }
            public bool IsActive { get; set; }
        }
    }
}