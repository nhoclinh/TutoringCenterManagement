using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin
{
    public class TeacherMonthlyReportModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public TeacherMonthlyReportModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public int FilterYear { get; set; } = DateTime.Today.Year;
        [BindProperty(SupportsGet = true)] public int FilterMonth { get; set; } = DateTime.Today.Month;
        public List<int> AvailableYears { get; set; } = new();

        // ── Dữ liệu báo cáo ──────────────────────────────────────────
        public List<TeacherMonthlyRow> Rows { get; set; } = new();
        public int TotalSessions { get; set; }
        public int TotalTeachers { get; set; }

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
            // Lấy tất cả giáo viên
            var allTeachers = await _context.Teachers
                .Select(t => new { t.AccountId, t.Fullname })
                .ToListAsync();

            // Lấy sessions trong tháng/năm được chọn
            var sessions = await _context.Sessions
                .Where(s => s.SessionDate.Year == FilterYear &&
                            s.SessionDate.Month == FilterMonth)
                .Select(s => new
                {
                    s.TeacherId,
                    s.Status,
                    s.ClassId
                })
                .ToListAsync();

            // Build rows cho mỗi giáo viên
            var rows = allTeachers.Select(t =>
            {
                var ts = sessions.Where(s => s.TeacherId == t.AccountId).ToList();
                var completed = ts.Count(s => s.Status == SessionStatus.Completed);
                var scheduled = ts.Count(s => s.Status == SessionStatus.Scheduled);
                var cancelled = ts.Count(s => s.Status == SessionStatus.Cancelled);
                var ongoing = ts.Count(s => s.Status == SessionStatus.Ongoing);
                var classCount = ts.Select(s => s.ClassId).Distinct().Count();
                var total = ts.Count;

                return new TeacherMonthlyRow
                {
                    TeacherName = t.Fullname,
                    Completed = completed,
                    Scheduled = scheduled,
                    Cancelled = cancelled,
                    Ongoing = ongoing,
                    ClassCount = classCount,
                    TotalSessions = total
                };
            }).ToList();

            // Sắp xếp: có buổi dạy lên trước (giảm dần), bằng 0 xuống cuối
            Rows = rows
                .OrderByDescending(r => r.TotalSessions > 0)
                .ThenByDescending(r => r.Completed)
                .ThenByDescending(r => r.TotalSessions)
                .ToList();

            TotalSessions = Rows.Sum(r => r.Completed);
            TotalTeachers = Rows.Count(r => r.TotalSessions > 0);
        }

        // ── View Model ────────────────────────────────────────────────
        public class TeacherMonthlyRow
        {
            public string TeacherName { get; set; } = string.Empty;
            public int Completed { get; set; }
            public int Scheduled { get; set; }
            public int Cancelled { get; set; }
            public int Ongoing { get; set; }
            public int ClassCount { get; set; }
            public int TotalSessions { get; set; }
            public string Initials => string.Join("",
                TeacherName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                           .Select(p => p[0]).Take(2)).ToUpper();
        }
    }
}