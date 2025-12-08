# requirements.md — Raspberry Pi Number Detection Scripts

This file lists the Python libraries needed to run the **YOLO-first + OCR-fallback** scripts on the Raspberry Pi and gives a short overview of how the flow works.

---

## Python libraries

Install with pip:

```bash
pip install ultralytics opencv-python pytesseract requests
```

If you are using Picamera2 (recommended on Pi OS):

```bash
pip install picamera2
```

> Note: On some Pi OS images, Picamera2 is best installed via apt if pip fails.

---

## System packages

Tesseract OCR engine:

```bash
sudo apt-get update
sudo apt-get install -y tesseract-ocr
```

If your OpenCV install needs build deps on your image, install standard tools:

```bash
sudo apt-get install -y python3-pip python3-venv
```

---

## What each library is used for

* **ultralytics**: loads and runs your YOLO model (`.pt`).
* **picamera2**: captures frames from the Pi camera in Python.
* **opencv-python (cv2)**: frame handling, preview, grayscale/blur/threshold, ROI extraction.
* **pytesseract**: Python wrapper for OCR.
* **requests**: POSTs detected values to your `number.php` API.

---

## How it works

1. The **YOLO watchdog** script initializes the Pi camera and runs YOLO on incoming frames.
2. It attempts to find a **valid numeric result** within a short timeout (default ~5 seconds).
3. If YOLO finds a valid number, it can optionally **POST** the value to the API.
4. If YOLO does not find a valid number in time, it **stops the camera** and hands off to the **OCR fallback** script.
5. The OCR script:

   * preprocesses frames,
   * extracts a centered ROI,
   * runs pytesseract with a digit whitelist,
   * cleans the output,
   * **POSTs** the value to the API when valid.
     
The fallback script can be run indavidually once the environment is set up properly, if you want to test on a none trained elavator (not myhall). Quick run script was left in node folder which uses bash to deploy the python script. Please note that it uses my implimentation so some editing might need to be done if you use different file names/directories. 
---

## Quick install summary

```bash
sudo apt-get update
sudo apt-get install -y tesseract-ocr
pip install ultralytics opencv-python pytesseract requests picamera2
```
