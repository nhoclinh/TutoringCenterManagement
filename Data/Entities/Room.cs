using System.ComponentModel.DataAnnotations;

namespace TutoringCenterManagement.Data.Entities
{
    /// <summary>
    /// Phòng học
    /// </summary>
    public class Room
    {
        [Key]
        public int RoomId { get; set; }

        [Required]
        [MaxLength(50)]
        public string RoomCode { get; set; } = string.Empty; // Mã phòng: "P101", "P202"

        [Required]
        [MaxLength(100)]
        public string RoomName { get; set; } = string.Empty; // Tên phòng

        [Required]
        public int Capacity { get; set; } // Sức chứa

        [MaxLength(500)]
        public string? Note { get; set; }

        // Navigation properties
        public ICollection<Session> Sessions { get; set; } = new List<Session>();
    }
}