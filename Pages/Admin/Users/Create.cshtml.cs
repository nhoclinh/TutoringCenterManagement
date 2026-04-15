using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Users
{
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CreateModel> _logger;

        public CreateModel(ApplicationDbContext context, ILogger<CreateModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public class InputModel
        {
            [Required] public string Role { get; set; } = string.Empty;
            [Required] public string Username { get; set; } = string.Empty;
            [Required] public string Password { get; set; } = string.Empty;
            [Required] public string Fullname { get; set; } = string.Empty;
            public Gender Gender { get; set; }
            public DateTime? Dob { get; set; }
            public string? Phone { get; set; }
            public string? Note { get; set; }

            // Teacher - Đổi từ Subject? sang List
            public List<int> Subjects { get; set; } = new List<int>();

            // Student
            public string? CurrentSchool { get; set; }
            public int? ParentId { get; set; }

            // Student — tạo phụ huynh mới inline
            public bool CreateNewParent { get; set; }
            public string? NewParent_Fullname { get; set; }
            public string? NewParent_Phone { get; set; }
            public string? NewParent_Username { get; set; }
            public string? NewParent_Password { get; set; }

            // Parent — gắn học sinh
            public List<int> StudentIds { get; set; } = new();

            // Staff
            public string? Title { get; set; }
        }

        // Danh sách phụ huynh cho dropdown (Student role)
        public List<ParentOption> Parents { get; set; } = new();

        // Danh sách học sinh chưa có phụ huynh (Parent role)
        public List<StudentOption> Students { get; set; } = new();

        public class StudentOption
        {
            public int StudentId { get; set; }
            public string Fullname { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string CurrentSchool { get; set; } = string.Empty;
        }

        public class ParentOption
        {
            public int ParentId { get; set; }
            public string Fullname { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");
            await LoadSelectData();
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Xóa validation errors của fields không liên quan đến role hiện tại
            ClearIrrelevantValidation();

            if (!ModelState.IsValid)
            {
                await LoadSelectData();
                return Page();
            }

            try
            {
                // Check username exists
                if (_context.Accounts.Any(a => a.Username == Input.Username))
                {
                    ModelState.AddModelError("Input.Username", "Username đã tồn tại!");
                    await LoadSelectData();
                    return Page();
                }

                // Create Account
                var account = new Data.Entities.Account
                {
                    Username = Input.Username,
                    Password = BCrypt.Net.BCrypt.HashPassword(Input.Password),
                    Role = Enum.Parse<Role>(Input.Role),
                    IsActive = IsActive.Active,
                    CreatedAt = DateTime.Now
                };
                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                // Create role-specific entity
                switch (Input.Role)
                {
                    case "Admin":
                        _context.Staffs.Add(new Staff
                        {
                            AccountId = account.AccountId,
                            Fullname = Input.Fullname,
                            Gender = Input.Gender,
                            Dob = Input.Dob,
                            Phone = Input.Phone,
                            Title = Input.Title
                        });
                        break;

                    case "Teacher":
                        var teacher = new Data.Entities.Teacher
                        {
                            AccountId = account.AccountId,
                            Fullname = Input.Fullname,
                            Gender = Input.Gender,
                            Dob = Input.Dob,
                            Phone = Input.Phone,
                            Note = Input.Note
                        };
                        _context.Teachers.Add(teacher);
                        await _context.SaveChangesAsync();

                        // Thêm subjects (tối đa 3)
                        foreach (var subjectValue in Input.Subjects.Take(3))
                        {
                            _context.TeacherSubjects.Add(new TeacherSubject
                            {
                                TeacherId = teacher.AccountId,
                                Subject = (Subject)subjectValue
                            });
                        }
                        break;

                    case "Student":
                        int? resolvedParentId = Input.ParentId == 0 ? null : Input.ParentId;

                        // Tạo phụ huynh mới inline nếu được chọn
                        if (Input.CreateNewParent
                            && !string.IsNullOrWhiteSpace(Input.NewParent_Username)
                            && !string.IsNullOrWhiteSpace(Input.NewParent_Fullname))
                        {
                            if (_context.Accounts.Any(a => a.Username == Input.NewParent_Username))
                            {
                                ModelState.AddModelError("Input.NewParent_Username",
                                    "Username phụ huynh đã tồn tại!");
                                await LoadSelectData();
                                return Page();
                            }

                            var parentAccount = new Data.Entities.Account
                            {
                                Username = Input.NewParent_Username,
                                Password = BCrypt.Net.BCrypt.HashPassword(
                                                Input.NewParent_Password ?? "parent123"),
                                Role = Role.Parent,
                                IsActive = IsActive.Active,
                                CreatedAt = DateTime.Now
                            };
                            _context.Accounts.Add(parentAccount);
                            await _context.SaveChangesAsync();

                            var newParent = new Parent
                            {
                                AccountId = parentAccount.AccountId,
                                Fullname = Input.NewParent_Fullname.Trim(),
                                Phone = Input.NewParent_Phone?.Trim()
                            };
                            _context.Parents.Add(newParent);
                            await _context.SaveChangesAsync();

                            resolvedParentId = newParent.AccountId;
                        }

                        _context.Students.Add(new Data.Entities.Student
                        {
                            AccountId = account.AccountId,
                            Fullname = Input.Fullname,
                            Gender = Input.Gender,
                            Dob = Input.Dob,
                            Phone = Input.Phone,
                            CurrentSchool = Input.CurrentSchool,
                            Note = Input.Note,
                            ParentId = resolvedParentId
                        });
                        break;

                    case "Parent":
                        _context.Parents.Add(new Parent
                        {
                            AccountId = account.AccountId,
                            Fullname = Input.Fullname,
                            Gender = Input.Gender,
                            Dob = Input.Dob,
                            Phone = Input.Phone,
                            Note = Input.Note
                        });
                        await _context.SaveChangesAsync();

                        // Gắn học sinh được chọn vào phụ huynh này
                        if (Input.StudentIds.Any())
                        {
                            var studentsToLink = await _context.Students
                                .Where(s => Input.StudentIds.Contains(s.AccountId))
                                .ToListAsync();
                            foreach (var s in studentsToLink)
                                s.ParentId = account.AccountId;
                        }
                        break;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Tạo tài khoản {Input.Username} thành công!";
                _logger.LogInformation("Created user {Username} with role {Role}", Input.Username, Input.Role);

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                ModelState.AddModelError("", "Có lỗi xảy ra khi tạo người dùng!");
                await LoadSelectData();
                return Page();
            }
        }

        private void ClearIrrelevantValidation()
        {
            // Chỉ giữ lại validation errors của role đang được chọn
            var role = Input.Role;

            // Các key cần xóa nếu không phải Student
            var studentKeys = new[]
            {
                "Input.NewParent_Fullname", "Input.NewParent_Username",
                "Input.NewParent_Password", "Input.NewParent_Phone"
            };

            if (role != "Student" || !Input.CreateNewParent)
            {
                foreach (var key in studentKeys)
                    ModelState.Remove(key);
            }

            // Xóa teacher subjects nếu không phải Teacher
            if (role != "Teacher")
                ModelState.Remove("Input.Subjects");

            // Xóa Title nếu không phải Admin
            if (role != "Admin")
                ModelState.Remove("Input.Title");

            // Xóa CurrentSchool, ParentId nếu không phải Student
            if (role != "Student")
            {
                ModelState.Remove("Input.CurrentSchool");
                ModelState.Remove("Input.ParentId");
            }

            // Xóa StudentIds nếu không phải Parent
            if (role != "Parent")
                ModelState.Remove("Input.StudentIds");
        }

        private async Task LoadSelectData()
        {
            // Danh sách phụ huynh cho Student dropdown
            Parents = await _context.Parents
                .Include(p => p.Account)
                .Where(p => p.Account.IsActive == IsActive.Active)
                .OrderBy(p => p.Fullname)
                .Select(p => new ParentOption
                {
                    ParentId = p.AccountId,
                    Fullname = p.Fullname,
                    Phone = p.Phone ?? "N/A"
                })
                .ToListAsync();

            // Danh sách học sinh chưa có phụ huynh cho Parent panel
            Students = await _context.Students
                .Where(s => s.ParentId == null)
                .Include(s => s.Account)
                .Where(s => s.Account.IsActive == IsActive.Active)
                .OrderBy(s => s.Fullname)
                .Select(s => new StudentOption
                {
                    StudentId = s.AccountId,
                    Fullname = s.Fullname,
                    Phone = s.Phone ?? "N/A",
                    CurrentSchool = s.CurrentSchool ?? "N/A"
                })
                .ToListAsync();
        }
    }
}