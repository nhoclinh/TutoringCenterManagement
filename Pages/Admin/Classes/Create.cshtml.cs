using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Classes
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

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập mã lớp")]
            [MaxLength(50, ErrorMessage = "Mã lớp tối đa 50 ký tự")]
            public string ClassCode { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng chọn môn học")]
            public Subject Subject { get; set; }

            public ClassStatus Status { get; set; } = ClassStatus.Active;

            [Range(1, 12, ErrorMessage = "Khối phải từ 1 đến 12")]
            public int? GradeLevel { get; set; }

            [MaxLength(50, ErrorMessage = "Tên lớp tối đa 50 ký tự")]
            public string? ClassName { get; set; }

            [MaxLength(500, ErrorMessage = "Mô tả tối đa 500 ký tự")]
            public string? Description { get; set; }
        }

        public IActionResult OnGet()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            if (!ModelState.IsValid)
                return Page();

            // Kiểm tra ClassCode đã tồn tại chưa
            var exists = await _context.Classes
                .AnyAsync(c => c.ClassCode == Input.ClassCode.Trim());

            if (exists)
            {
                ModelState.AddModelError("Input.ClassCode",
                    $"Mã lớp '{Input.ClassCode}' đã tồn tại!");
                return Page();
            }

            var newClass = new Class
            {
                ClassCode = Input.ClassCode.Trim().ToUpper(),
                Subject = Input.Subject,
                Status = Input.Status,
                GradeLevel = Input.GradeLevel,
                ClassName = Input.ClassName?.Trim(),
                Description = Input.Description?.Trim()
            };

            _context.Classes.Add(newClass);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] =
                $"Tạo lớp học '{newClass.ClassCode}' thành công!";

            return RedirectToPage("./Index");
        }
    }
}