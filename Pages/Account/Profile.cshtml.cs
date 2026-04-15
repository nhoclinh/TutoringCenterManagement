using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Account
{
    public class ProfileModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public ProfileModel(ApplicationDbContext context) => _context = context;

        // ── Chỉ đọc (sidebar) ─────────────────────────────────────────
        public string Username { get; set; } = string.Empty;
        public string RoleLabel { get; set; } = string.Empty;
        public string RoleCss { get; set; } = string.Empty;   // admin | teacher | student | parent
        public string RoleIcon { get; set; } = string.Empty;
        public string Initials { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }

        [BindProperty]
        public ProfileInput Input { get; set; } = new();

        public class ProfileInput
        {
            public int AccountId { get; set; }

            [Required(ErrorMessage = "Vui lòng nhập họ tên")]
            [MaxLength(100, ErrorMessage = "Họ tên tối đa 100 ký tự")]
            public string Fullname { get; set; } = string.Empty;

            [MaxLength(15, ErrorMessage = "Số điện thoại tối đa 15 ký tự")]
            public string? Phone { get; set; }

            public DateTime? Dob { get; set; }

            public Gender Gender { get; set; }

            // Staff (Admin)
            [MaxLength(100)]
            public string? Title { get; set; }

            // Student
            [MaxLength(200)]
            public string? CurrentSchool { get; set; }

            // Teacher / Student / Parent
            [MaxLength(500)]
            public string? Note { get; set; }
        }

        // ─────────────────────────────────────────────────────────────────
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

            // AccountId lấy từ DB theo session Username — không cần kiểm tra giả mạo
            if (!ModelState.IsValid)
            {
                LoadDisplayInfo(acc);
                return Page();
            }

            switch (acc.Role)
            {
                case Role.Admin when acc.Staff != null:
                    acc.Staff.Fullname = Input.Fullname;
                    acc.Staff.Phone = Input.Phone;
                    acc.Staff.Dob = Input.Dob;
                    acc.Staff.Gender = Input.Gender;
                    acc.Staff.Title = Input.Title;
                    break;
                case Role.Teacher when acc.Teacher != null:
                    acc.Teacher.Fullname = Input.Fullname;
                    acc.Teacher.Phone = Input.Phone;
                    acc.Teacher.Dob = Input.Dob;
                    acc.Teacher.Gender = Input.Gender;
                    acc.Teacher.Note = Input.Note;
                    break;
                case Role.Student when acc.Student != null:
                    acc.Student.Fullname = Input.Fullname;
                    acc.Student.Phone = Input.Phone;
                    acc.Student.Dob = Input.Dob;
                    acc.Student.Gender = Input.Gender;
                    acc.Student.CurrentSchool = Input.CurrentSchool;
                    acc.Student.Note = Input.Note;
                    break;
                case Role.Parent when acc.Parent != null:
                    acc.Parent.Fullname = Input.Fullname;
                    acc.Parent.Phone = Input.Phone;
                    acc.Parent.Dob = Input.Dob;
                    acc.Parent.Gender = Input.Gender;
                    acc.Parent.Note = Input.Note;
                    break;
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
            return RedirectToPage();
        }

        // ─────────────────────────────────────────────────────────────────
        private async Task<TutoringCenterManagement.Data.Entities.Account?> GetCurrentAccountAsync()
        {
            // Dùng "Username" — đây là key Login page đặt vào session
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
            CreatedAt = acc.CreatedAt;

            (RoleLabel, RoleCss, RoleIcon) = acc.Role switch
            {
                Role.Admin => ("Quản trị viên", "admin", "fa-shield-halved"),
                Role.Teacher => ("Giáo viên", "teacher", "fa-chalkboard-user"),
                Role.Student => ("Học sinh", "student", "fa-graduation-cap"),
                Role.Parent => ("Phụ huynh", "parent", "fa-house-user"),
                _ => ("Người dùng", "default", "fa-user")
            };

            string fullname;
            switch (acc.Role)
            {
                case Role.Admin when acc.Staff != null:
                    fullname = acc.Staff.Fullname;
                    Input = new ProfileInput
                    {
                        AccountId = acc.AccountId,
                        Fullname = acc.Staff.Fullname,
                        Phone = acc.Staff.Phone,
                        Dob = acc.Staff.Dob,
                        Gender = acc.Staff.Gender,
                        Title = acc.Staff.Title
                    };
                    break;
                case Role.Teacher when acc.Teacher != null:
                    fullname = acc.Teacher.Fullname;
                    Input = new ProfileInput
                    {
                        AccountId = acc.AccountId,
                        Fullname = acc.Teacher.Fullname,
                        Phone = acc.Teacher.Phone,
                        Dob = acc.Teacher.Dob,
                        Gender = acc.Teacher.Gender,
                        Note = acc.Teacher.Note
                    };
                    break;
                case Role.Student when acc.Student != null:
                    fullname = acc.Student.Fullname;
                    Input = new ProfileInput
                    {
                        AccountId = acc.AccountId,
                        Fullname = acc.Student.Fullname,
                        Phone = acc.Student.Phone,
                        Dob = acc.Student.Dob,
                        Gender = acc.Student.Gender,
                        CurrentSchool = acc.Student.CurrentSchool,
                        Note = acc.Student.Note
                    };
                    break;
                case Role.Parent when acc.Parent != null:
                    fullname = acc.Parent.Fullname;
                    Input = new ProfileInput
                    {
                        AccountId = acc.AccountId,
                        Fullname = acc.Parent.Fullname,
                        Phone = acc.Parent.Phone,
                        Dob = acc.Parent.Dob,
                        Gender = acc.Parent.Gender,
                        Note = acc.Parent.Note
                    };
                    break;
                default:
                    fullname = acc.Username;
                    Input = new ProfileInput { AccountId = acc.AccountId };
                    break;
            }

            var parts = fullname.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Initials = parts.Length >= 2
                ? $"{parts[0][0]}{parts[^1][0]}".ToUpper()
                : fullname.Substring(0, Math.Min(2, fullname.Length)).ToUpper();
        }
    }
}