using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;
using TutoringCenterManagement.Services.Interfaces;

namespace TutoringCenterManagement.Pages.Admin.Attendance
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
        public List<SuspendedStudentInfo> SuspendedStudents { get; set; } = new();
        public string CreatedByInfo { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string? LastUpdatedByInfo { get; set; }
        public DateTime? LastUpdatedAt { get; set; }

        public class InputModel
        {
            public int SessionId { get; set; }
            public List<int> AttendanceIds { get; set; } = new();
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

            SessionInfo = new SessionInfoModel
            {
                SessionId = session.SessionId,
                ClassId = session.ClassId,
                ClassName = session.Class.ClassCode,
                SessionDate = session.SessionDate,
                ShiftName = $"{session.Shift.ShiftName} ({session.Shift.StartTime:HH:mm}-{session.Shift.EndTime:HH:mm})"
            };

            Input.SessionId = sessionId;

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
                return RedirectToPage("./Index", new { classId = session.ClassId });
            }

            var first = attendances.First();

            // Dùng Service thay vì gọi private method
            CreatedByInfo = _attendanceService.GetUserFullname(first.CreatedByAccount);
            CreatedAt = first.CreatedAt;

            if (first.LastUpdatedBy.HasValue)
            {
                LastUpdatedByInfo = _attendanceService
                    .GetUserFullname(first.LastUpdatedByAccount);
                LastUpdatedAt = first.LastUpdatedAt;
            }

            var recordedStudentIds = attendances.Select(a => a.StudentId).ToHashSet();

            AttendanceList = attendances.Select(a => new AttendanceInfo
            {
                AttendanceId = a.AttendanceId,
                StudentId = a.StudentId,
                StudentName = a.Student.Fullname,
                CurrentSchool = a.Student.CurrentSchool ?? "N/A",
                CurrentStatus = a.Status == AttendanceStatus.Present ? "present" : "absent"
            }).ToList();

            var newStudents = await _context.ClassStudents
                .Where(cs => cs.ClassId == session.ClassId
                    && cs.Status == StudentClassStatus.Active
                    && (cs.LeftAt == null || cs.LeftAt > session.SessionDate)
                    && !recordedStudentIds.Contains(cs.StudentId))
                .Include(cs => cs.Student)
                .Select(cs => new AttendanceInfo
                {
                    AttendanceId = 0,
                    StudentId = cs.StudentId,
                    StudentName = cs.Student.Fullname,
                    CurrentSchool = cs.Student.CurrentSchool ?? "N/A",
                    CurrentStatus = "not_recorded"
                })
                .OrderBy(s => s.StudentName)
                .ToListAsync();

            AttendanceList.AddRange(newStudents);
            AttendanceList = AttendanceList.OrderBy(a => a.StudentName).ToList();

            SuspendedStudents = await _context.ClassStudents
                .Where(cs => cs.ClassId == session.ClassId
                    && cs.Status == StudentClassStatus.Suspended
                    && (cs.LeftAt == null || cs.LeftAt > session.SessionDate))
                .Include(cs => cs.Student)
                .Select(cs => new SuspendedStudentInfo
                {
                    StudentId = cs.StudentId,
                    StudentName = cs.Student.Fullname,
                    CurrentSchool = cs.Student.CurrentSchool ?? "N/A"
                })
                .OrderBy(s => s.StudentName)
                .ToListAsync();

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

                var session = await _context.Sessions.FindAsync(Input.SessionId);
                if (session == null) return NotFound();

                int presentCount = 0, absentCount = 0,
                    deletedCount = 0, createdCount = 0;

                for (int i = 0; i < Input.StudentIds.Count; i++)
                {
                    var attId = i < Input.AttendanceIds.Count ? Input.AttendanceIds[i] : 0;
                    var studentId = Input.StudentIds[i];
                    var statusStr = i < Input.AttendanceStatuses.Count
                        ? Input.AttendanceStatuses[i] : "not_recorded";

                    if (attId > 0)
                    {
                        var existing = await _context.Attendances.FindAsync(attId);
                        if (existing == null) continue;

                        if (statusStr == "not_recorded")
                        {
                            _context.Attendances.Remove(existing);
                            deletedCount++;
                        }
                        else
                        {
                            existing.Status = statusStr == "present"
                                ? AttendanceStatus.Present
                                : AttendanceStatus.Absent;
                            existing.LastUpdatedBy = currentAccountId.Value;
                            existing.LastUpdatedAt = DateTime.Now;

                            if (existing.Status == AttendanceStatus.Present) presentCount++;
                            else absentCount++;
                        }
                    }
                    else
                    {
                        if (statusStr == "not_recorded") continue;

                        var attStatus = statusStr == "present"
                            ? AttendanceStatus.Present
                            : AttendanceStatus.Absent;

                        _context.Attendances.Add(new Data.Entities.Attendance
                        {
                            SessionId = Input.SessionId,
                            StudentId = studentId,
                            Status = attStatus,
                            CheckInTime = DateTime.Now,
                            CreatedBy = currentAccountId.Value,
                            CreatedAt = DateTime.Now
                        });

                        if (attStatus == AttendanceStatus.Present) presentCount++;
                        else absentCount++;
                        createdCount++;
                    }
                }

                await _context.SaveChangesAsync();

                var msg = $"Cập nhật thành công! Có mặt: {presentCount}, Vắng: {absentCount}";
                if (deletedCount > 0) msg += $", Đã xóa: {deletedCount}";
                if (createdCount > 0) msg += $", Tạo mới: {createdCount}";
                TempData["SuccessMessage"] = msg;

                _logger.LogInformation(
                    "Admin updated attendance for session {SessionId}: present={P}, absent={A}, deleted={D}, created={C}",
                    Input.SessionId, presentCount, absentCount, deletedCount, createdCount);

                return RedirectToPage("./Index", new { classId = session.ClassId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating attendance");
                TempData["ErrorMessage"] = "Có lỗi xảy ra!";
                return Page();
            }
        }

        public async Task<IActionResult> OnPostDeleteAsync(int sessionId)
        {
            try
            {
                var session = await _context.Sessions.FindAsync(sessionId);
                if (session == null) return NotFound();

                var attendances = await _context.Attendances
                    .Where(a => a.SessionId == sessionId)
                    .ToListAsync();

                _context.Attendances.RemoveRange(attendances);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Xóa điểm danh thành công!";
                _logger.LogInformation(
                    "Admin deleted all attendance for session {SessionId}", sessionId);

                return RedirectToPage("./Index", new { classId = session.ClassId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting attendance");
                TempData["ErrorMessage"] = "Có lỗi xảy ra!";
                return RedirectToPage();
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

        public class AttendanceInfo
        {
            public int AttendanceId { get; set; }
            public int StudentId { get; set; }
            public string StudentName { get; set; } = string.Empty;
            public string CurrentSchool { get; set; } = string.Empty;
            public string CurrentStatus { get; set; } = "not_recorded";
            public bool IsNew => AttendanceId == 0;
        }

        public class SuspendedStudentInfo
        {
            public int StudentId { get; set; }
            public string StudentName { get; set; } = string.Empty;
            public string CurrentSchool { get; set; } = string.Empty;
        }
    }
}