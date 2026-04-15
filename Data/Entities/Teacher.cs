using TutoringCenterManagement.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TutoringCenterManagement.Data.Entities
{
    /// <summary>
    /// Giáo viên (Role = Teacher)
    /// </summary>
    public class Teacher
    {
        [Key]
        [ForeignKey(nameof(Account))]
        public int AccountId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Fullname { get; set; } = string.Empty;

        public DateTime? Dob { get; set; }

        [MaxLength(15)]
        public string? Phone { get; set; }

        public Gender Gender { get; set; }

        [MaxLength(500)]
        public string? Note { get; set; }

        // Navigation
        public Account Account { get; set; } = null!;
        public ICollection<TeacherSubject> TeacherSubjects { get; set; } = new List<TeacherSubject>();

        // Giáo viên chính của Session/Template
        public ICollection<Session> Sessions { get; set; } = new List<Session>();
        public ICollection<WeeklyScheduleTemplate> Templates { get; set; } = new List<WeeklyScheduleTemplate>();

        // Trợ giảng của Session/Template
        public ICollection<Session> AssistantSessions { get; set; } = new List<Session>();
        public ICollection<WeeklyScheduleTemplate> AssistantTemplates { get; set; } = new List<WeeklyScheduleTemplate>();
    }
}