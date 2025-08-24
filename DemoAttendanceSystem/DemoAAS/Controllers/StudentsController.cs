using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using DemoAAS.Data;
using DemoAAS.Models;
using Microsoft.AspNetCore.Http;
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
            return View(await _context.Students.ToListAsync());
        }

        // GET: Students/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Students/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,RollNo,Department,Semester,Division")] Student student, IFormFile referenceImage)
        {
            if (ModelState.IsValid)
            {
                if (referenceImage != null && referenceImage.Length > 0)
                {
                    using (var memoryStream = new MemoryStream())
                    {
                        await referenceImage.CopyToAsync(memoryStream);
                        student.ReferenceImage = memoryStream.ToArray();
                    }
                }
                _context.Add(student);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(student);
        }

        // GET: Students/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();
            var student = await _context.Students.FindAsync(id);
            if (student == null) return NotFound();
            return View(student);
        }

        // POST: Students/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("StudentId,Name,RollNo,Department,Semester,Division")] Student student, IFormFile? referenceImage)
        {
            if (id != student.StudentId) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    if (referenceImage != null && referenceImage.Length > 0)
                    {
                        using (var memoryStream = new MemoryStream())
                        {
                            await referenceImage.CopyToAsync(memoryStream);
                            student.ReferenceImage = memoryStream.ToArray();
                        }
                    }
                    else
                    {
                        // Keep the old image if a new one isn't uploaded
                        var existingStudent = await _context.Students.AsNoTracking().FirstOrDefaultAsync(s => s.StudentId == id);
                        if (existingStudent != null)
                        {
                            student.ReferenceImage = existingStudent.ReferenceImage;
                        }
                    }
                    _context.Update(student);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!_context.Students.Any(e => e.StudentId == student.StudentId)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(student);
        }
    }
}