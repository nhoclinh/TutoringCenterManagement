using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;
using TutoringCenterManagement.Services.Interfaces;

namespace TutoringCenterManagement.Services.Implementations
{
    public class ScheduleService : IScheduleService
    {
        private readonly ApplicationDbContext _context;

        public ScheduleService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ─────────────────────────────────────────────────────────────────────
        // ValidateScheduleTemplate
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(bool isValid, string errorMessage)> ValidateScheduleTemplate(
            int classId,
            int roomId,
            ShiftId shiftId,
            DayOfTheWeek dayOfWeek,
            DateOnly startDate,
            DateOnly endDate,
            int? excludeTemplateId = null,
            int? primaryTeacherId = null,
            int? assistantTeacherId = null)
        {
            // ── 1. Kiểm tra trùng phòng + ca + thứ ───────────────────────────
            var conflictRoom = await _context.WeeklyScheduleTemplates
                .Where(t => t.RoomId == roomId
                         && t.ShiftId == shiftId
                         && t.DayOfWeek == dayOfWeek
                         && t.TemplateId != (excludeTemplateId ?? 0)
                         && !(t.EndDate < startDate || t.StartDate > endDate))
                .Include(t => t.Class)
                .FirstOrDefaultAsync();

            if (conflictRoom != null)
            {
                return (false,
                    $"Trùng lịch phòng với lớp {conflictRoom.Class.ClassCode} " +
                    $"(Thứ {(int)dayOfWeek + 2}, Ca {(int)shiftId})");
            }

            // ── 2. Kiểm tra trùng giáo viên chính ────────────────────────────
            if (primaryTeacherId.HasValue)
            {
                var conflictPrimary = await _context.WeeklyScheduleTemplates
                    .Where(t => (t.TeacherId == primaryTeacherId.Value
                              || t.TeacherAssistantId == primaryTeacherId.Value)
                             && t.ShiftId == shiftId
                             && t.DayOfWeek == dayOfWeek
                             && t.TemplateId != (excludeTemplateId ?? 0)
                             && !(t.EndDate < startDate || t.StartDate > endDate))
                    .Include(t => t.Teacher)
                    .FirstOrDefaultAsync();

                if (conflictPrimary != null)
                {
                    var teacherName = await _context.Teachers
                        .Where(t => t.AccountId == primaryTeacherId.Value)
                        .Select(t => t.Fullname)
                        .FirstOrDefaultAsync() ?? $"ID {primaryTeacherId.Value}";

                    return (false,
                        $"Giáo viên {teacherName} đã có lịch dạy " +
                        $"(Thứ {(int)dayOfWeek + 2}, Ca {(int)shiftId}) " +
                        $"trong khoảng thời gian này!");
                }
            }

            // ── 3. Kiểm tra trùng giáo viên trợ giảng ────────────────────────
            if (assistantTeacherId.HasValue)
            {
                var conflictAssistant = await _context.WeeklyScheduleTemplates
                    .Where(t => (t.TeacherId == assistantTeacherId.Value
                              || t.TeacherAssistantId == assistantTeacherId.Value)
                             && t.ShiftId == shiftId
                             && t.DayOfWeek == dayOfWeek
                             && t.TemplateId != (excludeTemplateId ?? 0)
                             && !(t.EndDate < startDate || t.StartDate > endDate))
                    .Include(t => t.Teacher)
                    .FirstOrDefaultAsync();

                if (conflictAssistant != null)
                {
                    var assistantName = await _context.Teachers
                        .Where(t => t.AccountId == assistantTeacherId.Value)
                        .Select(t => t.Fullname)
                        .FirstOrDefaultAsync() ?? $"ID {assistantTeacherId.Value}";

                    return (false,
                        $"Giáo viên trợ giảng {assistantName} đã có lịch dạy " +
                        $"(Thứ {(int)dayOfWeek + 2}, Ca {(int)shiftId}) " +
                        $"trong khoảng thời gian này!");
                }
            }

            // ── 4. Kiểm tra lớp học còn Active ───────────────────────────────
            var classEntity = await _context.Classes.FindAsync(classId);
            if (classEntity == null || classEntity.Status == ClassStatus.Inactive)
                return (false, "Lớp học này đang Inactive, không thể tạo lịch!");

            return (true, string.Empty);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GenerateSessionsFromTemplate
        // ─────────────────────────────────────────────────────────────────────
        public async Task<List<Session>> GenerateSessionsFromTemplate(
            WeeklyScheduleTemplate template,
            int teacherId,
            int? teacherAssistantId = null)
        {
            var sessions = new List<Session>();

            var holidays = await _context.Holidays
                .Where(h => !(h.EndDate < template.StartDate || h.StartDate > template.EndDate))
                .ToListAsync();

            var currentDate = template.StartDate;

            while (currentDate <= template.EndDate)
            {
                if ((int)currentDate.DayOfWeek == ((int)template.DayOfWeek + 1) % 7)
                {
                    bool isHoliday = holidays.Any(h => currentDate >= h.StartDate && currentDate <= h.EndDate);

                    if (!isHoliday)
                    {
                        sessions.Add(new Session
                        {
                            ClassId = template.ClassId,
                            RoomId = template.RoomId,
                            ShiftId = template.ShiftId,
                            TemplateId = template.TemplateId,
                            TeacherId = teacherId,
                            TeacherAssistantId = teacherAssistantId,
                            SessionDate = currentDate,
                            Status = SessionStatus.Scheduled,
                            CreatedAt = DateTime.Now
                        });
                    }
                }
                currentDate = currentDate.AddDays(1);
            }

            _context.Sessions.AddRange(sessions);
            await _context.SaveChangesAsync();

            return sessions;
        }

        // ─────────────────────────────────────────────────────────────────────
        // CheckTeacherSubjectMatch
        // ─────────────────────────────────────────────────────────────────────
        public async Task<(bool hasWarning, string warningMessage)> CheckTeacherSubjectMatch(
            int teacherId, int classId)
        {
            var classSubject = await _context.Classes
                .Where(c => c.ClassId == classId)
                .Select(c => c.Subject)
                .FirstAsync();

            var teacherSubjects = await _context.TeacherSubjects
                .Where(ts => ts.TeacherId == teacherId)
                .Select(ts => ts.Subject)
                .ToListAsync();

            if (!teacherSubjects.Contains(classSubject))
            {
                var teacherName = await _context.Teachers
                    .Where(t => t.AccountId == teacherId)
                    .Select(t => t.Fullname)
                    .FirstAsync();

                return (true, $"Cảnh báo: Giáo viên {teacherName} không dạy môn này!");
            }

            return (false, string.Empty);
        }

        public Task<List<Session>> GenerateSessionsFromTemplate(
            WeeklyScheduleTemplate template, List<int> teacherIds)
        {
            throw new NotImplementedException();
        }
    }
}