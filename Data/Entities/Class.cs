using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Data.Entities
{
    public class Class
    {
        public int ClassId { get; set; }
        public string ClassCode { get; set; } = string.Empty;

        /// <summary>Khối lớp (1-12). Null nếu không áp dụng.</summary>
        public int? GradeLevel { get; set; }

        /// <summary>Tên lớp học (VD: 10A1, 11B2...)</summary>
        public string? ClassName { get; set; }

        public string? Description { get; set; }
        public Subject Subject { get; set; }
        public ClassStatus Status { get; set; } = ClassStatus.Active;

        // Navigation
        public ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();
        public ICollection<WeeklyScheduleTemplate> WeeklyScheduleTemplates { get; set; } = new List<WeeklyScheduleTemplate>();
        public ICollection<Session> Sessions { get; set; } = new List<Session>();
    }
}