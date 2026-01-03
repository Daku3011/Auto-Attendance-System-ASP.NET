using DemoAAS.Data;
using DemoAAS.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenCvSharp;
using OpenCvSharp.Dnn;
using OpenCvSharp.Face;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DemoAAS.Services
{
    public interface IFacialRecognitionService
    {
        Task<List<Student>> RecognizeStudents(byte[] capturedImageBytes);
        Task TrainModelAsync();
    }

    public class FacialRecognitionService : IFacialRecognitionService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly CascadeClassifier _faceCascade;
        private readonly Net? _faceDetectorNet;
        private readonly Size _trainingImageSize = new Size(200, 200);
        private FaceRecognizer _recognizer;
        private bool _isTrained = false;
        private readonly SemaphoreSlim _trainingLock = new SemaphoreSlim(1, 1);
        
        // Detection parameters
        private const float CONFIDENCE_THRESHOLD = 0.5f;
        private const double RECOGNITION_THRESHOLD = 10000; // Increased for more lenient matching
        private const int MIN_FACE_SIZE = 80;

        public FacialRecognitionService(IServiceScopeFactory scopeFactory, IWebHostEnvironment environment)
        {
            _scopeFactory = scopeFactory;

            var cascadePath = ResolveModelPath("haarcascade_frontalface_default.xml", environment.ContentRootPath)
                ?? "haarcascade_frontalface_default.xml";
            _faceCascade = new CascadeClassifier(cascadePath);

            try
            {
                var dnnConfigPath = ResolveModelPath("opencv_face_detector.pbtxt", environment.ContentRootPath);
                var dnnWeightsPath = ResolveModelPath("opencv_face_detector_uint8.pb", environment.ContentRootPath);

                if (dnnConfigPath != null && dnnWeightsPath != null)
                {
                    _faceDetectorNet = CvDnn.ReadNetFromCaffe(dnnConfigPath, dnnWeightsPath);
                }
                else
                {
                    Console.WriteLine("Warning: DNN model files missing. Using Haar Cascade only.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Could not load DNN model: {ex.Message}. Using Haar Cascade only.");
            }
            
            _recognizer = EigenFaceRecognizer.Create();
        }

        private static string? ResolveModelPath(string fileName, string contentRootPath)
        {
            var candidates = new[]
            {
                Path.Combine(contentRootPath, fileName),
                Path.Combine(AppContext.BaseDirectory, fileName),
                Path.Combine(Directory.GetCurrentDirectory(), fileName)
            };

            return candidates.FirstOrDefault(File.Exists);
        }

        private Mat PreprocessFace(Mat faceMat)
        {
            Mat gray = faceMat.Channels() == 3 ? new Mat() : faceMat.Clone();
            if (faceMat.Channels() == 3)
            {
                Cv2.CvtColor(faceMat, gray, ColorConversionCodes.BGR2GRAY);
            }
            
            Mat equalized = new Mat();
            Cv2.EqualizeHist(gray, equalized);
            
            Mat resized = new Mat();
            Cv2.Resize(equalized, resized, _trainingImageSize);
            
            Mat blurred = new Mat();
            Cv2.GaussianBlur(resized, blurred, new Size(5, 5), 0);
            
            gray.Dispose();
            equalized.Dispose();
            resized.Dispose();
            
            return blurred;
        }

        private List<Rect> DetectFaces(Mat image)
        {
            var faces = new List<Rect>();
            
            if (_faceDetectorNet != null && !_faceDetectorNet.Empty())
            {
                try
                {
                    var blob = CvDnn.BlobFromImage(image, 1.0, new Size(300, 300), 
                        new Scalar(104, 177, 123), false, false);
                    
                    _faceDetectorNet.SetInput(blob);
                    var detections = _faceDetectorNet.Forward();
                    
                    var data = new float[detections.Total() * detections.ElemSize()];
                    System.Runtime.InteropServices.Marshal.Copy(detections.Data, data, 0, data.Length);
                    
                    for (int i = 0; i < detections.Size(2); i++)
                    {
                        float confidence = data[i * 7 + 2];
                        
                        if (confidence > CONFIDENCE_THRESHOLD)
                        {
                            int x1 = (int)(data[i * 7 + 3] * image.Width);
                            int y1 = (int)(data[i * 7 + 4] * image.Height);
                            int x2 = (int)(data[i * 7 + 5] * image.Width);
                            int y2 = (int)(data[i * 7 + 6] * image.Height);
                            
                            int width = x2 - x1;
                            int height = y2 - y1;
                            
                            if (width >= MIN_FACE_SIZE && height >= MIN_FACE_SIZE)
                            {
                                faces.Add(new Rect(x1, y1, width, height));
                            }
                        }
                    }
                    
                    blob.Dispose();
                    detections.Dispose();
                    
                    if (faces.Count > 0)
                        return faces;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"DNN detection failed: {ex.Message}. Falling back to Haar Cascade.");
                }
            }
            
            var haarFaces = _faceCascade.DetectMultiScale(
                image, 
                scaleFactor: 1.1, 
                minNeighbors: 5,
                flags: HaarDetectionTypes.ScaleImage,
                minSize: new Size(MIN_FACE_SIZE, MIN_FACE_SIZE)
            );
            
            return haarFaces.ToList();
        }

        private bool IsFaceQualityGood(Mat faceMat)
        {
            Mat gray = faceMat.Channels() == 3 ? new Mat() : faceMat.Clone();
            if (faceMat.Channels() == 3)
            {
                Cv2.CvtColor(faceMat, gray, ColorConversionCodes.BGR2GRAY);
            }
            
            Mat laplacian = new Mat();
            Cv2.Laplacian(gray, laplacian, MatType.CV_64F);
            
            Cv2.MeanStdDev(laplacian, out Scalar mean, out Scalar stddev);
            double variance = stddev.Val0 * stddev.Val0;
            
            gray.Dispose();
            laplacian.Dispose();
            
            return variance > 50;
        }

        public async Task TrainModelAsync()
        {
            await _trainingLock.WaitAsync();
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    
                    var allPhotos = await context.StudentPhotos
                        .Include(p => p.Student)
                        .ToListAsync();

                    if (!allPhotos.Any())
                    {
                        _isTrained = false;
                        Console.WriteLine("No training photos found");
                        return;
                    }

                    var labels = new List<int>();
                    var mats = new List<Mat>();

                    foreach (var photo in allPhotos)
                    {
                        using (var mat = Mat.FromImageData(photo.ImageData, ImreadModes.Color))
                        {
                            var facesInReference = DetectFaces(mat);
                            
                            if (facesInReference.Count > 0)
                            {
                                var largestFaceRect = facesInReference.OrderByDescending(f => f.Width * f.Height).First();
                                
                                using (var croppedFace = new Mat(mat, largestFaceRect))
                                {
                                    if (IsFaceQualityGood(croppedFace))
                                    {
                                        var processedFace = PreprocessFace(croppedFace);
                                        mats.Add(processedFace);
                                        labels.Add(photo.StudentId);
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Warning: Low quality photo for student {photo.Student.Name}");
                                    }
                                }
                            }
                        }
                    }

                    if (mats.Count >= 2)
                    {
                        _recognizer.Dispose();
                        _recognizer = EigenFaceRecognizer.Create();
                        _recognizer.Train(mats, labels);
                        _isTrained = true;
                        Console.WriteLine($"Model trained with {mats.Count} face samples from {allPhotos.GroupBy(p => p.StudentId).Count()} students");
                    }
                    else
                    {
                        _isTrained = false;
                        Console.WriteLine("Not enough quality face samples for training (need at least 2)");
                    }

                    foreach (var mat in mats)
                    {
                        mat.Dispose();
                    }
                }
            }
            finally
            {
                _trainingLock.Release();
            }
        }

        public async Task<List<Student>> RecognizeStudents(byte[] capturedImageBytes)
        {
            var recognizedStudents = new List<Student>();

            if (!_isTrained)
            {
                await TrainModelAsync();
            }

            if (!_isTrained)
            {
                Console.WriteLine("Model not trained yet");
                return recognizedStudents;
            }

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                using (var capturedMat = Mat.FromImageData(capturedImageBytes, ImreadModes.Color))
                {
                    var faces = DetectFaces(capturedMat);

                    if (faces.Count == 0)
                    {
                        return recognizedStudents;
                    }

                    foreach (var faceRect in faces)
                    {
                        using (var croppedFace = new Mat(capturedMat, faceRect))
                        {
                            if (!IsFaceQualityGood(croppedFace))
                            {
                                Console.WriteLine("Detected face but quality too low");
                                continue;
                            }

                            var processedFace = PreprocessFace(croppedFace);

                            try
                            {
                                _recognizer.Predict(processedFace, out int predictedLabel, out double confidence);

                                if (confidence < RECOGNITION_THRESHOLD)
                                {
                                    var student = await context.Students.FindAsync(predictedLabel);
                                    if (student != null && !recognizedStudents.Any(s => s.StudentId == student.StudentId))
                                    {
                                        recognizedStudents.Add(student);
                                        Console.WriteLine($"Recognized: {student.Name} (confidence: {confidence:F2})");
                                    }
                                }
                                else
                                {
                                    Console.WriteLine($"Face detected but not recognized (confidence: {confidence:F2})");
                                }
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Recognition error: {ex.Message}");
                            }
                            finally
                            {
                                processedFace.Dispose();
                            }
                        }
                    }
                }
            }

            return recognizedStudents;
        }

        public void Dispose()
        {
            _faceCascade?.Dispose();
            _faceDetectorNet?.Dispose();
            _recognizer?.Dispose();
            _trainingLock?.Dispose();
        }
    }
}
