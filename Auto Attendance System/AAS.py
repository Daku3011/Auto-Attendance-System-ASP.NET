import os
import sys
import cv2
import csv
import math
import time
import glob
import argparse
from datetime import datetime
import numpy as np
import pandas as pd

# DeepFace (uses TensorFlow 2.12 in your venv)
from deepface import DeepFace

# Optional: file picker for convenience on Windows
try:
    from tkinter import Tk
    from tkinter.filedialog import askopenfilename
    TK_AVAILABLE = True
except Exception:
    TK_AVAILABLE = False


# -------------------- CONFIG --------------------
FACES_DIR            = "Faces"                 # Folder with known faces (Dwarkesh.jpg, Vinay.jpg, ...)
OUTPUT_DIR           = "outputs"               # Annotated images go here
ATTENDANCE_CSV       = "attendance.csv"
MODEL_NAME           = "VGG-Face"              # Keep consistent everywhere
DETECTOR_BACKEND     = "opencv"                # 'opencv' is lightweight; 'retinaface' is stronger (needs TF)
DISTANCE_METRIC      = "cosine"                # We'll implement cosine distance
VGGFACE_COSINE_THR   = 0.40                    # Common threshold for VGG-Face + cosine (lower = stricter)
DISPLAY_SCALE        = 65                      # % size for preview window (avoid huge popup)
FONT                 = cv2.FONT_HERSHEY_SIMPLEX


# -------------------- UTILS --------------------
def ensure_dirs():
    os.makedirs(FACES_DIR, exist_ok=True)
    os.makedirs(OUTPUT_DIR, exist_ok=True)
    if not os.path.exists(ATTENDANCE_CSV):
        pd.DataFrame(columns=["Name", "Date", "Time", "Confidence"]).to_csv(ATTENDANCE_CSV, index=False)

def list_image_files(folder):
    exts = ("*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp")
    files = []
    for e in exts:
        files.extend(glob.glob(os.path.join(folder, e)))
    return files

def cosine_distance(a: np.ndarray, b: np.ndarray) -> float:
    # lower = more similar
    a = np.asarray(a, dtype=np.float32)
    b = np.asarray(b, dtype=np.float32)
    denom = (np.linalg.norm(a) * np.linalg.norm(b)) + 1e-12
    return 1.0 - float(np.dot(a, b) / denom)

def confidence_from_distance(distance: float, threshold: float) -> float:
    # Rough mapping: distance 0.0 -> 1.0, distance >= threshold -> ~0.0
    # Clip to [0,1]
    conf = 1.0 - (distance / (threshold + 1e-6))
    return float(np.clip(conf, 0.0, 1.0))

def parse_facial_area(face_dict):
    """DeepFace.extract_faces may return either (x,y,w,h) or (x1,y1,x2,y2)."""
    fa = face_dict.get("facial_area", {})
    if all(k in fa for k in ("x", "y", "w", "h")):
        x, y, w, h = int(fa["x"]), int(fa["y"]), int(fa["w"]), int(fa["h"])
    elif all(k in fa for k in ("x1", "y1", "x2", "y2")):
        x1, y1, x2, y2 = int(fa["x1"]), int(fa["y1"]), int(fa["x2"]), int(fa["y2"])
        x, y, w, h = x1, y1, max(0, x2 - x1), max(0, y2 - y1)
    else:
        # fallback (should be rare)
        x = y = 0
        w = int(face_dict["face"].shape[1])
        h = int(face_dict["face"].shape[0])
    return x, y, w, h

def mark_attendance(name: str, conf: float):
    today = datetime.now().strftime("%Y-%m-%d")
    now_time = datetime.now().strftime("%H:%M:%S")
    df = pd.read_csv(ATTENDANCE_CSV)
    # allow one entry per person per date
    if not ((df["Name"] == name) & (df["Date"] == today)).any():
        new_row = {"Name": name, "Date": today, "Time": now_time, "Confidence": round(conf, 4)}
        df = df.append(new_row, ignore_index=True)
        df.to_csv(ATTENDANCE_CSV, index=False)
        print(f"[ATTENDANCE] Marked {name} at {now_time} (conf {conf*100:.1f}%)")
    else:
        print(f"[ATTENDANCE] {name} already marked today ({today}).")


