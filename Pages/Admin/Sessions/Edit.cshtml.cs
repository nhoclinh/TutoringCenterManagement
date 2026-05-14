// ============================================================
// FILE GỐC : Pages/Admin/Sessions/Edit.cshtml.cs
// FILE SỬA : Pages/Admin/Sessions/Edit_Fixed.cshtml.cs
//
// THAY ĐỔI SO VỚI BẢN GỐC — thêm 3 block validate trước khi lưu:
//
// [FIX-1] Check PHÒNG trùng: cùng RoomId + ShiftId + SessionDate, Status != Cancelled
//         EXCLUDE session đang edit (s.SessionId != Input.SessionId).
//         Chỉ áp dụng khi session KHÔNG từ template (IsFromTemplate=false),
//         vì session từ template không được đổi room/shift/date.
//
// [FIX-2] Check GV CHÍNH trùng lịch: exclude session đang edit.
//         Dùng effectiveDate/effectiveShift (gốc nếu từ template, input nếu session lẻ).
//
// [FIX-3] Check GV TRỢ GIẢNG trùng lịch: tương tự FIX-2.
// ============================================================

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
            public int PrimaryTeacherId { get; set; }
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

            // Validate GV chính và trợ giảng không trùng nhau
            if (Input.AssistantTeacherId.HasValue
                && Input.AssistantTeacherId.Value == Input.PrimaryTeacherId)
            {
                ModelState.AddModelError("Input.AssistantTeacherId",
                    "Giáo viên trợ giảng không được trùng với giáo viên chính!");
                await LoadData();
                return Page();
            }

            // Lấy session hiện tại từ DB
            var existingSession = await _context.Sessions.FindAsync(Input.SessionId);
            if (existingSession == null) return NotFound();

            // Ngày, ca, phòng thực tế sau khi save
            // (session từ template không được đổi date/shift/room → dùng giá trị gốc)
            var effectiveDate = Input.IsFromTemplate ? existingSession.SessionDate : Input.SessionDate;
            var effectiveShift = Input.IsFromTemplate ? existingSession.ShiftId : Input.ShiftId;
            var effectiveRoom = Input.IsFromTemplate ? existingSession.RoomId : Input.RoomId;

            // ── [FIX-1] Kiểm tra trùng PHÒNG ─────────────────────────────────
            // EXCLUDE chính session đang edit để không tự conflict với bản gốc.
            var conflictRoom = await _context.Sessions
                .Where(s => s.RoomId == effectiveRoom
                         && s.ShiftId == effectiveShift
                         && s.SessionDate == effectiveDate
                         && s.Status != SessionStatus.Cancelled
                         && s.SessionId != Input.SessionId)
                .Include(s => s.Class)
                .Include(s => s.Room)
                .FirstOrDefaultAsync();

            if (conflictRoom != null)
            {
                var roomName = conflictRoom.Room?.RoomCode ?? $"ID {effectiveRoom}";
                ModelState.AddModelError("Input.RoomId",
                    $"Phòng {roomName} đã có buổi học ca này vào ngày " +
                    $"{effectiveDate:dd/MM/yyyy} (Lớp {conflictRoom.Class.ClassCode})!");
                await LoadData();
                return Page();
            }

            // ── [FIX-2] Kiểm tra trùng lịch GV CHÍNH ────────────────────────
            var conflictPrimary = await _context.Sessions
                .Where(s => (s.TeacherId == Input.PrimaryTeacherId
                          || s.TeacherAssistantId == Input.PrimaryTeacherId)
                         && s.ShiftId == effectiveShift
                         && s.SessionDate == effectiveDate
                         && s.Status != SessionStatus.Cancelled
                         && s.SessionId != Input.SessionId)
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
                    $"{effectiveDate:dd/MM/yyyy} (Lớp {conflictPrimary.Class.ClassCode})!");
                await LoadData();
                return Page();
            }

            // ── [FIX-3] Kiểm tra trùng lịch GV TRỢ GIẢNG ────────────────────
            if (Input.AssistantTeacherId.HasValue)
            {
                var conflictAssistant = await _context.Sessions
                    .Where(s => (s.TeacherId == Input.AssistantTeacherId.Value
                              || s.TeacherAssistantId == Input.AssistantTeacherId.Value)
                             && s.ShiftId == effectiveShift
                             && s.SessionDate == effectiveDate
                             && s.Status != SessionStatus.Cancelled
                             && s.SessionId != Input.SessionId)
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
                        $"{effectiveDate:dd/MM/yyyy} (Lớp {conflictAssistant.Class.ClassCode})!");
                    await LoadData();
                    return Page();
                }
            }
            // ── [END FIX] ─────────────────────────────────────────────────────

            // Cập nhật session
            existingSession.Status = Input.Status;
            existingSession.Note = Input.Note;
            existingSession.TeacherId = Input.PrimaryTeacherId;
            existingSession.TeacherAssistantId = Input.AssistantTeacherId;

            // Chỉ update date/shift/room nếu KHÔNG phải từ template
            if (!Input.IsFromTemplate)
            {
                existingSession.SessionDate = Input.SessionDate;
                existingSession.ShiftId = Input.ShiftId;
                existingSession.RoomId = Input.RoomId;
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