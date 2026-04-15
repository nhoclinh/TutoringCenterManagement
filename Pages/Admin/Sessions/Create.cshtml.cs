using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Sessions
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

        public List<ClassInfo> Classes { get; set; } = new();
        public List<TeacherInfo> Teachers { get; set; } = new();
        public List<Shift> Shifts { get; set; } = new();
        public List<Room> Rooms { get; set; } = new();

        public class InputModel
        {
            [Required] public int ClassId { get; set; }

            // Giáo viên chính (bắt buộc)
            [Required(ErrorMessage = "Vui lòng chọn giáo viên chính")]
            public int PrimaryTeacherId { get; set; }

            // Giáo viên trợ giảng (tùy chọn)
            public int? AssistantTeacherId { get; set; }

            [Required] public DateOnly SessionDate { get; set; }
            [Required] public ShiftId ShiftId { get; set; }
            [Required] public int RoomId { get; set; }
            public string? Note { get; set; }
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

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

            // Validate giáo viên chính và trợ giảng không trùng nhau
            if (Input.AssistantTeacherId.HasValue
                && Input.AssistantTeacherId.Value == Input.PrimaryTeacherId)
            {
                ModelState.AddModelError("Input.AssistantTeacherId",
                    "Giáo viên trợ giảng không được trùng với giáo viên chính!");
                await LoadData();
                return Page();
            }

            // Tạo session — gán TeacherId trực tiếp
            var session = new Session
            {
                ClassId = Input.ClassId,
                SessionDate = Input.SessionDate,
                ShiftId = Input.ShiftId,
                RoomId = Input.RoomId,
                TemplateId = null,
                TeacherId = Input.PrimaryTeacherId,
                TeacherAssistantId = Input.AssistantTeacherId,
                Status = SessionStatus.Scheduled,
                Note = Input.Note,
                CreatedAt = DateTime.Now
            };

            _context.Sessions.Add(session);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Tạo buổi học lẻ thành công!";
            return RedirectToPage("./Index");
        }

        private async Task LoadData()
        {
            Classes = await _context.Classes
                .Where(c => c.Status == ClassStatus.Active)
                .Select(c => new ClassInfo
                {
                    ClassId = c.ClassId,
                    ClassCode = c.ClassCode,
                    SubjectName = c.Subject.ToString()
                })
                .ToListAsync();

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

        public class ClassInfo
        {
            public int ClassId { get; set; }
            public string ClassCode { get; set; } = string.Empty;
            public string SubjectName { get; set; } = string.Empty;
        }

        public class TeacherInfo
        {
            public int TeacherId { get; set; }
            public string Fullname { get; set; } = string.Empty;
        }
    }
}