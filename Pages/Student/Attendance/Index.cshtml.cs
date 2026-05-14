using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Student.Attendance
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public IndexModel(ApplicationDbContext context) => _context = context;

        [BindProperty(SupportsGet = true)] public string? FilterYear { get; set; }
        [BindProperty(SupportsGet = true)] public string? FilterMonth { get; set; }
        [BindProperty(SupportsGet = true)] public string? ClassFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? StatusFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? ToDate { get; set; }
        [BindProperty(SupportsGet = true)] public int? StudentIdFilter { get; set; }
        [BindProperty(SupportsGet = true)] public bool Export { get; set; } = false;

        public string ViewerName { get; set; } = string.Empty;
        public string ViewerRole { get; set; } = string.Empty;
        public bool IsParent => ViewerRole == "Parent";
        public bool HasDateRange { get; set; }
        public int ActiveYear { get; set; }
        public int ActiveMonth { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;

        public List<ChildInfo> MyChildren { get; set; } = new();
        public List<ClassInfo> Classes { get; set; } = new();
        public List<AttendanceInfo> AttendanceList { get; set; } = new();
        public StatisticsInfo Statistics { get; set; } = new();
        public List<ClassStats> ClassStatsList { get; set; } = new();
        public List<AttendanceInfo> RecentList { get; set; } = new();

        public string ChartLabelsJson { get; set; } = "[]";
        public string ChartPresentJson { get; set; } = "[]";
        public string ChartAbsentJson { get; set; } = "[]";
        public string ChartMode { get; set; } = "days";

        public async Task<IActionResult> OnGetAsync()
        {
            var role = HttpContext.Session.GetString("Role");
            var accountId = HttpContext.Session.GetInt32("AccountId");
            if (accountId == null || (role != "Student" && role != "Parent"))
                return RedirectToPage("/Account/Login");
            ViewerRole = role!;
            var aid = accountId.Value;

            List<int> studentIds = new();
            if (role == "Student")
            {
                var s = await _context.Students.FindAsync(aid);
                ViewerName = s?.Fullname ?? string.Empty;
                studentIds.Add(aid);
            }
            else
            {
                var p = await _context.Parents.Include(x => x.Students)
                    .FirstOrDefaultAsync(x => x.AccountId == aid);
                ViewerName = p?.Fullname ?? string.Empty;
                MyChildren = p?.Students.Select(s => new ChildInfo
                { StudentId = s.AccountId, Fullname = s.Fullname, School = s.CurrentSchool })
                    .ToList() ?? new();
                studentIds = StudentIdFilter.HasValue && StudentIdFilter > 0
                    ? new List<int> { StudentIdFilter.Value }
                    : MyChildren.Select(c => c.StudentId).ToList();
            }
            if (!studentIds.Any()) return Page();

            // Mutual exclusion: date range xóa năm/tháng
            HasDateRange = !string.IsNullOrEmpty(FromDate) || !string.IsNullOrEmpty(ToDate);
            if (HasDateRange) { FilterYear = null; FilterMonth = null; }

            ActiveYear = !string.IsNullOrEmpty(FilterYear) && int.TryParse(FilterYear, out int fy) ? fy : DateTime.Today.Year;
            ActiveMonth = !string.IsNullOrEmpty(FilterMonth) && int.TryParse(FilterMonth, out int fm) ? fm : 0;
            if (HasDateRange) { ActiveYear = 0; ActiveMonth = 0; }

            if (HasDateRange)
            {
                var parts = new List<string>();
                if (!string.IsNullOrEmpty(FromDate)) parts.Add("Từ " + FromDate);
                if (!string.IsNullOrEmpty(ToDate)) parts.Add("đến " + ToDate);
                PeriodLabel = string.Join(" ", parts);
            }
            else
                PeriodLabel = ActiveMonth == 0 ? $"Cả năm {ActiveYear}" : $"Tháng {ActiveMonth}/{ActiveYear}";

            Classes = await _context.ClassStudents
                .Where(cs => studentIds.Contains(cs.StudentId) && cs.Status == StudentClassStatus.Active)
                .Include(cs => cs.Class)
                .Select(cs => new ClassInfo { ClassId = cs.ClassId, ClassCode = cs.Class.ClassCode })
                .Distinct().OrderBy(c => c.ClassCode).ToListAsync();

            var allRaw = await _context.Attendances
                .Where(a => studentIds.Contains(a.StudentId))
                .Include(a => a.Student)
                .Include(a => a.Session).ThenInclude(s => s.Class)
                .Include(a => a.Session).ThenInclude(s => s.Shift)
                .Include(a => a.Session).ThenInclude(s => s.Room)
                .Include(a => a.Session).ThenInclude(s => s.Teacher)
                .Include(a => a.Session).ThenInclude(s => s.TeacherAssistant)
                .ToListAsync();

            // Một pipeline filter duy nhất
            var filtered = allRaw.AsEnumerable();
            if (!HasDateRange)
            {
                filtered = filtered.Where(a => a.Session.SessionDate.Year == ActiveYear);
                if (ActiveMonth > 0) filtered = filtered.Where(a => a.Session.SessionDate.Month == ActiveMonth);
            }
            else
            {
                if (!string.IsNullOrEmpty(FromDate) && DateOnly.TryParse(FromDate, out var fdDate))
                    filtered = filtered.Where(a => a.Session.SessionDate >= fdDate);
                if (!string.IsNullOrEmpty(ToDate) && DateOnly.TryParse(ToDate, out var tdDate))
                    filtered = filtered.Where(a => a.Session.SessionDate <= tdDate);
            }
            if (!string.IsNullOrEmpty(ClassFilter) && int.TryParse(ClassFilter, out int cid))
                filtered = filtered.Where(a => a.Session.ClassId == cid);
            if (StatusFilter == "present") filtered = filtered.Where(a => a.Status == AttendanceStatus.Present);
            else if (StatusFilter == "absent") filtered = filtered.Where(a => a.Status == AttendanceStatus.Absent);

            var fl = filtered.ToList();

            // Tất cả thống kê theo bộ lọc
            Statistics.TotalSessions = fl.Count;
            Statistics.PresentCount = fl.Count(a => a.Status == AttendanceStatus.Present);
            Statistics.AbsentCount = fl.Count(a => a.Status == AttendanceStatus.Absent);
            Statistics.AttendanceRate = fl.Count > 0
                ? Math.Round((double)Statistics.PresentCount / fl.Count * 100, 1) : 0;

            AttendanceList = fl.OrderByDescending(a => a.Session.SessionDate)
                .ThenByDescending(a => a.Session.Shift.StartTime)
                .Take(200).Select(a => MapToInfo(a, IsParent)).ToList();

            RecentList = fl.OrderByDescending(a => a.Session.SessionDate)
                .ThenByDescending(a => a.Session.Shift.StartTime)
                .Take(10).Select(a => MapToInfo(a, IsParent)).ToList();

            ClassStatsList = fl.GroupBy(a => new { a.Session.ClassId, a.Session.Class.ClassCode })
                .Select(g => {
                    var p2 = g.Count(a => a.Status == AttendanceStatus.Present);
                    var t2 = g.Count();
                    return new ClassStats
                    {
                        ClassCode = g.Key.ClassCode,
                        TotalSessions = t2,
                        PresentCount = p2,
                        AbsentCount = t2 - p2,
                        Rate = t2 > 0 ? Math.Round((double)p2 / t2 * 100, 1) : 0
                    };
                }).OrderByDescending(c => c.TotalSessions).ToList();

            // Chart: cả năm → theo tháng, còn lại → theo ngày
            if (!HasDateRange && ActiveMonth == 0)
            {
                ChartMode = "months";
                var pByM = fl.Where(a => a.Status == AttendanceStatus.Present)
                    .GroupBy(a => a.Session.SessionDate.Month).ToDictionary(g => g.Key, g => g.Count());
                var aByM = fl.Where(a => a.Status == AttendanceStatus.Absent)
                    .GroupBy(a => a.Session.SessionDate.Month).ToDictionary(g => g.Key, g => g.Count());
                ChartLabelsJson = JsonSerializer.Serialize(Enumerable.Range(1, 12).Select(m => "Tháng " + m));
                ChartPresentJson = JsonSerializer.Serialize(Enumerable.Range(1, 12).Select(m => pByM.GetValueOrDefault(m, 0)));
                ChartAbsentJson = JsonSerializer.Serialize(Enumerable.Range(1, 12).Select(m => aByM.GetValueOrDefault(m, 0)));
            }
            else
            {
                ChartMode = "days";
                List<DateOnly> dayList;
                if (fl.Any())
                {
                    var minD = fl.Min(a => a.Session.SessionDate);
                    var maxD = fl.Max(a => a.Session.SessionDate);
                    var span = (maxD.ToDateTime(TimeOnly.MinValue) - minD.ToDateTime(TimeOnly.MinValue)).Days + 1;
                    dayList = span <= 62
                        ? Enumerable.Range(0, span).Select(i => minD.AddDays(i)).ToList()
                        : Enumerable.Range(0, 30).Select(i => DateOnly.FromDateTime(DateTime.Today.AddDays(-29 + i))).ToList();
                }
                else
                    dayList = Enumerable.Range(0, 30).Select(i => DateOnly.FromDateTime(DateTime.Today.AddDays(-29 + i))).ToList();

                var pByD = fl.Where(a => a.Status == AttendanceStatus.Present)
                    .GroupBy(a => a.Session.SessionDate).ToDictionary(g => g.Key, g => g.Count());
                var aByD = fl.Where(a => a.Status == AttendanceStatus.Absent)
                    .GroupBy(a => a.Session.SessionDate).ToDictionary(g => g.Key, g => g.Count());
                ChartLabelsJson = JsonSerializer.Serialize(dayList.Select(d => d.ToString("dd/MM")));
                ChartPresentJson = JsonSerializer.Serialize(dayList.Select(d => pByD.GetValueOrDefault(d, 0)));
                ChartAbsentJson = JsonSerializer.Serialize(dayList.Select(d => aByD.GetValueOrDefault(d, 0)));
            }

            if (Export)
            {
                byte[] bytes;
                string filename;
                if (!IsParent)
                {
                    bytes = BuildExcelSingle(fl, ViewerName, "");
                    filename = $"BC_DiemDanh_{SanitizeStr(ViewerName)}_{SanitizeStr(PeriodLabel)}.xlsx";
                }
                else if (StudentIdFilter.HasValue && StudentIdFilter > 0)
                {
                    var child = MyChildren.FirstOrDefault(c => c.StudentId == StudentIdFilter.Value);
                    var childName = child?.Fullname ?? fl.FirstOrDefault()?.Student?.Fullname ?? "HocSinh";
                    var childSchool = child?.School ?? "";
                    bytes = BuildExcelSingle(fl, childName, childSchool);
                    filename = $"BC_DiemDanh_{SanitizeStr(childName)}_{SanitizeStr(PeriodLabel)}.xlsx";
                }
                else
                {
                    bytes = BuildExcelMulti(fl);
                    filename = $"BC_DiemDanh_TatCaConEm_{SanitizeStr(PeriodLabel)}.xlsx";
                }
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", filename);
            }

            return Page();
        }

        private static AttendanceInfo MapToInfo(TutoringCenterManagement.Data.Entities.Attendance a, bool isParent) => new()
        {
            SessionDate = a.Session.SessionDate,
            ClassName = a.Session.Class.ClassCode,
            ShiftName = a.Session.Shift.ShiftName,
            ShiftTime = $"{a.Session.Shift.StartTime:HH:mm}–{a.Session.Shift.EndTime:HH:mm}",
            RoomCode = a.Session.Room?.RoomCode ?? "—",
            TeacherName = a.Session.Teacher.Fullname,
            AssistantName = a.Session.TeacherAssistant?.Fullname,
            IsPresent = a.Status == AttendanceStatus.Present,
            StudentName = isParent ? a.Student?.Fullname : null
        };

        public class ChildInfo { public int StudentId { get; set; } public string Fullname { get; set; } = string.Empty; public string? School { get; set; } }
        public class ClassInfo { public int ClassId { get; set; } public string ClassCode { get; set; } = string.Empty; }
        public class AttendanceInfo
        {
            public DateOnly SessionDate { get; set; }
            public string ClassName { get; set; } = string.Empty;
            public string ShiftName { get; set; } = string.Empty; public string ShiftTime { get; set; } = string.Empty;
            public string RoomCode { get; set; } = string.Empty; public string TeacherName { get; set; } = string.Empty;
            public string? AssistantName { get; set; }
            public bool IsPresent { get; set; }
            public string? StudentName { get; set; }
        }
        public class StatisticsInfo { public int TotalSessions { get; set; } public int PresentCount { get; set; } public int AbsentCount { get; set; } public double AttendanceRate { get; set; } }
        public class ClassStats { public string ClassCode { get; set; } = string.Empty; public int TotalSessions { get; set; } public int PresentCount { get; set; } public int AbsentCount { get; set; } public double Rate { get; set; } }

        // ── Export helpers ────────────────────────────────────────────────────
        private static string SanitizeStr(string s) =>
            new string(s.Select(c => char.IsLetterOrDigit(c) || c == '_' ? c : '_').ToArray());

        private static string SanitizeSheetName(string s)
        {
            var clean = new string(s.Select(c => "\\/:*?[]".Contains(c) ? '_' : c).ToArray());
            return clean.Length > 31 ? clean[..31] : clean;
        }

        private static readonly string[] DayNamesArr = { "", "CN", "Hai", "Ba", "Tư", "Năm", "Sáu", "Bảy" };

        private static string GetSubjectLabel(Subject sub) => sub switch
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

        private byte[] BuildExcelSingle(List<Data.Entities.Attendance> records, string studentName, string school)
        {
            using var wb = new XLWorkbook();
            WriteStudentSheet(wb, records, studentName, school);
            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private byte[] BuildExcelMulti(List<Data.Entities.Attendance> allRecords)
        {
            using var wb = new XLWorkbook();

            // ── Sheet tổng hợp ────────────────────────────────────────────────
            var sumWs = wb.AddWorksheet("Tổng hợp");
            const int sCols = 7;
            XlMergeTitle(sumWs, 1, sCols, "TỔNG HỢP ĐIỂM DANH CON EM");
            XlMergeSubtitle(sumWs, 2, sCols, $"{ViewerName} · {PeriodLabel}");
            XlMergeInfo(sumWs, 3, sCols, $"Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}");

            XlWriteHeaders(sumWs, 5, new[] { "STT", "Họ tên", "Trường", "Tổng buổi", "Có mặt", "Vắng", "Tỉ lệ" });
            int sRow = 6; int stt = 1;
            foreach (var child in MyChildren)
            {
                var cr = allRecords.Where(a => a.StudentId == child.StudentId).ToList();
                int p = cr.Count(a => a.Status == AttendanceStatus.Present);
                int tot = cr.Count;
                double rate = tot > 0 ? Math.Round((double)p / tot * 100, 1) : 0;
                bool alt = sRow % 2 == 0;
                sumWs.Cell(sRow, 1).Value = stt++;
                sumWs.Cell(sRow, 2).Value = child.Fullname;
                sumWs.Cell(sRow, 3).Value = child.School ?? "";
                sumWs.Cell(sRow, 4).Value = tot;
                sumWs.Cell(sRow, 5).Value = p;
                sumWs.Cell(sRow, 6).Value = tot - p;
                sumWs.Cell(sRow, 7).Value = $"{rate}%";
                XlStyleDataRow(sumWs, sRow, sCols, alt);
                sRow++;
            }
            sumWs.Cell(sRow, 1).Value = "TỔNG CỘNG";
            sumWs.Range(sRow, 1, sRow, 6).Merge();
            var totAll = allRecords.Count;
            var pAll = allRecords.Count(a => a.Status == AttendanceStatus.Present);
            var rAll = totAll > 0 ? Math.Round((double)pAll / totAll * 100, 1) : 0;
            sumWs.Cell(sRow, 7).Value = $"{rAll}%";
            XlStyleSummary(sumWs, sRow, sCols);
            sumWs.Columns().AdjustToContents();
            if (sumWs.Column(2).Width < 24) sumWs.Column(2).Width = 24;

            // ── Sheet từng con ────────────────────────────────────────────────
            foreach (var child in MyChildren)
            {
                var cr = allRecords.Where(a => a.StudentId == child.StudentId).ToList();
                if (!cr.Any()) continue;
                WriteStudentSheet(wb, cr, child.Fullname, child.School ?? "");
            }

            using var ms = new MemoryStream();
            wb.SaveAs(ms);
            return ms.ToArray();
        }

        private void WriteStudentSheet(IXLWorkbook wb, List<Data.Entities.Attendance> records, string studentName, string school)
        {
            var list = records
                .OrderBy(a => a.Session.SessionDate)
                .ThenBy(a => a.Session.Shift.StartTime)
                .ToList();

            var ws = wb.AddWorksheet(SanitizeSheetName(studentName));
            const int cols = 9;

            int present = list.Count(a => a.Status == AttendanceStatus.Present);
            int absent  = list.Count(a => a.Status == AttendanceStatus.Absent);
            double rate = list.Count > 0 ? Math.Round((double)present / list.Count * 100, 1) : 0;

            string title = $"HỌC BẠ ĐIỂM DANH — {studentName.ToUpper()}";
            if (!string.IsNullOrEmpty(school)) title += $"  ({school})";

            XlMergeTitle(ws, 1, cols, title);
            XlMergeSubtitle(ws, 2, cols, PeriodLabel);
            XlMergeInfo(ws, 3, cols,
                $"Xuất ngày: {DateTime.Now:dd/MM/yyyy HH:mm}   |   " +
                $"Có mặt: {present}   Vắng: {absent}   Tỉ lệ chuyên cần: {rate}%");

            XlWriteHeaders(ws, 5, new[] { "STT", "Ngày học", "Thứ", "Ca học", "Phòng", "Lớp", "Môn học", "Giáo viên", "Điểm danh" });

            int row = 6; int stt = 1;
            foreach (var a in list)
            {
                bool alt = row % 2 == 0;
                var sess = a.Session;
                ws.Cell(row, 1).Value = stt++;
                ws.Cell(row, 2).Value = sess.SessionDate.ToString("dd/MM/yyyy");
                ws.Cell(row, 3).Value = DayNamesArr[(int)sess.SessionDate.DayOfWeek];
                ws.Cell(row, 4).Value = sess.Shift != null
                    ? $"{sess.Shift.ShiftName} ({sess.Shift.StartTime:HH\\:mm}–{sess.Shift.EndTime:HH\\:mm})" : "—";
                ws.Cell(row, 5).Value = sess.Room?.RoomCode ?? "—";
                ws.Cell(row, 6).Value = sess.Class?.ClassCode ?? "—";
                ws.Cell(row, 7).Value = sess.Class != null ? GetSubjectLabel(sess.Class.Subject) : "—";
                ws.Cell(row, 8).Value = sess.Teacher?.Fullname ?? "—";
                ws.Cell(row, 9).Value = a.Status == AttendanceStatus.Present ? "Có mặt" : "Vắng mặt";
                XlStyleDataRow(ws, row, cols, alt);

                ws.Cell(row, 9).Style.Font.Bold = true;
                ws.Cell(row, 9).Style.Font.FontColor = a.Status == AttendanceStatus.Present
                    ? XLColor.FromHtml("#059669") : XLColor.FromHtml("#e11d48");
                row++;
            }

            ws.Cell(row, 1).Value = "TỔNG CỘNG";
            ws.Range(row, 1, row, 8).Merge();
            ws.Cell(row, 9).Value = $"Có mặt: {present} / {list.Count}  ({rate}%)";
            XlStyleSummary(ws, row, cols);

            ws.Columns().AdjustToContents();
            if (ws.Column(8).Width < 20) ws.Column(8).Width = 20;
        }

        private static void XlMergeTitle(IXLWorksheet ws, int row, int cols, string text)
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

        private static void XlMergeSubtitle(IXLWorksheet ws, int row, int cols, string text)
        {
            var cell = ws.Cell(row, 1); cell.Value = text;
            ws.Range(row, 1, row, cols).Merge();
            cell.Style.Font.Bold = true; cell.Style.Font.FontSize = 11;
            cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#fff3ec");
            cell.Style.Font.FontColor = XLColor.FromHtml("#c04a00");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(row).Height = 18;
        }

        private static void XlMergeInfo(IXLWorksheet ws, int row, int cols, string text)
        {
            var cell = ws.Cell(row, 1); cell.Value = text;
            ws.Range(row, 1, row, cols).Merge();
            cell.Style.Font.Italic = true; cell.Style.Font.FontSize = 9;
            cell.Style.Font.FontColor = XLColor.FromHtml("#6b7280");
            cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Row(row).Height = 14;
        }

        private static void XlWriteHeaders(IXLWorksheet ws, int row, string[] headers)
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

        private static void XlStyleDataRow(IXLWorksheet ws, int row, int cols, bool alt)
        {
            var rng = ws.Range(row, 1, row, cols);
            rng.Style.Fill.BackgroundColor = alt ? XLColor.FromHtml("#f0f4ff") : XLColor.White;
            rng.Style.Border.OutsideBorder      = XLBorderStyleValues.Thin;
            rng.Style.Border.OutsideBorderColor = XLColor.FromHtml("#e5e7eb");
            rng.Style.Border.InsideBorder       = XLBorderStyleValues.Thin;
            rng.Style.Border.InsideBorderColor  = XLColor.FromHtml("#e5e7eb");
            for (int c = 3; c <= cols; c++)
                ws.Cell(row, c).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(row).Height = 16;
        }

        private static void XlStyleSummary(IXLWorksheet ws, int row, int cols)
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
    }
}