namespace TutoringCenterManagement.Data.Enums
{
    /// <summary>
    /// Trạng thái lịch học mẫu
    /// </summary>
    public enum ScheduleTemplateStatus
    {
        Upcoming = 0,        // Sắp diễn ra
        Active = 1,          // Đang hoạt động
        Finished = 2,        // Đã kết thúc
        FinishedEarly = 3    // Kết thúc sớm
    }
}