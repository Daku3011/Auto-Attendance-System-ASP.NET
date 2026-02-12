# ============================================================
# Stage 1: Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first (cache layer)
COPY DemoAAS/DemoAAS.csproj DemoAAS/
RUN dotnet restore DemoAAS/DemoAAS.csproj

# Copy the rest of the source code
COPY . .

# Publish the application
WORKDIR /src/DemoAAS
RUN dotnet publish -c Release -o /app/publish --no-restore

# ============================================================
# Stage 2: Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Install OpenCV native dependencies required by OpenCvSharp4
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    libc6-dev \
    libopencv-dev \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app

# Copy published application
COPY --from=build /app/publish .

# Copy ONNX model files and other detection models
COPY DemoAAS/arcface.onnx .
COPY DemoAAS/face_detection_yunet.onnx .
COPY DemoAAS/haarcascade_frontalface_default.xml .
COPY DemoAAS/opencv_face_detector.pbtxt .
COPY DemoAAS/opencv_face_detector_uint8.pb .

# Expose port
EXPOSE 8080

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

ENTRYPOINT ["dotnet", "DemoAAS.dll"]
