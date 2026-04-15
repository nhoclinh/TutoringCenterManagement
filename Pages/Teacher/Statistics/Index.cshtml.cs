using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Teacher.Statistics
{
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public IndexModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ──────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public string? FilterYear { get; set; }
        [BindProperty(SupportsGet = true)] public string? FilterMonth { get; set; }
        [BindProperty(SupportsGet = true)] public string? FromDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? ToDate { get; set; }
        [BindProperty(SupportsGet = true)] public string? ClassFilter { get; set; }
        [BindProperty(SupportsGet = true)] public string? SessionStatusFilter { get; set; }
        // "completed" | "scheduled" | "cancelled" | ""

        // ── Resolved filter state ────────────────────────────────────────
        public bool HasDateRange { get; set; }
        public int ActiveYear { get; set; }
        public int ActiveMonth { get; set; }
        public string PeriodLabel { get; set; } = string.Empty;

        // ── KPI toàn thời gian (không bị filter) ────────────────────────
        public int TotalSessionsAll { get; set; }
        public int TotalClassesAll { get; set; }
        public int ThisMonthSessions { get; set; }
        public double AvgMonthSessions { get; set; }

        // ── KPI theo filter ──────────────────────────────────────────────
        public int TotalSessions { get; set; }
        public int TotalClasses { get; set; }
        public int CompletedSessions { get; set; }
        public int CancelledSessions { get; set; }
        public int ScheduledSessions { get; set; }
        public double AttendanceRate { get; set; }

        // ── Computed: Tỷ lệ hủy buổi (%) ───────────────────────────────
        public double CancellationRate =>
            TotalSessions > 0 ? Math.Round((double)CancelledSessions / TotalSessions * 100, 1) : 0;

        // ── Computed: Tỷ lệ hoàn thành (%) ─────────────────────────────
        public double CompletionRate =>
            TotalSessions > 0 ? Math.Round((double)CompletedSessions / TotalSessions * 100, 1) : 0;

        // ── Attendance theo filter ───────────────────────────────────────
        public int PresentCount { get; set; }
        public int AbsentCount { get; set; }
        public int TotalAttendanceRecords { get; set; }

        // ── Mini insights ────────────────────────────────────────────────
        public int TotalStudentsTaught { get; set; }
        public int AttendedSessionsCount { get; set; }
        public int PendingAttendanceCount { get; set; }

        // ── Dropdown ────────────────────────────────────────────────────
        public List<ClassOption> AvailableClasses { get; set; } = new();

        // ── Chart JSON ───────────────────────────────────────────────────
        public string ChartMode { get; set; } = "monthly";
        public string ChartLabelsJson { get; set; } = "[]";
        public string ChartSessionJson { get; set; } = "[]";
        public string ChartPresentJson { get; set; } = "[]";
        public string Last12Months { get; set; } = "[]";
        public string Monthly12mData { get; set; } = "[]";
        public string ClassNames { get; set; } = "[]";
        public string ClassSessionCounts { get; set; } = "[]";
        public string ClassAttendedCounts { get; set; } = "[]";
        public string ClassAttRates { get; set; } = "[]";

        // ── Chart JSON — Stacked bar (Hoàn thành / Lịch / Hủy) ─────────
        public string ChartCompletedJson { get; set; } = "[]";
        public string ChartScheduledJson { get; set; } = "[]";
        public string ChartCancelledJson { get; set; } = "[]";
        // ── Chart JSON — Attendance rate line (%) ───────────────────────
        public string ChartAttRateJson { get; set; } = "[]";
        // ── Chart JSON — 12m stacked + att rate ─────────────────────────
        public string Chart12mCompletedJson { get; set; } = "[]";
        public string Chart12mScheduledJson { get; set; } = "[]";
        public string Chart12mCancelledJson { get; set; } = "[]";
        public string Chart12mAttRateJson { get; set; } = "[]";

        // ── Tables ───────────────────────────────────────────────────────
        public List<ClassDetailInfo> ClassDetails { get; set; } = new();
        public List<RecentSessionInfo> RecentSessions { get; set; } = new();

        // ═══════════════════════════════════════════════════════════════
        public async Task<IActionResult> OnGetAsync()
        {
            var role = HttpContext.Session.GetString("Role");
            var teacherId = HttpContext.Session.GetInt32("AccountId");
            if (role != "Teacher" || !teacherId.HasValue)
                return RedirectToPage("/Account/Login");

            // ── Resolve filter ───────────────────────────────────────────
            HasDateRange = !string.IsNullOrEmpty(FromDate) || !string.IsNullOrEmpty(ToDate);
            if (HasDateRange) { FilterYear = null; FilterMonth = null; }
            else { FromDate = null; ToDate = null; }

            ActiveYear = int.TryParse(FilterYear, out int fy) ? fy : DateTime.Today.Year;
            ActiveMonth = int.TryParse(FilterMonth, out int fm) ? fm : 0;
            if (HasDateRange) { ActiveYear = 0; ActiveMonth = 0; }

            var mn = new[]{"","Tháng 1","Tháng 2","Tháng 3","Tháng 4","Tháng 5","Tháng 6",
                           "Tháng 7","Tháng 8","Tháng 9","Tháng 10","Tháng 11","Tháng 12"};
            if (HasDateRange)
            {
                var p = new List<string>();
                if (!string.IsNullOrEmpty(FromDate)) p.Add("Từ " + FromDate);
                if (!string.IsNullOrEmpty(ToDate)) p.Add("đến " + ToDate);
                PeriodLabel = p.Any() ? string.Join(" ", p) : "Khoảng ngày";
            }
            else
                PeriodLabel = ActiveMonth == 0 ? $"Cả năm {ActiveYear}" : $"{mn[ActiveMonth]}/{ActiveYear}";

            var today = DateOnly.FromDateTime(DateTime.Today);
            var thisMonth = DateTime.Today.Month;
            var thisYear = DateTime.Today.Year;

            // ── Load toàn bộ sessions ────────────────────────────────────
            var allSessions = await _context.Sessions
                .Where(s => s.TeacherId == teacherId.Value
                         || s.TeacherAssistantId == teacherId.Value)
                .Include(s => s.Class)
                .Include(s => s.Shift)
                .Include(s => s.Room)
                .ToListAsync();

            TotalSessionsAll = allSessions.Count;
            TotalClassesAll = allSessions.Select(s => s.ClassId).Distinct().Count();
            ThisMonthSessions = allSessions
                .Count(s => s.SessionDate.Month == thisMonth && s.SessionDate.Year == thisYear);

            // Avg 6 tháng gần nhất
            AvgMonthSessions = Enumerable.Range(0, 6)
                .Select(i => DateTime.Today.AddMonths(-5 + i))
                .Select(m => (double)allSessions.Count(s =>
                    s.SessionDate.Month == m.Month && s.SessionDate.Year == m.Year))
                .Average();

            // ── Dropdown lớp ─────────────────────────────────────────────
            AvailableClasses = allSessions
                .GroupBy(s => new { s.ClassId, s.Class!.ClassCode })
                .Select(g => new ClassOption { ClassId = g.Key.ClassId, ClassCode = g.Key.ClassCode })
                .OrderBy(c => c.ClassCode).ToList();

            // ── Apply bộ lọc ─────────────────────────────────────────────
            var filtered = allSessions.AsEnumerable();

            if (HasDateRange)
            {
                if (!string.IsNullOrEmpty(FromDate) && DateOnly.TryParse(FromDate, out var fd))
                    filtered = filtered.Where(s => s.SessionDate >= fd);
                if (!string.IsNullOrEmpty(ToDate) && DateOnly.TryParse(ToDate, out var td))
                    filtered = filtered.Where(s => s.SessionDate <= td);
            }
            else
            {
                filtered = filtered.Where(s => s.SessionDate.Year == ActiveYear);
                if (ActiveMonth > 0)
                    filtered = filtered.Where(s => s.SessionDate.Month == ActiveMonth);
            }

            if (!string.IsNullOrEmpty(ClassFilter) && int.TryParse(ClassFilter, out int cid))
                filtered = filtered.Where(s => s.ClassId == cid);

            if (!string.IsNullOrEmpty(SessionStatusFilter))
            {
                filtered = SessionStatusFilter switch
                {
                    "completed" => filtered.Where(s => s.Status == SessionStatus.Completed),
                    "cancelled" => filtered.Where(s => s.Status == SessionStatus.Cancelled),
                    "scheduled" => filtered.Where(s => s.Status == SessionStatus.Scheduled
                                                    || s.Status == SessionStatus.Ongoing),
                    _ => filtered
                };
            }

            var fl = filtered.ToList();
            var flIds = fl.Select(s => s.SessionId).ToList();

            // ── KPI ──────────────────────────────────────────────────────
            TotalSessions = fl.Count;
            TotalClasses = fl.Select(s => s.ClassId).Distinct().Count();
            CompletedSessions = fl.Count(s => s.Status == SessionStatus.Completed);
            CancelledSessions = fl.Count(s => s.Status == SessionStatus.Cancelled);
            ScheduledSessions = fl.Count(s => s.Status == SessionStatus.Scheduled
                                           || s.Status == SessionStatus.Ongoing);

            // ── Attendance ───────────────────────────────────────────────
            var allAtts = await _context.Attendances
                .Where(a => flIds.Contains(a.SessionId))
                .ToListAsync();

            PresentCount = allAtts.Count(a => a.Status == AttendanceStatus.Present);
            AbsentCount = allAtts.Count(a => a.Status == AttendanceStatus.Absent);
            TotalAttendanceRecords = PresentCount + AbsentCount;
            AttendanceRate = TotalAttendanceRecords > 0
                ? Math.Round((double)PresentCount / TotalAttendanceRecords * 100, 1) : 0;

            AttendedSessionsCount = fl.Count(s => allAtts.Any(a => a.SessionId == s.SessionId));
            PendingAttendanceCount = fl.Count(s => s.SessionDate <= today
                && s.Status != SessionStatus.Cancelled
                && !allAtts.Any(a => a.SessionId == s.SessionId));
            TotalStudentsTaught = allAtts.Select(a => a.StudentId).Distinct().Count();

            // ── Class details ─────────────────────────────────────────────
            var classIds = fl.Select(s => s.ClassId).Distinct().ToList();
            var classInfoList = await _context.Classes
                .Where(c => classIds.Contains(c.ClassId))
                .Select(c => new {
                    c.ClassId,
                    c.ClassCode,
                    c.Subject,
                    StudentCount = c.ClassStudents.Count(cs => cs.Status == StudentClassStatus.Active)
                })
                .ToListAsync();

            ClassDetails = new();
            foreach (var ci in classInfoList)
            {
                var csIds = fl.Where(s => s.ClassId == ci.ClassId).Select(s => s.SessionId).ToList();
                var classAts = allAtts.Where(a => csIds.Contains(a.SessionId)).ToList();
                var p = classAts.Count(a => a.Status == AttendanceStatus.Present);
                var tot = classAts.Count;
                var attended = csIds.Count(sid => allAtts.Any(a => a.SessionId == sid));
                ClassDetails.Add(new ClassDetailInfo
                {
                    ClassCode = ci.ClassCode,
                    SubjectName = GetSubjectName(ci.Subject),
                    StudentCount = ci.StudentCount,
                    TotalSessions = csIds.Count,
                    CompletedCount = fl.Count(s => s.ClassId == ci.ClassId && s.Status == SessionStatus.Completed),
                    CancelledCount = fl.Count(s => s.ClassId == ci.ClassId && s.Status == SessionStatus.Cancelled),
                    AttendedSessions = attended,
                    AttendanceRate = tot > 0 ? Math.Round((double)p / tot * 100, 1) : 0
                });
            }
            ClassDetails = ClassDetails.OrderByDescending(c => c.TotalSessions).ToList();

            ClassNames = JsonSerializer.Serialize(ClassDetails.Select(c => c.ClassCode));
            ClassSessionCounts = JsonSerializer.Serialize(ClassDetails.Select(c => c.TotalSessions));
            ClassAttendedCounts = JsonSerializer.Serialize(ClassDetails.Select(c => c.AttendedSessions));
            ClassAttRates = JsonSerializer.Serialize(ClassDetails.Select(c => c.AttendanceRate));

            // ── Chart ────────────────────────────────────────────────────
            if (!HasDateRange && ActiveMonth == 0)
            {
                ChartMode = "monthly";
                var months = Enumerable.Range(1, 12).ToList();
                var sByM = fl.GroupBy(s => s.SessionDate.Month).ToDictionary(g => g.Key, g => g.Count());
                var pByM = allAtts.Where(a => a.Status == AttendanceStatus.Present)
                    .Join(fl, a => a.SessionId, s => s.SessionId, (a, s) => s.SessionDate.Month)
                    .GroupBy(m => m).ToDictionary(g => g.Key, g => g.Count());
                ChartLabelsJson = JsonSerializer.Serialize(months.Select(m => $"T{m}"));
                ChartSessionJson = JsonSerializer.Serialize(months.Select(m => sByM.GetValueOrDefault(m, 0)));
                ChartPresentJson = JsonSerializer.Serialize(months.Select(m => pByM.GetValueOrDefault(m, 0)));

                // Stacked bar: completed / scheduled / cancelled theo tháng
                var compByM = fl.Where(s => s.Status == SessionStatus.Completed)
                    .GroupBy(s => s.SessionDate.Month).ToDictionary(g => g.Key, g => g.Count());
                var schedByM = fl.Where(s => s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.Ongoing)
                    .GroupBy(s => s.SessionDate.Month).ToDictionary(g => g.Key, g => g.Count());
                var cancByM = fl.Where(s => s.Status == SessionStatus.Cancelled)
                    .GroupBy(s => s.SessionDate.Month).ToDictionary(g => g.Key, g => g.Count());
                ChartCompletedJson = JsonSerializer.Serialize(months.Select(m => compByM.GetValueOrDefault(m, 0)));
                ChartScheduledJson = JsonSerializer.Serialize(months.Select(m => schedByM.GetValueOrDefault(m, 0)));
                ChartCancelledJson = JsonSerializer.Serialize(months.Select(m => cancByM.GetValueOrDefault(m, 0)));

                // Attendance rate % theo tháng
                var attByM = allAtts
                    .Join(fl, a => a.SessionId, s => s.SessionId, (a, s) => new { a.Status, Month = s.SessionDate.Month })
                    .GroupBy(x => x.Month)
                    .ToDictionary(g => g.Key, g => {
                        var present = g.Count(x => x.Status == AttendanceStatus.Present);
                        var total = g.Count();
                        return total > 0 ? Math.Round((double)present / total * 100, 1) : (double?)null;
                    });
                ChartAttRateJson = JsonSerializer.Serialize(months.Select(m =>
                    attByM.TryGetValue(m, out var v) ? v : null));
            }
            else
            {
                ChartMode = "daily";
                List<DateOnly> days;
                if (fl.Any())
                {
                    var minD = fl.Min(s => s.SessionDate);
                    var maxD = fl.Max(s => s.SessionDate);
                    var span = (maxD.ToDateTime(TimeOnly.MinValue) - minD.ToDateTime(TimeOnly.MinValue)).Days + 1;
                    days = span <= 62
                        ? Enumerable.Range(0, span).Select(i => minD.AddDays(i)).ToList()
                        : Enumerable.Range(0, 30).Select(i => DateOnly.FromDateTime(DateTime.Today.AddDays(-29 + i))).ToList();
                }
                else
                    days = Enumerable.Range(0, 30).Select(i => DateOnly.FromDateTime(DateTime.Today.AddDays(-29 + i))).ToList();

                var sByD = fl.GroupBy(s => s.SessionDate).ToDictionary(g => g.Key, g => g.Count());
                var pByD = allAtts.Where(a => a.Status == AttendanceStatus.Present)
                    .Join(fl, a => a.SessionId, s => s.SessionId, (a, s) => s.SessionDate)
                    .GroupBy(d => d).ToDictionary(g => g.Key, g => g.Count());
                ChartLabelsJson = JsonSerializer.Serialize(days.Select(d => d.ToString("dd/MM")));
                ChartSessionJson = JsonSerializer.Serialize(days.Select(d => sByD.GetValueOrDefault(d, 0)));
                ChartPresentJson = JsonSerializer.Serialize(days.Select(d => pByD.GetValueOrDefault(d, 0)));

                // Stacked bar theo ngày
                var compByD = fl.Where(s => s.Status == SessionStatus.Completed)
                    .GroupBy(s => s.SessionDate).ToDictionary(g => g.Key, g => g.Count());
                var schedByD = fl.Where(s => s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.Ongoing)
                    .GroupBy(s => s.SessionDate).ToDictionary(g => g.Key, g => g.Count());
                var cancByD = fl.Where(s => s.Status == SessionStatus.Cancelled)
                    .GroupBy(s => s.SessionDate).ToDictionary(g => g.Key, g => g.Count());
                ChartCompletedJson = JsonSerializer.Serialize(days.Select(d => compByD.GetValueOrDefault(d, 0)));
                ChartScheduledJson = JsonSerializer.Serialize(days.Select(d => schedByD.GetValueOrDefault(d, 0)));
                ChartCancelledJson = JsonSerializer.Serialize(days.Select(d => cancByD.GetValueOrDefault(d, 0)));

                // Attendance rate % theo ngày
                var attByD = allAtts
                    .Join(fl, a => a.SessionId, s => s.SessionId, (a, s) => new { a.Status, Day = s.SessionDate })
                    .GroupBy(x => x.Day)
                    .ToDictionary(g => g.Key, g => {
                        var present = g.Count(x => x.Status == AttendanceStatus.Present);
                        var total = g.Count();
                        return total > 0 ? Math.Round((double)present / total * 100, 1) : (double?)null;
                    });
                ChartAttRateJson = JsonSerializer.Serialize(days.Select(d =>
                    attByD.TryGetValue(d, out var v) ? v : null));
            }

            // 12 tháng overview (toàn bộ)
            var last12 = Enumerable.Range(0, 12).Select(i => DateTime.Today.AddMonths(-11 + i)).ToList();
            Last12Months = JsonSerializer.Serialize(last12.Select(m => $"T{m.Month}/{m.Year}"));
            Monthly12mData = JsonSerializer.Serialize(last12.Select(m =>
                allSessions.Count(s => s.SessionDate.Month == m.Month && s.SessionDate.Year == m.Year)));

            // 12m stacked bar
            Chart12mCompletedJson = JsonSerializer.Serialize(last12.Select(m =>
                allSessions.Count(s => s.SessionDate.Month == m.Month && s.SessionDate.Year == m.Year
                                    && s.Status == SessionStatus.Completed)));
            Chart12mScheduledJson = JsonSerializer.Serialize(last12.Select(m =>
                allSessions.Count(s => s.SessionDate.Month == m.Month && s.SessionDate.Year == m.Year
                                    && (s.Status == SessionStatus.Scheduled || s.Status == SessionStatus.Ongoing))));
            Chart12mCancelledJson = JsonSerializer.Serialize(last12.Select(m =>
                allSessions.Count(s => s.SessionDate.Month == m.Month && s.SessionDate.Year == m.Year
                                    && s.Status == SessionStatus.Cancelled)));

            // 12m attendance rate: cần load thêm attendance của allSessions (đã có allAtts chỉ theo filter)
            // Dùng allSessions để tính attendance rate 12 tháng toàn bộ
            var all12mSessionIds = allSessions
                .Where(s => last12.Any(m => s.SessionDate.Month == m.Month && s.SessionDate.Year == m.Year))
                .Select(s => s.SessionId).ToList();
            var all12mAtts = await _context.Attendances
                .Where(a => all12mSessionIds.Contains(a.SessionId))
                .ToListAsync();
            Chart12mAttRateJson = JsonSerializer.Serialize(last12.Select(m => {
                var mSessionIds = allSessions
                    .Where(s => s.SessionDate.Month == m.Month && s.SessionDate.Year == m.Year)
                    .Select(s => s.SessionId).ToHashSet();
                var mAtts = all12mAtts.Where(a => mSessionIds.Contains(a.SessionId)).ToList();
                var present = mAtts.Count(a => a.Status == AttendanceStatus.Present);
                var total = mAtts.Count;
                return total > 0 ? (double?)Math.Round((double)present / total * 100, 1) : null;
            }));

            // ── Recent sessions — 10 buổi đã qua gần nhất ───────────────
            RecentSessions = fl
                .Where(s => s.SessionDate <= today)
                .OrderByDescending(s => s.SessionDate)
                .Take(10)
                .Select(s => new RecentSessionInfo
                {
                    SessionId = s.SessionId,
                    SessionDate = s.SessionDate,
                    ClassName = s.Class?.ClassCode ?? "N/A",
                    ShiftName = s.Shift != null ? $"{s.Shift.StartTime:HH\\:mm}–{s.Shift.EndTime:HH\\:mm}" : "N/A",
                    RoomCode = s.Room?.RoomCode ?? "N/A",
                    Status = s.Status,
                    HasAttendance = allAtts.Any(a => a.SessionId == s.SessionId)
                })
                .ToList();

            return Page();
        }

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

        public class ClassOption { public int ClassId { get; set; } public string ClassCode { get; set; } = ""; }

        public class ClassDetailInfo
        {
            public string ClassCode { get; set; } = "";
            public string SubjectName { get; set; } = "";
            public int StudentCount { get; set; }
            public int TotalSessions { get; set; }
            public int CompletedCount { get; set; }
            public int CancelledCount { get; set; }
            public int AttendedSessions { get; set; }
            public double AttendanceRate { get; set; }
        }

        public class RecentSessionInfo
        {
            public int SessionId { get; set; }
            public DateOnly SessionDate { get; set; }
            public string ClassName { get; set; } = "";
            public string ShiftName { get; set; } = "";
            public string RoomCode { get; set; } = "";
            public SessionStatus Status { get; set; }
            public bool HasAttendance { get; set; }
        }
    }
}