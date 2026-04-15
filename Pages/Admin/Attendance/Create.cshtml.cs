using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;
using TutoringCenterManagement.Services.Interfaces;

namespace TutoringCenterManagement.Pages.Admin.Attendance
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

        public class InputModel
        {
            public int SessionId { get; set; }
            public List<int> StudentIds { get; set; } = new();
            public List<string> AttendanceStatuses { get; set; } = new();
        }

        public async Task<IActionResult> OnGetAsync(int sessionId)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var session = await _context.Sessions
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null) return NotFound();

            var hasAttendance = await _context.Attendances
                .AnyAsync(a => a.SessionId == sessionId);

            if (hasAttendance)
            {
                TempData["ErrorMessage"] = "Buổi học này đã được điểm danh!";
                return RedirectToPage("./Index", new { classId = session.ClassId });
            }

            SessionInfo = new SessionInfoModel
            {
                SessionId = session.SessionId,
                ClassId = session.ClassId,
                ClassName = session.Class.ClassCode,
                SessionDate = session.SessionDate,
                ShiftName = $"{session.Shift.ShiftName} ({session.Shift.StartTime:HH:mm}-{session.Shift.EndTime:HH:mm})"
            };

            Input.SessionId = sessionId;

            // Dùng Service thay vì query thủ công
            var students = await _attendanceService
                .GetStudentsForAttendanceAsync(session.ClassId, session.SessionDate);

            ActiveStudents = students.Active
                .Select(s => new StudentInfo
                {
                    StudentId = s.StudentId,
                    Fullname = s.Fullname,
                    CurrentSchool = s.CurrentSchool,
                    StartedAt = s.StartedAt,
                    ClassStatus = StudentClassStatus.Active
                }).ToList();

            SuspendedStudents = students.Suspended
                .Select(s => new StudentInfo
                {
                    StudentId = s.StudentId,
                    Fullname = s.Fullname,
                    CurrentSchool = s.CurrentSchool,
                    StartedAt = s.StartedAt,
                    ClassStatus = StudentClassStatus.Suspended
                }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            try
            {
                var currentAccountId = HttpContext.Session.GetInt32("AccountId");
                if (!currentAccountId.HasValue)
                {
                    TempData["ErrorMessage"] = "Không xác định được người dùng!";
                    return RedirectToPage("/Account/Login");
                }

                var session = await _context.Sessions
                    .FirstOrDefaultAsync(s => s.SessionId == Input.SessionId);

                if (session == null) return NotFound();

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
                        CreatedBy = currentAccountId.Value,
                        CreatedAt = DateTime.Now
                    });
                }

                _context.Attendances.AddRange(attendances);
                await _context.SaveChangesAsync();

                var presentCount = attendances.Count(a => a.Status == AttendanceStatus.Present);
                var absentCount = attendances.Count(a => a.Status == AttendanceStatus.Absent);
                var notRecordedCount = Input.StudentIds.Count - attendances.Count;

                var msg = $"Điểm danh thành công! Có mặt: {presentCount}, Vắng: {absentCount}";
                if (notRecordedCount > 0) msg += $", Chưa ghi nhận: {notRecordedCount}";
                TempData["SuccessMessage"] = msg;

                _logger.LogInformation(
                    "Admin created attendance for session {SessionId}: present={P}, absent={A}, not_recorded={N}",
                    Input.SessionId, presentCount, absentCount, notRecordedCount);

                return RedirectToPage("./Index", new { classId = session.ClassId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating attendance");
                TempData["ErrorMessage"] = "Có lỗi xảy ra khi điểm danh!";
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
            public StudentClassStatus ClassStatus { get; set; }
            public bool IsSuspended => ClassStatus == StudentClassStatus.Suspended;
        }
    }
}