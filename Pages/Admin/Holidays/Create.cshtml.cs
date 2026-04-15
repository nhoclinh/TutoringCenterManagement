using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;

namespace TutoringCenterManagement.Pages.Admin.Holidays
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public CreateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public class InputModel
        {
            [Required] public string HolidayName { get; set; } = string.Empty;
            [Required] public DateOnly StartDate { get; set; }
            [Required] public DateOnly EndDate { get; set; }
            public string? Description { get; set; }
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");
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

            var holiday = new Holiday
            {
                HolidayName = Input.HolidayName,
                StartDate = Input.StartDate,
                EndDate = Input.EndDate,
                Description = Input.Description,
                CreatedAt = DateTime.Now
            };

            _context.Holidays.Add(holiday);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Tạo ngày lễ {Input.HolidayName} thành công!";
            return RedirectToPage("./Index");
        }
    }
}