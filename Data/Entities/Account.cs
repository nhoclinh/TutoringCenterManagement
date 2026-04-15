using TutoringCenterManagement.Data.Enums;
using System.ComponentModel.DataAnnotations;

namespace TutoringCenterManagement.Data.Entities
{
    public class Account
    {
        [Key]
        public int AccountId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Password { get; set; } = string.Empty;

        [Required]
        public Role Role { get; set; }

        [Required]
        public IsActive IsActive { get; set; } = IsActive.Active;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public Staff? Staff { get; set; }
        public Teacher? Teacher { get; set; }
        public Student? Student { get; set; }
        public Parent? Parent { get; set; }

        // Navigation cho Attendance Audit
        public ICollection<Attendance> CreatedAttendances { get; set; } = new List<Attendance>();
        public ICollection<Attendance> LastUpdatedAttendances { get; set; } = new List<Attendance>();
    }
}