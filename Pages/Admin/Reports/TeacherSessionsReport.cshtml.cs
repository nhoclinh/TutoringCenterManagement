using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin
{
    public class TeacherSessionsReportModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public TeacherSessionsReportModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public DateOnly DateFrom { get; set; }
        [BindProperty(SupportsGet = true)] public DateOnly DateTo   { get; set; }
        [BindProperty(SupportsGet = true)] public bool     Export   { get; set; } = false;

        // ── Dữ liệu báo cáo ──────────────────────────────────────────
        public List<TeacherSessionRow> Rows          { get; set; } = new();
        public int TotalTeachers  { get; set; }
        public int TotalCompleted { get; set; }
        public int TotalCancelled { get; set; }
        public int TotalScheduled { get; set; }
        public int TotalOngoing   { get; set; }

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
                var fn = $"BC_CaDayGiaoVien_{DateFrom:yyyyMMdd}_{DateTo:yyyyMMdd}.xlsx";
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fn);
            }

            return Page();
        }

        private async Task LoadDataAsync()
        {
            var allTeachers = await _context.Teachers
                .Select(t => new { t.AccountId, t.Fullname })
                .ToListAsync();

            var sessions = await _context.Sessions
                .Where(s => s.SessionDate >= DateFrom && s.SessionDate <= DateTo)
                .Select(s => new { s.TeacherId, s.Status, s.ClassId })
                .ToListAsync();

            Rows = allTeachers.Select(t =>
            {
                var ts = sessions.Where(s => s.TeacherId == t.AccountId).ToList();
                return new TeacherSessionRow
                {
                    TeacherName    = t.Fullname,
                    Completed      = ts.Count(s => s.Status == SessionStatus.Completed),
                    Scheduled      = ts.Count(s => s.Status == SessionStatus.Scheduled),
                    Cancelled      = ts.Count(s => s.Status == SessionStatus.Cancelled),
                    Ongoing        = ts.Count(s => s.Status == SessionStatus.Ongoing),
                    ClassCount     = ts.Select(s => s.ClassId).Distinct().Count(),
                    TotalSessions  = ts.Count
                };
            })
            .OrderByDescending(r => r.TotalSessions > 0)
            .ThenByDescending(r => r.Completed)
            .ThenByDescending(r => r.TotalSessions)
            .ToList();

            TotalTeachers  = Rows.Count(r => r.TotalSessions > 0);
            TotalCompleted = Rows.Sum(r => r.Completed);
            TotalCancelled = Rows.Sum(r => r.Cancelled);
            TotalScheduled = Rows.Sum(r => r.Scheduled);
            TotalOngoing   = Rows.Sum(r => r.Ongoing);
        }

        // ── Excel builder ─────────────────────────────────────────────
        private byte[] BuildExcel()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Ca Dạy Giáo Viên");
            const int cols = 7;

            // ── Tiêu đề ──
            MergeTitle(ws, 1, cols, "BÁO CÁO CA DẠY GIÁO VIÊN");
            MergeSubtitle(ws, 2, cols, $"Từ ngày {DateFrom:dd/MM/yyyy} đến ngày {DateTo:dd/MM/yyyy}");
            MergeInfo(ws, 3, cols, $"Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Tổng giáo viên có ca dạy: {TotalTeachers}");

            // ── Header ──
            string[] hdrs = { "STT", "Giáo viên", "Hoàn thành", "Đang dạy", "Lịch dạy", "Đã hủy", "Số lớp" };
            WriteHeaders(ws, 5, hdrs);

            // ── Dữ liệu ──
            int row = 6; int stt = 1;
            foreach (var r in Rows)
            {
                bool alt = row % 2 == 0;
                ws.Cell(row, 1).Value = r.TotalSessions > 0 ? (XLCellValue)stt++ : (XLCellValue)"—";
                ws.Cell(row, 2).Value = r.TeacherName;
                ws.Cell(row, 3).Value = r.Completed;
                ws.Cell(row, 4).Value = r.Ongoing;
                ws.Cell(row, 5).Value = r.Scheduled;
                ws.Cell(row, 6).Value = r.Cancelled;
                ws.Cell(row, 7).Value = r.ClassCount;
                StyleDataRow(ws, row, cols, alt);
                if (r.TotalSessions == 0)
                    ws.Range(row, 1, row, cols).Style.Font.FontColor = XLColor.FromHtml("#b0b8c9");
                row++;
            }

            // ── Tổng cộng ──
            ws.Cell(row, 1).Value = "TỔNG CỘNG";
            ws.Range(row, 1, row, 2).Merge();
            ws.Cell(row, 3).Value = TotalCompleted;
            ws.Cell(row, 4).Value = TotalOngoing;
            ws.Cell(row, 5).Value = TotalScheduled;
            ws.Cell(row, 6).Value = TotalCancelled;
            ws.Cell(row, 7).Value = Rows.Sum(r => r.ClassCount);
            StyleSummary(ws, row, cols);

            // ── Chú thích ──
            row += 2;
            ws.Cell(row, 1).Value = "Ghi chú:";
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row + 1, 1).Value = "• Hoàn thành: buổi học đã diễn ra và kết thúc";
            ws.Cell(row + 2, 1).Value = "• Lịch dạy: buổi học đã được lên lịch (chưa diễn ra)";
            ws.Cell(row + 3, 1).Value = "• Đã hủy: buổi học bị hủy trong kỳ";
            for (int nr = row; nr <= row + 3; nr++)
                ws.Cell(nr, 1).Style.Font.FontSize = 9;
            ws.Range(row, 1, row + 3, cols).Style.Font.FontColor = XLColor.FromHtml("#6b7280");

            AdjustColumns(ws, 2);
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ── Shared Excel Helpers ──────────────────────────────────────
        internal static void MergeTitle(IXLWorksheet ws, int row, int cols, string text)
        {
            var cell = ws.Cell(row, 1); cell.Value = text;
            ws.Range(row, 1, row, cols).Merge();
            cell.Style.Font.Bold = true; cell.Style.Font.FontSize = 14;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#e8590c");
            cell.Style.Font.FontColor = XLColor.White;
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            cell.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            ws.Row(row).Height = 24;
        }

        internal static void MergeSubtitle(IXLWorksheet ws, int row, int cols, string text)
        {
            var cell = ws.Cell(row, 1); cell.Value = text;
            ws.Range(row, 1, row, cols).Merge();
            cell.Style.Font.Bold = true; cell.Style.Font.FontSize = 11;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#fff3ec");
            cell.Style.Font.FontColor = XLColor.FromHtml("#c04a00");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(row).Height = 18;
        }

        internal static void MergeInfo(IXLWorksheet ws, int row, int cols, string text)
        {
            var cell = ws.Cell(row, 1); cell.Value = text;
            ws.Range(row, 1, row, cols).Merge();
            cell.Style.Font.Italic = true; cell.Style.Font.FontSize = 9;
            cell.Style.Font.FontColor = XLColor.FromHtml("#6b7280");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Row(row).Height = 14;
        }

        internal static void WriteHeaders(IXLWorksheet ws, int row, string[] headers)
        {
            for (int i = 0; i < headers.Length; i++)
            {
                var c = ws.Cell(row, i + 1);
                c.Value = headers[i];
                c.Style.Font.Bold = true;
                c.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e2433");
                c.Style.Font.FontColor = XLColor.White;
                c.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                c.Style.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
                c.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
                c.Style.Border.OutsideBorderColor = XLColor.FromHtml("#374151");
            }
            ws.Row(row).Height = 20;
        }

        internal static void StyleDataRow(IXLWorksheet ws, int row, int cols, bool alt)
        {
            var rng = ws.Range(row, 1, row, cols);
            rng.Style.Fill.BackgroundColor = alt
                ? XLColor.FromHtml("#f0f4ff") : XLColor.White;
            rng.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
            rng.Style.Border.OutsideBorderColor = XLColor.FromHtml("#e5e7eb");
            rng.Style.Border.InsideBorder       = XLBorderStyleValues.Thin;
            rng.Style.Border.InsideBorderColor  = XLColor.FromHtml("#e5e7eb");
            for (int c = 3; c <= cols; c++)
                ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(row).Height = 16;
        }

        internal static void StyleSummary(IXLWorksheet ws, int row, int cols)
        {
            var rng = ws.Range(row, 1, row, cols);
            rng.Style.Font.Bold = true;
            rng.Style.Fill.BackgroundColor = XLColor.FromHtml("#1e2433");
            rng.Style.Font.FontColor = XLColor.White;
            rng.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            rng.Style.Border.InsideBorder  = XLBorderStyleValues.Thin;
            rng.Style.Border.InsideBorderColor = XLColor.FromHtml("#374151");
            for (int c = 3; c <= cols; c++)
                ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(row).Height = 18;
        }

        internal static void AdjustColumns(IXLWorksheet ws, int nameCol)
        {
            ws.Columns().AdjustToContents();
            if (ws.Column(nameCol).Width < 24) ws.Column(nameCol).Width = 24;
        }

        // ── View Model ────────────────────────────────────────────────
        public class TeacherSessionRow
        {
            public string TeacherName   { get; set; } = string.Empty;
            public int    Completed     { get; set; }
            public int    Scheduled     { get; set; }
            public int    Cancelled     { get; set; }
            public int    Ongoing       { get; set; }
            public int    ClassCount    { get; set; }
            public int    TotalSessions { get; set; }

            public string Initials => string.Join("",
                TeacherName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p[0]).Take(2)).ToUpper();
        }
    }
}
