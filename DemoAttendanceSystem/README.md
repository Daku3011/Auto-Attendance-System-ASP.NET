# 🤖 Auto Attendance System — Technical Documentation

**DemoAttendanceSystem** is the solution directory containing the ASP.NET Core MVC project (`DemoAAS`) that powers the entire facial recognition attendance pipeline.

---

## ⚙️ Architecture Overview

```
                          ┌─────────────────────┐
                          │    Browser Client    │
                          │  (Camera / Upload)   │
                          └──────────┬──────────┘
                                     │  POST /Attendance/CaptureAttendance
                                     ▼
                          ┌─────────────────────┐
                          │ AttendanceController │
                          │   (ASP.NET MVC)      │
                          └──────────┬──────────┘
                                     │
                    ┌────────────────┴────────────────┐
                    ▼                                  ▼
          ┌──────────────────┐              ┌──────────────────┐
          │ FacialRecognition│              │  AttendanceHub   │
          │    Service       │              │   (SignalR)      │
          └───────┬──────────┘              └──────────────────┘
                  │
       ┌──────────┴──────────┐
       ▼                      ▼
┌──────────────┐    ┌──────────────────┐
│  YuNet ONNX  │    │ ArcFaceEmbedding │
│  (Detection) │    │    Service       │
└──────────────┘    │  (Recognition)   │
                    └──────────────────┘
                             │
                             ▼
                    ┌──────────────────┐
                    │   PostgreSQL     │
                    │  (EF Core 8)    │
                    └──────────────────┘
```

---

## 🧩 Core Components

### 1. Face Detection — YuNet

| Property | Value |
|:---|:---|
| **Model** | `face_detection_yunet.onnx` (227 KB) |
| **Framework** | OpenCvSharp4 `FaceDetectorYN` |
| **Capabilities** | Multi-face detection, rotation-robust, real-time speed |
| **Min Face Size** | Configurable (default: adaptive) |

### 2. Face Recognition — ArcFace (InsightFace)

| Property | Value |
|:---|:---|
| **Model** | `arcface.onnx` (130 MB, Git LFS) |
| **Runtime** | `Microsoft.ML.OnnxRuntime` |
| **Embedding Dim** | 512-D float vector |
| **Input Size** | 112 × 112 RGB |
| **Similarity** | Cosine Similarity (threshold ≥ 0.65) |
| **Normalization** | L2-normalized output |

### 3. Real-time Engine — SignalR

The `AttendanceHub` broadcasts the following events:

| Event | Payload | Description |
|:---|:---|:---|
| `AttendanceMarked` | `{ studentName, rollNo, timestamp }` | Fired when a student is successfully recognized |
| `RecognitionFailed` | `{ message }` | Fired when no match is found |

### 4. Data Models

```
Student ────────── StudentPhoto
  │                    │
  │ StudentId (PK)     │ PhotoId (PK)
  │ Name               │ StudentId (FK)
  │ RollNo             │ ImageData (byte[])
  │ Department          │ FaceEmbedding (float[]?)
  │                    │ UploadedAt
  │
  └──── Attendance
           │
           │ AttendanceId (PK)
           │ StudentId (FK)
           │ MarkedAt
           │ Classroom
           │ FacultyName
```

---

## 📦 NuGet Dependencies

| Package | Purpose |
|:---|:---|
| `Microsoft.EntityFrameworkCore` | ORM for PostgreSQL |
| `Npgsql.EntityFrameworkCore.PostgreSQL` | PostgreSQL EF Core provider |
| `Microsoft.AspNetCore.Identity.EntityFrameworkCore` | Authentication & authorization |
| `OpenCvSharp4` | Image processing & face detection |
| `OpenCvSharp4.Extensions` | Mat ↔ Bitmap conversion |
| `OpenCvSharp4.runtime.linux-x64` | Native OpenCV binaries (Linux) |
| `Microsoft.ML.OnnxRuntime` | ArcFace ONNX model inference |

---

## 🗄️ Database Setup

The project uses **PostgreSQL** with Entity Framework Core Code-First migrations.

### Connection String

Edit `appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=DemoAAS;Username=postgres;Password=your_password"
  }
}
```

### Apply Migrations

```bash
# From the DemoAttendanceSystem directory
dotnet ef database update --project DemoAAS
```

### Pending Migration: `AddFaceEmbedding`

If upgrading from the EigenFace version, you need to add the embedding column:

