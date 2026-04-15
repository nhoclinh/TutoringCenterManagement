using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using TutoringCenterManagement.Data.Enums;

namespace TutoringCenterManagement.Data.Entities
{
    public class TeacherSubject
    {
        [Key, Column(Order = 0)]
        public int TeacherId { get; set; }

        [Key, Column(Order = 1)]
        public Subject Subject { get; set; }

        // Navigation
        public Teacher Teacher { get; set; } = null!;
    }
}