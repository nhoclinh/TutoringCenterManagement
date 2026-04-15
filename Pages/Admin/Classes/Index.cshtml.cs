using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Classes
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public IndexModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<ClassViewModel> Classes { get; set; } = new();

        [BindProperty(SupportsGet = true)]
        public string? SearchString { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? SubjectFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? StatusFilter { get; set; }

        [BindProperty(SupportsGet = true)]
        public string? GradeFilter { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var query = _context.Classes
                .Include(c => c.ClassStudents)
                .AsQueryable();

            // Filter by Subject
            if (!string.IsNullOrEmpty(SubjectFilter))
            {
                if (int.TryParse(SubjectFilter, out int subjectValue))
                {
                    query = query.Where(c => c.Subject == (Subject)subjectValue);
                }
            }

            // Filter by Status
            if (!string.IsNullOrEmpty(StatusFilter))
            {
                if (StatusFilter == "Active")
                    query = query.Where(c => c.Status == ClassStatus.Active);
                else if (StatusFilter == "Inactive")
                    query = query.Where(c => c.Status == ClassStatus.Inactive);
            }

            // Filter by GradeLevel
            if (!string.IsNullOrEmpty(GradeFilter) && int.TryParse(GradeFilter, out int gradeValue))
            {
                query = query.Where(c => c.GradeLevel == gradeValue);
            }

            // Search by ClassCode, ClassName or Description
            if (!string.IsNullOrEmpty(SearchString))
            {
                var search = SearchString.ToLower();
                query = query.Where(c =>
                    c.ClassCode.ToLower().Contains(search) ||
                    (c.ClassName != null && c.ClassName.ToLower().Contains(search)) ||
                    (c.Description != null && c.Description.ToLower().Contains(search))
                );
            }

            var classes = await query.OrderBy(c => c.ClassCode).ToListAsync();

            Classes = classes.Select(c => new ClassViewModel
            {
                ClassId = c.ClassId,
                ClassCode = c.ClassCode,
                ClassName = c.ClassName ?? "",
                GradeLevel = c.GradeLevel,
                Description = c.Description ?? "",
                Subject = c.Subject,
                SubjectName = c.Subject switch
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
                },
                Status = c.Status,
                StudentCount = c.ClassStudents.Count(cs => cs.Status == StudentClassStatus.Active),
            }).ToList();

            return Page();
        }

        public async Task<IActionResult> OnPostDeleteAsync(int classId)
        {
            try
            {
                var classEntity = await _context.Classes.FindAsync(classId);
                if (classEntity == null)
                {
                    TempData["ErrorMessage"] = "Không tìm thấy lớp học!";
                    return RedirectToPage();
                }

                _context.Classes.Remove(classEntity);
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Đã xóa lớp {classEntity.ClassCode}!";
            }
            catch (DbUpdateException)
            {
                TempData["ErrorMessage"] = "Không thể xóa lớp này vì có học sinh hoặc buổi học liên quan!";
            }

            return RedirectToPage();
        }

        public class ClassViewModel
        {
            public int ClassId { get; set; }
            public string ClassCode { get; set; } = string.Empty;
            public string ClassName { get; set; } = string.Empty;
            public int? GradeLevel { get; set; }
            public string Description { get; set; } = string.Empty;
            public Subject Subject { get; set; }
            public string SubjectName { get; set; } = string.Empty;
            public ClassStatus Status { get; set; }
            public int StudentCount { get; set; }
        }
    }
}