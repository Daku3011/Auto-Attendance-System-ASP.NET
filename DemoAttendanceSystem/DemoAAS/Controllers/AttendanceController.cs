using Microsoft.AspNetCore.Mvc;
using DemoAAS.Data;
using DemoAAS.Services;
using DemoAAS.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;

namespace DemoAAS.Controllers
{
    public class AttendanceController : Controller
    {
        private static readonly Regex ImageDataRegex = new Regex(@"data:image/(?<type>.+?),(?<data>.+)", RegexOptions.Compiled);
        private readonly ApplicationDbContext _db;
        private readonly IFacialRecognitionService _recognitionService;

        public AttendanceController(ApplicationDbContext db, IFacialRecognitionService recognitionService)
        {
            _db = db;
            _recognitionService = recognitionService;
        }

        public IActionResult Index()
        {
            var attendanceList = _db.Attendances.Include(a => a.Student).OrderByDescending(a => a.LectureTime).ToList();
            return View(attendanceList);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> MarkAttendance([FromBody] MarkAttendanceViewModel model)
        {
            if (model?.ImageData == null)
            {
                return Json(new { success = false, message = "No image data received." });
            }

            if (!TryGetImageBytes(model.ImageData, out var imageBytes))
            {
                return Json(new { success = false, message = "Invalid image data received." });
            }

            // 1. Call the new method name: RecognizeStudents
            var recognizedStudents = await _recognitionService.RecognizeStudents(imageBytes);

            if (recognizedStudents == null || !recognizedStudents.Any())
            {
                return Json(new { success = false, message = "No registered students were recognized." });
            }

            // 2. Loop through the list of students returned by the service
            foreach (var student in recognizedStudents)
            {
                var alreadyMarked = await _db.Attendances
                    .AnyAsync(a => a.StudentId == student.StudentId && a.LectureTime > DateTime.Now.AddMinutes(-5));

                if (!alreadyMarked)
                {
                    var newAttendance = new Attendance
                    {
                        // 3. Access StudentId on the 'student' object inside the loop
                        StudentId = student.StudentId,
                        FacultyName = "Ms. Dwakesh",
                        LectureTime = DateTime.Now,
                        ClassroomNo = "101-CAM",
                        Status = "Present"
                    };
                    _db.Attendances.Add(newAttendance);
                }
            }

            await _db.SaveChangesAsync();

            var names = string.Join(", ", recognizedStudents.Select(s => s.Name));
            return Json(new { success = true, message = $"Attendance marked for: {names}." });
        }

        private static bool TryGetImageBytes(string imageData, out byte[] imageBytes)
        {
            imageBytes = Array.Empty<byte>();

            var match = ImageDataRegex.Match(imageData);
            if (!match.Success)
            {
                return false;
            }

            var base64Data = match.Groups["data"].Value;
            if (string.IsNullOrWhiteSpace(base64Data))
            {
                return false;
            }

            var buffer = new byte[base64Data.Length];
            if (!Convert.TryFromBase64String(base64Data, buffer, out var bytesWritten))
            {
                return false;
            }

            imageBytes = buffer[..bytesWritten];
            return true;
        }
    }
}
