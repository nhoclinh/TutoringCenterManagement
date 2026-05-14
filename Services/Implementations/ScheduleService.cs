// ============================================================
// FILE GỐC : Services/Implementations/ScheduleService.cs
// FILE SỬA : Services/Implementations/ScheduleService_Fixed.cs
//
// THAY ĐỔI SO VỚI BẢN GỐC:
//
// [FIX-1] Check PHÒNG: bổ sung query Sessions để phát hiện session lẻ
//         cùng phòng + ca + thứ đã tồn tại trong range ngày.
//         Bản gốc chỉ check WeeklyScheduleTemplates → bỏ sót session lẻ.
//
// [FIX-2] Check GV CHÍNH: đổi từ query WeeklyScheduleTemplates sang Sessions.
//         Bản gốc bỏ sót session lẻ (TemplateId=null) của GV.
//         EF Core không translate (int)s.SessionDate.DayOfWeek sang SQL
//         → load candidates về C# trước, lọc DayOfWeek + holiday phía C#.
//
// [FIX-3] Check GV TRỢ GIẢNG: tương tự FIX-2.
// ============================================================

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
            // Chuyển DayOfTheWeek enum (Mon=0) → System.DayOfWeek (.NET, Sun=0, Mon=1)
            // Dùng chung cho tất cả các check bên dưới
            int targetDotNetDow = ((int)dayOfWeek + 1) % 7;

            // Load holidays trong range — dùng chung cho tất cả các check
            var holidays = await _context.Holidays
                .Where(h => !(h.EndDate < startDate || h.StartDate > endDate))
                .ToListAsync();

            // ── 1. Kiểm tra trùng PHÒNG ──────────────────────────────────────
            // [FIX-1] Query Sessions thay vì (chỉ) WeeklyScheduleTemplates.
            // Lý do: template đã sinh sessions rồi → sessions là source of truth.
            // Session lẻ cùng phòng + ca + ngày cũng phải bị chặn.
            //
            // Bước 1: SQL lọc RoomId + ShiftId + date range + Status (EF dịch được)
            // Bước 2: C# lọc DayOfWeek + holiday (EF không dịch được DayOfWeek cast)
            var roomCandidates = await _context.Sessions
                .Where(s => s.RoomId == roomId
                         && s.ShiftId == shiftId
                         && s.SessionDate >= startDate
                         && s.SessionDate <= endDate
                         && s.Status != SessionStatus.Cancelled
                         && (excludeTemplateId == null || s.TemplateId != excludeTemplateId))
                .Include(s => s.Class)
                .ToListAsync();

            var conflictRoomSession = roomCandidates.FirstOrDefault(s =>
                (int)s.SessionDate.DayOfWeek == targetDotNetDow &&
                !holidays.Any(h => s.SessionDate >= h.StartDate && s.SessionDate <= h.EndDate));

            if (conflictRoomSession != null)
            {
                return (false,
                    $"Trùng lịch phòng với lớp {conflictRoomSession.Class.ClassCode} " +
                    $"(Thứ {(int)dayOfWeek + 2}, Ca {(int)shiftId}) " +
                    $"— Ngày trùng đầu tiên: {conflictRoomSession.SessionDate:dd/MM/yyyy}");
            }

            // ── 2. Kiểm tra trùng GV CHÍNH ───────────────────────────────────
            // [FIX-2] Query Sessions (bao gồm session lẻ TemplateId=null).
            // Không lọc DayOfWeek trong SQL → load về C# rồi lọc.
            if (primaryTeacherId.HasValue)
            {
                var primaryCandidates = await _context.Sessions
                    .Where(s => (s.TeacherId == primaryTeacherId.Value
                              || s.TeacherAssistantId == primaryTeacherId.Value)
                             && s.ShiftId == shiftId
                             && s.SessionDate >= startDate
                             && s.SessionDate <= endDate
                             && s.Status != SessionStatus.Cancelled
                             && (excludeTemplateId == null || s.TemplateId != excludeTemplateId))
                    .ToListAsync();

                var conflictPrimary = primaryCandidates.FirstOrDefault(s =>
                    (int)s.SessionDate.DayOfWeek == targetDotNetDow &&
                    !holidays.Any(h => s.SessionDate >= h.StartDate && s.SessionDate <= h.EndDate));

                if (conflictPrimary != null)
                {
                    var teacherName = await _context.Teachers
                        .Where(t => t.AccountId == primaryTeacherId.Value)
                        .Select(t => t.Fullname)
                        .FirstOrDefaultAsync() ?? $"ID {primaryTeacherId.Value}";

                    return (false,
                        $"Giáo viên {teacherName} đã có lịch dạy " +
                        $"(Thứ {(int)dayOfWeek + 2}, Ca {(int)shiftId}) " +
                        $"trong khoảng thời gian này! " +
                        $"(Ngày trùng đầu tiên: {conflictPrimary.SessionDate:dd/MM/yyyy})");
                }
            }

            // ── 3. Kiểm tra trùng GV TRỢ GIẢNG ──────────────────────────────
            // [FIX-3] Tương tự FIX-2.
            if (assistantTeacherId.HasValue)
            {
                var assistantCandidates = await _context.Sessions
                    .Where(s => (s.TeacherId == assistantTeacherId.Value
                              || s.TeacherAssistantId == assistantTeacherId.Value)
                             && s.ShiftId == shiftId
                             && s.SessionDate >= startDate
                             && s.SessionDate <= endDate
                             && s.Status != SessionStatus.Cancelled
                             && (excludeTemplateId == null || s.TemplateId != excludeTemplateId))
                    .ToListAsync();

                var conflictAssistant = assistantCandidates.FirstOrDefault(s =>
                    (int)s.SessionDate.DayOfWeek == targetDotNetDow &&
                    !holidays.Any(h => s.SessionDate >= h.StartDate && s.SessionDate <= h.EndDate));

                if (conflictAssistant != null)
                {
                    var assistantName = await _context.Teachers
                        .Where(t => t.AccountId == assistantTeacherId.Value)
                        .Select(t => t.Fullname)
                        .FirstOrDefaultAsync() ?? $"ID {assistantTeacherId.Value}";

                    return (false,
                        $"Giáo viên trợ giảng {assistantName} đã có lịch dạy " +
                        $"(Thứ {(int)dayOfWeek + 2}, Ca {(int)shiftId}) " +
                        $"trong khoảng thời gian này! " +
                        $"(Ngày trùng đầu tiên: {conflictAssistant.SessionDate:dd/MM/yyyy})");
                }
            }

            // ── 4. Kiểm tra lớp học còn Active ───────────────────────────────
            var classEntity = await _context.Classes.FindAsync(classId);
            if (classEntity == null || classEntity.Status == ClassStatus.Inactive)
                return (false, "Lớp học này đang Inactive, không thể tạo lịch!");

            return (true, string.Empty);
        }

        // ─────────────────────────────────────────────────────────────────────
        // GenerateSessionsFromTemplate — KHÔNG thay đổi
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
                    bool isHoliday = holidays.Any(h =>
                        currentDate >= h.StartDate && currentDate <= h.EndDate);

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
        // CheckTeacherSubjectMatch — KHÔNG thay đổi
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