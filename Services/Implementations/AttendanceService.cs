using Microsoft.EntityFrameworkCore;
using TutoringCenterManagement.Data;
using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;
using TutoringCenterManagement.Services.Interfaces;

namespace TutoringCenterManagement.Services.Implementations
{
    public class AttendanceService : IAttendanceService
    {
        private readonly ApplicationDbContext _context;

        public AttendanceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public bool IsWithinAttendanceWindow(
            DateOnly sessionDate, TimeOnly startTime, TimeOnly endTime)
        {
            var start = sessionDate.ToDateTime(startTime);
            var deadline = sessionDate.ToDateTime(endTime).AddHours(3);
            return DateTime.Now >= start && DateTime.Now <= deadline;
        }

        public double? GetHoursRemaining(DateOnly sessionDate, TimeOnly endTime)
        {
            var sessionEnd = sessionDate.ToDateTime(endTime);
            var hoursSinceEnd = (DateTime.Now - sessionEnd).TotalHours;
            return hoursSinceEnd <= 3
                ? Math.Max(0, 3 - hoursSinceEnd)
                : null;
        }

        public async Task<AttendanceStudentList> GetStudentsForAttendanceAsync(
            int classId, DateOnly sessionDate)
        {
            var all = await _context.ClassStudents
                .Where(cs => cs.ClassId == classId
                    && cs.Status != StudentClassStatus.Left
                    && (cs.LeftAt == null || cs.LeftAt > sessionDate))
                .Include(cs => cs.Student)
                .Select(cs => new StudentAttendanceItem
                {
                    StudentId = cs.StudentId,
                    Fullname = cs.Student.Fullname,
                    CurrentSchool = cs.Student.CurrentSchool ?? "N/A",
                    StartedAt = cs.StartedAt,
                    IsActive = cs.Status == StudentClassStatus.Active
                })
                .OrderBy(s => s.Fullname)
                .ToListAsync();

            return new AttendanceStudentList
            {
                Active = all.Where(s => s.IsActive).ToList(),
                Suspended = all.Where(s => !s.IsActive).ToList()
            };
        }

        public string GetUserFullname(Account? account)
        {
            if (account == null) return "N/A";
            return account.Role switch
            {
                Role.Admin => account.Staff?.Fullname ?? "Admin",
                Role.Teacher => account.Teacher?.Fullname ?? "Teacher",
                _ => "Unknown"
            };
        }
    }
}