# -------------------- LOAD KNOWN FACES --------------------
def load_known_embeddings():
    print("[INFO] Loading known faces from:", FACES_DIR)
    known = []   # list of (name, embedding)
    files = list_image_files(FACES_DIR)

    if not files:
        print(f"[WARN] No images found in '{FACES_DIR}'. "
              f"Add images like '{FACES_DIR}/Alice.jpg' and run again.")
        return known

    for path in files:
        name = os.path.splitext(os.path.basename(path))[0]  # filename without extension
        try:
            # Create embedding once per person image
            reps = DeepFace.represent(
                img_path=path,
                model_name=MODEL_NAME,
                detector_backend=DETECTOR_BACKEND,
                enforce_detection=True
            )
            emb = reps[0]["embedding"]
            known.append((name, np.asarray(emb, dtype=np.float32)))
            print(f"[OK] Loaded embedding for '{name}'")
        except Exception as e:
            print(f"[ERROR] Failed to embed '{path}': {e}")

    if not known:
        print("[WARN] No valid embeddings were created from Faces/.")
    return known


# -------------------- RECOGNIZE IN A CLASSROOM PHOTO --------------------
def recognize_from_image(image_path: str, known_db, threshold=VGGFACE_COSINE_THR):
    if not os.path.exists(image_path):
        raise FileNotFoundError(f"Image not found: {image_path}")

    print(f"[INFO] Processing classroom image: {image_path}")
    img_bgr = cv2.imread(image_path)
    if img_bgr is None:
        raise RuntimeError("Failed to read the image (format unsupported or path issue).")

    img_rgb = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2RGB)

    # Detect faces
    faces = DeepFace.extract_faces(
        img_path=img_rgb,
        detector_backend=DETECTOR_BACKEND,
        enforce_detection=False
    )
    print(f"[INFO] Detected {len(faces)} face(s).")

    recognized_names = set()

    for f in faces:
        x, y, w, h = parse_facial_area(f)
        # safety clamp to image bounds
        x, y = max(0, x), max(0, y)
        w = max(1, min(w, img_bgr.shape[1] - x))
        h = max(1, min(h, img_bgr.shape[0] - y))

        # Compute embedding for the detected face crop
        try:
            rep = DeepFace.represent(
                img_path=img_rgb[y:y+h, x:x+w],  # can pass numpy array
                model_name=MODEL_NAME,
                detector_backend=DETECTOR_BACKEND,
                enforce_detection=False
            )
            face_emb = np.asarray(rep[0]["embedding"], dtype=np.float32)
        except Exception as e:
            print(f"[WARN] Skipping a face (embedding failed): {e}")
            continue

        # Compare with known database
        best_name = "Unknown"
        best_dist = 1e9

        for name, known_emb in known_db:
            dist = cosine_distance(face_emb, known_emb)
            if dist < best_dist:
                best_dist = dist
                best_name = name

        conf = confidence_from_distance(best_dist, threshold)
        label = f"{best_name} ({conf*100:.1f}%)" if best_dist <= threshold else "Unknown"

        # Attendance marking only when confident enough
        if best_dist <= threshold:
            recognized_names.add(best_name)
            mark_attendance(best_name, conf)

        # Draw bounding box & label
        color = (0, 200, 0) if best_dist <= threshold else (0, 0, 255)
        cv2.rectangle(img_bgr, (x, y), (x + w, y + h), color, 2)
        cv2.putText(img_bgr, label, (x, max(20, y - 10)), FONT, 0.8, color, 2, cv2.LINE_AA)

    # Save annotated image
    ts = datetime.now().strftime("%Y%m%d_%H%M%S")
    out_path = os.path.join(OUTPUT_DIR, f"recognized_{ts}.jpg")
    cv2.imwrite(out_path, img_bgr)
    print(f"[INFO] Saved annotated image -> {out_path}")

    # Preview resized
    scale = np.clip(DISPLAY_SCALE, 10, 100) / 100.0
    preview = cv2.resize(img_bgr, (int(img_bgr.shape[1]*scale), int(img_bgr.shape[0]*scale)))
    cv2.imshow("Recognition Result", preview)
    cv2.waitKey(0)
    cv2.destroyAllWindows()

    if recognized_names:
        print("[INFO] Recognized:", ", ".join(sorted(recognized_names)))
    else:
        print("[INFO] No matches above threshold. You can lower the threshold if needed.")


