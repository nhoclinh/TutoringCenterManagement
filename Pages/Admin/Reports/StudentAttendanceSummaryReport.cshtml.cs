using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin
{
    public class StudentAttendanceSummaryReportModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public StudentAttendanceSummaryReportModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public DateOnly DateFrom { get; set; }
        [BindProperty(SupportsGet = true)] public DateOnly DateTo   { get; set; }
        [BindProperty(SupportsGet = true)] public bool     Export   { get; set; } = false;

        // ── Dữ liệu báo cáo ──────────────────────────────────────────
        public List<StudentSummaryRow> Rows         { get; set; } = new();
        public int    TotalStudents    { get; set; }
        public int    TotalPresent     { get; set; }
        public int    TotalAbsent      { get; set; }
        public double OverallRate      { get; set; }

        public string PeriodLabel =>
            $"{DateFrom:dd/MM/yyyy} – {DateTo:dd/MM/yyyy}";

        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            if (DateFrom == default)
                DateFrom = new DateOnly(DateTime.Today.Year, DateTime.Today.Month, 1);
            if (DateTo == default)
                DateTo = DateOnly.FromDateTime(DateTime.Today);

            await LoadDataAsync();

            if (Export)
            {
                var bytes = BuildExcel();
                var fn = $"BC_DiemDanhHocSinh_{DateFrom:yyyyMMdd}_{DateTo:yyyyMMdd}.xlsx";
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fn);
            }

            return Page();
        }

        private async Task LoadDataAsync()
        {
            var allStudents = await _context.Students
                .Select(s => new { s.AccountId, s.Fullname, s.CurrentSchool })
                .ToListAsync();

            var attendances = await _context.Attendances
                .Where(a => a.Session.SessionDate >= DateFrom && a.Session.SessionDate <= DateTo)
                .Select(a => new { a.StudentId, a.Status, ClassId = a.Session.ClassId })
                .ToListAsync();

            Rows = allStudents.Select(s =>
            {
                var atts    = attendances.Where(a => a.StudentId == s.AccountId).ToList();
                var present = atts.Count(a => a.Status == AttendanceStatus.Present);
                var absent  = atts.Count(a => a.Status == AttendanceStatus.Absent);
                var total   = atts.Count;
                var classes = atts.Select(a => a.ClassId).Distinct().Count();
                var rate    = total > 0 ? Math.Round((double)present / total * 100, 1) : 0.0;

                return new StudentSummaryRow
                {
                    StudentName    = s.Fullname,
                    CurrentSchool  = s.CurrentSchool ?? "",
                    Present        = present,
                    Absent         = absent,
                    TotalSessions  = total,
                    ClassCount     = classes,
                    AttendanceRate = rate
                };
            })
            .OrderByDescending(r => r.TotalSessions > 0)
            .ThenByDescending(r => r.AttendanceRate)
            .ThenBy(r => r.Absent)
            .ToList();

            TotalStudents = Rows.Count(r => r.TotalSessions > 0);
            TotalPresent  = Rows.Sum(r => r.Present);
            TotalAbsent   = Rows.Sum(r => r.Absent);
            int totalAll  = TotalPresent + TotalAbsent;
            OverallRate   = totalAll > 0 ? Math.Round((double)TotalPresent / totalAll * 100, 1) : 0;
        }

        // ── Excel builder ─────────────────────────────────────────────
        private byte[] BuildExcel()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Điểm Danh Học Sinh");
            const int cols = 7;

            TeacherSessionsReportModel.MergeTitle(ws, 1, cols, "BÁO CÁO ĐIỂM DANH HỌC SINH");
            TeacherSessionsReportModel.MergeSubtitle(ws, 2, cols,
                $"Từ ngày {DateFrom:dd/MM/yyyy} đến ngày {DateTo:dd/MM/yyyy}");
            TeacherSessionsReportModel.MergeInfo(ws, 3, cols,
                $"Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Học sinh có điểm danh: {TotalStudents}   |   Tỉ lệ chuyên cần TB: {OverallRate}%");

            string[] hdrs = { "STT", "Học sinh", "Trường", "Có mặt", "Vắng mặt", "Tổng buổi", "Tỉ lệ (%)" };
            TeacherSessionsReportModel.WriteHeaders(ws, 5, hdrs);

            int row = 6; int stt = 1;
            foreach (var r in Rows)
            {
                bool alt = row % 2 == 0;
                ws.Cell(row, 1).Value = r.TotalSessions > 0 ? (XLCellValue)stt++ : (XLCellValue)"—";
                ws.Cell(row, 2).Value = r.StudentName;
                ws.Cell(row, 3).Value = r.CurrentSchool;
                ws.Cell(row, 4).Value = r.Present;
                ws.Cell(row, 5).Value = r.Absent;
                ws.Cell(row, 6).Value = r.TotalSessions;
                ws.Cell(row, 7).Value = r.TotalSessions > 0 ? (XLCellValue)r.AttendanceRate : (XLCellValue)"—";
                TeacherSessionsReportModel.StyleDataRow(ws, row, cols, alt);

                // Color-code attendance rate cell
                if (r.TotalSessions > 0)
                {
                    var rateCell = ws.Cell(row, 7);
                    rateCell.Style.Font.Bold = true;
                    rateCell.Style.Font.FontColor = r.RateLevel switch
                    {
                        "high" => XLColor.FromHtml("#059669"),
                        "mid"  => XLColor.FromHtml("#d97706"),
                        _      => XLColor.FromHtml("#e11d48")
                    };
                }
                if (r.TotalSessions == 0)
                    ws.Range(row, 1, row, cols).Style.Font.FontColor = XLColor.FromHtml("#b0b8c9");
                row++;
            }

            // Tổng cộng
            ws.Cell(row, 1).Value = "TỔNG CỘNG";
            ws.Range(row, 1, row, 3).Merge();
            ws.Cell(row, 4).Value = TotalPresent;
            ws.Cell(row, 5).Value = TotalAbsent;
            ws.Cell(row, 6).Value = TotalPresent + TotalAbsent;
            ws.Cell(row, 7).Value = OverallRate;
            TeacherSessionsReportModel.StyleSummary(ws, row, cols);

            // Chú thích mức độ
            row += 2;
            ws.Cell(row, 1).Value = "Phân loại tỉ lệ chuyên cần:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row + 1, 1).Value = "• Tốt (≥ 90%): Đánh giá xuất sắc";
            ws.Cell(row + 2, 1).Value = "• Trung bình (70 – 89%): Cần theo dõi";
            ws.Cell(row + 3, 1).Value = "• Yếu (< 70%): Cần can thiệp, liên hệ phụ huynh";
            for (int nr = row; nr <= row + 3; nr++)
                ws.Cell(nr, 1).Style.Font.FontSize = 9;
            ws.Range(row, 1, row + 3, cols).Style.Font.FontColor = XLColor.FromHtml("#6b7280");

            TeacherSessionsReportModel.AdjustColumns(ws, 2);
            ws.Column(3).Width = 20;

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ── View Model ────────────────────────────────────────────────
        public class StudentSummaryRow
        {
            public string StudentName    { get; set; } = string.Empty;
            public string CurrentSchool  { get; set; } = string.Empty;
            public int    Present        { get; set; }
            public int    Absent         { get; set; }
            public int    TotalSessions  { get; set; }
            public int    ClassCount     { get; set; }
            public double AttendanceRate { get; set; }

            public string RateLevel =>
                TotalSessions == 0 ? "none" :
                AttendanceRate >= 90 ? "high" :
                AttendanceRate >= 70 ? "mid" : "low";

            public string Initials => string.Join("",
                StudentName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p[0]).Take(2)).ToUpper();
        }
    }
}
