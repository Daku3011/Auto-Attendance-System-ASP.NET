using Microsoft.EntityFrameworkCore;
using DemoAAS.Models;

namespace DemoAAS.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Student> Students { get; set; } = null!;
        public DbSet<Attendance> Attendances { get; set; } = null!;
    }
}
