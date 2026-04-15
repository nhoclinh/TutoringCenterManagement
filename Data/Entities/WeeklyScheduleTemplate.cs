using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Data.Entities
{
    public class WeeklyScheduleTemplate
    {
        [Key]
        public int TemplateId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Required]
        public ShiftId ShiftId { get; set; }

        [Required]
        public DateOnly StartDate { get; set; }

        [Required]
        public DateOnly EndDate { get; set; }

        [Required]
        public DayOfTheWeek DayOfWeek { get; set; }

        [Required]
        public ScheduleTemplateStatus Status { get; set; } = ScheduleTemplateStatus.Upcoming;

        [Required]
        public int RoomId { get; set; }

        [Required]
        public int TeacherId { get; set; }

        public int? TeacherAssistantId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public Room Room { get; set; } = null!;
        public Class Class { get; set; } = null!;
        public Shift Shift { get; set; } = null!;
        public Teacher Teacher { get; set; } = null!;
        public Teacher? TeacherAssistant { get; set; }
        public ICollection<Session> Sessions { get; set; } = new List<Session>();
    }
}