using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Pages.Admin
{
    public class ReportsModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        public ReportsModel(ApplicationDbContext context) => _context = context;

        // ── Bộ lọc ────────────────────────────────────────────────────
        [BindProperty(SupportsGet = true)] public int FilterYear { get; set; } = DateTime.Today.Year;
        [BindProperty(SupportsGet = true)] public int FilterMonth { get; set; } = 0; // 0 = cả năm
        public List<int> AvailableYears { get; set; } = new();

        // ── KPI ───────────────────────────────────────────────────────
        public int TotalActiveStudents { get; set; }
        public int TotalTeachers { get; set; }
        public int TotalActiveClasses { get; set; }
        public int TotalRooms { get; set; }
        public int TotalSessionsCompleted { get; set; }
        public int TotalSessionsCancelled { get; set; }
        public int TotalSessionsScheduled { get; set; }
        public double AvgAttendanceRate { get; set; }
        public int NewStudentsInPeriod { get; set; }
        public int TotalHolidays { get; set; }

        // ── Biểu đồ: Buổi học theo tháng ─────────────────────────────
        public List<MonthlySessionStat> MonthlySessionStats { get; set; } = new();

        // ── Biểu đồ: Học sinh theo môn ───────────────────────────────
        public List<SubjectStudentStat> SubjectStudentStats { get; set; } = new();

        // ── Biểu đồ: Điểm danh theo môn ─────────────────────────────
        public List<SubjectAttendanceStat> SubjectAttendanceStats { get; set; } = new();

        // ── Biểu đồ: Học sinh mới theo tháng ─────────────────────────
        public List<MonthlyNewStudentStat> MonthlyNewStudentStats { get; set; } = new();

        // ── Bảng: Top lớp vắng ───────────────────────────────────────
        public List<ClassAbsenceStat> TopAbsenceClasses { get; set; } = new();

        // ── Bảng: Giáo viên ──────────────────────────────────────────
        public List<TeacherSessionStat> TeacherSessionStats { get; set; } = new();

        // ── Trạng thái học sinh / lớp ────────────────────────────────
        public int StudentActive { get; set; }
        public int StudentSuspended { get; set; }
        public int StudentLeft { get; set; }
        public int ClassActive { get; set; }
        public int ClassInactive { get; set; }

        // ── Thống kê phòng ────────────────────────────────────────────
        public List<RoomUsageStat> RoomUsageStats { get; set; } = new();

        // ─────────────────────────────────────────────────────────────
        public async Task<IActionResult> OnGetAsync()
        {
            if (HttpContext.Session.GetString("Role") != "Admin")
                return RedirectToPage("/Account/Login");

            // Năm có dữ liệu
            var sessionYears = await _context.Sessions
                .Select(s => s.SessionDate.Year).Distinct().ToListAsync();
            AvailableYears = sessionYears.Union(new[] { DateTime.Today.Year })
                .OrderByDescending(y => y).ToList();
            if (!AvailableYears.Contains(FilterYear))
                AvailableYears.Insert(0, FilterYear);

            await LoadKpiAsync();
            await LoadMonthlySessionsAsync();
            await LoadSubjectStatsAsync();
            await LoadAttendanceBySubjectAsync();
            await LoadMonthlyNewStudentsAsync();
            await LoadTopAbsenceClassesAsync();
            await LoadTeacherStatsAsync();
            await LoadRoomStatsAsync();
            LoadClassStudentStatus();

            return Page();
        }

        // ── KPI ──────────────────────────────────────────────────────
        private async Task LoadKpiAsync()
        {
            TotalActiveStudents = await _context.ClassStudents
                .Where(cs => cs.Status == StudentClassStatus.Active)
                .Select(cs => cs.StudentId).Distinct().CountAsync();

            TotalTeachers = await _context.Teachers.CountAsync();
            TotalActiveClasses = await _context.Classes.CountAsync(c => c.Status == ClassStatus.Active);
            TotalRooms = await _context.Rooms.CountAsync();

            var sessionQ = _context.Sessions.Where(s =>
                s.SessionDate.Year == FilterYear &&
                (FilterMonth == 0 || s.SessionDate.Month == FilterMonth));

            TotalSessionsCompleted = await sessionQ.CountAsync(s => s.Status == SessionStatus.Completed);
            TotalSessionsCancelled = await sessionQ.CountAsync(s => s.Status == SessionStatus.Cancelled);
            TotalSessionsScheduled = await sessionQ.CountAsync(s => s.Status == SessionStatus.Scheduled);

            var attQ = _context.Attendances.Where(a =>
                a.Session.SessionDate.Year == FilterYear &&
                (FilterMonth == 0 || a.Session.SessionDate.Month == FilterMonth));
            var total = await attQ.CountAsync();
            var present = await attQ.CountAsync(a => a.Status == AttendanceStatus.Present);
            AvgAttendanceRate = total > 0 ? Math.Round((double)present / total * 100, 1) : 0;

            NewStudentsInPeriod = await _context.ClassStudents
                .CountAsync(cs =>
                    cs.StartedAt.Year == FilterYear &&
                    (FilterMonth == 0 || cs.StartedAt.Month == FilterMonth));

            TotalHolidays = await _context.Holidays
                .CountAsync(h => h.StartDate.Year == FilterYear);
        }

        // ── Buổi học theo tháng ───────────────────────────────────────
        private async Task LoadMonthlySessionsAsync()
        {
            var raw = await _context.Sessions
                .Where(s => s.SessionDate.Year == FilterYear)
                .GroupBy(s => new { s.SessionDate.Month, s.Status })
                .Select(g => new { g.Key.Month, g.Key.Status, Count = g.Count() })
                .ToListAsync();

            MonthlySessionStats = Enumerable.Range(1, 12).Select(m => new MonthlySessionStat
            {
                Month = m,
                Completed = raw.Where(r => r.Month == m && r.Status == SessionStatus.Completed).Sum(r => r.Count),
                Cancelled = raw.Where(r => r.Month == m && r.Status == SessionStatus.Cancelled).Sum(r => r.Count),
                Scheduled = raw.Where(r => r.Month == m && r.Status == SessionStatus.Scheduled).Sum(r => r.Count),
            }).ToList();
        }

        // ── Học sinh theo môn ─────────────────────────────────────────
        private async Task LoadSubjectStatsAsync()
        {
            SubjectStudentStats = await _context.ClassStudents
                .Where(cs => cs.Status == StudentClassStatus.Active)
                .GroupBy(cs => cs.Class.Subject)
                .Select(g => new SubjectStudentStat
                {
                    Subject = g.Key,
                    Count = g.Select(cs => cs.StudentId).Distinct().Count()
                })
                .OrderByDescending(s => s.Count)
                .ToListAsync();
        }

        // ── Điểm danh theo môn ───────────────────────────────────────
        private async Task LoadAttendanceBySubjectAsync()
        {
            var raw = await _context.Attendances
                .Where(a => a.Session.SessionDate.Year == FilterYear &&
                            (FilterMonth == 0 || a.Session.SessionDate.Month == FilterMonth))
                .GroupBy(a => new { a.Session.Class.Subject, a.Status })
                .Select(g => new { g.Key.Subject, g.Key.Status, Count = g.Count() })
                .ToListAsync();

            SubjectAttendanceStats = raw
                .GroupBy(r => r.Subject)
                .Select(g => new SubjectAttendanceStat
                {
                    Subject = g.Key,
                    Present = g.Where(r => r.Status == AttendanceStatus.Present).Sum(r => r.Count),
                    Absent = g.Where(r => r.Status == AttendanceStatus.Absent).Sum(r => r.Count),
                })
                .OrderBy(s => s.Subject).ToList();
        }

        // ── Học sinh mới theo tháng ───────────────────────────────────
        private async Task LoadMonthlyNewStudentsAsync()
        {
            var raw = await _context.ClassStudents
                .Where(cs => cs.StartedAt.Year == FilterYear)
                .GroupBy(cs => cs.StartedAt.Month)
                .Select(g => new { Month = g.Key, Count = g.Select(cs => cs.StudentId).Distinct().Count() })
                .ToListAsync();

            MonthlyNewStudentStats = Enumerable.Range(1, 12).Select(m => new MonthlyNewStudentStat
            {
                Month = m,
                Count = raw.FirstOrDefault(r => r.Month == m)?.Count ?? 0
            }).ToList();
        }

        // ── Top lớp vắng ─────────────────────────────────────────────
        private async Task LoadTopAbsenceClassesAsync()
        {
            var raw = await _context.Attendances
                .Where(a => a.Session.SessionDate.Year == FilterYear &&
                            (FilterMonth == 0 || a.Session.SessionDate.Month == FilterMonth))
                .GroupBy(a => new {
                    a.Session.ClassId,
                    a.Session.Class.ClassCode,
                    a.Session.Class.Subject,
                    a.Session.Class.ClassName
                })
                .Select(g => new
                {
                    g.Key.ClassId,
                    g.Key.ClassCode,
                    g.Key.Subject,
                    g.Key.ClassName,
                    Total = g.Count(),
                    Absent = g.Count(a => a.Status == AttendanceStatus.Absent),
                    Present = g.Count(a => a.Status == AttendanceStatus.Present),
                }).ToListAsync();

            TopAbsenceClasses = raw.Where(r => r.Total > 0)
                .Select(r => new ClassAbsenceStat
                {
                    ClassCode = r.ClassCode,
                    ClassName = r.ClassName ?? "",
                    Subject = r.Subject,
                    TotalAtt = r.Total,
                    AbsentCount = r.Absent,
                    AbsenceRate = Math.Round((double)r.Absent / r.Total * 100, 1)
                })
                .OrderByDescending(r => r.AbsenceRate).Take(8).ToList();
        }

        // ── Giáo viên ────────────────────────────────────────────────
        private async Task LoadTeacherStatsAsync()
        {
            var raw = await _context.Sessions
                .Where(s => s.SessionDate.Year == FilterYear &&
                            (FilterMonth == 0 || s.SessionDate.Month == FilterMonth) &&
                            s.Status == SessionStatus.Completed)
                .GroupBy(s => new { s.TeacherId, s.Teacher.Fullname })
                .Select(g => new
                {
                    g.Key.TeacherId,
                    g.Key.Fullname,
                    SessionCount = g.Count(),
                    ClassCount = g.Select(s => s.ClassId).Distinct().Count()
                })
                .OrderByDescending(t => t.SessionCount)
                .Take(10).ToListAsync();

            TeacherSessionStats = raw.Select(r => new TeacherSessionStat
            {
                TeacherName = r.Fullname,
                SessionCount = r.SessionCount,
                ClassCount = r.ClassCount
            }).ToList();
        }

        // ── Phòng học ────────────────────────────────────────────────
        private async Task LoadRoomStatsAsync()
        {
            var raw = await _context.Sessions
                .Where(s => s.SessionDate.Year == FilterYear &&
                            (FilterMonth == 0 || s.SessionDate.Month == FilterMonth) &&
                            s.Status == SessionStatus.Completed)
                .GroupBy(s => new { s.RoomId, s.Room.RoomCode, s.Room.RoomName, s.Room.Capacity })
                .Select(g => new
                {
                    g.Key.RoomCode,
                    g.Key.RoomName,
                    g.Key.Capacity,
                    UsedSessions = g.Count()
                })
                .OrderByDescending(r => r.UsedSessions)
                .Take(6).ToListAsync();

            var maxSess = raw.Any() ? raw.Max(r => r.UsedSessions) : 1;
            RoomUsageStats = raw.Select(r => new RoomUsageStat
            {
                RoomCode = r.RoomCode,
                RoomName = r.RoomName,
                Capacity = r.Capacity,
                UsedSessions = r.UsedSessions,
                UsagePct = maxSess > 0 ? Math.Round((double)r.UsedSessions / maxSess * 100, 0) : 0
            }).ToList();
        }

        // ── Trạng thái học sinh / lớp ─────────────────────────────────
        private void LoadClassStudentStatus()
        {
            var groups = _context.ClassStudents
                .GroupBy(cs => cs.Status)
                .Select(g => new { g.Key, Count = g.Select(cs => cs.StudentId).Distinct().Count() })
                .ToList();

            StudentActive = groups.FirstOrDefault(g => g.Key == StudentClassStatus.Active)?.Count ?? 0;
            StudentSuspended = groups.FirstOrDefault(g => g.Key == StudentClassStatus.Suspended)?.Count ?? 0;
            StudentLeft = groups.FirstOrDefault(g => g.Key == StudentClassStatus.Left)?.Count ?? 0;
            ClassActive = _context.Classes.Count(c => c.Status == ClassStatus.Active);
            ClassInactive = _context.Classes.Count(c => c.Status == ClassStatus.Inactive);
        }

        // ── View Models ───────────────────────────────────────────────
        public class MonthlySessionStat
        {
            public int Month { get; set; }
            public int Completed { get; set; }
            public int Cancelled { get; set; }
            public int Scheduled { get; set; }
            public int Total => Completed + Cancelled + Scheduled;
        }

        public class SubjectStudentStat
        {
            public Subject Subject { get; set; }
            public int Count { get; set; }
            public string Label => Subject switch
            {
                Subject.Math => "Toán",
                Subject.Vietnamese => "T.Việt",
                Subject.English => "T.Anh",
                Subject.Physics => "Vật lý",
                Subject.Biology => "Sinh học",
                Subject.Chemistry => "Hóa học",
                Subject.Geography => "Địa lý",
                Subject.History => "Lịch sử",
                _ => "Khác"
            };
            public string LabelFull => Subject switch
            {
                Subject.Math => "Toán",
                Subject.Vietnamese => "Tiếng Việt",
                Subject.English => "Tiếng Anh",
                Subject.Physics => "Vật lý",
                Subject.Biology => "Sinh học",
                Subject.Chemistry => "Hóa học",
                Subject.Geography => "Địa lý",
                Subject.History => "Lịch sử",
                _ => "Môn khác"
            };
        }

        public class SubjectAttendanceStat
        {
            public Subject Subject { get; set; }
            public int Present { get; set; }
            public int Absent { get; set; }
            public int Total => Present + Absent;
            public double Rate => Total > 0 ? Math.Round((double)Present / Total * 100, 1) : 0;
            public string Label => Subject switch
            {
                Subject.Math => "Toán",
                Subject.Vietnamese => "T.Việt",
                Subject.English => "T.Anh",
                Subject.Physics => "Vật lý",
                Subject.Biology => "Sinh học",
                Subject.Chemistry => "Hóa học",
                Subject.Geography => "Địa lý",
                Subject.History => "Lịch sử",
                _ => "Khác"
            };
        }

        public class MonthlyNewStudentStat
        {
            public int Month { get; set; }
            public int Count { get; set; }
        }

        public class ClassAbsenceStat
        {
            public string ClassCode { get; set; } = string.Empty;
            public string ClassName { get; set; } = string.Empty;
            public Subject Subject { get; set; }
            public int TotalAtt { get; set; }
            public int AbsentCount { get; set; }
            public double AbsenceRate { get; set; }
            public string SubjectLabel => Subject switch
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
        }

        public class TeacherSessionStat
        {
            public string TeacherName { get; set; } = string.Empty;
            public int SessionCount { get; set; }
            public int ClassCount { get; set; }
        }

        public class RoomUsageStat
        {
            public string RoomCode { get; set; } = string.Empty;
            public string RoomName { get; set; } = string.Empty;
            public int Capacity { get; set; }
            public int UsedSessions { get; set; }
            public double UsagePct { get; set; }
        }
    }
}