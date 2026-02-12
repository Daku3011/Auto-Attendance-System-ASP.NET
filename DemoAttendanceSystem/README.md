# 🤖 Auto Attendance System (ASP.NET Core)

A sophisticated, real-time facial recognition-based attendance management system designed for modern classrooms and offices.

![Version](https://img.shields.io/badge/version-1.0.0-blue)
![ASP.NET](https://img.shields.io/badge/platform-ASP.NET%20Core-purple)
![OpenCV](https://img.shields.io/badge/engine-OpenCVSharp-green)

---

## 🌟 Key Features

### 1. 🤖 Advanced Facial Recognition
- **Face Detection**: Powered by the modern **YuNet** ONNX model for high-speed, multi-face detection.
- **Recognition Algorithm**: Uses the **EigenFace** algorithm with adaptive quality scoring.
- **Dynamic Training**: Automatically retrains the internal model in the background when new students or photos are registered.

### 2. ⚡ Real-time Feedback
- **SignalR Integration**: Broadcasts recognition events instantly to the dashboard.
- **Live Sidebar**: Shows a history of students recognized in the current session without page reloads.
- **High-Resolution Capture**: Optimized camera stream for improved accuracy in varying light conditions.

### 3. 🔐 Enterprise-Grade Security
- **Secure Management**: ASP.NET Core Identity integration with role-based access.
- **Protected Routes**: Critical student data and settings are secured with `[Authorize]` attributes.
- **CSRF Protection**: Full Antiforgery token implementation for all AJAX and form submissions.

### 4. 📊 Reporting & Analytics
- **Advanced Filtering**: Search attendance records by Classroom, Faculty name, and Date Range.
- **CSV Export**: Download comprehensive attendance reports with a single click.
- **Duplicate Prevention**: Intelligently prevents marking a student multiple times within a configurable window.

### 5. 📹 Flexible Capture Sources
- **Web-based Capture**: Integrated browser camera support.
- **RTSP Streaming**: Built-in support for pulling feeds from external IP cameras.

---

## 🛠️ Technology Stack

| Layer | Technology |
| :--- | :--- |
| **Development Framework** | ASP.NET Core 8.0 / 9.0 |
| **Database** | PostgreSQL (Entity Framework Core) |
| **Deep Learning** | OpenCV / OpenCvSharp4 / YuNet ONNX |
| **Real-time Engine** | SignalR |
| **Authentication** | ASP.NET Core Identity |
| **Styling** | Bootstrap 5 + Custom Glassmorphism UI |

---

## 🚀 Getting Started

### Prerequisites
- .NET 8.0 SDK or higher
- PostgreSQL Database
- OpenCV Runtimes (automatically handled via NuGet)

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/[your-username]/Auto-Attendance-System.git
   cd Auto-Attendance-System
   ```

2. **Configure the Database**
   Update the connection string in `appsettings.json`:
   ```json
   "ConnectionStrings": {
     "DefaultConnection": "Host=localhost;Database=DemoAAS;Username=postgres;Password=your_password"
   }
   ```

3. **Run Migrations**
   ```bash
   dotnet ef database update
   ```

4. **Run the Application**
   ```bash
   dotnet run --project DemoAAS
   ```

---

## 📈 Usage Guide

1. **Registration**: Go to the **Students** tab, register a student, and upload at least 3-5 high-quality photos.
2. **Attendance**: Navigate to the **Home** page, start the camera, and position the face within the frame.
3. **Real-time Monitoring**: Once recognized, the student's name will instantly appear in the "Recent Attendance" sidebar.
4. **Reports**: Use the **Attendance Records** tab to filter and export data as needed.

---

## 🗺️ Roadmap

- [ ] Deep Learning embedding-based recognition (ArcFace/FaceNet).



