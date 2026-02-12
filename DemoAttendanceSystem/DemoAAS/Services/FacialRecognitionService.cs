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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.ML.OnnxRuntime;

namespace DemoAAS.Services
{
    public interface IFacialRecognitionService
    {
        Task<List<Student>> RecognizeStudents(byte[] capturedImageBytes);
        Task TrainModelAsync();
        void TriggerTraining();
    }

    public class FacialRecognitionService : IFacialRecognitionService, IDisposable
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private Net? _faceDetectorNet;
        private readonly Size _trainingImageSize = new Size(112, 112); // ArcFace input size
        private IArcFaceEmbeddingService _embeddingService;
        private bool _isTrained = false;
        private readonly SemaphoreSlim _trainingLock = new SemaphoreSlim(1, 1);
        
        // Detection and Recognition parameters
        private const float CONFIDENCE_THRESHOLD = 0.6f;
        private const double SIMILARITY_THRESHOLD = 0.65; // Cosine similarity threshold (0.5-0.8 range)
        private const int MIN_FACE_SIZE = 60;
        private const string YUNET_MODEL_FILE = "face_detection_yunet.onnx";
        private const string ARCFACE_MODEL_FILE = "arcface.onnx";

        public FacialRecognitionService(IServiceScopeFactory scopeFactory, IWebHostEnvironment environment)
        {
            _scopeFactory = scopeFactory;
            InitializeModels(environment.ContentRootPath);
        }

        private void InitializeModels(string contentRootPath)
        {
            InitializeYuNet(contentRootPath);
            var arcfacePath = ResolveModelPath(ARCFACE_MODEL_FILE, contentRootPath);
            if (arcfacePath != null)
            {
                _embeddingService = new ArcFaceEmbeddingService(arcfacePath);
                Console.WriteLine("ArcFace model loaded successfully.");
            }
            else
            {
                Console.WriteLine($"Error: {ARCFACE_MODEL_FILE} not found.");
            }
        }

        private bool _trainingRequested = false;
        public void TriggerTraining()
        {
            if (!_trainingRequested)
            {
                _trainingRequested = true;
                Task.Run(async () => {
                    await Task.Delay(5000); // Wait for photos to be fully saved
                    await TrainModelAsync();
                    _trainingRequested = false;
                });
            }
        }

