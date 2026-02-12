# 🎓 Auto Attendance System

### AI-Powered Facial Recognition Attendance for Modern Classrooms

[![ASP.NET Core](https://img.shields.io/badge/ASP.NET%20Core-8.0-512BD4?style=for-the-badge&logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-4169E1?style=for-the-badge&logo=postgresql&logoColor=white)](https://www.postgresql.org/)
[![OpenCV](https://img.shields.io/badge/OpenCV-5C3EE8?style=for-the-badge&logo=opencv&logoColor=white)](https://opencv.org/)
[![ONNX Runtime](https://img.shields.io/badge/ONNX%20Runtime-007808?style=for-the-badge&logo=onnx&logoColor=white)](https://onnxruntime.ai/)
[![SignalR](https://img.shields.io/badge/SignalR-Real--time-FF4088?style=for-the-badge)](https://learn.microsoft.com/aspnet/core/signalr/)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow?style=for-the-badge)](LICENSE.md)

---

A production-ready web application that automates classroom attendance using **deep-learning face recognition**. Capture a photo, detect every face in the frame, and instantly mark attendance — all in real time.

---

## 🧠 How It Works

```
┌─────────────┐     ┌──────────────┐     ┌───────────────────┐     ┌────────────────┐
│  📸 Camera  │────▶│  YuNet ONNX  │────▶│  ArcFace (ONNX)   │────▶│ 🔍 Cosine      │
│  Capture    │     │  Detection   │     │  512-D Embedding   │     │    Similarity   │
└─────────────┘     └──────────────┘     └───────────────────┘     └────────┬───────┘
                                                                            │
                         ┌──────────────────────────────────────────────────┘
                         ▼
               ┌───────────────────┐     ┌─────────────────┐     ┌─────────────────┐
               │ 🎯 Match Against  │────▶│ ✅ Mark          │────▶│ 📡 SignalR       │
               │    Student DB     │     │    Attendance     │     │    Broadcast     │
               └───────────────────┘     └─────────────────┘     └─────────────────┘
```

1. **Capture** — A photo is taken from a browser webcam or an RTSP IP camera.
2. **Detect** — The YuNet ONNX model locates every face in the frame.
3. **Embed** — Each cropped face is passed through **ArcFace** (InsightFace) to produce a 512-dimensional embedding.
4. **Match** — Embeddings are compared against stored student embeddings using **Cosine Similarity** (threshold ≥ 0.65).
5. **Record** — Attendance is saved to the database with duplicate-prevention logic.
6. **Broadcast** — SignalR pushes the result instantly to every connected dashboard.

---

## ✨ Features

| Category | Feature |
|:---|:---|
| **🤖 AI Engine** | ArcFace (InsightFace) 512-D embeddings via ONNX Runtime |
| **👁️ Face Detection** | YuNet ONNX — fast, multi-face, rotation-robust |
| **⚡ Real-time Updates** | SignalR WebSocket broadcasts for live attendance feed |
| **🔐 Security** | ASP.NET Core Identity · Role-based access · CSRF protection |
| **📊 Reports** | Filter by Classroom / Faculty / Date Range · CSV export |
| **📹 Capture Sources** | Browser webcam + RTSP IP camera support |
| **🧑‍🎓 Student CRUD** | Full student profile management with multi-photo upload |
| **🔄 Auto Training** | Background model retraining when new photos are added |
| **🛡️ Duplicate Guard** | Configurable time window to prevent re-marking |

---

## 🛠️ Technology Stack

| Layer | Technology |
|:---|:---|
| **Framework** | ASP.NET Core 8.0 MVC (C#) |
| **Database** | PostgreSQL · Entity Framework Core 8 |
| **Face Detection** | YuNet ONNX model |
| **Face Recognition** | ArcFace (InsightFace) ONNX model via `Microsoft.ML.OnnxRuntime` |
| **Image Processing** | OpenCvSharp4 (.NET wrapper for OpenCV) |
| **Real-time** | ASP.NET Core SignalR |
| **Auth** | ASP.NET Core Identity |
| **Frontend** | Bootstrap 5 · Custom glassmorphism CSS · JavaScript |
| **Version Control** | Git · Git LFS (for ONNX model files) |

---

## � Project Structure

```
Auto-Attendance-System-ASP.NET/
├── readme.md                       ← You are here
├── Report.pdf                      ← Project report
└── DemoAttendanceSystem/
    ├── .gitignore
    ├── .gitattributes               ← Git LFS tracking rules
    ├── README.md                    ← Technical documentation
    └── DemoAAS/
        ├── Controllers/
        │   ├── AttendanceController.cs    ← Capture, recognize, mark
        │   ├── StudentsController.cs      ← Student CRUD + photo upload
        │   └── HomeController.cs          ← Landing page
        ├── Services/
        │   ├── FacialRecognitionService.cs ← Core recognition pipeline
        │   └── ArcFaceEmbeddingService.cs  ← ONNX inference wrapper
        ├── Hubs/
        │   └── AttendanceHub.cs           ← SignalR real-time hub
        ├── Models/
        │   ├── Student.cs
        │   ├── StudentPhoto.cs            ← Includes FaceEmbedding field
        │   └── Attendance.cs
        ├── Data/
        │   └── ApplicationDbContext.cs
        ├── Views/                         ← Razor views (MVC)
        ├── arcface.onnx                   ← ArcFace model (Git LFS)
        ├── face_detection_yunet.onnx      ← YuNet model (Git LFS)
        └── Program.cs
```

---

## 🚀 Getting Started

### Prerequisites

| Requirement | Version |
|:---|:---|
| .NET SDK | 8.0+ |
| PostgreSQL | 14+ |
| Git LFS | 3.0+ (for cloning ONNX models) |

### Installation

```bash
# 1. Install Git LFS (required for ONNX model files)
git lfs install

# 2. Clone the repository
git clone https://github.com/Daku3011/Auto-Attendance-System-ASP.NET.git
cd Auto-Attendance-System-ASP.NET/DemoAttendanceSystem
```

**3. Configure the database** — Edit `DemoAAS/appsettings.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Database=DemoAAS;Username=postgres;Password=your_password"
}
```

```bash
# 4. Apply database migrations
dotnet ef database update --project DemoAAS

# 5. Run the application
dotnet run --project DemoAAS
```

> 🌐 Open your browser at `https://localhost:5001` (or the port shown in the terminal).

---

## 📋 Usage Guide

### Step 1 — Register Students
Navigate to **Students → Create New**. Enter the student's details and upload **3–5 clear, front-facing photos** per student. The system will automatically extract and store face embeddings.

### Step 2 — Take Attendance
Go to the **Attendance** page. Click **Start Camera**, position students in the frame, and hit **Capture & Mark Attendance**. The system detects all faces, matches them, and logs attendance instantly.

### Step 3 — Monitor in Real Time
Recognized students appear in the **live sidebar** via SignalR — no page refresh needed. Connected dashboards update automatically.

### Step 4 — Export Reports
Visit **Attendance Records** → filter by Classroom, Faculty, or Date Range → click **Export CSV**.

---

## 🔮 Roadmap

- [x] ArcFace (InsightFace) deep-learning embeddings
- [x] SignalR real-time attendance broadcasts
- [x] ASP.NET Core Identity authentication
- [ ] Continuous "Live Mode" scanning without manual capture
- [ ] Attendance analytics dashboard with charts
- [ ] Docker containerization for one-command deployment
- [ ] Mobile-responsive PWA for tablet kiosks

---

## 🤝 Contributing

Contributions are welcome! Please open an issue first to discuss what you'd like to change.

1. Fork the repository
2. Create your feature branch (`git checkout -b feature/amazing-feature`)
3. Commit your changes (`git commit -m 'Add amazing feature'`)
4. Push to the branch (`git push origin feature/amazing-feature`)
5. Open a Pull Request

---