# -------------------- ENTRY --------------------
def pick_file_dialog():
    if not TK_AVAILABLE:
        return None
    try:
        root = Tk()
        root.withdraw()
        root.update()
        path = askopenfilename(title="Select classroom photo",
                               filetypes=[("Images", "*.jpg *.jpeg *.png *.bmp *.webp")])
        root.destroy()
        return path if path else None
    except Exception:
        return None

def main():
    global DETECTOR_BACKEND, MODEL_NAME
    ensure_dirs()

    parser = argparse.ArgumentParser(description="Auto Attendance - Image Recognition with DeepFace")
    parser.add_argument("-i", "--image", help="Path to classroom image")
    parser.add_argument("--threshold", type=float, default=VGGFACE_COSINE_THR,
                        help=f"Match threshold (cosine). Default {VGGFACE_COSINE_THR}")
    parser.add_argument("--backend", default=DETECTOR_BACKEND, choices=["opencv", "retinaface", "mediapipe", "mtcnn", "yolov8"],
                        help="Face detector backend")
    parser.add_argument("--model", default=MODEL_NAME, choices=["VGG-Face", "Facenet", "Facenet512", "ArcFace", "OpenFace", "DeepFace", "Dlib"],
                        help="Embedding model")
    args = parser.parse_args()

    DETECTOR_BACKEND = args.backend
    MODEL_NAME = args.model

    known_db = load_known_embeddings()
    if not known_db:
        print("\n[STOP] No known faces loaded. Put reference photos in the 'Faces' folder "
              "with the file name as the person's name (e.g., Faces/Alice.jpg).")
        return

    image_path = args.image
    if not image_path:
        image_path = pick_file_dialog()

    if not image_path:
        print("[STOP] No image selected. Pass with -i IMAGE_PATH or select via dialog.")
        return

    recognize_from_image(image_path, known_db, threshold=float(args.threshold))


if __name__ == "__main__":
    main()




#--------------------------
#THIS IS FOR GOOGLE COLLABE
#--------------------------
# # !pip install deepface # Already installed in previous cells
# import os
# import sys
# import cv2
# import csv
# import math
# import time
# import glob
# # import argparse # Remove argparse
# from datetime import datetime
# import numpy as np
# import pandas as pd
# from google.colab import files # Import files for Colab upload
# from IPython.display import Image, display # For displaying image results

# # DeepFace (uses TensorFlow 2.12 in your venv)
# from deepface import DeepFace

# # Optional: file picker for convenience on Windows
# # try:
# #     from tkinter import Tk
# #     from tkinter.filedialog import askopenfilename
# #     TK_AVAILABLE = True
# # except Exception:
# #     TK_AVAILABLE = False # Not relevant in Colab

# # -------------------- CONFIG --------------------
# FACES_DIR            = "Faces"                 # Folder with known faces (Dwarkesh.jpg, Vinay.jpg, ...)
# OUTPUT_DIR           = "outputs"               # Annotated images go here
# ATTENDANCE_CSV       = "attendance.csv"
# MODEL_NAME           = "VGG-Face"              # Keep consistent everywhere
# DETECTOR_BACKEND     = "opencv"                # 'opencv' is lightweight; 'retinaface' is stronger (needs TF)
# DISTANCE_METRIC      = "cosine"                # We'll implement cosine distance
# VGGFACE_COSINE_THR   = 0.40                    # Common threshold for VGG-Face + cosine (lower = stricter)
# # DISPLAY_SCALE        = 65                      # % size for preview window (avoid huge popup) # Not directly used in Colab display
# FONT                 = cv2.FONT_HERSHEY_SIMPLEX


# # -------------------- UTILS --------------------
# def ensure_dirs():
#     os.makedirs(FACES_DIR, exist_ok=True)
#     os.makedirs(OUTPUT_DIR, exist_ok=True)
#     if not os.path.exists(ATTENDANCE_CSV):
#         pd.DataFrame(columns=["Name", "Date", "Time", "Confidence"]).to_csv(ATTENDANCE_CSV, index=False)

# def list_image_files(folder):
#     exts = ("*.jpg", "*.jpeg", "*.png", "*.bmp", "*.webp")
#     files = []
#     for e in exts:
#         files.extend(glob.glob(os.path.join(folder, e)))
#     return files

