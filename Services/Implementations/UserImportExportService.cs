using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;
using TutoringCenterManagement.Services.Interfaces;

namespace TutoringCenterManagement.Services.Implementations
{
    public class UserImportExportService : IUserImportExportService
    {
        private readonly ApplicationDbContext _context;

        private static readonly Dictionary<string, Subject> SubjectMap =
            new(StringComparer.OrdinalIgnoreCase)
            {
                { "Math",       Subject.Math       },
                { "Vietnamese", Subject.Vietnamese },
                { "English",    Subject.English    },
                { "Physics",    Subject.Physics    },
                { "Biology",    Subject.Biology    },
                { "Chemistry",  Subject.Chemistry  },
                { "Geography",  Subject.Geography  },
                { "History",    Subject.History    },
                { "Other",      Subject.Other      }
            };

        public UserImportExportService(ApplicationDbContext context)
        {
            _context = context;
        }

        // =============================================
        // EXPORT
        // =============================================

        public async Task<byte[]> ExportAllUsersAsync()
        {
            using var wb = new XLWorkbook();
            await BuildStaffSheet(wb);
            await BuildTeacherSheet(wb);
            await BuildStudentSheet(wb);
            await BuildParentSheet(wb);
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private async Task BuildStaffSheet(XLWorkbook wb)
        {
            var ws = wb.AddWorksheet("Staff");
            SetHeaders(ws, new[] { "Username", "IsActive", "CreatedAt", "Fullname", "Dob", "Phone", "Gender", "Title" });

            var staffs = await _context.Staffs
                .Include(s => s.Account)
                .OrderBy(s => s.Fullname)
                .ToListAsync();

            int row = 2;
            foreach (var s in staffs)
            {
                ws.Cell(row, 1).Value = s.Account.Username;
                ws.Cell(row, 2).Value = s.Account.IsActive.ToString();
                ws.Cell(row, 3).Value = s.Account.CreatedAt.ToString("dd/MM/yyyy");
                ws.Cell(row, 4).Value = s.Fullname;
                ws.Cell(row, 5).Value = s.Dob?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 6).Value = s.Phone ?? "";
                ws.Cell(row, 7).Value = s.Gender.ToString();
                ws.Cell(row, 8).Value = s.Title ?? "";
                row++;
            }
            ApplyTableStyle(ws, row - 1, 8);
        }

        private async Task BuildTeacherSheet(XLWorkbook wb)
        {
            var ws = wb.AddWorksheet("Teacher");
            SetHeaders(ws, new[] { "Username", "IsActive", "CreatedAt", "Fullname", "Dob", "Phone", "Gender", "Note", "Subject1", "Subject2", "Subject3" });

            var teachers = await _context.Teachers
                .Include(t => t.Account)
                .Include(t => t.TeacherSubjects)
                .OrderBy(t => t.Fullname)
                .ToListAsync();

            int row = 2;
            foreach (var t in teachers)
            {
                var subjects = t.TeacherSubjects.Select(ts => ts.Subject.ToString()).ToList();
                ws.Cell(row, 1).Value = t.Account.Username;
                ws.Cell(row, 2).Value = t.Account.IsActive.ToString();
                ws.Cell(row, 3).Value = t.Account.CreatedAt.ToString("dd/MM/yyyy");
                ws.Cell(row, 4).Value = t.Fullname;
                ws.Cell(row, 5).Value = t.Dob?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 6).Value = t.Phone ?? "";
                ws.Cell(row, 7).Value = t.Gender.ToString();
                ws.Cell(row, 8).Value = t.Note ?? "";
                ws.Cell(row, 9).Value = subjects.ElementAtOrDefault(0) ?? "";
                ws.Cell(row, 10).Value = subjects.ElementAtOrDefault(1) ?? "";
                ws.Cell(row, 11).Value = subjects.ElementAtOrDefault(2) ?? "";
                row++;
            }
            ApplyTableStyle(ws, row - 1, 11);
        }

