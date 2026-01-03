using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace DemoAAS.Models
{
    public class Student
    {
        [Key]
        public int StudentId { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        public string RollNo { get; set; } = string.Empty;

        [Required]
        public string Department { get; set; } = string.Empty;

        public string Semester { get; set; } = string.Empty;
        public string Division { get; set; } = string.Empty;

        public byte[]? ReferenceImage { get; set; }
        
        // Navigation property for multiple photos
        public virtual ICollection<StudentPhoto> Photos { get; set; } = new List<StudentPhoto>();
    }
}
