using Microsoft.AspNetCore.Mvc;
using DemoAAS.Data;
using DemoAAS.Services;
using DemoAAS.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;
using System.Collections.Generic;
using OpenCvSharp;

namespace DemoAAS.Controllers
{
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly IFacialRecognitionService _recognitionService;
        private readonly IConfiguration _configuration;
        private readonly Microsoft.AspNetCore.SignalR.IHubContext<DemoAAS.Hubs.AttendanceHub> _hubContext;

        public AttendanceController(ApplicationDbContext db, IFacialRecognitionService recognitionService, IConfiguration configuration, Microsoft.AspNetCore.SignalR.IHubContext<DemoAAS.Hubs.AttendanceHub> hubContext)
        {
            _db = db;
            _recognitionService = recognitionService;
            _configuration = configuration;
            _hubContext = hubContext;
        }

        public IActionResult Index()
        {
            return View();
        }

        // GET: Attendance/Records
        public IActionResult Records(string? classroom, string? faculty, DateTime? fromDate, DateTime? toDate)
        {
            var query = _db.Attendances
                .Include(a => a.Student)
                .AsQueryable();

            // Apply filters
            if (!string.IsNullOrEmpty(classroom))
            {
                query = query.Where(a => a.ClassroomNo.Contains(classroom));
            }

            if (!string.IsNullOrEmpty(faculty))
            {
                query = query.Where(a => a.FacultyName.Contains(faculty));
            }

            if (fromDate.HasValue)
            {
                var fromDateUtc = fromDate.Value.ToUniversalTime();
                query = query.Where(a => a.LectureTime >= fromDateUtc);
            }

            if (toDate.HasValue)
            {
                var toDateUtc = toDate.Value.AddDays(1).ToUniversalTime(); // Include entire day
                query = query.Where(a => a.LectureTime < toDateUtc);
            }

            var attendanceList = query
                .OrderByDescending(a => a.LectureTime)
                .Take(100) // Limit to 100 records
                .ToList();

            // Pass filter values to view
            ViewBag.Classroom = classroom;
            ViewBag.Faculty = faculty;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

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

            var base64Data = Regex.Match(model.ImageData, @"data:image/(?<type>.+?),(?<data>.+)").Groups["data"].Value;
            var imageBytes = Convert.FromBase64String(base64Data);

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
                    .AnyAsync(a => a.StudentId == student.StudentId && a.LectureTime > DateTime.UtcNow.AddMinutes(-5));

                if (!alreadyMarked)
                {
                    var newAttendance = new Attendance
                    {
                        // 3. Access StudentId on the 'student' object inside the loop
                        StudentId = student.StudentId,
                        FacultyName = _configuration["AttendanceSettings:FacultyName"] ?? "Unknown Faculty",
                        LectureTime = DateTime.UtcNow,
                        ClassroomNo = _configuration["AttendanceSettings:ClassroomNo"] ?? "Unknown Class",
                        Status = "Present"
                    };
                    _db.Attendances.Add(newAttendance);
                    
                    // Broadcast the update via SignalR
                    await _hubContext.Clients.All.SendAsync("ReceiveAttendanceUpdate", student.Name, "Present");
                }
            }

            await _db.SaveChangesAsync();

            var names = string.Join(", ", recognizedStudents.Select(s => s.Name));
            return Json(new { success = true, message = $"Attendance marked for: {names}." });
        }

        public IActionResult ExportToCsv(string? classroom, string? faculty, DateTime? fromDate, DateTime? toDate)
        {
            var query = _db.Attendances
                .Include(a => a.Student)
                .AsQueryable();

            if (!string.IsNullOrEmpty(classroom)) query = query.Where(a => a.ClassroomNo.Contains(classroom));
            if (!string.IsNullOrEmpty(faculty)) query = query.Where(a => a.FacultyName.Contains(faculty));
            if (fromDate.HasValue) query = query.Where(a => a.LectureTime >= fromDate.Value.ToUniversalTime());
            if (toDate.HasValue) query = query.Where(a => a.LectureTime < toDate.Value.AddDays(1).ToUniversalTime());

            var records = query.OrderByDescending(a => a.LectureTime).ToList();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("Student Name,Roll No,Department,Classroom,Faculty,Lecture Time,Status");

            foreach (var record in records)
            {
                csv.AppendLine($"{record.Student?.Name},{record.Student?.RollNo},{record.Student?.Department},{record.ClassroomNo},{record.FacultyName},{record.LectureTime:yyyy-MM-dd HH:mm:ss}, {record.Status}");
            }

            return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", $"Attendance_Report_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        public IActionResult Classroom()
        {
            return View();
        }

        [HttpGet]
        public async Task StreamFeed(string rtspUrl)
        {
            if (string.IsNullOrEmpty(rtspUrl))
            {
                Response.StatusCode = 400;
                return;
            }

            Response.Headers.Append("Content-Type", "multipart/x-mixed-replace; boundary=frame");

            // Run in a separate task to avoid blocking the request thread? 
            // Actually, we are writing to the response body, so we must stay on this thread.
            // But VideoCapture might block.
            
            using (var capture = new VideoCapture(rtspUrl))
            {
                if (!capture.IsOpened())
                {
                    // Handle error
                    return;
                }

                using (var frame = new Mat())
                {
                    while (!HttpContext.RequestAborted.IsCancellationRequested)
                    {
                        if (capture.Read(frame) && !frame.Empty())
                        {
                            // Optional: Resize for performance
                            // Cv2.Resize(frame, frame, new Size(640, 480));

                            // Encode to JPEG
                            var imageBytes = frame.ImEncode(".jpg");

                            // Write boundary and headers
                            var header = $"--frame\r\nContent-Type: image/jpeg\r\nContent-Length: {imageBytes.Length}\r\n\r\n";
                            var headerBytes = System.Text.Encoding.ASCII.GetBytes(header);
                            
                            await Response.Body.WriteAsync(headerBytes, 0, headerBytes.Length);
                            await Response.Body.WriteAsync(imageBytes, 0, imageBytes.Length);
                            await Response.Body.WriteAsync(System.Text.Encoding.ASCII.GetBytes("\r\n"), 0, 2);

                            // Control frame rate (approx 15 FPS)
                            await Task.Delay(66); 
                        }
                        else
                        {
                            // If stream ends or fails, wait a bit and try again or break
                            await Task.Delay(1000);
                            if (!capture.IsOpened()) break;
                        }
                    }
                }
            }
        }
    }
}
