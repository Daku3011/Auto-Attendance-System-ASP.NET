using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace DemoAAS.Services
{
    public interface IArcFaceEmbeddingService
    {
        float[] GetEmbedding(Mat faceMat);
    }

    public class ArcFaceEmbeddingService : IArcFaceEmbeddingService, IDisposable
    {
        private readonly InferenceSession _session;
        private readonly string _inputName;
        private readonly int _inputSize = 112;

        public ArcFaceEmbeddingService(string modelPath)
        {
            _session = new InferenceSession(modelPath);
            _inputName = _session.InputMetadata.Keys.First();
        }

        public float[] GetEmbedding(Mat faceMat)
        {
            using var resized = new Mat();
            Cv2.Resize(faceMat, resized, new Size(_inputSize, _inputSize));
            
            // ArcFace usually expects RGB [0, 255] or normalized. 
            // Most ArcFace ONNX models from HuggingFace/InsightFace expect (1, 3, 112, 112) and RGB.
            using var rgb = new Mat();
            Cv2.CvtColor(resized, rgb, ColorConversionCodes.BGR2RGB);

            var tensor = ConvertMatToTensor(rgb);
            var inputs = new List<NamedOnnxValue>
            {
                NamedOnnxValue.CreateFromTensor(_inputName, tensor)
            };

            using var results = _session.Run(inputs);
            var output = results.First().AsEnumerable<float>().ToArray();

            // Normalize embedding for Cosine Similarity (which becomes Dot Product after normalization)
            return Normalize(output);
        }

        private DenseTensor<float> ConvertMatToTensor(Mat mat)
        {
            var tensor = new DenseTensor<float>(new[] { 1, 3, _inputSize, _inputSize });
            mat.GetGenericIndexer<Vec3b>().GetData(out Vec3b[] data);

            for (int y = 0; y < _inputSize; y++)
            {
                for (int x = 0; x < _inputSize; x++)
                {
                    var color = data[y * _inputSize + x];
                    tensor[0, 0, y, x] = color.Item0; // R
                    tensor[0, 1, y, x] = color.Item1; // G
                    tensor[0, 2, y, x] = color.Item2; // B
                }
            }
            return tensor;
        }

        private float[] Normalize(float[] v)
        {
            double norm = Math.Sqrt(v.Sum(x => (double)x * x));
            if (norm < 1e-6) return v;
            return v.Select(x => (float)(x / norm)).ToArray();
        }

        public void Dispose()
        {
            _session?.Dispose();
        }
    }
}
