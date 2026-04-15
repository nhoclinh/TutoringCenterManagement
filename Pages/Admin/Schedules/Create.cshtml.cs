using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;
using TutoringCenterManagement.Services.Interfaces;

namespace TutoringCenterManagement.Pages.Admin.Schedules
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly IScheduleService _scheduleService;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(ApplicationDbContext context,
            IScheduleService scheduleService,
            ILogger<CreateModel> logger)
        {
            _context = context;
            _scheduleService = scheduleService;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? WarningMessage { get; set; }

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

            [Required] public DayOfTheWeek DayOfWeek { get; set; }
            [Required] public ShiftId ShiftId { get; set; }
            [Required] public int RoomId { get; set; }
            [Required] public DateOnly StartDate { get; set; }
            [Required] public DateOnly EndDate { get; set; }
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

            // Validate ngày
            if (Input.EndDate < Input.StartDate)
            {
                ModelState.AddModelError("Input.EndDate", "Ngày kết thúc phải sau ngày bắt đầu!");
                await LoadData();
                return Page();
            }

            // Validate giáo viên không trùng nhau
            if (Input.AssistantTeacherId.HasValue
                && Input.AssistantTeacherId.Value == Input.PrimaryTeacherId)
            {
                ModelState.AddModelError("Input.AssistantTeacherId",
                    "Giáo viên trợ giảng không được trùng với giáo viên chính!");
                await LoadData();
                return Page();
            }

            try
            {
                // ── Validate conflict phòng + ca + thứ + giáo viên ──────────
                // Truyền thêm primaryTeacherId và assistantTeacherId để service
                // kiểm tra luôn trùng lịch giáo viên.
                var (isValid, errorMessage) = await _scheduleService.ValidateScheduleTemplate(
                    Input.ClassId,
                    Input.RoomId,
                    Input.ShiftId,
                    Input.DayOfWeek,
                    Input.StartDate,
                    Input.EndDate,
                    excludeTemplateId: null,
                    primaryTeacherId: Input.PrimaryTeacherId,        // <-- thêm
                    assistantTeacherId: Input.AssistantTeacherId);     // <-- thêm

                if (!isValid)
                {
                    ModelState.AddModelError("", errorMessage);
                    await LoadData();
                    return Page();
                }

                // Tạo template
                var template = new WeeklyScheduleTemplate
                {
                    ClassId = Input.ClassId,
                    RoomId = Input.RoomId,
                    ShiftId = Input.ShiftId,
                    DayOfWeek = Input.DayOfWeek,
                    StartDate = Input.StartDate,
                    EndDate = Input.EndDate,
                    TeacherId = Input.PrimaryTeacherId,
                    TeacherAssistantId = Input.AssistantTeacherId,
                    Status = Input.StartDate > DateOnly.FromDateTime(DateTime.Today)
                                            ? ScheduleTemplateStatus.Upcoming
                                            : ScheduleTemplateStatus.Active,
                    CreatedAt = DateTime.Now
                };

                _context.WeeklyScheduleTemplates.Add(template);
                await _context.SaveChangesAsync();

                var sessions = await _scheduleService.GenerateSessionsFromTemplate(
                    template,
                    Input.PrimaryTeacherId,
                    Input.AssistantTeacherId);

                TempData["SuccessMessage"] =
                    $"Tạo lịch học thành công! Đã sinh {sessions.Count} buổi học.";

                _logger.LogInformation(
                    "Created schedule template {TemplateId} for class {ClassId}, generated {Count} sessions",
                    template.TemplateId, Input.ClassId, sessions.Count);

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating schedule template");
                ModelState.AddModelError("", "Có lỗi xảy ra khi tạo lịch học!");
                await LoadData();
                return Page();
            }
        }

        private async Task LoadData()
        {
            Classes = await _context.Classes
                .Where(c => c.Status == ClassStatus.Active)
                .Select(c => new ClassInfo
                {
                    ClassId = c.ClassId,
                    ClassCode = c.ClassCode,
                    SubjectName = GetSubjectName(c.Subject)
                })
                .ToListAsync();

            Teachers = await _context.Teachers
                .Include(t => t.TeacherSubjects)
                .Select(t => new TeacherInfo
                {
                    TeacherId = t.AccountId,
                    Fullname = t.Fullname,
                    Subjects = t.TeacherSubjects.Select(ts => (int)ts.Subject).ToList(),
                    SubjectNames = t.TeacherSubjects.Select(ts => GetSubjectName(ts.Subject)).ToList()
                })
                .ToListAsync();

            Shifts = await _context.Shifts.OrderBy(s => s.ShiftId).ToListAsync();
            Rooms = await _context.Rooms.OrderBy(r => r.RoomCode).ToListAsync();
        }

        private static string GetSubjectName(Subject subject) => subject switch
        {
            Subject.Math => "Toán",
            Subject.Vietnamese => "Tiếng Việt",
            Subject.English => "Tiếng Anh",
            Subject.Physics => "Vật lý",
            Subject.Biology => "Sinh học",
            Subject.Chemistry => "Hóa học",
            Subject.Geography => "Địa lý",
            Subject.History => "Lịch sử",
            _ => "Khác"
        };

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
            public List<int> Subjects { get; set; } = new();
            public List<string> SubjectNames { get; set; } = new();
        }
    }
}