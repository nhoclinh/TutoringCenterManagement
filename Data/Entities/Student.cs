using TutoringCenterManagement.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TutoringCenterManagement.Data.Entities
{
    /// <summary>
    /// Học sinh (Role = Student)
    /// </summary>
    public class Student
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

        [MaxLength(200)]
        public string? CurrentSchool { get; set; } // Trường đang học

        [MaxLength(500)]
        public string? Note { get; set; }

        // Foreign key cho Parent (nullable - có thể không có phụ huynh trong hệ thống)
        public int? ParentId { get; set; }

        // Navigation properties
        public Account Account { get; set; } = null!;
        public Parent? Parent { get; set; }

        // Navigation cho ClassStudent (học sinh tham gia nhiều lớp)
        public ICollection<ClassStudent> ClassStudents { get; set; } = new List<ClassStudent>();

        // Navigation cho Attendance
        public ICollection<Attendance> Attendances { get; set; } = new List<Attendance>();
    }
}