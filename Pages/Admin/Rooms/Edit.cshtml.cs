using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using TutoringCenterManagement.Data;

namespace TutoringCenterManagement.Pages.Admin.Rooms
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
            public int RoomId { get; set; }
            public string RoomCode { get; set; } = string.Empty;
            public string RoomName { get; set; } = string.Empty;
            public int Capacity { get; set; }
            public string? Note { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var room = await _context.Rooms.FindAsync(id);
            if (room == null) return NotFound();

            Input.RoomId = room.RoomId;
            Input.RoomCode = room.RoomCode;
            Input.RoomName = room.RoomName;
            Input.Capacity = room.Capacity;
            Input.Note = room.Note;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var room = await _context.Rooms.FindAsync(Input.RoomId);
            if (room == null) return NotFound();

            room.RoomName = Input.RoomName;
            room.Capacity = Input.Capacity;
            room.Note = Input.Note;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Cập nhật phòng {room.RoomCode} thành công!";
            return RedirectToPage("./Index");
        }
    }
}