        private void InitializeYuNet(string contentRootPath)
        {
            try
            {
                var modelPath = ResolveModelPath(YUNET_MODEL_FILE, contentRootPath);
                if (modelPath != null)
                {
                    _faceDetectorNet = CvDnn.ReadNetFromOnnx(modelPath);
                    Console.WriteLine("YuNet ONNX model loaded successfully.");
                }
                else
                {
                    Console.WriteLine($"Error: {YUNET_MODEL_FILE} not found. Face detection will fail.");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error initializing YuNet: {ex.Message}");
            }
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

        private List<(Rect BBox, float[] Landmarks, float Confidence)> DetectFacesYuNet(Mat image)
        {
            var detectedFaces = new List<(Rect, float[], float)>();
            
            if (_faceDetectorNet == null) return detectedFaces;

            // YuNet prefers sizes that are multiples of 32. 
            // 320x320 is a robust standard that avoids dimension mismatch errors in feature maps.
            Size inputSize = new Size(320, 320);
            float scaleX = (float)image.Width / inputSize.Width;
            float scaleY = (float)image.Height / inputSize.Height;

            using var blob = CvDnn.BlobFromImage(image, 1.0, inputSize, new Scalar(0, 0, 0), true, false);
            
            _faceDetectorNet.SetInput(blob);
            using var faces = _faceDetectorNet.Forward();

            if (faces.Empty()) return detectedFaces;

            // YuNet output is N rows of 15 values
            for (int i = 0; i < faces.Rows; i++)
            {
                // Multiply coordinates by our scale factors to get original image coordinates
                float x = faces.At<float>(i, 0) * scaleX;
                float y = faces.At<float>(i, 1) * scaleY;
                float w = faces.At<float>(i, 2) * scaleX;
                float h = faces.At<float>(i, 3) * scaleY;
                float conf = faces.At<float>(i, 14);

                if (conf >= CONFIDENCE_THRESHOLD && w >= MIN_FACE_SIZE && h >= MIN_FACE_SIZE)
                {
                    float[] landmarks = new float[10];
                    for (int j = 0; j < 10; j++)
                    {
                        float scale = (j % 2 == 0) ? scaleX : scaleY;
                        landmarks[j] = faces.At<float>(i, 4 + j) * scale;
                    }
                    detectedFaces.Add((new Rect((int)x, (int)y, (int)w, (int)h), landmarks, conf));
                }
            }
            
            return detectedFaces;
        }

        private float CalculateFaceQualityScore(Rect bbox, float[] landmarks, Mat faceMat)
        {
            // 1. Frontality Score (Eye symmetry relative to nose)
            // Landmarks: 0:RE, 1:RE_Y, 2:LE, 3:LE_Y, 4:NT, 5:NT_Y ...
            float re_x = landmarks[0];
            float le_x = landmarks[2];
            float nt_x = landmarks[4];
            
            float dist_re_nt = Math.Abs(re_x - nt_x);
            float dist_le_nt = Math.Abs(le_x - nt_x);
            float total_eye_dist = Math.Abs(re_x - le_x);
            
            float symmetry = 1.0f - (Math.Abs(dist_re_nt - dist_le_nt) / (total_eye_dist + 1e-6f));
            
            // 2. Resolution Score (IPD - Inter-Pupillary Distance)
            float ipd = (float)Math.Sqrt(Math.Pow(landmarks[0] - landmarks[2], 2) + Math.Pow(landmarks[1] - landmarks[3], 2));
            float resolutionScore = Math.Min(ipd / 100.0f, 1.0f); // Normalize IPD relative to 100px

            // 3. Clarity Score (Laplacian Variance)
            float clarityScore = 0;
            using (Mat gray = new Mat())
            {
                Cv2.CvtColor(faceMat, gray, ColorConversionCodes.BGR2GRAY);
                using (Mat laplacian = new Mat())
                {
                    Cv2.Laplacian(gray, laplacian, MatType.CV_64F);
                    Cv2.MeanStdDev(laplacian, out Scalar mean, out Scalar stddev);
                    double variance = stddev.Val0 * stddev.Val0;
                    clarityScore = (float)Math.Min(variance / 100.0, 1.0); // Normalize variance
                }
            }

            // Weighted Average
            return (symmetry * 0.4f) + (resolutionScore * 0.3f) + (clarityScore * 0.3f);
        }

        public async Task TrainModelAsync()
        {
            await _trainingLock.WaitAsync();
            try
            {
                using (var scope = _scopeFactory.CreateScope())
                {
                    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                    var allPhotos = await context.StudentPhotos.Include(p => p.Student).ToListAsync();

                    if (!allPhotos.Any())
                    {
                        _isTrained = false;
                        return;
                    }

                    var labels = new List<int>();
                    var mats = new List<Mat>();

                    foreach (var photo in allPhotos)
                    {
                        if (photo.FaceEmbedding != null) continue; // Already processed

                        using var mat = Mat.FromImageData(photo.ImageData, ImreadModes.Color);
                        var faces = DetectFacesYuNet(mat);
                        
                        if (faces.Count > 0)
                        {
                            var bestFace = faces
                                .Select(f => new { Data = f, Score = CalculateFaceQualityScore(f.BBox, f.Landmarks, new Mat(mat, f.BBox)) })
                                .OrderByDescending(f => f.Score)
                                .First();

                            using var croppedFace = new Mat(mat, bestFace.Data.BBox);
                            photo.FaceEmbedding = _embeddingService.GetEmbedding(croppedFace);
                        }
                    }

                    await context.SaveChangesAsync();
                    _isTrained = true;
                    Console.WriteLine("Embeddings updated in database.");
                }
            }
            finally
            {
                _trainingLock.Release();
            }
        }

        private float CalculateCosineSimilarity(float[] v1, float[] v2)
        {
            if (v1 == null || v2 == null || v1.Length != v2.Length) return 0;
            float dotProduct = 0;
            for (int i = 0; i < v1.Length; i++) dotProduct += v1[i] * v2[i];
            // Since embeddings are pre-normalized in ArcFaceEmbeddingService, Dot Product IS Cosine Similarity
            return dotProduct;
        }

        public async Task<List<Student>> RecognizeStudents(byte[] capturedImageBytes)
        {
            var recognizedStudents = new List<Student>();

            if (!_isTrained) await TrainModelAsync();

            using (var scope = _scopeFactory.CreateScope())
            {
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var dbPhotos = await context.StudentPhotos.Where(p => p.FaceEmbedding != null).Include(p => p.Student).ToListAsync();

                using (var capturedMat = Mat.FromImageData(capturedImageBytes, ImreadModes.Color))
                {
                    var detections = DetectFacesYuNet(capturedMat);

                    foreach (var det in detections)
                    {
                        using var crop = new Mat(capturedMat, det.BBox);
                        var embedding = _embeddingService.GetEmbedding(crop);

                        double bestSim = 0;
                        Student? bestMatch = null;

                        foreach (var dbPhoto in dbPhotos)
                        {
                            float sim = CalculateCosineSimilarity(embedding, dbPhoto.FaceEmbedding!);
                            if (sim > bestSim && sim > SIMILARITY_THRESHOLD)
                            {
                                bestSim = sim;
                                bestMatch = dbPhoto.Student;
                            }
                        }

                        if (bestMatch != null && !recognizedStudents.Any(s => s.StudentId == bestMatch.StudentId))
                        {
                            recognizedStudents.Add(bestMatch);
                            Console.WriteLine($"Recognized: {bestMatch.Name} (Sim: {bestSim:F3})");
                        }
                    }
                }
            }

            return recognizedStudents;
        }

        public void Dispose()
        {
            _faceDetectorNet?.Dispose();
            (_embeddingService as IDisposable)?.Dispose();
            _trainingLock?.Dispose();
        }
    }
}
