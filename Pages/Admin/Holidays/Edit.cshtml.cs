using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TutoringCenterManagement.Data;

namespace TutoringCenterManagement.Pages.Admin.Holidays
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public EditModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            public int HolidayId { get; set; }
            public string HolidayName { get; set; } = string.Empty;
            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
            public string? Description { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var holiday = await _context.Holidays.FindAsync(id);
            if (holiday == null) return NotFound();

            Input.HolidayId = holiday.HolidayId;
            Input.HolidayName = holiday.HolidayName;
            Input.StartDate = holiday.StartDate;
            Input.EndDate = holiday.EndDate;
            Input.Description = holiday.Description;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            if (Input.EndDate < Input.StartDate)
            {
                ModelState.AddModelError("Input.EndDate", "Ngày kết thúc phải sau ngày bắt đầu!");
                return Page();
            }

            var holiday = await _context.Holidays.FindAsync(Input.HolidayId);
            if (holiday == null) return NotFound();

            holiday.HolidayName = Input.HolidayName;
            holiday.StartDate = Input.StartDate;
            holiday.EndDate = Input.EndDate;
            holiday.Description = Input.Description;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Cập nhật {holiday.HolidayName} thành công!";
            return RedirectToPage("./Index");
        }
    }
}