using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LoginModel> _logger;

        public LoginModel(ApplicationDbContext context, ILogger<LoginModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public class InputModel
        {
            [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
            public string Username { get; set; } = string.Empty;

            [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            public bool RememberMe { get; set; }
        }

        public IActionResult OnGet()
        {
            if (!string.IsNullOrEmpty(HttpContext.Session.GetString("Username")))
                return RedirectToHome();

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            try
            {
                var account = await _context.Accounts
                    .Include(a => a.Staff)
                    .Include(a => a.Teacher)
                    .Include(a => a.Student)
                    .Include(a => a.Parent)
                    .FirstOrDefaultAsync(a => a.Username == Input.Username);

                if (account == null || !BCrypt.Net.BCrypt.Verify(Input.Password, account.Password))
                {
                    // ✅ Dùng ViewData thay TempData — tồn tại trong cùng request (return Page())
                    ViewData["LoginError"] = "Tên đăng nhập hoặc mật khẩu không đúng!";
                    return Page();
                }

                if (account.IsActive != IsActive.Active)
                {
                    ViewData["LoginError"] = "Tài khoản đã bị vô hiệu hóa!";
                    return Page();
                }

                string fullname = account.Role switch
                {
                    Role.Admin => account.Staff?.Fullname ?? "Admin",
                    Role.Teacher => account.Teacher?.Fullname ?? "Teacher",
                    Role.Student => account.Student?.Fullname ?? "Student",
                    Role.Parent => account.Parent?.Fullname ?? "Parent",
                    _ => "User"
                };

                HttpContext.Session.SetInt32("AccountId", account.AccountId);
                HttpContext.Session.SetString("Username", account.Username);
                HttpContext.Session.SetString("Role", account.Role.ToString());
                HttpContext.Session.SetString("Fullname", fullname);

                _logger.LogInformation("User {Username} logged in", account.Username);

                return RedirectToHome();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Login error");
                ViewData["LoginError"] = "Có lỗi xảy ra, vui lòng thử lại!";
                return Page();
            }
        }

        private IActionResult RedirectToHome()
        {
            var role = HttpContext.Session.GetString("Role");

            return role switch
            {
                "Admin" => RedirectToPage("/Admin/Dashboard"),
                "Teacher" => RedirectToPage("/Teacher/Schedule"),
                "Student" => RedirectToPage("/Student/Schedule"),
                "Parent" => RedirectToPage("/Student/Schedule"),
                _ => RedirectToPage("/Index")
            };
        }
    }
}