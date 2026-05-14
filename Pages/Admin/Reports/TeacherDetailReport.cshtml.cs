using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin
{
    public class TeacherDetailReportModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public TeacherDetailReportModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public int      TeacherId { get; set; } = 0;
        [BindProperty(SupportsGet = true)] public DateOnly DateFrom  { get; set; }
        [BindProperty(SupportsGet = true)] public DateOnly DateTo    { get; set; }
        [BindProperty(SupportsGet = true)] public bool     Export    { get; set; } = false;

        // ── Dropdown ─────────────────────────────────────────────────
        public List<TeacherOption> Teachers    { get; set; } = new();
        public string SelectedTeacherName      { get; set; } = string.Empty;

        // ── Dữ liệu báo cáo ──────────────────────────────────────────
        public List<SessionDetailRow> Rows     { get; set; } = new();
        public int CountCompleted  { get; set; }
        public int CountCancelled  { get; set; }
        public int CountScheduled  { get; set; }
        public int CountOngoing    { get; set; }

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

            Teachers = await _context.Teachers
                .OrderBy(t => t.Fullname)
                .Select(t => new TeacherOption { Id = t.AccountId, Name = t.Fullname })
                .ToListAsync();

            if (TeacherId > 0)
            {
                SelectedTeacherName = Teachers.FirstOrDefault(t => t.Id == TeacherId)?.Name ?? "";
                await LoadDataAsync();
            }

            if (Export && TeacherId > 0)
            {
                var bytes = BuildExcel();
                var safeName = SelectedTeacherName.Replace(' ', '_');
                var fn = $"BC_CaDayChiTiet_{safeName}_{DateFrom:yyyyMMdd}_{DateTo:yyyyMMdd}.xlsx";
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fn);
            }

            return Page();
        }

        private async Task LoadDataAsync()
        {
            var sessions = await _context.Sessions
                .Where(s => s.TeacherId == TeacherId
                         && s.SessionDate >= DateFrom
                         && s.SessionDate <= DateTo)
                .Include(s => s.Class)
                .Include(s => s.Room)
                .Include(s => s.Shift)
                .Include(s => s.TeacherAssistant)
                .OrderBy(s => s.SessionDate)
                .ThenBy(s => s.Shift.StartTime)
                .ToListAsync();

            Rows = sessions.Select(s => new SessionDetailRow
            {
                SessionId         = s.SessionId,
                SessionDate       = s.SessionDate,
                DayLabel          = DayNames[(int)s.SessionDate.DayOfWeek],
                ShiftName         = s.Shift?.ShiftName ?? "—",
                ShiftTime         = s.Shift != null
                    ? $"{s.Shift.StartTime:hh\\:mm} – {s.Shift.EndTime:hh\\:mm}" : "—",
                RoomCode          = s.Room?.RoomCode ?? "—",
                ClassCode         = s.Class?.ClassCode ?? "—",
                ClassName         = s.Class?.ClassName ?? "",
                Subject           = s.Class?.Subject ?? Subject.Other,
                AssistantName     = s.TeacherAssistant?.Fullname ?? "",
                Status            = s.Status,
                Note              = s.Note ?? ""
            }).ToList();

            CountCompleted = Rows.Count(r => r.Status == SessionStatus.Completed);
            CountCancelled = Rows.Count(r => r.Status == SessionStatus.Cancelled);
            CountScheduled = Rows.Count(r => r.Status == SessionStatus.Scheduled);
            CountOngoing   = Rows.Count(r => r.Status == SessionStatus.Ongoing);
        }

        // ── Excel builder ─────────────────────────────────────────────
        private byte[] BuildExcel()
        {
            using var wb = new XLWorkbook();
            var ws = wb.AddWorksheet("Ca Dạy Chi Tiết");
            const int cols = 9;

            TeacherSessionsReportModel.MergeTitle(ws, 1, cols,
                $"BÁO CÁO CHI TIẾT CA DẠY — {SelectedTeacherName.ToUpper()}");
            TeacherSessionsReportModel.MergeSubtitle(ws, 2, cols,
                $"Từ ngày {DateFrom:dd/MM/yyyy} đến ngày {DateTo:dd/MM/yyyy}");
            TeacherSessionsReportModel.MergeInfo(ws, 3, cols,
                $"Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}   |   Tổng ca: {Rows.Count}   Hoàn thành: {CountCompleted}   Đã hủy: {CountCancelled}   Lịch dạy: {CountScheduled}");

            string[] hdrs = { "STT", "Ngày dạy", "Thứ", "Ca học", "Phòng", "Lớp", "Môn học", "Trợ giảng", "Trạng thái" };
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
                ws.Cell(row, 8).Value = r.AssistantName;
                ws.Cell(row, 9).Value = r.StatusLabel;
                TeacherSessionsReportModel.StyleDataRow(ws, row, cols, alt);

                // Color status cell
                ws.Cell(row, 9).Style.Font.Bold = true;
                ws.Cell(row, 9).Style.Font.FontColor = r.Status switch
                {
                    SessionStatus.Completed => XLColor.FromHtml("#059669"),
                    SessionStatus.Cancelled => XLColor.FromHtml("#e11d48"),
                    SessionStatus.Ongoing   => XLColor.FromHtml("#d97706"),
                    _                       => XLColor.FromHtml("#2563eb")
                };
                row++;
            }

            // Summary
            ws.Cell(row, 1).Value = "TỔNG CỘNG";
            ws.Range(row, 1, row, 8).Merge();
            ws.Cell(row, 9).Value = $"{Rows.Count} ca";
            TeacherSessionsReportModel.StyleSummary(ws, row, cols);

            TeacherSessionsReportModel.AdjustColumns(ws, 6);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // ── View Models ───────────────────────────────────────────────
        public class TeacherOption
        {
            public int    Id   { get; set; }
            public string Name { get; set; } = string.Empty;
            public string Initials => string.Join("",
                Name.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p[0]).Take(2)).ToUpper();
        }

        public class SessionDetailRow
        {
            public int           SessionId     { get; set; }
            public DateOnly      SessionDate   { get; set; }
            public string        DayLabel      { get; set; } = string.Empty;
            public string        ShiftName     { get; set; } = string.Empty;
            public string        ShiftTime     { get; set; } = string.Empty;
            public string        RoomCode      { get; set; } = string.Empty;
            public string        ClassCode     { get; set; } = string.Empty;
            public string        ClassName     { get; set; } = string.Empty;
            public Subject       Subject       { get; set; }
            public string        AssistantName { get; set; } = string.Empty;
            public SessionStatus Status        { get; set; }
            public string        Note          { get; set; } = string.Empty;

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

            public string StatusLabel => Status switch
            {
                SessionStatus.Completed => "Hoàn thành",
                SessionStatus.Cancelled => "Đã hủy",
                SessionStatus.Ongoing   => "Đang dạy",
                _                       => "Lịch dạy"
            };
        }
    }
}
