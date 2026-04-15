using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Schedules
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<ClassInfo> Classes { get; set; } = new();
        public List<TemplateGroup> TemplatesByClass { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? ClassFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            await LoadPageDataAsync();
            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int templateId)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var template = await _context.WeeklyScheduleTemplates.FindAsync(templateId);
            if (template == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch học!";
                return RedirectToPage();
            }

            _context.WeeklyScheduleTemplates.Remove(template);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Xóa lịch học thành công!";
            return RedirectToPage();
        }

        /// <summary>
        /// Kết thúc sớm lịch học mẫu:
        /// - Cập nhật EndDate của template thành earlyEndDate
        /// - Cập nhật Status của template thành FinishedEarly
        /// - Huỷ tất cả Session có SessionDate > earlyEndDate và Status == Scheduled hoặc Ongoing
        /// </summary>
        public async Task<IActionResult> OnPostFinishEarlyAsync(int templateId, DateOnly earlyEndDate)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var template = await _context.WeeklyScheduleTemplates
                .Include(t => t.Sessions)
                .FirstOrDefaultAsync(t => t.TemplateId == templateId);

            if (template == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy lịch học!";
                return RedirectToPage();
            }

            // Validate: ngày kết thúc sớm phải >= StartDate và < EndDate hiện tại
            if (earlyEndDate < template.StartDate)
            {
                TempData["ErrorMessage"] = "Ngày kết thúc sớm không được nhỏ hơn ngày bắt đầu lịch học!";
                return RedirectToPage();
            }

            if (earlyEndDate >= template.EndDate)
            {
                TempData["ErrorMessage"] = "Ngày kết thúc sớm phải nhỏ hơn ngày kết thúc gốc của lịch học!";
                return RedirectToPage();
            }

            // Cập nhật template
            template.EndDate = earlyEndDate;
            template.Status = ScheduleTemplateStatus.FinishedEarly;

            // Huỷ các session chưa diễn ra sau ngày kết thúc sớm
            var sessionsToCancel = template.Sessions
                .Where(s => s.SessionDate > earlyEndDate
                         && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.Ongoing))
                .ToList();

            foreach (var session in sessionsToCancel)
            {
                session.Status = SessionStatus.Cancelled;
            }

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã kết thúc sớm lịch học. {sessionsToCancel.Count} buổi học đã bị huỷ.";
            return RedirectToPage();
        }

        // ─── Helpers ──────────────────────────────────────────────────────────

        private async Task LoadPageDataAsync()
        {
            Classes = await _context.Classes
                .Select(c => new ClassInfo
                {
                    ClassId = c.ClassId,
                    ClassCode = c.ClassCode,
                    SubjectName = c.Subject.ToString()
                })
                .ToListAsync();

            var query = _context.WeeklyScheduleTemplates
                .Include(t => t.Class)
                .Include(t => t.Shift)
                .Include(t => t.Room)
                .AsQueryable();

            if (!string.IsNullOrEmpty(ClassFilter) && int.TryParse(ClassFilter, out int classId))
                query = query.Where(t => t.ClassId == classId);

            if (!string.IsNullOrEmpty(StatusFilter) && int.TryParse(StatusFilter, out int status))
                query = query.Where(t => t.Status == (ScheduleTemplateStatus)status);

            var templates = await query
                .OrderBy(t => t.Class.ClassCode)
                .ThenBy(t => t.DayOfWeek)
                .ToListAsync();

            TemplatesByClass = templates
                .GroupBy(t => new { t.ClassId, t.Class.ClassCode, t.Class.Subject })
                .Select(g => new TemplateGroup
                {
                    ClassId = g.Key.ClassId,
                    ClassName = g.Key.ClassCode,
                    SubjectName = GetSubjectName(g.Key.Subject),
                    Templates = g.Select(t => new TemplateViewModel
                    {
                        TemplateId = t.TemplateId,
                        DayOfWeek = t.DayOfWeek,
                        ShiftName = t.Shift.ShiftName,
                        ShiftTime = $"{t.Shift.StartTime:HH:mm} - {t.Shift.EndTime:HH:mm}",
                        RoomCode = t.Room.RoomCode,
                        StartDate = t.StartDate,
                        EndDate = t.EndDate,
                        Status = t.Status
                    }).ToList()
                })
                .ToList();
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

        // ─── Nested view models ────────────────────────────────────────────────

        public class ClassInfo
        {
            public int ClassId { get; set; }
            public string ClassCode { get; set; } = string.Empty;
            public string SubjectName { get; set; } = string.Empty;
        }

        public class TemplateGroup
        {
            public int ClassId { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public string SubjectName { get; set; } = string.Empty;
            public List<TemplateViewModel> Templates { get; set; } = new();
        }

        public class TemplateViewModel
        {
            public int TemplateId { get; set; }
            public DayOfTheWeek DayOfWeek { get; set; }
            public string ShiftName { get; set; } = string.Empty;
            public string ShiftTime { get; set; } = string.Empty;
            public string RoomCode { get; set; } = string.Empty;
            public DateOnly StartDate { get; set; }
            public DateOnly EndDate { get; set; }
            public ScheduleTemplateStatus Status { get; set; }
        }
    }
}