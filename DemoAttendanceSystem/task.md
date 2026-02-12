<![CDATA[# 📋 Auto Attendance System — Development Roadmap

## ✅ Completed

- [x] Face detection using YuNet ONNX model
- [x] Face recognition using ArcFace (InsightFace) CNN with 512-D embeddings
- [x] Cosine Similarity matching with configurable threshold
- [x] Student CRUD with multi-photo upload
- [x] Attendance logging with duplicate prevention
- [x] SignalR real-time attendance broadcasts
- [x] ASP.NET Core Identity authentication
- [x] Attendance records filtering (Classroom, Faculty, Date Range)
- [x] CSV export for attendance reports
- [x] Git LFS for ONNX model files
- [x] `.gitignore` cleanup (removed build artifacts from tracking)

---

## 📹 Phase 1 — CCTV / IP Camera Integration

- [ ] Create `RtspCameraService` (`IHostedService` background service)
  - [ ] Read RTSP stream using `OpenCvSharp.VideoCapture`
  - [ ] Frame sampling (1 frame/sec to avoid overload)
  - [ ] Auto-reconnection on stream drop
- [ ] Multi-camera support
  - [ ] Camera config section in `appsettings.json`
  - [ ] Camera label mapping (e.g., "Room 101", "Main Gate")
- [ ] Feed RTSP frames into existing `FacialRecognitionService`
- [ ] Camera Management UI (Add / Remove / Configure cameras from browser)
- [ ] Live Preview Panel (show bounding boxes on detected faces)

---

## 🛡️ Phase 2 — Anti-Spoofing & Security

- [ ] Liveness detection (prevent photo-of-a-photo attacks)
  - [ ] Eye blink detection
  - [ ] Head movement challenge
- [ ] Rate limiting on recognition API endpoints
- [ ] Audit logging for admin actions
- [ ] Session timeout and inactivity auto-logout

---

## 📊 Phase 3 — Analytics Dashboard

- [ ] Attendance summary dashboard (daily / weekly / monthly charts)
- [ ] Per-student attendance percentage timeline
- [ ] Absentee alerts (email notification after X consecutive absences)
- [ ] Department-wise attendance comparison
- [ ] Heatmap showing peak attendance hours

---

## ⚡ Phase 4 — Performance & Scalability

- [ ] GPU acceleration using `Microsoft.ML.OnnxRuntime.Gpu`
- [ ] Batch inference for multi-camera setups
- [ ] Embedding caching (in-memory for hot-path lookups)
- [ ] Background queue for recognition requests (`System.Threading.Channels`)

---

## 🐳 Phase 5 — Deployment & DevOps

- [ ] Dockerfile for the ASP.NET app
- [ ] `docker-compose.yml` (app + PostgreSQL + optional pgAdmin)
- [ ] CI/CD pipeline (GitHub Actions)
  - [ ] Build & test on push
  - [ ] Auto-deploy to staging on PR merge
- [ ] Health check endpoint (`/health`)
- [ ] Environment-based config (`appsettings.Production.json`)

---

## 📱 Phase 6 — Extended Features

- [ ] REST API layer for mobile/external integration
- [ ] Mobile-responsive PWA for tablet kiosk mode
- [ ] Notification service (email / SMS to parents/teachers)
- [ ] Continuous "Live Mode" (auto-scan without manual capture)
- [ ] QR code fallback for when face recognition fails
- [ ] Multi-language support (i18n)

---

## 🐛 Known Issues & Tech Debt

- [ ] `arcface.onnx` is 130 MB — consider model quantization (INT8) to reduce size
- [ ] EF Core migration needed for `FaceEmbedding` column on fresh installs
- [ ] No unit tests yet — add xUnit test project
- [ ] Hardcoded similarity threshold — make it configurable via `appsettings.json`
]]>
