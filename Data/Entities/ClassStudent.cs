using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Data.Entities
{
    /// <summary>
    /// Bảng trung gian - Quan hệ nhiều-nhiều giữa Class và Student
    /// Theo dõi trạng thái và thời gian học của học sinh trong lớp
    /// </summary>
    public class ClassStudent
    {
        // Composite Primary Key
        [Key, Column(Order = 0)]
        public int ClassId { get; set; }

        [Key, Column(Order = 1)]
        public int StudentId { get; set; }

        // Tracking Info
        [Required]
        public DateOnly StartedAt { get; set; } // Ngày bắt đầu học (VD: học thử từ ngày X)

        public DateOnly? LeftAt { get; set; } // NULL = đang học, có giá trị = đã nghỉ

        [Required]
        public StudentClassStatus Status { get; set; } = StudentClassStatus.Active;

        // Navigation properties
        public Class Class { get; set; } = null!;
        public Student Student { get; set; } = null!;
    }
}