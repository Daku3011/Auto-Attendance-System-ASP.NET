using DemoAAS.Data;
using DemoAAS.Models;
using Microsoft.EntityFrameworkCore;
using OpenCvSharp;
using OpenCvSharp.Face;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DemoAAS.Services
{
    public interface IFacialRecognitionService
    {
        Task<List<Student>> RecognizeStudents(byte[] capturedImageBytes);
    }

    public class FacialRecognitionService : IFacialRecognitionService
    {
        private readonly ApplicationDbContext _context;
        private readonly CascadeClassifier _faceCascade;
        private readonly Size _trainingImageSize = new Size(200, 200);

        public FacialRecognitionService(ApplicationDbContext context)
        {
            _context = context;
            _faceCascade = new CascadeClassifier("haarcascade_frontalface_default.xml");
        }

        public async Task<List<Student>> RecognizeStudents(byte[] capturedImageBytes)
        {
            var studentsWithImages = await _context.Students
                .Where(s => s.ReferenceImage != null)
                .ToListAsync();

            if (!studentsWithImages.Any())
            {
                return new List<Student>();
            }

            var recognizer = LBPHFaceRecognizer.Create();

            var labels = new List<int>();
            var mats = new List<Mat>();

            // --- CORE LOGIC CHANGE: Process reference images before training ---
            foreach (var student in studentsWithImages)
            {
                using (var mat = Mat.FromImageData(student.ReferenceImage, ImreadModes.Grayscale))
                {
                    // 1. Detect faces in the reference image
                    var facesInReference = _faceCascade.DetectMultiScale(mat);
                    if (facesInReference.Length > 0)
                    {
                        // 2. Assume the largest face is the student's and crop it
                        var largestFaceRect = facesInReference.OrderByDescending(f => f.Width * f.Height).First();
                        using (var croppedFace = new Mat(mat, largestFaceRect))
                        {
                            // 3. Resize the cropped face to a standard size for training
                            var resizedMat = croppedFace.Resize(_trainingImageSize);
                            mats.Add(resizedMat);
                            labels.Add(student.StudentId);
                        }
                    }
                    // If no face is found in the reference image, it will be skipped.
                }
            }

            // Ensure we have something to train before proceeding
            if (!mats.Any())
            {
                return new List<Student>();
            }

            recognizer.Train(mats, labels);

            var capturedMat = Mat.FromImageData(capturedImageBytes, ImreadModes.Grayscale);
            var faces = _faceCascade.DetectMultiScale(capturedMat, 1.1, 5, HaarDetectionTypes.ScaleImage);

            if (faces.Length == 0)
            {
                return new List<Student>();
            }

            var recognizedStudents = new List<Student>();

            foreach (var faceRect in faces)
            {
                using (var capturedFaceMat = new Mat(capturedMat, faceRect))
                {
                    var resizedCapturedFace = capturedFaceMat.Resize(_trainingImageSize);
                    recognizer.Predict(resizedCapturedFace, out int predictedLabel, out double confidence);

                    if (confidence < 70)
                    {
                        var student = await _context.Students.FindAsync(predictedLabel);
                        if (student != null && !recognizedStudents.Any(s => s.StudentId == student.StudentId))
                        {
                            recognizedStudents.Add(student);
                        }
                    }
                }
            }

            // Dispose of Mats to prevent memory leaks
            foreach (var mat in mats)
            {
                mat.Dispose();
            }
            capturedMat.Dispose();
            recognizer.Dispose();

            return recognizedStudents;
        }
    }
}