# def cosine_distance(a: np.ndarray, b: np.ndarray) -> float:
#     # lower = more similar
#     a = np.asarray(a, dtype=np.float32)
#     b = np.asarray(b, dtype=np.float32)
#     denom = (np.linalg.norm(a) * np.linalg.norm(b)) + 1e-12
#     return 1.0 - float(np.dot(a, b) / denom)

# def confidence_from_distance(distance: float, threshold: float) -> float:
#     # Rough mapping: distance 0.0 -> 1.0, distance >= threshold -> ~0.0
#     # Clip to [0,1]
#     conf = 1.0 - (distance / (threshold + 1e-6))
#     return float(np.clip(conf, 0.0, 1.0))

# def parse_facial_area(face_dict):
#     """DeepFace.extract_faces may return either (x,y,w,h) or (x1,y1,x2,y2)."""
#     fa = face_dict.get("facial_area", {})
#     if all(k in fa for k in ("x", "y", "w", "h")):
#         x, y, w, h = int(fa["x"]), int(fa["y"]), int(fa["w"]), int(fa["h"])
#     elif all(k in fa for k in ("x1", "y1", "x2", "y2")):
#         x1, y1, x2, y2 = int(fa["x1"]), int(fa["y1"]), int(fa["x2"]), int(fa["y2"])
#         x, y, w, h = x1, y1, max(0, x2 - x1), max(0, y2 - y1)
#     else:
#         # fallback (should be rare)
#         x = y = 0
#         w = int(face_dict["face"].shape[1])
#         h = int(face_dict["face"].shape[0])
#     return x, y, w, h

# def mark_attendance(name: str, conf: float):
#     today = datetime.now().strftime("%Y-%m-%d")
#     now_time = datetime.now().strftime("%H:%M:%S")
#     df = pd.read_csv(ATTENDANCE_CSV)
#     # allow one entry per person per date
#     if not ((df["Name"] == name) & (df["Date"] == today)).any():
#         new_row = {"Name": name, "Date": today, "Time": now_time, "Confidence": round(conf, 4)}
#         # Use pd.concat instead of _append
#         df = pd.concat([df, pd.DataFrame([new_row])], ignore_index=True)
#         df.to_csv(ATTENDANCE_CSV, index=False)
#         print(f"[ATTENDANCE] Marked {name} at {now_time} (conf {conf*100:.1f}%)")
#     else:
#         print(f"[ATTENDANCE] {name} already marked today ({today}).")


# # -------------------- LOAD KNOWN FACES --------------------
# def load_known_embeddings():
#     print("[INFO] Loading known faces from:", FACES_DIR)
#     known = []   # list of (name, embedding)
#     files = list_image_files(FACES_DIR)

#     if not files:
#         print(f"[WARN] No images found in '{FACES_DIR}'. "
#               f"Add images like '{FACES_DIR}/Alice.jpg' and run again.")
#         return known

#     for path in files:
#         name = os.path.splitext(os.path.basename(path))[0]  # filename without extension
#         try:
#             # Create embedding once per person image
#             reps = DeepFace.represent(
#                 img_path=path,
#                 model_name=MODEL_NAME,
#                 detector_backend=DETECTOR_BACKEND,
#                 enforce_detection=True
#             )
#             emb = reps[0]["embedding"]
#             known.append((name, np.asarray(emb, dtype=np.float32)))
#             print(f"[OK] Loaded embedding for '{name}'")
#         except Exception as e:
#             print(f"[ERROR] Failed to embed '{path}': {e}")

#     if not known:
#         print("[WARN] No valid embeddings were created from Faces/.")
#     return known


# # -------------------- RECOGNIZE IN A CLASSROOM PHOTO --------------------
# def recognize_from_image(image_path: str, known_db, threshold=VGGFACE_COSINE_THR):
#     if not os.path.exists(image_path):
#         raise FileNotFoundError(f"Image not found: {image_path}")

#     print(f"[INFO] Processing classroom image: {image_path}")
#     img_bgr = cv2.imread(image_path)
#     if img_bgr is None:
#         raise RuntimeError("Failed to read the image (format unsupported or path issue).")

#     img_rgb = cv2.cvtColor(img_bgr, cv2.COLOR_BGR2RGB)

