using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin
{
    public class StudentMonthlyReportModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public StudentMonthlyReportModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public int FilterYear { get; set; } = DateTime.Today.Year;
        [BindProperty(SupportsGet = true)] public int FilterMonth { get; set; } = DateTime.Today.Month;
        public List<int> AvailableYears { get; set; } = new();

        // ── Dữ liệu báo cáo ──────────────────────────────────────────
        public List<StudentMonthlyRow> Rows { get; set; } = new();
        public int TotalPresent { get; set; }
        public int TotalAbsent { get; set; }
        public int TotalStudents { get; set; } // có điểm danh

        public string PeriodLabel => $"Tháng {FilterMonth}/{FilterYear}";

        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            // Năm có dữ liệu
            var sessionYears = await _context.Sessions
                .Select(s => s.SessionDate.Year).Distinct().ToListAsync();
            AvailableYears = sessionYears.Union(new[] { DateTime.Today.Year })
                .OrderByDescending(y => y).ToList();
            if (!AvailableYears.Contains(FilterYear))
                AvailableYears.Insert(0, FilterYear);

            await LoadReportAsync();
            return Page();
        }

        private async Task LoadReportAsync()
        {
            // Lấy tất cả học viên đang active hoặc có điểm danh
            var allStudents = await _context.Students
                .Select(s => new { s.AccountId, s.Fullname, s.CurrentSchool })
                .ToListAsync();

            // Lấy điểm danh trong tháng được chọn
            var attendances = await _context.Attendances
                .Where(a => a.Session.SessionDate.Year == FilterYear &&
                            a.Session.SessionDate.Month == FilterMonth)
                .Select(a => new
                {
                    a.StudentId,
                    a.Status,
                    ClassId = a.Session.ClassId
                })
                .ToListAsync();

            // Build rows
            var rows = allStudents.Select(s =>
            {
                var atts = attendances.Where(a => a.StudentId == s.AccountId).ToList();
                var present = atts.Count(a => a.Status == AttendanceStatus.Present);
                var absent = atts.Count(a => a.Status == AttendanceStatus.Absent);
                var total = atts.Count;
                var classes = atts.Select(a => a.ClassId).Distinct().Count();
                var rate = total > 0 ? Math.Round((double)present / total * 100, 1) : 0.0;

                return new StudentMonthlyRow
                {
                    StudentName = s.Fullname,
                    CurrentSchool = s.CurrentSchool,
                    Present = present,
                    Absent = absent,
                    TotalSessions = total,
                    ClassCount = classes,
                    AttendanceRate = rate
                };
            }).ToList();

            // Sắp xếp: có buổi học lên trước, trong đó sắp theo tỉ lệ vắng tăng dần (ít vắng = tốt lên trên)
            // Bằng 0 xuống cuối
            Rows = rows
                .OrderByDescending(r => r.TotalSessions > 0)
                .ThenByDescending(r => r.AttendanceRate)
                .ThenBy(r => r.Absent)
                .ToList();

            TotalPresent = Rows.Sum(r => r.Present);
            TotalAbsent = Rows.Sum(r => r.Absent);
            TotalStudents = Rows.Count(r => r.TotalSessions > 0);
        }

        // ── View Model ────────────────────────────────────────────────
        public class StudentMonthlyRow
        {
            public string StudentName { get; set; } = string.Empty;
            public string? CurrentSchool { get; set; }
            public int Present { get; set; }
            public int Absent { get; set; }
            public int TotalSessions { get; set; }
            public int ClassCount { get; set; }
            public double AttendanceRate { get; set; }

            public string RateLevel =>
                TotalSessions == 0 ? "none" :
                AttendanceRate >= 90 ? "high" :
                AttendanceRate >= 70 ? "mid" : "low";

            public string Initials => string.Join("",
                StudentName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p[0]).Take(2)).ToUpper();
        }
    }
}