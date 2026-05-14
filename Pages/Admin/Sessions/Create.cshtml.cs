// ============================================================
// FILE GỐC : Pages/Admin/Sessions/Create.cshtml.cs
// FILE SỬA : Pages/Admin/Sessions/Create_Fixed.cshtml.cs
//
// THAY ĐỔI SO VỚI BẢN GỐC — thêm 3 block validate trước khi tạo session:
//
// [FIX-1] Check PHÒNG trùng: cùng RoomId + ShiftId + SessionDate, Status != Cancelled
//         Bản gốc không có check này → 2 session cùng phòng + ca + ngày được tạo tự do.
//
// [FIX-2] Check GV CHÍNH trùng lịch: cùng ShiftId + SessionDate, Status != Cancelled
//         Bản gốc không có check này → 1 GV dạy 2 chỗ cùng lúc.
//
// [FIX-3] Check GV TRỢ GIẢNG trùng lịch: tương tự FIX-2.
//
// Tất cả check đều query thẳng bảng Sessions (source of truth duy nhất),
// bao gồm cả session lẻ (TemplateId=null) lẫn session từ template.
// ============================================================

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

            [Required(ErrorMessage = "Vui lòng chọn giáo viên chính")]
            public int PrimaryTeacherId { get; set; }

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

            // Validate GV chính và trợ giảng không trùng nhau
            if (Input.AssistantTeacherId.HasValue
                && Input.AssistantTeacherId.Value == Input.PrimaryTeacherId)
            {
                ModelState.AddModelError("Input.AssistantTeacherId",
                    "Giáo viên trợ giảng không được trùng với giáo viên chính!");
                await LoadData();
                return Page();
            }

            // ── [FIX-1] Kiểm tra trùng PHÒNG ─────────────────────────────────
            // Query Sessions: cùng phòng + ca + ngày, chưa bị cancel.
            // Bao gồm cả session lẻ lẫn session sinh từ template.
            var conflictRoom = await _context.Sessions
                .Where(s => s.RoomId == Input.RoomId
                         && s.ShiftId == Input.ShiftId
                         && s.SessionDate == Input.SessionDate
                         && s.Status != SessionStatus.Cancelled)
                .Include(s => s.Class)
                .Include(s => s.Room)
                .FirstOrDefaultAsync();

            if (conflictRoom != null)
            {
                var roomName = conflictRoom.Room?.RoomCode ?? $"ID {Input.RoomId}";
                ModelState.AddModelError("Input.RoomId",
                    $"Phòng {roomName} đã có buổi học ca này vào ngày " +
                    $"{Input.SessionDate:dd/MM/yyyy} (Lớp {conflictRoom.Class.ClassCode})!");
                await LoadData();
                return Page();
            }

            // ── [FIX-2] Kiểm tra trùng lịch GV CHÍNH ────────────────────────
            var conflictPrimary = await _context.Sessions
                .Where(s => (s.TeacherId == Input.PrimaryTeacherId
                          || s.TeacherAssistantId == Input.PrimaryTeacherId)
                         && s.ShiftId == Input.ShiftId
                         && s.SessionDate == Input.SessionDate
                         && s.Status != SessionStatus.Cancelled)
                .Include(s => s.Class)
                .FirstOrDefaultAsync();

            if (conflictPrimary != null)
            {
                var primaryName = await _context.Teachers
                    .Where(t => t.AccountId == Input.PrimaryTeacherId)
                    .Select(t => t.Fullname)
                    .FirstOrDefaultAsync() ?? $"ID {Input.PrimaryTeacherId}";

                ModelState.AddModelError("Input.PrimaryTeacherId",
                    $"Giáo viên {primaryName} đã có lịch dạy ca này vào ngày " +
                    $"{Input.SessionDate:dd/MM/yyyy} (Lớp {conflictPrimary.Class.ClassCode})!");
                await LoadData();
                return Page();
            }

            // ── [FIX-3] Kiểm tra trùng lịch GV TRỢ GIẢNG ────────────────────
            if (Input.AssistantTeacherId.HasValue)
            {
                var conflictAssistant = await _context.Sessions
                    .Where(s => (s.TeacherId == Input.AssistantTeacherId.Value
                              || s.TeacherAssistantId == Input.AssistantTeacherId.Value)
                             && s.ShiftId == Input.ShiftId
                             && s.SessionDate == Input.SessionDate
                             && s.Status != SessionStatus.Cancelled)
                    .Include(s => s.Class)
                    .FirstOrDefaultAsync();

                if (conflictAssistant != null)
                {
                    var assistantName = await _context.Teachers
                        .Where(t => t.AccountId == Input.AssistantTeacherId.Value)
                        .Select(t => t.Fullname)
                        .FirstOrDefaultAsync() ?? $"ID {Input.AssistantTeacherId.Value}";

                    ModelState.AddModelError("Input.AssistantTeacherId",
                        $"Giáo viên trợ giảng {assistantName} đã có lịch dạy ca này vào ngày " +
                        $"{Input.SessionDate:dd/MM/yyyy} (Lớp {conflictAssistant.Class.ClassCode})!");
                    await LoadData();
                    return Page();
                }
            }
            // ── [END FIX] ─────────────────────────────────────────────────────

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