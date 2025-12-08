from picamera2 import Picamera2
import cv2
import pytesseract
import re
import requests

API_URL = "http://178.128.234.40/number.php"
API_KEY = "" #(Set in terminal  or here)

def send_value_to_api(text):
    cleaned = text.strip()
    if not cleaned:
        print("[API] Empty OCR result, not sending.")
        return
    
    try:
        value = int(cleaned)
    except ValueError:
        print(f"[API] Non-numeric OCR result '{cleaned}', not sending.")
        return
    
    headers = {"Content-Type": "application/json", "X-API-Key": API_KEY}
    payload = {"value": value}
    
    try:
        resp = requests.post(API_URL, headers=headers, json=payload, timeout=5)
        print(f"[API] Sent value={value}, status={resp.status_code}, body={resp.text}")
    except requests.RequestException as e:
        print("[API] Error sending to API:", e)

picam2 = Picamera2()
config = picam2.create_preview_configuration(main={"format": "RGB888", "size": (1280, 720)})
picam2.configure(config)
picam2.start()

print("Press 'q' in the window or Ctrl+C in the terminal to quit.")

frame_count = 0
OCR_EVERY_N_FRAMES = 10
custom_config = r"--psm 10 -c tessedit_char_whitelist=0123456789"

try:
    while True:
        frame = picam2.capture_array()
        
        gray = cv2.cvtColor(frame, cv2.COLOR_RGB2GRAY)
        gray = cv2.GaussianBlur(gray, (5, 5), 0)
        _, thresh = cv2.threshold(gray, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        
        h, w = thresh.shape
        roi_h = int(h * 0.5)
        roi_w = int(w * 0.2)
        y1 = h // 2 - roi_h // 2
        x1 = w // 2 - roi_w // 2
        y2 = y1 + roi_h
        x2 = x1 + roi_w
        
        preview_frame = frame.copy()
        cv2.rectangle(preview_frame, (x1, y1), (x2, y2), (0, 255, 0), 2)
        cv2.imshow("PiCam OCR", preview_frame)
        
        if frame_count % OCR_EVERY_N_FRAMES == 0:
            roi = thresh[y1:y2, x1:x2]
            raw_text = pytesseract.image_to_string(roi, lang="eng", config=custom_config)
            text = re.sub(r"[^0-9]", "", raw_text)
            digit = text.strip()
            
            print("=== OCR RESULT (frame", frame_count, ") ===")
            print("Raw:", repr(raw_text))
            print("Cleaned:", repr(digit))
            print("-" * 40)
            
            send_value_to_api(digit)
        
        frame_count += 1
        
        if cv2.waitKey(1) & 0xFF == ord("q"):
            break

except KeyboardInterrupt:
    pass
finally:
    picam2.stop()
    cv2.destroyAllWindows()
