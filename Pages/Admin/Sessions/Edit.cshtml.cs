using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Sessions
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

        public List<TeacherInfo> Teachers { get; set; } = new();
        public List<Shift> Shifts { get; set; } = new();
        public List<Room> Rooms { get; set; } = new();

        public class InputModel
        {
            public int SessionId { get; set; }
            public int ClassId { get; set; }
            public string ClassName { get; set; } = string.Empty;

            // Giáo viên chính (bắt buộc)
            public int PrimaryTeacherId { get; set; }

            // Giáo viên trợ giảng (tùy chọn)
            public int? AssistantTeacherId { get; set; }

            public DateOnly SessionDate { get; set; }
            public ShiftId ShiftId { get; set; }
            public string ShiftName { get; set; } = string.Empty;
            public int RoomId { get; set; }
            public string RoomName { get; set; } = string.Empty;
            public SessionStatus Status { get; set; }
            public string? Note { get; set; }
            public bool IsFromTemplate { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var session = await _context.Sessions
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .Include(s => s.Teacher)
                .Include(s => s.TeacherAssistant)
                .FirstOrDefaultAsync(s => s.SessionId == id);

            if (session == null) return NotFound();

            Input = new InputModel
            {
                SessionId = session.SessionId,
                ClassId = session.ClassId,
                ClassName = session.Class.ClassCode,
                PrimaryTeacherId = session.TeacherId,
                AssistantTeacherId = session.TeacherAssistantId,
                SessionDate = session.SessionDate,
                ShiftId = session.ShiftId,
                ShiftName = $"{session.Shift.ShiftName} ({session.Shift.StartTime:HH:mm}-{session.Shift.EndTime:HH:mm})",
                RoomId = session.RoomId,
                RoomName = session.Room.RoomCode,
                Status = session.Status,
                Note = session.Note,
                IsFromTemplate = session.TemplateId.HasValue
            };

            await LoadData();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
            {
                await LoadData();
                return Page();
            }

            // Validate trùng giáo viên
            if (Input.AssistantTeacherId.HasValue
                && Input.AssistantTeacherId.Value == Input.PrimaryTeacherId)
            {
                ModelState.AddModelError("Input.AssistantTeacherId",
                    "Giáo viên trợ giảng không được trùng với giáo viên chính!");
                await LoadData();
                return Page();
            }

            var session = await _context.Sessions.FindAsync(Input.SessionId);
            if (session == null) return NotFound();

            // Cập nhật status, note
            session.Status = Input.Status;
            session.Note = Input.Note;

            // Cập nhật giáo viên trực tiếp
            session.TeacherId = Input.PrimaryTeacherId;
            session.TeacherAssistantId = Input.AssistantTeacherId;

            // Chỉ update date, shift, room nếu KHÔNG phải từ template
            if (!Input.IsFromTemplate)
            {
                session.SessionDate = Input.SessionDate;
                session.ShiftId = Input.ShiftId;
                session.RoomId = Input.RoomId;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật buổi học thành công!";
            return RedirectToPage("./Index");
        }

        private async Task LoadData()
        {
            Teachers = await _context.Teachers
                .Select(t => new TeacherInfo
                {
                    TeacherId = t.AccountId,
                    Fullname = t.Fullname
                })
                .ToListAsync();

            Shifts = await _context.Shifts.ToListAsync();
            Rooms = await _context.Rooms.ToListAsync();
        }

        public class TeacherInfo
        {
            public int TeacherId { get; set; }
            public string Fullname { get; set; } = string.Empty;
        }
    }
}