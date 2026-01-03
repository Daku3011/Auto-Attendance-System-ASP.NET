
import socket
import sys

target_ip = "192.168.0.101"
port = 554

paths = [
    "/",
    "/live/ch0",
    "/live/ch1",
    "/h264/ch1/main/av_stream",
    "/cam/realmonitor?channel=1&subtype=0",
    "/stream1",
    "/1",
    "/11",
    "/12",
    "/live.sdp",
    "/mpeg4",
    "/ucast/11",
    "/media/video1",
    "/onvif1"
]

def check_path(path):
    try:
        s = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        s.settimeout(2)
        s.connect((target_ip, port))
        
        request = f"DESCRIBE rtsp://{target_ip}{path} RTSP/1.0\r\nCSeq: 1\r\n\r\n"
        s.sendall(request.encode())
        
        response = s.recv(4096).decode('utf-8', errors='ignore')
        s.close()
        
        if "RTSP/1.0 200 OK" in response:
            return "200 OK"
        elif "RTSP/1.0 401 Unauthorized" in response:
            return "401 Unauthorized (Auth Required)"
        elif "RTSP/1.0 404" in response:
            return "404 Not Found"
        else:
            first_line = response.split('\n')[0].strip()
            return f"Response: {first_line}"
            
    except Exception as e:
        return f"Error: {str(e)}"

print(f"Scanning RTSP paths on {target_ip}...")
found = False
for path in paths:
    result = check_path(path)
    if result.startswith("200") or result.startswith("401"):
        print(f"[FOUND] {path} -> {result}")
        found = True
    else:
        print(f"[Failed] {path} -> {result}")
        pass

if not found:
    print("No common paths found.")
