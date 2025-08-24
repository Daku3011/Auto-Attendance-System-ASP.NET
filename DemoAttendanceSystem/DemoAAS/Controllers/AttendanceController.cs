using Microsoft.AspNetCore.Mvc;
using DemoAAS.Data;
using DemoAAS.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

namespace DemoAAS.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _db;

        public AttendanceController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /Attendance
        public IActionResult Index()
        {
            // Seed some demo students if none exist
            if (!_db.Students.Any())
            {
                _db.Students.AddRange(
                    new Student { Name = "Alice", RollNo = "CSE101", Department = "CSE", Semester = "5", Division = "A" },
                    new Student { Name = "Bob", RollNo = "CSE102", Department = "CSE", Semester = "5", Division = "A" }
                );
                _db.SaveChanges();
            }

            var attendanceList = _db.Attendances.Include(a => a.Student).OrderByDescending(a => a.LectureTime).ToList();
            return View(attendanceList);
        }

        // --- FIX APPLIED HERE ---
        // This attribute tells ASP.NET Core to skip the security token check for this specific action,
        // allowing our JavaScript fetch request to work without a 400 Bad Request error.
        [IgnoreAntiforgeryToken]
        [HttpPost]
        public IActionResult MarkAttendance([FromBody] MarkAttendanceViewModel model)
        {
            // --- SIMULATION LOGIC ---
            // In a real application, you would decode the model.ImageData,
            // run it through a facial recognition model, and find the matching student.

            // For this demo, we'll just pretend we recognized student "Alice".
            var studentToMark = _db.Students.FirstOrDefault(s => s.RollNo == "CSE101");

            if (studentToMark == null)
            {
                return Json(new { success = false, message = "Demo student 'Alice' not found." });
            }

            // Create a new attendance record
            var newAttendance = new Attendance
            {
                StudentId = studentToMark.StudentId,
                FacultyName = "Dr. Hetvi", // Or any other demo name
                LectureTime = DateTime.Now,
                ClassroomNo = "101-CAM",    // Indicate it came from the camera
                Status = "Present"
            };

            _db.Attendances.Add(newAttendance);
            _db.SaveChanges();

            // Return a success message
            return Json(new { success = true, message = $"Attendance marked for {studentToMark.Name} at {newAttendance.LectureTime}." });
        }
    }
}
