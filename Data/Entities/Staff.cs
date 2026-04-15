using TutoringCenterManagement.Data.Enums;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace TutoringCenterManagement.Data.Entities
{
    /// <summary>
    /// Nhân viên/Quản lý (Role = Admin)
    /// </summary>
    public class Staff
    {
        [Key]
        [ForeignKey(nameof(Account))]
        public int AccountId { get; set; }

        [Required]
        [MaxLength(100)]
        public string Fullname { get; set; } = string.Empty;

        public DateTime? Dob { get; set; } // Date of Birth - nullable

        [MaxLength(15)]
        public string? Phone { get; set; }

        [MaxLength(100)]
        public string? Title { get; set; } // Chức danh: "Quản lý", "Nhân viên văn phòng", "Kế toán"...

        public Gender Gender { get; set; }

        // Navigation property
        public Account Account { get; set; } = null!;

    }
}