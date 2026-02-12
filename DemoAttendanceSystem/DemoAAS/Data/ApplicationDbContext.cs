using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using DemoAAS.Models;

namespace DemoAAS.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.ConfigureWarnings(w =>
                w.Ignore(RelationalEventId.PendingModelChangesWarning));
            base.OnConfiguring(optionsBuilder);
        }

        public DbSet<Student> Students { get; set; } = null!;
        public DbSet<Attendance> Attendances { get; set; } = null!;
        public DbSet<StudentPhoto> StudentPhotos { get; set; } = null!;
    }
}