#     # Detect faces
#     faces = DeepFace.extract_faces(
#         img_path=img_rgb,
#         detector_backend=DETECTOR_BACKEND,
#         enforce_detection=False
#     )
#     print(f"[INFO] Detected {len(faces)} face(s).")

#     recognized_names = set()

#     for f in faces:
#         x, y, w, h = parse_facial_area(f)
#         # safety clamp to image bounds
#         x, y = max(0, x), max(0, y)
#         w = max(1, min(w, img_bgr.shape[1] - x))
#         h = max(1, min(h, img_bgr.shape[0] - y))

#         # Compute embedding for the detected face crop
#         try:
#             # Use the face crop (numpy array) directly
#             face_emb = DeepFace.represent(
#                 img_path=img_rgb[y:y+h, x:x+w],
#                 model_name=MODEL_NAME,
#                 detector_backend=DETECTOR_BACKEND,
#                 enforce_detection=False
#             )[0]["embedding"]
#             face_emb = np.asarray(face_emb, dtype=np.float32)
#         except Exception as e:
#             print(f"[WARN] Skipping a face (embedding failed): {e}")
#             continue

#         # Compare with known database
#         best_name = "Unknown"
#         best_dist = 1e9

#         for name, known_emb in known_db:
#             dist = cosine_distance(face_emb, known_emb)
#             if dist < best_dist:
#                 best_dist = dist
#                 best_name = name

#         conf = confidence_from_distance(best_dist, threshold)
#         label = f"{best_name} ({conf*100:.1f}%)" if best_dist <= threshold else "Unknown"

#         # Attendance marking only when confident enough
#         if best_dist <= threshold:
#             recognized_names.add(best_name)
#             mark_attendance(best_name, conf)

#         # Draw bounding box & label
#         color = (0, 200, 0) if best_dist <= threshold else (0, 0, 255)
#         cv2.rectangle(img_bgr, (x, y), (x + w, y + h), color, 2)
#         cv2.putText(img_bgr, label, (x, max(20, y - 10)), FONT, 0.8, color, 2, cv2.LINE_AA)

#     # Save annotated image
#     ts = datetime.now().strftime("%Y%m%d_%H%M%S")
#     out_path = os.path.join(OUTPUT_DIR, f"recognized_{ts}.jpg")
#     cv2.imwrite(out_path, img_bgr)
#     print(f"[INFO] Saved annotated image -> {out_path}")

#     # Display the image in Colab
#     display(Image(out_path))

#     if recognized_names:
#         print("[INFO] Recognized:", ", ".join(sorted(recognized_names)))
#     else:
#         print("[INFO] No matches above threshold.")


# # -------------------- ENTRY --------------------
# # def pick_file_dialog(): # Not needed in Colab
# #     if not TK_AVAILABLE:
# #         return None
# #     try:
# #         root = Tk()
# #         root.withdraw()
# #         root.update()
# #         path = askopenfilename(title="Select classroom photo",
# #                                filetypes=[("Images", "*.jpg *.jpeg *.png *.bmp *.webp")])
# #         root.destroy()
# #         return path if path else None
# #     except Exception:
# #         return None

# def main():
#     global DETECTOR_BACKEND, MODEL_NAME, VGGFACE_COSINE_THR # Make configurable if needed

#     ensure_dirs()

#     # Set parameters directly
#     image_path = None # Will be set by file upload
#     threshold = VGGFACE_COSINE_THR # Use default or set a variable
#     backend = DETECTOR_BACKEND # Use default or set a variable
#     model = MODEL_NAME # Use default or set a variable

#     DETECTOR_BACKEND = backend
#     MODEL_NAME = model
#     VGGFACE_COSINE_THR = threshold

#     known_db = load_known_embeddings()
#     if not known_db:
#         print("\n[STOP] No known faces loaded. Put reference photos in the 'Faces' folder "
#               "with the file name as the person's name (e.g., Faces/Alice.jpg).")
#         return

#     print("[INFO] Please upload the classroom photo.")
#     uploaded = files.upload()

#     if not uploaded:
#         print("[STOP] No image uploaded.")
#         return

#     # Assuming only one file is uploaded for the classroom photo
#     image_path = list(uploaded.keys())[0]

#     recognize_from_image(image_path, known_db, threshold=VGGFACE_COSINE_THR)


# if __name__ == "__main__":
#     main()
