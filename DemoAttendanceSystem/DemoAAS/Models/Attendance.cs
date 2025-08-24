using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace DemoAAS.Models
{
    public class Attendance
    {
        [Key]
        public int AttendanceId { get; set; }

        [Required]
        public int StudentId { get; set; }

        [ForeignKey("StudentId")]
        public Student Student { get; set; } = null!;

        [Required]
        public string FacultyName { get; set; } = string.Empty;

        [Required]
        public DateTime LectureTime { get; set; }

        [Required]
        public string ClassroomNo { get; set; } = string.Empty;

        [Required]
        public string Status { get; set; } = string.Empty;
    }
}
