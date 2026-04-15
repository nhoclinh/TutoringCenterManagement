using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Classes
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
            public int ClassId { get; set; }
            public string ClassCode { get; set; } = string.Empty;
            public Subject Subject { get; set; }
            public ClassStatus Status { get; set; }

            [Range(1, 12, ErrorMessage = "Khối phải từ 1 đến 12")]
            public int? GradeLevel { get; set; }

            [MaxLength(50, ErrorMessage = "Tên lớp tối đa 50 ký tự")]
            public string? ClassName { get; set; }

            [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
            public string? Description { get; set; }
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var classEntity = await _context.Classes.FindAsync(id);
            if (classEntity == null) return NotFound();

            Input = new InputModel
            {
                ClassId = classEntity.ClassId,
                ClassCode = classEntity.ClassCode,
                Subject = classEntity.Subject,
                Status = classEntity.Status,
                GradeLevel = classEntity.GradeLevel,
                ClassName = classEntity.ClassName,
                Description = classEntity.Description
            };

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var classEntity = await _context.Classes.FindAsync(Input.ClassId);
            if (classEntity == null) return NotFound();

            classEntity.Subject = Input.Subject;
            classEntity.Status = Input.Status;
            classEntity.GradeLevel = Input.GradeLevel;
            classEntity.ClassName = Input.ClassName?.Trim();
            classEntity.Description = Input.Description;

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã cập nhật lớp {classEntity.ClassCode} thành công!";
            return RedirectToPage("./Index");
        }
    }
}