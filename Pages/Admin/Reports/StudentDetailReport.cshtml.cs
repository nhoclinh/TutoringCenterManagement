using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin
{
    public class StudentDetailReportModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public StudentDetailReportModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public int      StudentId { get; set; } = 0;
        [BindProperty(SupportsGet = true)] public DateOnly DateFrom  { get; set; }
        [BindProperty(SupportsGet = true)] public DateOnly DateTo    { get; set; }
        [BindProperty(SupportsGet = true)] public bool     Export    { get; set; } = false;

        // ── Dropdown ─────────────────────────────────────────────────
        public List<StudentOption> Students   { get; set; } = new();
        public string SelectedStudentName     { get; set; } = string.Empty;
        public string SelectedStudentSchool   { get; set; } = string.Empty;

        // ── Dữ liệu báo cáo ──────────────────────────────────────────
        public List<AttendanceDetailRow> Rows { get; set; } = new();
        public int CountPresent  { get; set; }
        public int CountAbsent   { get; set; }
        public double AttRate    { get; set; }

        public string PeriodLabel =>
            $"{DateFrom:dd/MM/yyyy} – {DateTo:dd/MM/yyyy}";

        private static readonly string[] DayNames =
            { "", "CN", "Hai", "Ba", "Tư", "Năm", "Sáu", "Bảy" };

        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            if (DateFrom == default)
                DateFrom = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
            if (DateTo == default)
                DateTo = DateOnly.FromDateTime(DateTime.Today);

            Students = await _context.Students
                .OrderBy(s => s.Fullname)
                .Select(s => new StudentOption
                {
                    Id     = s.AccountId,
                    Name   = s.Fullname,
                    School = s.CurrentSchool ?? ""
                })
                .ToListAsync();

            if (StudentId > 0)
            {
                var sel = Students.FirstOrDefault(s => s.Id == StudentId);
                SelectedStudentName   = sel?.Name   ?? "";
                SelectedStudentSchool = sel?.School ?? "";
                await LoadDataAsync();
            }

            if (Export && StudentId > 0)
            {
                var bytes = BuildExcel();
                var safeName = SelectedStudentName.Replace(' ', '_');
                var fn = $"BC_DiemDanhChiTiet_{safeName}_{DateFrom:yyyyMMdd}_{DateTo:yyyyMMdd}.xlsx";
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fn);
            }

            return Page();
        }

        private async Task LoadDataAsync()
        {
            var attendances = await _context.Attendances
                .Where(a => a.StudentId == StudentId
                         && a.Session.SessionDate >= DateFrom
                         && a.Session.SessionDate <= DateTo)
                .Include(a => a.Session).ThenInclude(s => s.Class)
                .Include(a => a.Session).ThenInclude(s => s.Room)
                .Include(a => a.Session).ThenInclude(s => s.Shift)
                .Include(a => a.Session).ThenInclude(s => s.Teacher)
                .OrderBy(a => a.Session.SessionDate)
                .ThenBy(a => a.Session.Shift.StartTime)
                .ToListAsync();

            Rows = attendances.Select(a => new AttendanceDetailRow
            {
                AttendanceId  = a.AttendanceId,
                SessionDate   = a.Session.SessionDate,
                DayLabel      = DayNames[(int)a.Session.SessionDate.DayOfWeek],
                ShiftName     = a.Session.Shift?.ShiftName ?? "—",
                ShiftTime     = a.Session.Shift != null
                    ? $"{a.Session.Shift.StartTime:hh\\:mm} – {a.Session.Shift.EndTime:hh\\:mm}" : "—",
                RoomCode      = a.Session.Room?.RoomCode ?? "—",
                ClassCode     = a.Session.Class?.ClassCode ?? "—",
                ClassName     = a.Session.Class?.ClassName ?? "",
                Subject       = a.Session.Class?.Subject ?? Subject.Other,
                TeacherName   = a.Session.Teacher?.Fullname ?? "—",
                Status        = a.Status,
                CheckInTime = TimeOnly.FromDateTime(a.CheckInTime),
                Note          = a.Note ?? ""
            }).ToList();

            CountPresent = Rows.Count(r => r.Status == AttendanceStatus.Present);
            CountAbsent  = Rows.Count(r => r.Status == AttendanceStatus.Absent);
            int total    = CountPresent + CountAbsent;
            AttRate      = total > 0 ? Math.Round((double)CountPresent / total * 100, 1) : 0;
        }

        // ── Excel builder ─────────────────────────────────────────────
        private byte[] BuildExcel()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Điểm Danh Chi Tiết");
            const int cols = 9;

            string title = $"HỌC BẠ ĐIỂM DANH — {SelectedStudentName.ToUpper()}";
            if (!string.IsNullOrEmpty(SelectedStudentSchool))
                title += $"  ({SelectedStudentSchool})";

            TeacherSessionsReportModel.MergeTitle(ws, 1, cols, title);
            TeacherSessionsReportModel.MergeSubtitle(ws, 2, cols,
                $"Từ ngày {DateFrom:dd/MM/yyyy} đến ngày {DateTo:dd/MM/yyyy}");
            TeacherSessionsReportModel.MergeInfo(ws, 3, cols,
                $"Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Có mặt: {CountPresent}   Vắng: {CountAbsent}   Tỉ lệ chuyên cần: {AttRate}%");

            string[] hdrs = { "STT", "Ngày học", "Thứ", "Ca học", "Phòng", "Lớp", "Môn học", "Giáo viên", "Điểm danh" };
            TeacherSessionsReportModel.WriteHeaders(ws, 5, hdrs);

            int row = 6; int stt = 1;
            foreach (var r in Rows)
            {
                bool alt = row % 2 == 0;
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = r.SessionDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 3).Value = r.DayLabel;
                ws.Cell(row, 4).Value = $"{r.ShiftName} ({r.ShiftTime})";
                ws.Cell(row, 5).Value = r.RoomCode;
                ws.Cell(row, 6).Value = string.IsNullOrEmpty(r.ClassName)
                    ? r.ClassCode : $"{r.ClassCode} - {r.ClassName}";
                ws.Cell(row, 7).Value = r.SubjectLabel;
                ws.Cell(row, 8).Value = r.TeacherName;
                ws.Cell(row, 9).Value = r.StatusLabel;
                TeacherSessionsReportModel.StyleDataRow(ws, row, cols, alt);

                // Color attendance status
                ws.Cell(row, 9).Style.Font.Bold = true;
                ws.Cell(row, 9).Style.Font.FontColor = r.Status == AttendanceStatus.Present
                    ? XLColor.FromHtml("#059669") : XLColor.FromHtml("#e11d48");
                row++;
            }

            // Summary
            ws.Cell(row, 1).Value = "TỔNG CỘNG";
            ws.Range(row, 1, row, 8).Merge();
            ws.Cell(row, 9).Value = $"Có mặt: {CountPresent} / {Rows.Count}  ({AttRate}%)";
            TeacherSessionsReportModel.StyleSummary(ws, row, cols);

            TeacherSessionsReportModel.AdjustColumns(ws, 6);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ── View Models ───────────────────────────────────────────────
        public class StudentOption
        {
            public int    Id     { get; set; }
            public string Name   { get; set; } = string.Empty;
            public string School { get; set; } = string.Empty;
            public string Initials => string.Join("",
                Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p[0]).Take(2)).ToUpper();
        }

        public class AttendanceDetailRow
        {
            public int              AttendanceId  { get; set; }
            public DateOnly         SessionDate   { get; set; }
            public string           DayLabel      { get; set; } = string.Empty;
            public string           ShiftName     { get; set; } = string.Empty;
            public string           ShiftTime     { get; set; } = string.Empty;
            public string           RoomCode      { get; set; } = string.Empty;
            public string           ClassCode     { get; set; } = string.Empty;
            public string           ClassName     { get; set; } = string.Empty;
            public Subject          Subject       { get; set; }
            public string           TeacherName   { get; set; } = string.Empty;
            public AttendanceStatus Status        { get; set; }
            public TimeOnly?        CheckInTime   { get; set; }
            public string           Note          { get; set; } = string.Empty;

            public string SubjectLabel => Subject switch
            {
                Subject.Math       => "Toán",
                Subject.Vietnamese => "Tiếng Việt",
                Subject.English    => "Tiếng Anh",
                Subject.Physics    => "Vật lý",
                Subject.Biology    => "Sinh học",
                Subject.Chemistry  => "Hóa học",
                Subject.Geography  => "Địa lý",
                Subject.History    => "Lịch sử",
                _                  => "Môn khác"
            };

            public string StatusLabel => Status == AttendanceStatus.Present
                ? "Có mặt" : "Vắng mặt";
        }
    }
}
