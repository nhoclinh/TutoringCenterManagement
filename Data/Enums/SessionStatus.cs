namespace TutoringCenterManagement.Data.Enums
{
    /// <summary>
    /// Trạng thái buổi học
    /// </summary>
    public enum SessionStatus
    {
        Scheduled = 0,   // Đã lên lịch
        Ongoing = 1,     // Đang diễn ra
        Completed = 2,   // Đã hoàn thành
        Cancelled = 3    // Đã hủy
    }
}