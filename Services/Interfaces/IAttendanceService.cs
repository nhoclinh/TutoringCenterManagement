using TutoringCenterManagement.Data.Entities;

namespace TutoringCenterManagement.Services.Interfaces
{
    public interface IAttendanceService
    {
        /// <summary>
        /// Teacher được sửa từ lúc buổi học bắt đầu đến 3 giờ sau khi kết thúc.
        /// </summary>
        bool IsWithinAttendanceWindow(DateOnly sessionDate,
            TimeOnly startTime, TimeOnly endTime);

        /// <summary>
        /// Số giờ còn lại được phép sửa. Null = đã hết hạn (khoá).
        /// </summary>
        double? GetHoursRemaining(DateOnly sessionDate, TimeOnly endTime);

        /// <summary>
        /// Lấy danh sách học sinh trong lớp tại thời điểm buổi học.
        /// </summary>
        Task<AttendanceStudentList> GetStudentsForAttendanceAsync(
            int classId, DateOnly sessionDate);

        /// <summary>
        /// Lấy tên đầy đủ của người tạo/cập nhật điểm danh.
        /// </summary>
        string GetUserFullname(Account? account);
    }

    public class AttendanceStudentList
    {
        public List<StudentAttendanceItem> Active { get; set; } = new();
        public List<StudentAttendanceItem> Suspended { get; set; } = new();
    }

    public class StudentAttendanceItem
    {
        public int StudentId { get; set; }
        public string Fullname { get; set; } = string.Empty;
        public string CurrentSchool { get; set; } = string.Empty;
        public DateOnly StartedAt { get; set; }
        public bool IsActive { get; set; }
    }
}