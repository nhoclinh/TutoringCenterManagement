using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;

namespace TutoringCenterManagement.Pages.Admin.Holidays
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<HolidayViewModel> Holidays { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var holidays = await _context.Holidays
                .OrderBy(h => h.StartDate)
                .ToListAsync();

            Holidays = holidays.Select(h => new HolidayViewModel
            {
                HolidayId = h.HolidayId,
                HolidayName = h.HolidayName,
                StartDate = h.StartDate,
                EndDate = h.EndDate,
                Description = h.Description ?? "",
                DayCount = h.EndDate.DayNumber - h.StartDate.DayNumber + 1
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int holidayId)
        {
            var holiday = await _context.Holidays.FindAsync(holidayId);
            if (holiday == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy ngày nghỉ!";
                return RedirectToPage();
            }

            _context.Holidays.Remove(holiday);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Xóa ngày lễ {holiday.HolidayName} thành công!";
            return RedirectToPage();
        }

        public class HolidayViewModel
        {
            public int HolidayId { get; set; }
            public string HolidayName { get; set; } = string.Empty;
            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
            public string Description { get; set; } = string.Empty;
            public int DayCount { get; set; }
        }
    }
}