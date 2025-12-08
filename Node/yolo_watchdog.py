import requests  # add this import
import argparse
import os
import re
import sys
import time
from typing import Optional, Tuple, List

import cv2
from picamera2 import Picamera2
from ultralytics import YOLO

API_URL = os.getenv("API_URL", "http://178.128.234.40/number.php")
API_KEY = os.getenv("API_KEY")  # set this in your shell

def send_value_to_api(text: str):
    """Strip, validate as digit/number, and POST to the API."""
    if text is None:
        return

    cleaned = str(text).strip()
    if not cleaned:
        print("[API] Empty YOLO result, not sending.")
        return

    try:
        value = int(cleaned)
    except ValueError:
        print(f"[API] Non-numeric YOLO result '{cleaned}', not sending.")
        return

    if not API_KEY:
        print("[API] Missing API_KEY env var, not sending.")
        return

    headers = {
        "Content-Type": "application/json",
        "X-API-Key": API_KEY,
    }
    payload = {"value": value}

    try:
        resp = requests.post(API_URL, headers=headers, json=payload, timeout=5)
        print(f"[API] Sent value={value}, status={resp.status_code}, body={resp.text}")
    except requests.RequestException as e:
        print("[API] Error sending to API:", e)



def is_valid_number(num_str: str, min_val: int, max_val: int) -> bool:
    if not num_str or not (num_str := num_str.strip()):
        return False
    if not re.fullmatch(r"\d{1,3}", num_str):
        return False
    return min_val <= int(num_str) <= max_val


def _extract_digits_from_label(label: str) -> Optional[str]:
    if label is None:
        return None
    m = re.search(r"\d+", str(label))
    return m.group() if m else None


def detect_number_yolo(frame, model: YOLO, conf_thresh: float) -> Tuple[Optional[str], float]:
    results = model(frame, verbose=False)
    if not results:
        return None, 0.0

    r = results[0]
    boxes = getattr(r, "boxes", None)
    if boxes is None or len(boxes) == 0:
        return None, 0.0

    names = model.names
    full_number_candidates: List[Tuple[str, float, float]] = []
    digit_candidates: List[Tuple[str, float, float]] = []

    for b in boxes:
        try:
            conf = float(b.conf.item())
            if conf < conf_thresh:
                continue

            cls_id = int(b.cls.item())
            label = names.get(cls_id, str(cls_id))
            digit_str = _extract_digits_from_label(label)
            if not digit_str:
                continue

            xyxy = b.xyxy[0].tolist()
            x_center = (xyxy[0] + xyxy[2]) / 2.0

            if len(digit_str) >= 2:
                full_number_candidates.append((digit_str, conf, x_center))
            else:
                digit_candidates.append((digit_str, conf, x_center))
        except Exception:
            continue

    if full_number_candidates:
        full_number_candidates.sort(key=lambda x: x[1], reverse=True)
        return full_number_candidates[0][0], full_number_candidates[0][1]

    if digit_candidates:
        digit_candidates.sort(key=lambda x: x[2])
        digits = [d for d, c, x in digit_candidates]
        confs = [c for d, c, x in digit_candidates]
        assembled = "".join(digits)[:3]
        return assembled, min(confs) if confs else 0.0

    return None, 0.0


def handoff_to_fallback(picam2: Picamera2, fallback_path: str):
    try:
        picam2.stop()
    except Exception:
        pass
    cv2.destroyAllWindows()
    os.execv(sys.executable, [sys.executable, fallback_path])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--model", required=True, help="Path to YOLO .pt")
    parser.add_argument("--timeout", type=float, default=5.0, help="Seconds to wait for valid YOLO number")
    parser.add_argument("--conf", type=float, default=0.25)
    parser.add_argument("--min-val", type=int, default=0)
    parser.add_argument("--max-val", type=int, default=99)
    parser.add_argument("--fallback", default="ocr_fallback.py")
    parser.add_argument("--yolo-every-n-frames", type=int, default=1)
    parser.add_argument("--debug", action="store_true")
    args = parser.parse_args()

    model = YOLO(args.model)
    picam2 = Picamera2()
    config = picam2.create_preview_configuration(main={"format": "RGB888", "size": (1280, 720)})
    picam2.configure(config)
    picam2.start()

    start = time.monotonic()
    frame_count = 0

    try:
        while (time.monotonic() - start) < args.timeout:
            frame = picam2.capture_array()

            cv2.imshow("YOLO Watchdog", frame)
            if cv2.waitKey(1) & 0xFF == ord("q"):
                break

            if frame_count % args.yolo_every_n_frames == 0:
                num, conf = detect_number_yolo(frame, model, args.conf)

                if args.debug:
                    print(f"[YOLO] candidate={num} conf={conf:.3f}")

                if num and is_valid_number(num, args.min_val, args.max_val):
                    print(f"[YOLO] VALID number found: {num}")
                    return

            frame_count += 1

        print(f"[YOLO] No valid number within {args.timeout}s. Switching to OCR fallback...")
        handoff_to_fallback(picam2, args.fallback)

    finally:
        try:
            picam2.stop()
        except Exception:
            pass
        cv2.destroyAllWindows()


if __name__ == "__main__":
    main()
