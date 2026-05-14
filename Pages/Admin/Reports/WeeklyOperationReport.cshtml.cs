using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin
{
    public class WeeklyOperationReportModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public WeeklyOperationReportModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public DateOnly DateFrom { get; set; }
        [BindProperty(SupportsGet = true)] public DateOnly DateTo   { get; set; }
        [BindProperty(SupportsGet = true)] public bool     Export   { get; set; } = false;

        public string PeriodLabel =>
            $"{DateFrom:dd/MM/yyyy} – {DateTo:dd/MM/yyyy}";

        // ── Buổi học ─────────────────────────────────────────────────
        public int SessionsCompleted  { get; set; }
        public int SessionsCancelled  { get; set; }
        public int SessionsScheduled  { get; set; }
        public int SessionsOngoing    { get; set; }
        public int SessionsTotal      => SessionsCompleted + SessionsCancelled + SessionsScheduled + SessionsOngoing;
        public double CancelRate      => SessionsTotal > 0
            ? Math.Round((double)SessionsCancelled / SessionsTotal * 100, 1) : 0;
        public double CompletionRate  => SessionsTotal > 0
            ? Math.Round((double)SessionsCompleted / SessionsTotal * 100, 1) : 0;

        // ── Điểm danh ────────────────────────────────────────────────
        public int    AttPresent      { get; set; }
        public int    AttAbsent       { get; set; }
        public int    AttTotal        => AttPresent + AttAbsent;
        public double AttRate         => AttTotal > 0
            ? Math.Round((double)AttPresent / AttTotal * 100, 1) : 0;

        // ── Học sinh ─────────────────────────────────────────────────
        public int StudentNewEnrolled { get; set; }  // ClassStudent.StartedAt in period
        public int StudentLeft        { get; set; }  // ClassStudent.LeftAt in period
        public int StudentActiveNow   { get; set; }  // ClassStudent Active hiện tại
        public int StudentSuspended   { get; set; }  // ClassStudent Suspended

        // ── Giáo viên ────────────────────────────────────────────────
        public int TeacherTotal       { get; set; }
        public int TeacherNewInPeriod { get; set; }  // Account.CreatedAt in period

        // ── Lớp học ──────────────────────────────────────────────────
        public int ClassActive        { get; set; }
        public int ClassInactive      { get; set; }

        // ── Phòng học ────────────────────────────────────────────────
        public int RoomTotal          { get; set; }

        // ── Chi tiết: top giáo viên ──────────────────────────────────
        public List<TeacherStatRow>   TopTeachers    { get; set; } = new();

        // ── Chi tiết: top lớp vắng ───────────────────────────────────
        public List<ClassAbsenceRow>  TopAbsClasses  { get; set; } = new();

        // ── Chi tiết: điểm danh theo môn ────────────────────────────
        public List<SubjectAttRow>    SubjectStats   { get; set; } = new();

        // ── Chi tiết: buổi học theo lớp ─────────────────────────────
        public List<ClassSessionRow>  ClassSessions  { get; set; } = new();

        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            // Mặc định = tuần hiện tại (Thứ Hai → Chủ Nhật)
            if (DateFrom == default)
            {
                var dow       = (int)DateTime.Today.DayOfWeek;
                var daysToMon = dow == 0 ? 6 : dow - 1;
                DateFrom = DateOnly.FromDateTime(DateTime.Today.AddDays(-daysToMon));
            }
            if (DateTo == default)
            {
                DateTo = DateFrom.AddDays(6);
                if (DateTo > DateOnly.FromDateTime(DateTime.Today))
                    DateTo = DateOnly.FromDateTime(DateTime.Today);
            }

            await LoadAllDataAsync();

            if (Export)
            {
                var bytes = BuildExcel();
                var fn = $"BC_VanHanh_{DateFrom:yyyyMMdd}_{DateTo:yyyyMMdd}.xlsx";
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fn);
            }

            return Page();
        }

        private async Task LoadAllDataAsync()
        {
            var dtFrom = DateFrom.ToDateTime(TimeOnly.MinValue);
            var dtTo   = DateTo.ToDateTime(TimeOnly.MaxValue);

            // ── Buổi học ──
            var sessions = await _context.Sessions
                .Where(s => s.SessionDate >= DateFrom && s.SessionDate <= DateTo)
                .Select(s => new { s.Status, s.TeacherId, s.Teacher.Fullname, s.ClassId })
                .ToListAsync();

            SessionsCompleted = sessions.Count(s => s.Status == SessionStatus.Completed);
            SessionsCancelled = sessions.Count(s => s.Status == SessionStatus.Cancelled);
            SessionsScheduled = sessions.Count(s => s.Status == SessionStatus.Scheduled);
            SessionsOngoing   = sessions.Count(s => s.Status == SessionStatus.Ongoing);

            // ── Điểm danh ──
            var attendances = await _context.Attendances
                .Where(a => a.Session.SessionDate >= DateFrom && a.Session.SessionDate <= DateTo)
                .Select(a => new { a.Status, a.StudentId, SubjectId = a.Session.Class.Subject, ClassId = a.Session.ClassId })
                .ToListAsync();

            AttPresent = attendances.Count(a => a.Status == AttendanceStatus.Present);
            AttAbsent  = attendances.Count(a => a.Status == AttendanceStatus.Absent);

            // ── Học sinh ──
            StudentNewEnrolled = await _context.ClassStudents
                .CountAsync(cs => cs.StartedAt >= DateFrom && cs.StartedAt <= DateTo);

            StudentLeft = await _context.ClassStudents
                .CountAsync(cs => cs.LeftAt != null && cs.LeftAt >= DateFrom && cs.LeftAt <= DateTo);

            StudentActiveNow  = await _context.ClassStudents
                .Where(cs => cs.Status == StudentClassStatus.Active)
                .Select(cs => cs.StudentId).Distinct().CountAsync();

            StudentSuspended  = await _context.ClassStudents
                .Where(cs => cs.Status == StudentClassStatus.Suspended)
                .Select(cs => cs.StudentId).Distinct().CountAsync();

            // ── Giáo viên ──
            TeacherTotal = await _context.Teachers.CountAsync();

            TeacherNewInPeriod = await _context.Teachers
                .CountAsync(t => t.Account.CreatedAt >= dtFrom && t.Account.CreatedAt <= dtTo);

            // ── Lớp / Phòng ──
            ClassActive   = await _context.Classes.CountAsync(c => c.Status == ClassStatus.Active);
            ClassInactive = await _context.Classes.CountAsync(c => c.Status == ClassStatus.Inactive);
            RoomTotal     = await _context.Rooms.CountAsync();

            // ── Top giáo viên (theo buổi hoàn thành) ──
            var teacherGroups = sessions
                .Where(s => s.Status == SessionStatus.Completed)
                .GroupBy(s => new { s.TeacherId, s.Fullname })
                .Select(g => new TeacherStatRow
                {
                    TeacherName  = g.Key.Fullname,
                    Completed    = g.Count(),
                    ClassCount   = g.Select(s => s.ClassId).Distinct().Count()
                })
                .OrderByDescending(r => r.Completed)
                .Take(10).ToList();
            TopTeachers = teacherGroups;

            // ── Top lớp vắng ──
            var classAbsGroups = attendances
                .GroupBy(a => a.ClassId)
                .Select(g => new
                {
                    ClassId    = g.Key,
                    Total      = g.Count(),
                    Absent     = g.Count(a => a.Status == AttendanceStatus.Absent),
                })
                .Where(g => g.Total > 0)
                .OrderByDescending(g => (double)g.Absent / g.Total)
                .Take(8).ToList();

            var classIds   = classAbsGroups.Select(g => g.ClassId).ToList();
            var classInfos = await _context.Classes
                .Where(c => classIds.Contains(c.ClassId))
                .Select(c => new { c.ClassId, c.ClassCode, c.ClassName, c.Subject })
                .ToListAsync();

            TopAbsClasses = classAbsGroups.Select(g =>
            {
                var ci = classInfos.FirstOrDefault(c => c.ClassId == g.ClassId);
                return new ClassAbsenceRow
                {
                    ClassCode    = ci?.ClassCode   ?? "—",
                    ClassName    = ci?.ClassName   ?? "",
                    Subject      = ci?.Subject     ?? Subject.Other,
                    TotalAtt     = g.Total,
                    AbsentCount  = g.Absent,
                    AbsenceRate  = Math.Round((double)g.Absent / g.Total * 100, 1)
                };
            }).ToList();

            // ── Điểm danh theo môn ──
            SubjectStats = attendances
                .GroupBy(a => a.SubjectId)
                .Select(g => new SubjectAttRow
                {
                    Subject = g.Key,
                    Present = g.Count(a => a.Status == AttendanceStatus.Present),
                    Absent  = g.Count(a => a.Status == AttendanceStatus.Absent)
                })
                .OrderBy(s => s.Subject)
                .ToList();

            // ── Buổi học theo lớp ──
            var clsSessionGroups = sessions
                .GroupBy(s => s.ClassId)
                .Select(g => new
                {
                    ClassId   = g.Key,
                    Completed = g.Count(s => s.Status == SessionStatus.Completed),
                    Cancelled = g.Count(s => s.Status == SessionStatus.Cancelled),
                    Total     = g.Count()
                })
                .Where(g => g.Total > 0)
                .OrderByDescending(g => g.Completed)
                .Take(10).ToList();

            var clsIds2   = clsSessionGroups.Select(g => g.ClassId).ToList();
            var clsInfos2 = await _context.Classes
                .Where(c => clsIds2.Contains(c.ClassId))
                .Select(c => new { c.ClassId, c.ClassCode, c.ClassName, c.Subject })
                .ToListAsync();

            ClassSessions = clsSessionGroups.Select(g =>
            {
                var ci = clsInfos2.FirstOrDefault(c => c.ClassId == g.ClassId);
                return new ClassSessionRow
                {
                    ClassCode  = ci?.ClassCode ?? "—",
                    ClassName  = ci?.ClassName ?? "",
                    Subject    = ci?.Subject   ?? Subject.Other,
                    Completed  = g.Completed,
                    Cancelled  = g.Cancelled,
                    Total      = g.Total
                };
            }).ToList();
        }

        // ── Excel builder ─────────────────────────────────────────────
        private byte[] BuildExcel()
        {
            using var wb = new XLWorkbook();

            BuildSummarySheet(wb);
            BuildSessionSheet(wb);
            BuildAttendanceSheet(wb);

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        // Sheet 1: Tổng quan
        private void BuildSummarySheet(XLWorkbook wb)
        {
            var ws = wb.AddWorksheet("Tổng Quan Vận Hành");
            const int cols = 4;

            TeacherSessionsReportModel.MergeTitle(ws, 1, cols, "BÁO CÁO VẬN HÀNH TRUNG TÂM");
            TeacherSessionsReportModel.MergeSubtitle(ws, 2, cols,
                $"Kỳ báo cáo: {DateFrom:dd/MM/yyyy} – {DateTo:dd/MM/yyyy}");
            TeacherSessionsReportModel.MergeInfo(ws, 3, cols,
                $"Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}");

            void SectionHeader(IXLWorksheet w, int row, string text, string color)
            {
                w.Cell(row, 1).Value = text;
                w.Range(row, 1, row, cols).Merge();
                w.Cell(row, 1).Style.Font.Bold = true;
                w.Cell(row, 1).Style.Font.FontSize = 11;
                w.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromHtml(color);
                w.Cell(row, 1).Style.Font.FontColor = XLColor.White;
                w.Row(row).Height = 18;
            }

            void DataRow(IXLWorksheet w, int row, string label, object val1, object? val2 = null, string? note = null)
            {
                w.Cell(row, 1).Value = label;
                w.Cell(row, 2).Value = val1 is int i ? (XLCellValue)i : (XLCellValue)(val1?.ToString() ?? "");
                w.Cell(row, 2).Style.Font.Bold = true;
                w.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                if (val2 != null)
                {
                    w.Cell(row, 3).Value = val2 is double d ? (XLCellValue)d : (XLCellValue)(val2?.ToString() ?? "");
                    w.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                }
                if (note != null) { w.Cell(row, 4).Value = note; w.Cell(row, 4).Style.Font.Italic = true; w.Cell(row, 4).Style.Font.FontColor = XLColor.Gray; }
                bool alt = row % 2 == 0;
                w.Range(row, 1, row, cols).Style.Fill.BackgroundColor = alt ? XLColor.FromHtml("#f0f4ff") : XLColor.White;
                w.Range(row, 1, row, cols).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                w.Range(row, 1, row, cols).Style.Border.OutsideBorderColor = XLColor.FromHtml("#e5e7eb");
            }

            // Headers cho table
            ws.Cell(4, 1).Value = "Chỉ số"; ws.Cell(4, 2).Value = "Giá trị";
            ws.Cell(4, 3).Value = "Tỉ lệ (%)"; ws.Cell(4, 4).Value = "Ghi chú";
            TeacherSessionsReportModel.WriteHeaders(ws, 4, new[] { "Chỉ số", "Giá trị", "Tỉ lệ (%)", "Ghi chú" });

            int r = 5;
            // Buổi học
            SectionHeader(ws, r++, "I. BUỔI HỌC", "#1e2433");
            DataRow(ws, r++, "Tổng số ca trong kỳ", SessionsTotal);
            DataRow(ws, r++, "Ca đã hoàn thành",     SessionsCompleted, CompletionRate, "% trên tổng ca");
            DataRow(ws, r++, "Ca đang diễn ra",       SessionsOngoing);
            DataRow(ws, r++, "Ca lịch chưa dạy",      SessionsScheduled);
            DataRow(ws, r++, "Ca bị hủy",             SessionsCancelled, CancelRate,
                CancelRate > 10 ? "⚠ Tỉ lệ hủy cao" : "Bình thường");

            r++;
            // Điểm danh
            SectionHeader(ws, r++, "II. ĐIỂM DANH HỌC SINH", "#0f6cbf");
            DataRow(ws, r++, "Tổng lượt điểm danh",  AttTotal);
            DataRow(ws, r++, "Lượt có mặt",           AttPresent, AttRate, "Tỉ lệ chuyên cần");
            DataRow(ws, r++, "Lượt vắng mặt",         AttAbsent,
                AttTotal > 0 ? Math.Round(100 - AttRate, 1) : (object)0,
                AttRate < 70 ? "⚠ Chuyên cần thấp" : AttRate >= 90 ? "Tốt" : "Trung bình");

            r++;
            // Học sinh
            SectionHeader(ws, r++, "III. HỌC SINH", "#059669");
            DataRow(ws, r++, "Đang học (active)",      StudentActiveNow);
            DataRow(ws, r++, "Tạm nghỉ (suspended)",   StudentSuspended);
            DataRow(ws, r++, "Nhập học trong kỳ",      StudentNewEnrolled,
                note: StudentNewEnrolled > 0 ? $"+{StudentNewEnrolled} học sinh mới" : "Không có học sinh mới");
            DataRow(ws, r++, "Nghỉ học trong kỳ",      StudentLeft,
                note: StudentLeft > 0 ? $"-{StudentLeft} học sinh" : "Không có học sinh nghỉ");

            r++;
            // Giáo viên & Hạ tầng
            SectionHeader(ws, r++, "IV. GIÁO VIÊN & HẠ TẦNG", "#7c3aed");
            DataRow(ws, r++, "Tổng giáo viên",         TeacherTotal);
            DataRow(ws, r++, "Giáo viên mới trong kỳ", TeacherNewInPeriod,
                note: TeacherNewInPeriod > 0 ? $"+{TeacherNewInPeriod} GV mới" : "Không có GV mới");
            DataRow(ws, r++, "Lớp đang hoạt động",     ClassActive);
            DataRow(ws, r++, "Lớp tạm ngừng",          ClassInactive);
            DataRow(ws, r++, "Tổng phòng học",          RoomTotal);

            ws.Column(1).Width = 30;
            ws.Column(2).Width = 12;
            ws.Column(3).Width = 14;
            ws.Column(4).Width = 28;
        }

        // Sheet 2: Chi tiết buổi học
        private void BuildSessionSheet(XLWorkbook wb)
        {
            var ws = wb.AddWorksheet("Chi Tiết Buổi Học");
            const int cols = 5;

            TeacherSessionsReportModel.MergeTitle(ws, 1, cols, "CHI TIẾT BUỔI HỌC THEO GIÁO VIÊN & LỚP");
            TeacherSessionsReportModel.MergeSubtitle(ws, 2, cols,
                $"{DateFrom:dd/MM/yyyy} – {DateTo:dd/MM/yyyy}");

            // Top giáo viên
            int r = 4;
            ws.Cell(r, 1).Value = "TOP GIÁO VIÊN (buổi hoàn thành)";
            ws.Range(r, 1, r, cols).Merge();
            ws.Cell(r, 1).Style.Font.Bold = true; ws.Cell(r, 1).Style.Font.FontSize = 11;
            ws.Cell(r, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e2433");
            ws.Cell(r, 1).Style.Font.FontColor = XLColor.White; r++;

            TeacherSessionsReportModel.WriteHeaders(ws, r,
                new[] { "STT", "Giáo viên", "Hoàn thành", "Số lớp", "Ghi chú" });
            r++;

            int stt = 1;
            foreach (var t in TopTeachers)
            {
                bool alt = r % 2 == 0;
                ws.Cell(r, 1).Value = stt++;
                ws.Cell(r, 2).Value = t.TeacherName;
                ws.Cell(r, 3).Value = t.Completed;
                ws.Cell(r, 4).Value = t.ClassCount;
                ws.Cell(r, 5).Value = stt == 2 ? "🥇" : stt == 3 ? "🥈" : stt == 4 ? "🥉" : "";
                TeacherSessionsReportModel.StyleDataRow(ws, r, cols, alt); r++;
            }

            r++;
            ws.Cell(r, 1).Value = "BUỔI HỌC THEO LỚP";
            ws.Range(r, 1, r, cols).Merge();
            ws.Cell(r, 1).Style.Font.Bold = true; ws.Cell(r, 1).Style.Font.FontSize = 11;
            ws.Cell(r, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#1e2433");
            ws.Cell(r, 1).Style.Font.FontColor = XLColor.White; r++;

            TeacherSessionsReportModel.WriteHeaders(ws, r,
                new[] { "STT", "Lớp", "Môn học", "Hoàn thành", "Đã hủy" });
            r++;

            stt = 1;
            foreach (var c in ClassSessions)
            {
                bool alt = r % 2 == 0;
                ws.Cell(r, 1).Value = stt++;
                ws.Cell(r, 2).Value = string.IsNullOrEmpty(c.ClassName) ? c.ClassCode : $"{c.ClassCode} - {c.ClassName}";
                ws.Cell(r, 3).Value = c.SubjectLabel;
                ws.Cell(r, 4).Value = c.Completed;
                ws.Cell(r, 5).Value = c.Cancelled;
                TeacherSessionsReportModel.StyleDataRow(ws, r, cols, alt); r++;
            }

            TeacherSessionsReportModel.AdjustColumns(ws, 2);
        }

        // Sheet 3: Điểm danh & vắng
        private void BuildAttendanceSheet(XLWorkbook wb)
        {
            var ws = wb.AddWorksheet("Điểm Danh & Chuyên Cần");
            const int cols = 5;

            TeacherSessionsReportModel.MergeTitle(ws, 1, cols, "ĐIỂM DANH & TỈ LỆ CHUYÊN CẦN");
            TeacherSessionsReportModel.MergeSubtitle(ws, 2, cols,
                $"{DateFrom:dd/MM/yyyy} – {DateTo:dd/MM/yyyy}");

            // Điểm danh theo môn
            int r = 4;
            ws.Cell(r, 1).Value = "ĐIỂM DANH THEO MÔN HỌC";
            ws.Range(r, 1, r, cols).Merge();
            ws.Cell(r, 1).Style.Font.Bold = true; ws.Cell(r, 1).Style.Font.FontSize = 11;
            ws.Cell(r, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#0f6cbf");
            ws.Cell(r, 1).Style.Font.FontColor = XLColor.White; r++;

            TeacherSessionsReportModel.WriteHeaders(ws, r,
                new[] { "Môn học", "Có mặt", "Vắng mặt", "Tổng", "Tỉ lệ (%)" });
            r++;

            foreach (var s in SubjectStats)
            {
                bool alt = r % 2 == 0;
                ws.Cell(r, 1).Value = s.SubjectLabel;
                ws.Cell(r, 2).Value = s.Present;
                ws.Cell(r, 3).Value = s.Absent;
                ws.Cell(r, 4).Value = s.Total;
                ws.Cell(r, 5).Value = s.Rate;
                TeacherSessionsReportModel.StyleDataRow(ws, r, cols, alt);
                ws.Cell(r, 5).Style.Font.Bold = true;
                ws.Cell(r, 5).Style.Font.FontColor = s.Rate >= 90
                    ? XLColor.FromHtml("#059669") : s.Rate >= 70
                    ? XLColor.FromHtml("#d97706") : XLColor.FromHtml("#e11d48");
                r++;
            }

            r++;
            ws.Cell(r, 1).Value = "TOP LỚP VẮng CAO";
            ws.Range(r, 1, r, cols).Merge();
            ws.Cell(r, 1).Style.Font.Bold = true; ws.Cell(r, 1).Style.Font.FontSize = 11;
            ws.Cell(r, 1).Style.Fill.BackgroundColor = XLColor.FromHtml("#c92a2a");
            ws.Cell(r, 1).Style.Font.FontColor = XLColor.White; r++;

            TeacherSessionsReportModel.WriteHeaders(ws, r,
                new[] { "Lớp", "Môn", "Vắng", "Tổng", "Tỉ lệ vắng (%)" });
            r++;

            foreach (var c in TopAbsClasses)
            {
                bool alt = r % 2 == 0;
                ws.Cell(r, 1).Value = string.IsNullOrEmpty(c.ClassName) ? c.ClassCode : $"{c.ClassCode} - {c.ClassName}";
                ws.Cell(r, 2).Value = c.SubjectLabel;
                ws.Cell(r, 3).Value = c.AbsentCount;
                ws.Cell(r, 4).Value = c.TotalAtt;
                ws.Cell(r, 5).Value = c.AbsenceRate;
                TeacherSessionsReportModel.StyleDataRow(ws, r, cols, alt);
                ws.Cell(r, 5).Style.Font.Bold = true;
                ws.Cell(r, 5).Style.Font.FontColor = c.AbsenceRate >= 30
                    ? XLColor.FromHtml("#e11d48") : c.AbsenceRate >= 15
                    ? XLColor.FromHtml("#d97706") : XLColor.FromHtml("#059669");
                r++;
            }

            TeacherSessionsReportModel.AdjustColumns(ws, 1);
        }

        // ── View Models ───────────────────────────────────────────────
        public class TeacherStatRow
        {
            public string TeacherName { get; set; } = string.Empty;
            public int    Completed   { get; set; }
            public int    ClassCount  { get; set; }
            public string Initials    => string.Join("",
                TeacherName.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                    .Select(p => p[0]).Take(2)).ToUpper();
        }

        public class ClassAbsenceRow
        {
            public string  ClassCode    { get; set; } = string.Empty;
            public string  ClassName    { get; set; } = string.Empty;
            public Subject Subject      { get; set; }
            public int     TotalAtt     { get; set; }
            public int     AbsentCount  { get; set; }
            public double  AbsenceRate  { get; set; }
            public string  SubjectLabel => SubjectName(Subject);
        }

        public class SubjectAttRow
        {
            public Subject Subject { get; set; }
            public int     Present { get; set; }
            public int     Absent  { get; set; }
            public int     Total   => Present + Absent;
            public double  Rate    => Total > 0
                ? Math.Round((double)Present / Total * 100, 1) : 0;
            public string  SubjectLabel => SubjectName(Subject);
        }

        public class ClassSessionRow
        {
            public string  ClassCode  { get; set; } = string.Empty;
            public string  ClassName  { get; set; } = string.Empty;
            public Subject Subject    { get; set; }
            public int     Completed  { get; set; }
            public int     Cancelled  { get; set; }
            public int     Total      { get; set; }
            public string  SubjectLabel => SubjectName(Subject);
        }

        private static string SubjectName(Subject s) => s switch
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
    }
}
