using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Account
{
    public class ChangePasswordModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public ChangePasswordModel(ApplicationDbContext context) => _context = context;

        public string Fullname { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public string RoleCss { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public ChangePasswordInput Input { get; set; } = new();

        public class ChangePasswordInput
        {
            [Required(ErrorMessage = "Vui lòng nhập mật khẩu hiện tại")]
            public string CurrentPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu mới")]
            [MinLength(6, ErrorMessage = "Mật khẩu mới tối thiểu 6 ký tự")]
            [MaxLength(255)]
            public string NewPassword { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng xác nhận mật khẩu mới")]
            [Compare(nameof(NewPassword), ErrorMessage = "Mật khẩu xác nhận không khớp!")]
            public string ConfirmPassword { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            var acc = await GetCurrentAccountAsync();
            if (acc == null) return RedirectToPage("/Account/Login");
            LoadDisplayInfo(acc);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var acc = await GetCurrentAccountAsync();
            if (acc == null) return RedirectToPage("/Account/Login");

            if (!ModelState.IsValid)
            {
                LoadDisplayInfo(acc);
                return Page();
            }

            // Kiểm tra mật khẩu hiện tại bằng BCrypt
            if (!BCrypt.Net.BCrypt.Verify(Input.CurrentPassword, acc.Password))
            {
                ModelState.AddModelError("Input.CurrentPassword", "Mật khẩu hiện tại không đúng!");
                LoadDisplayInfo(acc);
                return Page();
            }

            // Kiểm tra mật khẩu mới không được trùng mật khẩu cũ
            if (BCrypt.Net.BCrypt.Verify(Input.NewPassword, acc.Password))
            {
                ModelState.AddModelError("Input.NewPassword", "Mật khẩu mới phải khác mật khẩu hiện tại!");
                LoadDisplayInfo(acc);
                return Page();
            }

            // Lưu mật khẩu mới dưới dạng BCrypt hash
            acc.Password = BCrypt.Net.BCrypt.HashPassword(Input.NewPassword);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Đổi mật khẩu thành công!";
            return RedirectToPage();
        }

        private async Task<TutoringCenterManagement.Data.Entities.Account?> GetCurrentAccountAsync()
        {
            // Login page dùng: HttpContext.Session.SetString("Username", account.Username)
            var username = HttpContext.Session.GetString("Username");
            if (string.IsNullOrEmpty(username)) return null;

            return await _context.Accounts
                .Include(a => a.Staff)
                .Include(a => a.Teacher)
                .Include(a => a.Student)
                .Include(a => a.Parent)
                .FirstOrDefaultAsync(a => a.Username == username);
        }

        private void LoadDisplayInfo(TutoringCenterManagement.Data.Entities.Account acc)
        {
            Username = acc.Username;

            (RoleLabel, RoleCss) = acc.Role switch
            {
                Role.Admin => ("Quản trị viên", "admin"),
                Role.Teacher => ("Giáo viên", "teacher"),
                Role.Student => ("Học sinh", "student"),
                Role.Parent => ("Phụ huynh", "parent"),
                _ => ("Người dùng", "default")
            };

            Fullname = acc.Role switch
            {
                Role.Admin when acc.Staff != null => acc.Staff.Fullname,
                Role.Teacher when acc.Teacher != null => acc.Teacher.Fullname,
                Role.Student when acc.Student != null => acc.Student.Fullname,
                Role.Parent when acc.Parent != null => acc.Parent.Fullname,
                _ => acc.Username
            };

            var parts = Fullname.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Initials = parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
                : Fullname.Substring(0, Math.Min(2, Fullname.Length)).ToUpper();
        }
    }
}