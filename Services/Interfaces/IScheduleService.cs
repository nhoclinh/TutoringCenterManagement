using TutoringCenterManagement.Data.Entities;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Services.Interfaces
{
    public interface IScheduleService
    {
        /// <summary>
        /// Validate mẫu lịch học trước khi tạo / cập nhật.
        /// Kiểm tra:
        ///   1. Trùng phòng + ca + thứ trong khoảng ngày.
        ///   2. Trùng giáo viên chính (primaryTeacherId) + ca + thứ trong khoảng ngày.
        ///   3. Trùng giáo viên trợ giảng (assistantTeacherId) + ca + thứ trong khoảng ngày.
        ///   4. Lớp học có Status = Inactive.
        /// </summary>
        Task<(bool isValid, string errorMessage)> ValidateScheduleTemplate(
            int classId,
            int roomId,
            ShiftId shiftId,
            DayOfTheWeek dayOfWeek,
            DateOnly startDate,
            DateOnly endDate,
            int? excludeTemplateId = null,
            int? primaryTeacherId = null,   // <-- thêm mới
            int? assistantTeacherId = null);  // <-- thêm mới

        Task<List<Session>> GenerateSessionsFromTemplate(
            WeeklyScheduleTemplate template,
            int teacherId,
            int? teacherAssistantId = null);

        Task<(bool hasWarning, string warningMessage)> CheckTeacherSubjectMatch(
            int teacherId, int classId);
    }
}