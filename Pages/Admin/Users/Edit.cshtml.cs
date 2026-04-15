using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Users
{
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<EditModel> _logger;

        public EditModel(ApplicationDbContext context, ILogger<EditModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public class InputModel
        {
            public int AccountId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Role { get; set; } = string.Empty;
            public string RoleDisplay { get; set; } = string.Empty;
            public string? NewPassword { get; set; }

            [Required] public string Fullname { get; set; } = string.Empty;
            public Gender Gender { get; set; }
            public DateTime? Dob { get; set; }
            public string? Phone { get; set; }
            public string? Note { get; set; }

            public List<int> Subjects { get; set; } = new List<int>();
            public string? CurrentSchool { get; set; }
            public int? ParentId { get; set; }        // Student: phụ huynh
            public List<int> StudentIds { get; set; } = new(); // Parent: học sinh gắn
            public string? Title { get; set; }
        }

        // Dropdown data
        public List<ParentOption> Parents { get; set; } = new();
        public List<StudentOption> Students { get; set; } = new();

        public class ParentOption
        {
            public int ParentId { get; set; }
            public string Fullname { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
        }

        public class StudentOption
        {
            public int StudentId { get; set; }
            public string Fullname { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string CurrentSchool { get; set; } = string.Empty;
        }

        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var account = await _context.Accounts
                .Include(a => a.Staff)
                .Include(a => a.Teacher).ThenInclude(t => t.TeacherSubjects)
                .Include(a => a.Student)
                .Include(a => a.Parent)
                .FirstOrDefaultAsync(a => a.AccountId == id);

            if (account == null) return NotFound();

            Input.AccountId = account.AccountId;
            Input.Username = account.Username;
            Input.Role = account.Role.ToString();
            Input.RoleDisplay = account.Role switch
            {
                Role.Admin => "Admin",
                Role.Teacher => "Giáo viên",
                Role.Student => "Học sinh",
                Role.Parent => "Phụ huynh",
                _ => "Unknown"
            };

            switch (account.Role)
            {
                case Role.Admin:
                    Input.Fullname = account.Staff?.Fullname ?? "";
                    Input.Gender = account.Staff?.Gender ?? Gender.Male;
                    Input.Dob = account.Staff?.Dob;
                    Input.Phone = account.Staff?.Phone;
                    Input.Title = account.Staff?.Title;
                    break;

                case Role.Teacher:
                    Input.Fullname = account.Teacher?.Fullname ?? "";
                    Input.Gender = account.Teacher?.Gender ?? Gender.Male;
                    Input.Dob = account.Teacher?.Dob;
                    Input.Phone = account.Teacher?.Phone;
                    Input.Note = account.Teacher?.Note;
                    Input.Subjects = account.Teacher?.TeacherSubjects
                        .Select(ts => (int)ts.Subject).ToList() ?? new List<int>();
                    break;

                case Role.Student:
                    Input.Fullname = account.Student?.Fullname ?? "";
                    Input.Gender = account.Student?.Gender ?? Gender.Male;
                    Input.Dob = account.Student?.Dob;
                    Input.Phone = account.Student?.Phone;
                    Input.Note = account.Student?.Note;
                    Input.CurrentSchool = account.Student?.CurrentSchool;
                    Input.ParentId = account.Student?.ParentId;
                    break;

                case Role.Parent:
                    Input.Fullname = account.Parent?.Fullname ?? "";
                    Input.Gender = account.Parent?.Gender ?? Gender.Male;
                    Input.Dob = account.Parent?.Dob;
                    Input.Phone = account.Parent?.Phone;
                    Input.Note = account.Parent?.Note;
                    Input.StudentIds = await _context.Students
                        .Where(s => s.ParentId == account.AccountId)
                        .Select(s => s.AccountId)
                        .ToListAsync();
                    break;
            }

            await LoadSelectData(account.Role, account.AccountId);
            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            try
            {
                var account = await _context.Accounts
                    .Include(a => a.Staff)
                    .Include(a => a.Teacher).ThenInclude(t => t.TeacherSubjects)
                    .Include(a => a.Student)
                    .Include(a => a.Parent)
                    .FirstOrDefaultAsync(a => a.AccountId == Input.AccountId);

                if (account == null) return NotFound();

                // Update password if provided
                if (!string.IsNullOrEmpty(Input.NewPassword))
                {
                    account.Password = BCrypt.Net.BCrypt.HashPassword(Input.NewPassword);
                }

                // Update role-specific data
                switch (account.Role)
                {
                    case Role.Admin:
                        if (account.Staff != null)
                        {
                            account.Staff.Fullname = Input.Fullname;
                            account.Staff.Gender = Input.Gender;
                            account.Staff.Dob = Input.Dob;
                            account.Staff.Phone = Input.Phone;
                            account.Staff.Title = Input.Title;
                        }
                        break;

                    case Role.Teacher:
                        if (account.Teacher != null)
                        {
                            account.Teacher.Fullname = Input.Fullname;
                            account.Teacher.Gender = Input.Gender;
                            account.Teacher.Dob = Input.Dob;
                            account.Teacher.Phone = Input.Phone;
                            account.Teacher.Note = Input.Note;

                            // Update subjects
                            _context.TeacherSubjects.RemoveRange(account.Teacher.TeacherSubjects);
                            foreach (var subjectValue in Input.Subjects.Take(3))
                            {
                                _context.TeacherSubjects.Add(new TeacherSubject
                                {
                                    TeacherId = account.Teacher.AccountId,
                                    Subject = (Subject)subjectValue
                                });
                            }
                        }
                        break;

                    case Role.Student:
                        if (account.Student != null)
                        {
                            account.Student.Fullname = Input.Fullname;
                            account.Student.Gender = Input.Gender;
                            account.Student.Dob = Input.Dob;
                            account.Student.Phone = Input.Phone;
                            account.Student.Note = Input.Note;
                            account.Student.CurrentSchool = Input.CurrentSchool;
                            account.Student.ParentId = (Input.ParentId == 0) ? null : Input.ParentId;
                        }
                        break;

                    case Role.Parent:
                        if (account.Parent != null)
                        {
                            account.Parent.Fullname = Input.Fullname;
                            account.Parent.Gender = Input.Gender;
                            account.Parent.Dob = Input.Dob;
                            account.Parent.Phone = Input.Phone;
                            account.Parent.Note = Input.Note;
                        }
                        // 1. Hủy gắn học sinh không còn trong danh sách
                        var oldLinked = await _context.Students
                            .Where(s => s.ParentId == account.AccountId).ToListAsync();
                        foreach (var s in oldLinked)
                            if (!Input.StudentIds.Contains(s.AccountId))
                                s.ParentId = null;
                        // 2. Gắn học sinh mới được chọn
                        if (Input.StudentIds.Any())
                        {
                            var toLink = await _context.Students
                                .Where(s => Input.StudentIds.Contains(s.AccountId)).ToListAsync();
                            foreach (var s in toLink)
                                s.ParentId = account.AccountId;
                        }
                        break;
                }

                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = $"Cập nhật tài khoản {Input.Username} thành công!";
                _logger.LogInformation("Updated user {Username}", Input.Username);

                return RedirectToPage("./Index");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user");
                ModelState.AddModelError("", "Có lỗi xảy ra!");
                var acc2 = await _context.Accounts.FindAsync(Input.AccountId);
                if (acc2 != null) await LoadSelectData(acc2.Role, acc2.AccountId);
                return Page();
            }
        }

        private async Task LoadSelectData(Role role, int accountId)
        {
            if (role == Role.Student)
            {
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
            }
            else if (role == Role.Parent)
            {
                // Học sinh chưa có PH hoặc đang gắn với PH này
                Students = await _context.Students
                    .Where(s => s.ParentId == null || s.ParentId == accountId)
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
}