```bash
dotnet ef migrations add AddFaceEmbedding --project DemoAAS
dotnet ef database update --project DemoAAS
```

This adds the `FaceEmbedding` (`float[]?`) column to the `StudentPhotos` table. Existing photos will have their embeddings auto-generated the next time training is triggered.

---

## 🔧 Configuration

### Key Settings in `FacialRecognitionService.cs`

| Constant | Default | Description |
|:---|:---|:---|
| `SIMILARITY_THRESHOLD` | `0.65` | Minimum cosine similarity for a match |
| `_inputSize` (ArcFace) | `112` | Input image size for embedding extraction |
| Score filter (YuNet) | `> 0.9` | Minimum detection confidence |

### ONNX Model Paths

Models are resolved relative to the application's content root:

| Model | Default Path | Size |
|:---|:---|:---|
| YuNet | `face_detection_yunet.onnx` | 227 KB |
| ArcFace | `arcface.onnx` | 130 MB |

> ⚠️ The `arcface.onnx` file is tracked via **Git LFS**. Ensure you have `git lfs install` configured before cloning.

---

## 🏃 Running the Application

```bash
# Development mode
dotnet run --project DemoAAS

# With hot reload
dotnet watch run --project DemoAAS

# Production build
dotnet publish DemoAAS -c Release -o ./publish
```

### HTTPS / Camera Access

Browsers require HTTPS to access the camera. In development, the .NET dev certificate handles this automatically. For production, configure a proper TLS certificate.

---

## 🧪 Training Pipeline

When training is triggered (via the Attendance page or on startup):

1. **Load** all `StudentPhoto` records from the database
2. **Skip** photos that already have a `FaceEmbedding` stored
3. **Decode** the `ImageData` bytes into an OpenCV `Mat`
4. **Detect** faces using YuNet — take the region with the highest confidence
5. **Crop & resize** the face to 112×112
6. **Extract** a 512-D embedding using ArcFace ONNX
7. **L2-normalize** the embedding and save it to the `FaceEmbedding` column

---

## 🔍 Recognition Pipeline

When a captured image arrives at `/Attendance/CaptureAttendance`:

1. **Decode** the base64 image
2. **Detect** all faces using YuNet
3. **For each face:**
   - Crop and preprocess to 112×112
   - Generate 512-D embedding via ArcFace
   - Compare against all stored embeddings using Cosine Similarity
   - If best similarity ≥ 0.65 → match found
4. **Deduplicate** — skip if student was already marked recently
5. **Save** attendance records to database
6. **Broadcast** results via SignalR

---

## 📁 Directory Layout

```
DemoAttendanceSystem/
├── .gitignore
├── .gitattributes
├── README.md                        ← This file
└── DemoAAS/
    ├── Program.cs                   ← App entry, DI, middleware
    ├── DemoAAS.csproj               ← Project config & NuGet refs
    ├── appsettings.json             ← Connection strings & config
    │
    ├── Controllers/
    │   ├── AttendanceController.cs  ← Capture, recognize, export CSV
    │   ├── StudentsController.cs    ← CRUD + photo upload
    │   └── HomeController.cs        ← Landing page
    │
    ├── Services/
    │   ├── FacialRecognitionService.cs  ← Detection + recognition pipeline
    │   └── ArcFaceEmbeddingService.cs   ← ONNX inference wrapper
    │
    ├── Hubs/
    │   └── AttendanceHub.cs         ← SignalR real-time hub
    │
    ├── Models/
    │   ├── Student.cs
    │   ├── StudentPhoto.cs          ← Includes FaceEmbedding field
    │   ├── Attendance.cs
    │   ├── MarkAttendanceViewModel.cs
    │   └── ErrorViewModel.cs
    │
    ├── Data/
    │   └── ApplicationDbContext.cs
    │
    ├── Migrations/                  ← EF Core migration files
    │
    ├── Views/                       ← Razor views
    │   ├── Attendance/
    │   ├── Students/
    │   ├── Home/
    │   └── Shared/
    │
    ├── wwwroot/                     ← Static assets (CSS, JS, images)
    │
    ├── arcface.onnx                 ← ArcFace model (Git LFS)
    └── face_detection_yunet.onnx    ← YuNet model (Git LFS)
```

---

## 📄 License

MIT License — see the root [LICENSE.md](../LICENSE.md) for details.
]]>