        private async Task BuildStudentSheet(XLWorkbook wb)
        {
            var ws = wb.AddWorksheet("Student");
            SetHeaders(ws, new[] { "Username", "IsActive", "CreatedAt", "Fullname", "Dob", "Phone", "Gender", "CurrentSchool", "Note", "ParentUsername" });

            var students = await _context.Students
                .Include(s => s.Account)
                .Include(s => s.Parent).ThenInclude(p => p!.Account)
                .OrderBy(s => s.Fullname)
                .ToListAsync();

            int row = 2;
            foreach (var s in students)
            {
                ws.Cell(row, 1).Value = s.Account.Username;
                ws.Cell(row, 2).Value = s.Account.IsActive.ToString();
                ws.Cell(row, 3).Value = s.Account.CreatedAt.ToString("dd/MM/yyyy");
                ws.Cell(row, 4).Value = s.Fullname;
                ws.Cell(row, 5).Value = s.Dob?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 6).Value = s.Phone ?? "";
                ws.Cell(row, 7).Value = s.Gender.ToString();
                ws.Cell(row, 8).Value = s.CurrentSchool ?? "";
                ws.Cell(row, 9).Value = s.Note ?? "";
                ws.Cell(row, 10).Value = s.Parent?.Account?.Username ?? "";
                row++;
            }
            ApplyTableStyle(ws, row - 1, 10);
        }

        private async Task BuildParentSheet(XLWorkbook wb)
        {
            var ws = wb.AddWorksheet("Parent");
            SetHeaders(ws, new[] { "Username", "IsActive", "CreatedAt", "Fullname", "Dob", "Phone", "Gender", "Note", "Students (phân cách bằng dấu phẩy)" });

            var parents = await _context.Parents
                .Include(p => p.Account)
                .Include(p => p.Students).ThenInclude(s => s.Account)
                .OrderBy(p => p.Fullname)
                .ToListAsync();

            int row = 2;
            foreach (var p in parents)
            {
                var studentUsernames = string.Join(", ", p.Students.Select(s => s.Account.Username));
                ws.Cell(row, 1).Value = p.Account.Username;
                ws.Cell(row, 2).Value = p.Account.IsActive.ToString();
                ws.Cell(row, 3).Value = p.Account.CreatedAt.ToString("dd/MM/yyyy");
                ws.Cell(row, 4).Value = p.Fullname;
                ws.Cell(row, 5).Value = p.Dob?.ToString("dd/MM/yyyy") ?? "";
                ws.Cell(row, 6).Value = p.Phone ?? "";
                ws.Cell(row, 7).Value = p.Gender.ToString();
                ws.Cell(row, 8).Value = p.Note ?? "";
                ws.Cell(row, 9).Value = studentUsernames;
                row++;
            }
            ApplyTableStyle(ws, row - 1, 9);
        }

        // =============================================
        // IMPORT
        // =============================================

        public record ImportResult(int Success, int Failed, List<string> Errors);

        public async Task<ImportResult> ImportUsersAsync(Stream fileStream)
        {
            var errors = new List<string>();
            int success = 0;
            int failed = 0;

            using var wb = new XLWorkbook(fileStream);

            var sheetRoleMap = new Dictionary<string, Role>
            {
                { "Staff",   Role.Admin   },
                { "Teacher", Role.Teacher },
                { "Student", Role.Student },
                { "Parent",  Role.Parent  }
            };

            foreach (var (sheetName, role) in sheetRoleMap)
            {
                if (!wb.TryGetWorksheet(sheetName, out var ws)) continue;

                var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;

                for (int row = 2; row <= lastRow; row++)
                {
                    if (ws.Row(row).IsEmpty()) continue;

                    try
                    {
                        // Template mới: col1=Username, col2=Password, col3=Fullname (không có AccountId)
                        var username = ws.Cell(row, 1).GetString().Trim();
                        var password = ws.Cell(row, 2).GetString().Trim();
                        var fullname = ws.Cell(row, 3).GetString().Trim();

                        if (string.IsNullOrEmpty(username))
                        { errors.Add($"[{sheetName}] Dòng {row}: Username không được để trống"); failed++; continue; }

                        if (string.IsNullOrEmpty(password))
                        { errors.Add($"[{sheetName}] Dòng {row}: Password không được để trống"); failed++; continue; }

                        if (string.IsNullOrEmpty(fullname))
                        { errors.Add($"[{sheetName}] Dòng {row}: Fullname không được để trống"); failed++; continue; }

                        if (await _context.Accounts.AnyAsync(a => a.Username == username))
                        { errors.Add($"[{sheetName}] Dòng {row}: Username '{username}' đã tồn tại"); failed++; continue; }

                        // col4=Dob, col5=Phone, col6=Gender
                        var dobStr = ws.Cell(row, 4).GetString().Trim();
                        var phone = ws.Cell(row, 5).GetString().Trim();
                        var genderStr = ws.Cell(row, 6).GetString().Trim();

                        DateTime? dob = null;
                        if (!string.IsNullOrEmpty(dobStr) &&
                            DateTime.TryParseExact(dobStr,
                                new[] { "dd/MM/yyyy", "yyyy-MM-dd", "M/d/yyyy" },
                                null, System.Globalization.DateTimeStyles.None, out var parsedDob))
                            dob = parsedDob;

                        // Dropdown đảm bảo chỉ Male/Female — fallback Male nếu ô trống
                        var gender = genderStr.Equals("Female", StringComparison.OrdinalIgnoreCase)
                            ? Gender.Female : Gender.Male;

                        var account = new Account
                        {
                            Username = username,
                            Password = BCrypt.Net.BCrypt.HashPassword(password),
                            Role = role,
                            IsActive = IsActive.Active,
                            CreatedAt = DateTime.Now
                        };
                        _context.Accounts.Add(account);
                        await _context.SaveChangesAsync();

                        switch (role)
                        {
                            case Role.Admin:
                                // col7=Title
                                _context.Staffs.Add(new Staff
                                {
                                    AccountId = account.AccountId,
                                    Fullname = fullname,
                                    Dob = dob,
                                    Phone = phone.Length > 0 ? phone : null,
                                    Gender = gender,
                                    Title = ws.Cell(row, 7).GetString().Trim() is { Length: > 0 } t ? t : null
                                });
                                break;

                            case Role.Teacher:
                                // col7=Note, col8=Subject1, col9=Subject2, col10=Subject3
                                _context.Teachers.Add(new Teacher
                                {
                                    AccountId = account.AccountId,
                                    Fullname = fullname,
                                    Dob = dob,
                                    Phone = phone.Length > 0 ? phone : null,
                                    Gender = gender,
                                    Note = ws.Cell(row, 7).GetString().Trim() is { Length: > 0 } n ? n : null
                                });
                                await _context.SaveChangesAsync();

                                var addedSubjects = new HashSet<Subject>();
                                for (int col = 8; col <= 10; col++)
                                {
                                    var subjectStr = ws.Cell(row, col).GetString().Trim();
                                    if (string.IsNullOrEmpty(subjectStr)) continue;
                                    if (!SubjectMap.TryGetValue(subjectStr, out var subject)) continue;
                                    if (!addedSubjects.Add(subject)) continue;
                                    _context.TeacherSubjects.Add(new TeacherSubject
                                    {
                                        TeacherId = account.AccountId,
                                        Subject = subject
                                    });
                                }
                                break;

                            case Role.Student:
                                // col7=CurrentSchool, col8=Note, col9=ParentUsername
                                int? parentId = null;
                                var parentUsername = ws.Cell(row, 9).GetString().Trim();
                                if (!string.IsNullOrEmpty(parentUsername))
                                {
                                    var parentAcc = await _context.Accounts
                                        .FirstOrDefaultAsync(a => a.Username == parentUsername && a.Role == Role.Parent);
                                    if (parentAcc != null)
                                        parentId = parentAcc.AccountId;
                                }

                                _context.Students.Add(new Student
                                {
                                    AccountId = account.AccountId,
                                    Fullname = fullname,
                                    Dob = dob,
                                    Phone = phone.Length > 0 ? phone : null,
                                    Gender = gender,
                                    CurrentSchool = ws.Cell(row, 7).GetString().Trim() is { Length: > 0 } cs ? cs : null,
                                    Note = ws.Cell(row, 8).GetString().Trim() is { Length: > 0 } sn ? sn : null,
                                    ParentId = parentId
                                });
                                break;

                            case Role.Parent:
                                // col7=Note, col8=Students
                                _context.Parents.Add(new Parent
                                {
                                    AccountId = account.AccountId,
                                    Fullname = fullname,
                                    Dob = dob,
                                    Phone = phone.Length > 0 ? phone : null,
                                    Gender = gender,
                                    Note = ws.Cell(row, 7).GetString().Trim() is { Length: > 0 } pn ? pn : null
                                });
                                await _context.SaveChangesAsync();

                                var studentsCell = ws.Cell(row, 8).GetString().Trim();
                                if (!string.IsNullOrEmpty(studentsCell))
                                {
                                    var studentUsernames = studentsCell.Split(',',
                                        StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                                    foreach (var stuUsername in studentUsernames)
                                    {
                                        var stuAcc = await _context.Accounts
                                            .FirstOrDefaultAsync(a => a.Username == stuUsername && a.Role == Role.Student);
                                        if (stuAcc == null) continue;

                                        var student = await _context.Students.FindAsync(stuAcc.AccountId);
                                        if (student == null) continue;
                                        student.ParentId = account.AccountId;
                                    }
                                }
                                break;
                        }

                        await _context.SaveChangesAsync();
                        success++;
                    }
                    catch (Exception ex)
                    {
                        errors.Add($"[{sheetName}] Dòng {row}: Lỗi hệ thống - {ex.Message}");
                        failed++;
                    }
                }
            }

            return new ImportResult(success, failed, errors);
        }

        // =============================================
        // TEMPLATE — dropdown validation, không có dòng mẫu
        // =============================================

        public byte[] GenerateImportTemplate()
        {
            using var wb = new XLWorkbook();

            // Sheet ẩn chứa danh sách môn học để dùng cho Data Validation
            var wsRef = wb.AddWorksheet("_Ref");
            var subjects = new[] { "Math", "Vietnamese", "English", "Physics", "Biology", "Chemistry", "Geography", "History", "Other" };
            for (int i = 0; i < subjects.Length; i++)
                wsRef.Cell(i + 1, 1).Value = subjects[i];
            wsRef.Hide();

            BuildTemplateSheet(wb, "Staff", new[]
            {
                "Username *", "Password *", "Fullname *",
                "Dob (dd/MM/yyyy)", "Phone", "Gender *", "Title"
            },
            genderCol: 6, subjectCols: null, studentCol: null, parentCol: null);

            BuildTemplateSheet(wb, "Teacher", new[]
            {
                "Username *", "Password *", "Fullname *",
                "Dob (dd/MM/yyyy)", "Phone", "Gender *", "Note",
                "Subject1", "Subject2", "Subject3"
            },
            genderCol: 6, subjectCols: new[] { 8, 9, 10 }, studentCol: null, parentCol: null);

            BuildTemplateSheet(wb, "Student", new[]
            {
                "Username *", "Password *", "Fullname *",
                "Dob (dd/MM/yyyy)", "Phone", "Gender *",
                "CurrentSchool", "Note", "ParentUsername"
            },
            genderCol: 6, subjectCols: null, studentCol: null, parentCol: null);

            BuildTemplateSheet(wb, "Parent", new[]
            {
                "Username *", "Password *", "Fullname *",
                "Dob (dd/MM/yyyy)", "Phone", "Gender *", "Note",
                "Students (phân cách bằng dấu phẩy)"
            },
            genderCol: 6, subjectCols: null, studentCol: null, parentCol: null);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private static void BuildTemplateSheet(
            XLWorkbook wb, string sheetName, string[] headers,
            int genderCol, int[]? subjectCols, int? studentCol, int? parentCol)
        {
            var ws = wb.AddWorksheet(sheetName);

            // Headers
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x2E74B5);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
                cell.Style.Border.BottomBorderColor = XLColor.FromArgb(0x1F5396);
            }
            ws.Row(1).Height = 22;
            ws.SheetView.FreezeRows(1);

            // Tô màu nhạt các cột bắt buộc (Username, Password, Fullname, Gender)
            foreach (int col in new[] { 1, 2, 3, genderCol })
            {
                ws.Range(2, col, 1000, col).Style
                  .Fill.BackgroundColor = XLColor.FromArgb(0xFFF2CC); // vàng nhạt
            }

            // Dropdown Gender: Male / Female
            ws.Range(2, genderCol, 1000, genderCol)
              .SetDataValidation()
              .List("\"Male,Female\"", true);

            // Dropdown Subject (tham chiếu sheet _Ref)
            if (subjectCols != null)
            {
                foreach (var col in subjectCols)
                {
                    ws.Range(2, col, 1000, col)
                      .SetDataValidation()
                      .List("_Ref!$A$1:$A$9", true);
                }
            }

            ws.Columns().AdjustToContents();
        }

        // =============================================
        // HELPERS
        // =============================================

        private static void SetHeaders(IXLWorksheet ws, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cell(1, i + 1);
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x4472C4);
                cell.Style.Font.FontColor = XLColor.White;
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            }
            ws.Row(1).Height = 20;
        }

        private static void ApplyTableStyle(IXLWorksheet ws, int lastRow, int lastCol)
        {
            if (lastRow < 2) return;
            var range = ws.Range(1, 1, lastRow, lastCol);
            range.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            range.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
            ws.Columns().AdjustToContents();
        }
    }
}