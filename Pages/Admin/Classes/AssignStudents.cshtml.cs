using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin.Classes
{
    public class AssignStudentsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public AssignStudentsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public ClassInfo CurrentClass { get; set; } = new();
        public List<ClassStudentInfo> EnrolledStudents { get; set; } = new();
        public List<StudentOption> AvailableStudents { get; set; } = new();

        [BindProperty]
        public AddStudentInput AddInput { get; set; } = new();

        [BindProperty]
        public UpdateStatusInput UpdateInput { get; set; } = new();

        public class AddStudentInput
        {
            public int ClassId { get; set; }

            [Required(ErrorMessage = "Vui lòng chọn học sinh")]
            public int StudentId { get; set; }

            [Required(ErrorMessage = "Vui lòng chọn ngày bắt đầu")]
            public DateOnly StartedAt { get; set; } = DateOnly.FromDateTime(DateTime.Today);
        }

        public class UpdateStatusInput
        {
            public int ClassId { get; set; }
            public int StudentId { get; set; }

            [Required]
            public StudentClassStatus Status { get; set; }

            // Có giá trị khi Status = Left, null khi Active/Reserved
            public DateOnly? LeftAt { get; set; }
        }

        // =============================================
        // GET
        // =============================================
        public async Task<IActionResult> OnGetAsync(int id)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var classEntity = await _context.Classes.FindAsync(id);
            if (classEntity == null) return NotFound();

            CurrentClass = MapClassInfo(classEntity);
            AddInput.ClassId = id;
            AddInput.StartedAt = DateOnly.FromDateTime(DateTime.Today);

            await LoadData(id);
            return Page();
        }

        // =============================================
        // POST: Thêm học sinh vào lớp
        // =============================================
        public async Task<IActionResult> OnPostAddAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            if (!ModelState.IsValid)
            {
                await LoadPageData(AddInput.ClassId);
                return Page();
            }

            var existing = await _context.ClassStudents
                .FirstOrDefaultAsync(cs => cs.ClassId == AddInput.ClassId
                                        && cs.StudentId == AddInput.StudentId);

            if (existing != null)
            {
                if (existing.Status == StudentClassStatus.Active)
                {
                    TempData["ErrorMessage"] = "Học sinh này đang hoạt động trong lớp!";
                    return RedirectToPage(new { id = AddInput.ClassId });
                }

                // Đã có record cũ (Left/Reserved) → kích hoạt lại
                existing.Status = StudentClassStatus.Active;
                existing.StartedAt = AddInput.StartedAt;
                existing.LeftAt = null;
                await _context.SaveChangesAsync();

                TempData["SuccessMessage"] = "Đã thêm lại học sinh vào lớp!";
                return RedirectToPage(new { id = AddInput.ClassId });
            }

            _context.ClassStudents.Add(new ClassStudent
            {
                ClassId = AddInput.ClassId,
                StudentId = AddInput.StudentId,
                StartedAt = AddInput.StartedAt,
                LeftAt = null,
                Status = StudentClassStatus.Active
            });
            await _context.SaveChangesAsync();

            var name = await _context.Students
                .Where(s => s.AccountId == AddInput.StudentId)
                .Select(s => s.Fullname)
                .FirstOrDefaultAsync();

            TempData["SuccessMessage"] = $"Đã thêm học sinh '{name}' vào lớp!";
            return RedirectToPage(new { id = AddInput.ClassId });
        }

        // =============================================
        // POST: Cập nhật trạng thái học sinh
        // Active/Reserved → LeftAt = null
        // Left            → LeftAt = ngày chọn hoặc hôm nay
        // =============================================
        public async Task<IActionResult> OnPostUpdateStatusAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            var cs = await _context.ClassStudents
                .FirstOrDefaultAsync(x => x.ClassId == UpdateInput.ClassId
                                       && x.StudentId == UpdateInput.StudentId);

            if (cs == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy học sinh trong lớp!";
                return RedirectToPage(new { id = UpdateInput.ClassId });
            }

            var oldStatus = cs.Status;
            cs.Status = UpdateInput.Status;

            switch (UpdateInput.Status)
            {
                case StudentClassStatus.Active:
                case StudentClassStatus.Suspended:
                    cs.LeftAt = null;
                    break;

                case StudentClassStatus.Left:
                    cs.LeftAt = UpdateInput.LeftAt ?? DateOnly.FromDateTime(DateTime.Today);
                    break;
            }

            await _context.SaveChangesAsync();

            var studentName = await _context.Students
                .Where(s => s.AccountId == UpdateInput.StudentId)
                .Select(s => s.Fullname)
                .FirstOrDefaultAsync();

            TempData["SuccessMessage"] =
                $"Cập nhật '{studentName}': {GetStatusLabel(oldStatus)} → {GetStatusLabel(UpdateInput.Status)}";

            return RedirectToPage(new { id = UpdateInput.ClassId });
        }

        // =============================================
        // POST: Xóa cứng học sinh khỏi lớp
        // Chỉ cho phép khi chưa có điểm danh
        // =============================================
        public async Task<IActionResult> OnPostDeleteAsync(int classId, int studentId)
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            // Kiểm tra có attendance chưa
            var hasAttendance = await _context.Attendances
                .AnyAsync(a => a.StudentId == studentId
                            && a.Session.ClassId == classId);

            if (hasAttendance)
            {
                TempData["ErrorMessage"] =
                    "Không thể xóa! Học sinh đã có dữ liệu điểm danh. " +
                    "Hãy chuyển sang trạng thái 'Đã nghỉ' thay vì xóa.";
                return RedirectToPage(new { id = classId });
            }

            var cs = await _context.ClassStudents
                .FirstOrDefaultAsync(x => x.ClassId == classId
                                       && x.StudentId == studentId);

            if (cs == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy học sinh trong lớp!";
                return RedirectToPage(new { id = classId });
            }

            var studentName = await _context.Students
                .Where(s => s.AccountId == studentId)
                .Select(s => s.Fullname)
                .FirstOrDefaultAsync();

            _context.ClassStudents.Remove(cs);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = $"Đã xóa học sinh '{studentName}' khỏi lớp!";
            return RedirectToPage(new { id = classId });
        }

        // =============================================
        // HELPERS
        // =============================================
        private async Task LoadPageData(int classId)
        {
            var classEntity = await _context.Classes.FindAsync(classId);
            if (classEntity != null)
                CurrentClass = MapClassInfo(classEntity);
            AddInput.ClassId = classId;
            await LoadData(classId);
        }

        private async Task LoadData(int classId)
        {
            // Tất cả học sinh trong lớp (mọi trạng thái)
            EnrolledStudents = await _context.ClassStudents
                .Where(cs => cs.ClassId == classId)
                .Include(cs => cs.Student)
                .OrderBy(cs => cs.Status)
                .ThenBy(cs => cs.Student.Fullname)
                .Select(cs => new ClassStudentInfo
                {
                    StudentId = cs.StudentId,
                    Fullname = cs.Student.Fullname,
                    Phone = cs.Student.Phone ?? "N/A",
                    CurrentSchool = cs.Student.CurrentSchool ?? "N/A",
                    StartedAt = cs.StartedAt,
                    LeftAt = cs.LeftAt,
                    Status = cs.Status,
                    Gender = cs.Student.Gender
                })
                .ToListAsync();

            // Học sinh chưa từng trong lớp
            var enrolledIds = EnrolledStudents.Select(s => s.StudentId).ToList();

            AvailableStudents = await _context.Students
                .Where(s => !enrolledIds.Contains(s.AccountId))
                .Include(s => s.Account)
                .Where(s => s.Account.IsActive == IsActive.Active)
                .OrderBy(s => s.Fullname)
                .Select(s => new StudentOption
                {
                    StudentId = s.AccountId,
                    Fullname = s.Fullname,
                    Phone = s.Phone ?? "N/A"
                })
                .ToListAsync();
        }

        private static ClassInfo MapClassInfo(Class c) => new ClassInfo
        {
            ClassId = c.ClassId,
            ClassCode = c.ClassCode,
            SubjectName = GetSubjectName(c.Subject),
            Status = c.Status
        };

        public static string GetStatusLabel(StudentClassStatus s) => s switch
        {
            StudentClassStatus.Active => "Đang học",
            StudentClassStatus.Suspended => "Bảo lưu",
            StudentClassStatus.Left => "Đã nghỉ",
            _ => s.ToString()
        };

        public static string GetStatusBadge(StudentClassStatus s) => s switch
        {
            StudentClassStatus.Active => "bg-success",
            StudentClassStatus.Suspended => "bg-warning text-dark",
            StudentClassStatus.Left => "bg-secondary",
            _ => "bg-light text-dark"
        };

        private static string GetSubjectName(Subject s) => s switch
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
            public ClassStatus Status { get; set; }
        }

        public class ClassStudentInfo
        {
            public int StudentId { get; set; }
            public string Fullname { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
            public string CurrentSchool { get; set; } = string.Empty;
            public DateOnly StartedAt { get; set; }
            public DateOnly? LeftAt { get; set; }
            public StudentClassStatus Status { get; set; }
            public Gender Gender { get; set; }
        }

        public class StudentOption
        {
            public int StudentId { get; set; }
            public string Fullname { get; set; } = string.Empty;
            public string Phone { get; set; } = string.Empty;
        }
    }
}