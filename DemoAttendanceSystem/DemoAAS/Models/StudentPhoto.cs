using System;
using System.ComponentModel.DataAnnotations;

namespace DemoAAS.Models
{
    public class StudentPhoto
    {
        [Key]
        public int PhotoId { get; set; }
        public int StudentId { get; set; }
        public byte[] ImageData { get; set; } = null!;
        public float[]? FaceEmbedding { get; set; }
        public DateTime UploadedAt { get; set; }
        
        public virtual Student Student { get; set; } = null!;
    }
}
