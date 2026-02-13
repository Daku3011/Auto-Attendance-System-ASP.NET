# ============================================================
# Stage 1: Build
# ============================================================
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copy project file and restore dependencies first (cache layer)
COPY DemoAAS/DemoAAS.csproj DemoAAS/
RUN dotnet restore DemoAAS/DemoAAS.csproj -r linux-x64

# Copy the rest of the source code
COPY . .

# Publish the application with linux-x64 runtime to include native libs
WORKDIR /src/DemoAAS
RUN dotnet publish -c Release -o /app/publish -r linux-x64 --self-contained false --no-restore

# ============================================================
# Stage 2: Runtime
# ============================================================
FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime

# Install native dependencies required by OpenCvSharp4
# Note: Debian 12 installs libtesseract5, but OpenCvSharp expects libtesseract.so.4
# We create a symlink to trick it.
RUN apt-get update && apt-get install -y --no-install-recommends \
    libgdiplus \
    libc6-dev \
    libopencv-dev \
    libtesseract-dev \
    libgtk2.0-0 \
    && rm -rf /var/lib/apt/lists/* \
    && ln -s /usr/lib/x86_64-linux-gnu/libtesseract.so.5 /usr/lib/x86_64-linux-gnu/libtesseract.so.4 \
    && ln -s /usr/lib/x86_64-linux-gnu/libjpeg.so.62 /usr/lib/x86_64-linux-gnu/libjpeg.so.8

WORKDIR /app

# Copy published application (includes runtimes/linux-x64/native/)
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
