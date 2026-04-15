using System.ComponentModel.DataAnnotations;

namespace TutoringCenterManagement.Data.Entities
{
    /// <summary>
    /// Ngày nghỉ lễ - Dùng để loại trừ khi tạo Session từ Template
    /// </summary>
    public class Holiday
    {
        [Key]
        public int HolidayId { get; set; }

        [Required]
        [MaxLength(200)]
        public string HolidayName { get; set; } = string.Empty; // Tên ngày lễ

        [Required]
        public DateOnly StartDate { get; set; } // Ngày bắt đầu nghỉ

        [Required]
        public DateOnly EndDate { get; set; } // Ngày kết thúc nghỉ

        [MaxLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}