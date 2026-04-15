using TutoringCenterManagement.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TutoringCenterManagement.Data.Entities
{
    /// <summary>
    /// Phụ huynh (Role = Parent)
    /// </summary>
    public class Parent
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
        public string? Note { get; set; } // Ghi chú về phụ huynh

        // Navigation property
        public Account Account { get; set; } = null!;

        // Navigation cho Students (1 phụ huynh có nhiều con)
        public ICollection<Student> Students { get; set; } = new List<Student>();
    }
}