using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;

namespace TutoringCenterManagement.Pages.Admin.Rooms
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Room> Rooms { get; set; } = new();

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            Rooms = await _context.Rooms.OrderBy(r => r.RoomCode).ToListAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int roomId)
        {
            try
            {
                var room = await _context.Rooms.FindAsync(roomId);
                if (room == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy phòng!";
                    return RedirectToPage();
                }

                _context.Rooms.Remove(room);
                await _context.SaveChangesAsync();
                TempData["SuccessMessage"] = $"Xóa phòng {room.RoomCode} thành công!";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = "Không thể xóa phòng này vì có buổi học liên quan!";
            }

            return RedirectToPage();
        }
    }
}