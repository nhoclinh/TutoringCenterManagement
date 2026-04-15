using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;

namespace TutoringCenterManagement.Pages.Admin.Rooms
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
            [Required] public string RoomCode { get; set; } = string.Empty;
            [Required] public string RoomName { get; set; } = string.Empty;
            [Required] public int Capacity { get; set; }
            public string? Note { get; set; }
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

            if (_context.Rooms.Any(r => r.RoomCode == Input.RoomCode))
            {
                ModelState.AddModelError("Input.RoomCode", "Mã phòng đã tồn tại!");
                return Page();
            }

            var room = new Room
            {
                RoomCode = Input.RoomCode,
                RoomName = Input.RoomName,
                Capacity = Input.Capacity,
                Note = Input.Note
            };

            _context.Rooms.Add(room);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Tạo phòng {Input.RoomCode} thành công!";
            return RedirectToPage("./Index");
        }
    }
}