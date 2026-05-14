using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace TutoringCenterManagement.Pages.Admin
{
    public class ReportModel : PageModel
    {
        public string DefaultDateFrom { get; set; } = string.Empty;
        public string DefaultDateTo   { get; set; } = string.Empty;
        public string DefaultWeekFrom { get; set; } = string.Empty;
        public string DefaultWeekTo   { get; set; } = string.Empty;

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var today      = DateOnly.FromDateTime(DateTime.Today);
            var monthStart = new DateOnly(today.Year, today.Month, 1);
            DefaultDateFrom = monthStart.ToString("yyyy-MM-dd");
            DefaultDateTo   = today.ToString("yyyy-MM-dd");

            // Current week: Monday → Sunday
            var dow       = (int)DateTime.Today.DayOfWeek;
            var daysToMon = dow == 0 ? 6 : dow - 1;
            var weekStart = DateOnly.FromDateTime(DateTime.Today.AddDays(-daysToMon));
            var weekEnd   = weekStart.AddDays(6);
            DefaultWeekFrom = weekStart.ToString("yyyy-MM-dd");
            DefaultWeekTo   = weekEnd.ToString("yyyy-MM-dd");

            return Page();
        }
    }
}
