using System.ComponentModel.DataAnnotations;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Data.Entities
{
    /// <summary>
    /// Ca học với khung giờ cố định
    /// </summary>
    public class Shift
    {
        [Key]
        public ShiftId ShiftId { get; set; }

        [Required]
        [MaxLength(50)]
        public string ShiftName { get; set; } = string.Empty; // "Ca 1", "Ca 2"...

        [Required]
        public TimeOnly StartTime { get; set; }

        [Required]
        public TimeOnly EndTime { get; set; }

        [MaxLength(200)]
        public string? Description { get; set; }

        // Navigation properties
        public ICollection<WeeklyScheduleTemplate> WeeklyScheduleTemplates { get; set; } = new List<WeeklyScheduleTemplate>();
        public ICollection<Session> Sessions { get; set; } = new List<Session>();
    }
}