using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoAAS.Data;
using DemoAAS.Models;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Linq;

namespace DemoAAS.Controllers
{
    public class StudentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StudentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Students
        public async Task<IActionResult> Index()
        {
            var students = await _context.Students
                .Include(s => s.Photos)
                .ToListAsync();
            return View(students);
        }

        // GET: Students/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,RollNo,Department,Semester,Division")] Student student, List<IFormFile> photos)
        {
            if (ModelState.IsValid)
            {
                // Save student first to get StudentId
                _context.Add(student);
                await _context.SaveChangesAsync();
                
                // Process multiple photos (limit to 5)
                if (photos != null && photos.Count > 0)
                {
                    int photoCount = 0;
                    foreach (var photo in photos.Take(5)) // Limit to 5 photos
                    {
                        if (photo != null && photo.Length > 0)
                        {
                            using (var memoryStream = new MemoryStream())
                            {
                                await photo.CopyToAsync(memoryStream);
                                var imageData = memoryStream.ToArray();
                                
                                // Save first photo as ReferenceImage for backward compatibility
                                if (photoCount == 0)
                                {
                                    student.ReferenceImage = imageData;
                                }
                                
                                // Add to StudentPhotos table
                                var studentPhoto = new StudentPhoto
                                {
                                    StudentId = student.StudentId,
                                    ImageData = imageData,
                                    UploadedAt = DateTime.UtcNow // Use UTC for PostgreSQL
                                };
                                _context.StudentPhotos.Add(studentPhoto);
                                photoCount++;
                            }
                        }
                    }
                    await _context.SaveChangesAsync();
                }
                
                return RedirectToAction(nameof(Index));
            }
            return View(student);
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            
            var student = await _context.Students
                .Include(s => s.Photos)
                .FirstOrDefaultAsync(s => s.StudentId == id);
                
            if (student == null) return NotFound();
            return View(student);
        }

        // POST: Students/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StudentId,Name,RollNo,Department,Semester,Division")] Student student, List<IFormFile>? newPhotos)
        {
            if (id != student.StudentId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    // Get existing student data
                    var existingStudent = await _context.Students
                        .AsNoTracking()
                        .FirstOrDefaultAsync(s => s.StudentId == id);
                    
                    if (existingStudent != null)
                    {
                        student.ReferenceImage = existingStudent.ReferenceImage;
                    }
                    
                    _context.Update(student);
                    
                    // Add new photos if provided
                    if (newPhotos != null && newPhotos.Count > 0)
                    {
                        var currentPhotoCount = await _context.StudentPhotos
                            .CountAsync(p => p.StudentId == id);
                        
                        var remainingSlots = 5 - currentPhotoCount;
                        
                        foreach (var photo in newPhotos.Take(remainingSlots))
                        {
                            if (photo != null && photo.Length > 0)
                            {
                                using (var memoryStream = new MemoryStream())
                                {
                                    await photo.CopyToAsync(memoryStream);
                                    var studentPhoto = new StudentPhoto
                                    {
                                        StudentId = id,
                                        ImageData = memoryStream.ToArray(),
                                        UploadedAt = DateTime.UtcNow // Use UTC for PostgreSQL
                                    };
                                    _context.StudentPhotos.Add(studentPhoto);
                                }
                            }
                        }
                    }
                    
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Students.Any(e => e.StudentId == student.StudentId)) 
                        return NotFound();
                    else 
                        throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(student);
        }

        // GET: Students/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();
            
            var student = await _context.Students
                .Include(s => s.Photos)
                .FirstOrDefaultAsync(s => s.StudentId == id);
                
            if (student == null) return NotFound();
            return View(student);
        }
        
        // POST: Students/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var student = await _context.Students
                .Include(s => s.Photos)
                .FirstOrDefaultAsync(s => s.StudentId == id);
                
            if (student != null)
            {
                // Delete all associated photos first
                _context.StudentPhotos.RemoveRange(student.Photos);
                
                // Delete the student
                _context.Students.Remove(student);
                await _context.SaveChangesAsync();
            }
            
            return RedirectToAction(nameof(Index));
        }

        // POST: Students/DeletePhoto/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePhoto(int photoId, int studentId)
        {
            var photo = await _context.StudentPhotos.FindAsync(photoId);
            if (photo != null)
            {
                _context.StudentPhotos.Remove(photo);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Edit), new { id = studentId });
        }
    }
}