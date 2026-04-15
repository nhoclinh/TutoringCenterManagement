using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Data.Entities
{
    public class Session
    {
        [Key]
        public int SessionId { get; set; }

        [Required]
        public int ClassId { get; set; }

        [Required]
        public int RoomId { get; set; }

        [Required]
        public ShiftId ShiftId { get; set; }

        public int? TemplateId { get; set; }

        [Required]
        public int TeacherId { get; set; }

        public int? TeacherAssistantId { get; set; }

        [Required]
        public DateOnly SessionDate { get; set; }

        [Required]
        public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

        [MaxLength(500)]
        public string? Note { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation
        public Class Class { get; set; } = null!;
        public Room Room { get; set; } = null!;
        public Shift Shift { get; set; } = null!;
        public WeeklyScheduleTemplate? Template { get; set; }
        public Teacher Teacher { get; set; } = null!;
        public Teacher? TeacherAssistant { get; set; }
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}