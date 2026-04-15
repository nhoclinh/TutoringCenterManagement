using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Data.Entities
{
    /// <summary>
    /// Điểm danh - Entity duy nhất có Audit Fields
    /// </summary>
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        // Foreign Keys
        [Required]
        public int SessionId { get; set; }

        [Required]
        public int StudentId { get; set; }

        // Attendance Info
        [Required]
        public AttendanceStatus Status { get; set; } = AttendanceStatus.Absent;

        [Required]
        public DateTime CheckInTime { get; set; } // Thời gian điểm danh

        [MaxLength(500)]
        public string? Note { get; set; }

        // Audit Fields - Dùng AccountId để hỗ trợ cả Teacher và Staff
        [Required]
        public int CreatedBy { get; set; } // AccountId (Teacher hoặc Staff)

        [Required]
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public int? LastUpdatedBy { get; set; } // AccountId (Teacher hoặc Staff)

        public DateTime? LastUpdatedAt { get; set; }

        // Navigation properties
        public Session Session { get; set; } = null!;
        public Student Student { get; set; } = null!;

        // Audit navigation - Trỏ đến Account
        [ForeignKey(nameof(CreatedBy))]
        public Account CreatedByAccount { get; set; } = null!;

        [ForeignKey(nameof(LastUpdatedBy))]
        public Account? LastUpdatedByAccount { get; set; }
    }
}