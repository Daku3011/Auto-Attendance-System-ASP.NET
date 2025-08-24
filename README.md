# Auto Attendance System using Facial Recognition

![Auto Attendance System Demo](https://i.imgur.com/your-demo-gif.gif) 
*--(Optional: You can create a GIF of your project working and upload it to a site like Imgur to replace this link)--*

This project is a fully functional web application built with **ASP.NET Core MVC** that automates the process of taking attendance using real-time facial recognition. The system captures video from a webcam, detects multiple faces in the frame, and marks attendance by matching them against a database of registered students.

---

## ✨ Features

-   **Real-time Face Capture:** Uses the browser's webcam to capture a live video feed.
-   **Multi-Face Recognition:** Detects and recognizes multiple registered students in a single photo.
-   **Database Integration:** Stores student details and reference photos in a SQL Server database using Entity Framework Core.
-   **Student Management (CRUD):** A dedicated section for administrators to add, edit, and view student profiles, including uploading their reference photos.
-   **Attendance Logging:** Records every successful attendance instance with a timestamp.
-   **Immediate Feedback:** The UI provides instant feedback on whether a student was recognized or not.

---

## 🛠️ Tech Stack

-   **Backend:** ASP.NET Core 8 MVC, C#
-   **Database:** SQL Server, Entity Framework Core 8
-   **Facial Recognition:** [OpenCvSharp](https://github.com/shimat/opencvsharp) (a .NET wrapper for OpenCV)
-   **Frontend:** HTML, CSS, Bootstrap, JavaScript
-   **Real-time Communication:** Fetch API (for sending image data from client to server)

---

## 🚀 Getting Started

Follow these instructions to get a copy of the project up and running on your local machine for development and testing purposes.

### Prerequisites

-   [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
-   [Visual Studio 2022](https://visualstudio.microsoft.com/vs/) with the "ASP.NET and web development" workload
-   [SQL Server](https://www.microsoft.com/en-us/sql-server/sql-server-downloads) (Express edition is sufficient)

### Installation

1.  **Clone the repository:**
    ```bash
    git clone [https://github.com/YOUR_USERNAME/YOUR_REPOSITORY_NAME.git](https://github.com/YOUR_USERNAME/YOUR_REPOSITORY_NAME.git)
    cd YOUR_REPOSITORY_NAME
    ```

2.  **Configure the Database Connection:**
    -   Open the `appsettings.json` file.
    -   Modify the `DefaultConnection` string to point to your local SQL Server instance.
    ```json
    "ConnectionStrings": {
      "DefaultConnection": "Server=localhost\\SQLEXPRESS;Database=AutoAttendanceDB;Trusted_Connection=True;Encrypt=False;"
    }
    ```

3.  **Apply Database Migrations:**
    -   Open the Package Manager Console in Visual Studio (`View` > `Other Windows` > `Package Manager Console`).
    -   Run the following command to create the database and its tables:
    ```powershell
    Update-Database
    ```

4.  **Run the Application:**
    -   Press `F5` or click the "Start" button in Visual Studio to launch the project.
    -   Your browser will open to the application's home page.

---

## 📋 Usage

1.  **Add Students:**
    -   Navigate to the `/Students` page.
    -   Click "Create New" to add students to the database.
    -   **Important:** You must upload a clear, front-facing reference photo for each student for the recognition to work.

2.  **Take Attendance:**
    -   Navigate to the main attendance page (`/DemoAttendance`).
    -   Click "Start Camera" and grant the browser permission to use your webcam.
    -   Position one or more registered students in the camera frame.
    -   Click "Capture & Mark Attendance".
    -   The system will process the image and display a success message with the names of the recognized students. The attendance log will be updated in real-time.

---

## 🔮 Future Improvements

-   [ ] Train the model with multiple images per student for higher accuracy.
-   [ ] Add a "Live Mode" that continuously scans the video feed without needing to click "Capture".
-   [ ] Implement user roles (Admin, Faculty) for better security.
-   [ ] Create detailed attendance reports that can be filtered by date or course.

---

## 📄 License

This project is licensed under the MIT License - see the [LICENSE.md](LICENSE.md) file for details.
