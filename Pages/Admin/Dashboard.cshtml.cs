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

        public int TotalStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalClasses { get; set; }
        public int TodaySessions { get; set; }

        public string Last7Days { get; set; } = "[]";
        public string AttendancePresentData { get; set; } = "[]";
        public string AttendanceAbsentData { get; set; } = "[]";
        public string ClassNames { get; set; } = "[]";
        public string ClassStudentCounts { get; set; } = "[]";

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            TotalStudents = await _context.Students.CountAsync();
            TotalTeachers = await _context.Teachers.CountAsync();
            TotalClasses = await _context.Classes.CountAsync();

            var today = DateOnly.FromDateTime(DateTime.Today);
            TodaySessions = await _context.Sessions
                .Where(s => s.SessionDate == today)
                .CountAsync();

            var last7Days = Enumerable.Range(0, 7)
                .Select(i => DateTime.Today.AddDays(-6 + i))
                .ToList();

            var attendanceStats = new List<int>();
            var absentStats = new List<int>();

            foreach (var day in last7Days)
            {
                var dayOnly = DateOnly.FromDateTime(day);
                var sessionIds = await _context.Sessions
                    .Where(s => s.SessionDate == dayOnly)
                    .Select(s => s.SessionId)
                    .ToListAsync();

                var present = await _context.Attendances
                    .Where(a => sessionIds.Contains(a.SessionId) && a.Status == AttendanceStatus.Present)
                    .CountAsync();

                var absent = await _context.Attendances
                    .Where(a => sessionIds.Contains(a.SessionId) && a.Status == AttendanceStatus.Absent)
                    .CountAsync();

                attendanceStats.Add(present);
                absentStats.Add(absent);
            }

            Last7Days = JsonSerializer.Serialize(last7Days.Select(d => d.ToString("dd/MM")));
            AttendancePresentData = JsonSerializer.Serialize(attendanceStats);
            AttendanceAbsentData = JsonSerializer.Serialize(absentStats);

            var classStats = await _context.Classes
                .Select(c => new
                {
                    c.ClassCode,
                    StudentCount = c.ClassStudents.Count(cs => cs.Status == StudentClassStatus.Active)
                })
                .Where(c => c.StudentCount > 0)
                .ToListAsync();

            ClassNames = JsonSerializer.Serialize(classStats.Select(c => c.ClassCode));
            ClassStudentCounts = JsonSerializer.Serialize(classStats.Select(c => c.StudentCount));

            return Page();
        }

        public async Task<JsonResult> OnGetGetEventsAsync(string start, string end)
        {
            var startDate = DateOnly.Parse(start.Split('T')[0]);
            var endDate = DateOnly.Parse(end.Split('T')[0]);

            var sessions = await _context.Sessions
                .Where(s => s.SessionDate >= startDate && s.SessionDate <= endDate)
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .ToListAsync();

            var events = sessions.Select(s => new
            {
                title = s.Class.ClassCode,
                start = s.SessionDate.ToString("yyyy-MM-dd") + "T" + s.Shift.StartTime.ToString("HH:mm"),
                end = s.SessionDate.ToString("yyyy-MM-dd") + "T" + s.Shift.EndTime.ToString("HH:mm"),
                extendedProps = new
                {
                    shift = s.Shift.ShiftName,
                    room = s.Room.RoomCode,
                    sessionId = s.SessionId
                }
            });

            return new JsonResult(events);
        }
    }
}