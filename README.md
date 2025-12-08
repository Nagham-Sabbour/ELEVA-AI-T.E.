# ELEVATE XR – VR Elevator Guidance System

This repository contains the **VR/XR portion** of the ELEVATE project: a Meta Quest–based elevator guidance system that overlays a virtual elevator onto the real world to help users anticipate **which car will arrive** and **at which floor**, without modifying existing building infrastructure.

The VR app is built in **Unity (OpenXR + URP + Passthrough)** and is designed to work together with:
- An **edge AI camera module** (Raspberry Pi + Sony IMX500) that reads elevator floor displays.
- A [**lightweight HTTP API**](http://178.128.234.40/number.php) that publishes live elevator floor numbers for consumption by the headset.
- A separate research pipeline (data, models, and analysis) described in the [accompanying paper](https://edas.info/showPaper.php?m=1571228321).

This README focuses on the VR project, but gives a short overview of the other components for context.

---

## Table of Contents

- [Project Overview](#project-overview)
- [System Architecture](#system-architecture)
  - [1. VR / XR Client (this repo)](#1-vr--xr-client-this-repo)
  - [2. Edge AI Camera Module](#2-edge-ai-camera-module)
  - [3. Backend API Service](#3-backend-api-service)
- [Core VR Features](#core-vr-features)
- [Controls (Quest)](#controls-quest)
- [Project Structure](#project-structure)
- [Requirements](#requirements)
- [Setup – Unity Project](#setup--unity-project)
- [Building and Deploying to Quest](#building-and-deploying-to-quest)
- [Configuration](#configuration)
  - [Manual Elevator Mode (Controller-Driven)](#manual-elevator-mode-controller-driven)
  - [Live API Mode (Edge-AI–Driven)](#live-api-mode-edge-ai-driven)
- [Testing Without a Quest](#testing-without-a-quest)
- [Limitations](#limitations)
- [Future Work](#future-work)
- [Citation](#citation)

---

## Project Overview

In many modern buildings, there is **no clear indication** of which elevator car will arrive where—especially on upper floors and in multi-elevator banks. This creates:
- Time pressure for users with **wheelchairs and mobility devices**.
- Unsafe “last-second” movements toward the correct door.
- Congestion and confusion in high-traffic buildings.

**ELEVATE XR** addresses this by:

1. Reading the elevator’s floor indicator using an **edge AI camera** (no changes to the elevator controller).
2. Publishing the current floor to a **simple HTTP API**.
3. Rendering a virtual elevator in **VR passthrough** on a Meta Quest 2/3, aligned with the real elevator shaft.
4. Letting users:
   - Align and lock the virtual rig to the building.
   - Move virtually between floors to see where the car is relative to them.
   - Optionally run entirely from **live floor data**.

---

## System Architecture

### 1. VR / XR Client (this repo)

Unity-based Meta Quest application that:

- Enables **passthrough** so the user sees the real environment.
- Renders a **virtual elevator shaft + cab** in front of the user, scaled to match the Myhal Centre at the University of Toronto.
- Provides an **in-world floor control panel** and HUD to show:
  - User’s current floor.
  - Elevator car’s current floor.
  - Direction of motion (up/down).

The VR app can operate in two modes:

- **Manual mode** – elevator floor is controlled by the right-hand controller buttons (no network dependency).
- **API mode** – elevator floor is driven by the live HTTP endpoint (edge AI module).

### 2. Edge AI Camera Module

> **Not part of this repo, but part of the project.**

- Hardware: **Raspberry Pi 4 + Sony IMX500** AI-enabled image sensor.
- Function:
  - Continuously captures the elevator’s **seven-segment or dot-matrix display**.
  - Runs a **YOLO-based digit detector** on-device to read the current floor number.
  - Falls back to **Tesseract OCR** if the detection model fails for a period of time.
- Output:
  - Publishes a small JSON object to an HTTP endpoint, e.g.:

    ```json
    {
      "value": 3,
      "ts": 1700000000
    }
    ```

  - Only the floor number and timestamp are transmitted (no raw images), preserving privacy and bandwidth.

### 3. Backend API Service

> **Also not part of this repo, but consumed by it.**
- We will leave the current api up for the month of december https://178.128.234.40/number.php, you should be able to manipulate the elevator in vr from here with no need for the api key

- A very small HTTP service (e.g., PHP/Flask/Node) that:
  - Receives updates from the edge AI module.
  - Stores the current floor in a simple state (e.g., file, in-memory, or small DB).
  - Serves that state to clients at a URL like:

    ```text
    http://<server>/number.json
    ```

- The VR client polls this endpoint at a fixed interval and uses the `value` field as the **target floor**.

---

## Core VR Features

From the headset user’s perspective, the VR app provides:

- **Passthrough visualization**  
  The real world is visible via Quest passthrough; the elevator and UI are drawn as semi-transparent overlays, minimizing occlusion and collision risk.

- **Elevator rig placement and alignment**
  - On startup, the elevator rig spawns **~2 meters in front** of the user.
  - The user can:
    - Move the rig on the **XZ plane** with the right joystick.
    - Rotate the rig around the **Y-axis** with the left trigger (hold) and lock/unlock with a double-click.

- **Virtual floor control**
  - A world-space control panel displays:
    - Current user floor.
    - Up/Down buttons to change floors.
  - The user points with a controller and clicks the trigger to move their “virtual floor” up or down.
  - Changing floors vertically shifts the entire rig so that the corresponding floor is flush with the real floor the user is standing on.

- **Elevator motion**
  - The cab moves **one floor at a time**, taking ~5 seconds per floor.
  - When jumping multiple floors (in API mode), the cab animates through intermediate floors at a constant speed, and the HUD updates floor numbers as they are passed.
  - The UI shows:
    - `You: <userFloor>`
    - `Elevator: <currentElevatorFloor>`
    - Direction: `↑`, `↓`, or `-` (idle)

- **Transparent overlays**
  - Elevator mesh, rig, and UI elements are rendered with **semi-transparent materials**, allowing users to maintain environmental awareness (walls, obstacles, people) while using the guidance system.

---

## Controls (Quest)

> Exact mappings can be adjusted in your project. This is the default behavior reflected in the scripts.

**Rig Placement & Rotation (`ElevatorPlacement.cs`)**

- **Right joystick (axis)** – Move the **entire elevator rig** in the XZ plane (relative to where you’re looking).
- **Right joystick click** – Snap the rig back to ~2 m in front of your head, keeping the current vertical alignment (floor).
- **Left trigger (hold)** – Rotate the rig around the Y-axis to face your current view direction.
- **Left trigger (double-click)** – Lock/unlock rig placement:
  - **Locked**: rig cannot be moved/rotated.
  - **Unlocked**: placement controls are active again.

**User Floor Control (`UserFloorPanelController.cs` + `ElevatorController.cs`)**

Depending on your setup, you have two options:

1. **Controller buttons (legacy/manual)**  
   - **Left controller A/B (primary/secondary)** – Change your virtual floor up/down (`ChangeUserFloor()`).
   - **Right controller A/B (primary/secondary)** – Move the elevator cab up/down one floor.

2. **Laser-pointer panel (recommended)**  
   - **Right trigger** – Cast a ray from the controller; clicking the **Up** or **Down** 3D button on the user panel calls `ChangeUserFloor(+1/-1)`.

**HUD and UI**

- Floor labels and arrow direction update automatically as the user or elevator moves.

---

## Project Structure

High-level Unity structure (names may vary slightly):

- `Assets/Scripts/`
  - `ElevatorController.cs`  
    - Handles floor logic, cab animation, UI updates, and optional API polling.
  - `ElevatorPlacement.cs`  
    - Handles rig placement in front of the user, movement on the XZ plane, and rotation/lock with the left controller.
  - `UserFloorPanelController.cs`  
    - Handles raycast interaction with the user floor control panel (if using the laser-pointer UI).
- `Assets/Scenes/`
  - Main scene containing:
    - Elevator rig (shaft + cab).
    - User floor panel.
    - HUD / TextMeshPro world-space texts.
    - XR Origin (camera + controllers).

---

## Requirements

- **Unity**: Unity 6 (6000.x) or Unity 2022+ with:
  - **URP (Universal Render Pipeline)**
  - **OpenXR** as the XR backend
  - **Meta Quest / Oculus OpenXR features enabled**
- **Target hardware**: Meta Quest 2 or Quest 3
- **Packages** (typical):
  - TextMeshPro (built-in)
  - OpenXR Plugin
  - Meta XR / Oculus Integration (or equivalent OpenXR feature group)
  - URP

---

## Setup – Unity Project

### 1. Clone and open the project

    git clone <this-repo-url>
    cd <repo-folder>

1. Open **Unity Hub**.  
2. Click **Open project** and select this folder.  
3. Let Unity import/upgrade assets as needed.

---

### 2. Switch to Android / Quest

1. Go to **File → Build Settings…**  
2. Select **Android** in the platform list.  
3. Click **Switch Platform** (wait for it to re-import).

---

### 3. Configure XR / OpenXR

1. Open **Edit → Project Settings → XR Plug-in Management**.  
2. Under the **Android** tab:
   - Enable **OpenXR**.
3. Click **OpenXR** in the left-side XR menu and:
   - Ensure the **Meta Quest / Oculus feature group** is enabled.
   - Enable any **Quest passthrough / camera features** required by your Meta XR/OpenXR plugin (name may vary depending on package version).

> The exact labels differ slightly by SDK version, but you want **OpenXR + Quest features + passthrough support**.

---

### 4. Configure URP / Camera for Passthrough

1. Make sure your project is using **URP** (Universal Render Pipeline) for Android.  
2. Select the **Main Camera** (usually under the XR Origin).  
3. In the camera / render-pipeline-specific component:
   - Set the background / environment to the mode required for passthrough (often **Solid Color** with alpha 0, or a special **XR/AR background** component, depending on the Quest SDK you use).
4. Ensure any **AR/VR camera background component** required by your Meta XR/OpenXR passthrough is added and enabled (for example, a Meta passthrough layer component, depending on the integration).

> If you see a black background instead of passthrough, check:  
> - XR/OpenXR Quest feature is enabled  
> - Correct URP asset is active for Android  
> - Passthrough/camera background component is on the main camera

---

### 5. Open the main scene

1. Open **Assets/Scenes/** (or wherever your scenes are stored).  
2. Open the main VR scene (for example, `ElevatorScene.unity`) that includes:
   - The **ElevatorRig** (shaft + cab).  
   - The **XR Origin / XR Rig** with the camera and controller anchors.  
   - The **HUD TextMeshPro objects**.  
   - The **User Floor Panel** (with Up/Down buttons and floor text).

---

### 6. Wire up script references

#### ElevatorController

Select the GameObject with **`ElevatorController`** and confirm:

- **Elevator Setup**  
  - `elevatorCab` → drag the cab mesh Transform (the moving elevator box).  
  - `baseHeight` → center Y of the cab at floor 0.  
    - If you manually place the cab in the editor, you can set `baseHeight` = its local Y, or in `Start()` read it from `elevatorCab.localPosition.y`.  
  - `floorHeight` → vertical distance between floors (for example, `4f`).  
  - `travelTimePerFloor` → time in seconds to move one floor (for example, `5f`).  
  - `minFloor`, `maxFloor` → valid floor range (for example, `0` to `8`).

- **UI**  
  - `userFloorText` → world-space TextMeshPro showing **“You: X”**.  
  - `elevatorFloorText` → world-space TextMeshPro showing **“Elevator: Y”**.  
  - `directionText` → world-space TextMeshPro showing **↑ / ↓ / -**.  
  - `apiDebugText` → optional TextMeshPro for debugging; can be left `null` in manual mode.

- **User Floor Panel**  
  - `userFloorPanel` → the panel Transform that holds the Up/Down buttons and the 3D floor text.  
  - `xrCamera` → drag the **Main Camera** (usually under XR Origin).  
    - If left `null`, `Start()` can try to auto-assign `Camera.main`.

#### ElevatorPlacement

Select the elevator rig root (often the same object that has `ElevatorController`) and ensure:

- `ElevatorPlacement` is attached.  
- `xrCamera` is set to the **Main Camera** (or left null to auto-assign).  
- `initialDistance` ≈ `2f` (to spawn the rig 2m in front of the user).  
- `moveSpeed` as desired (for example, `1.5f`).

#### UserFloorPanelController (if you use the laser-pointer panel)

Select the **UserFloorPanel** object and confirm:

- `Elevator` → drag the **ElevatorController** object.  
- `floorText` → the TextMeshPro in the center of the panel that shows the user floor number.  
- `upButton` → the Transform for the Up button cube.  
- `downButton` → the Transform for the Down button cube.  
- `rayLine` (optional) → a LineRenderer used to visualize the controller ray.  
  - Create an empty GameObject, add a **LineRenderer**, assign a simple material and set `Use World Space = true`, then drag that object here.

Make sure `UpButton` and `DownButton` cubes have **BoxCollider** components.

---

## Building and Deploying to Quest

1. **Connect the Quest**
   - Enable **Developer Mode** in the Meta/Oculus app.  
   - Connect the Quest via USB and allow debugging on the headset.

2. **Build Settings**
   - `File → Build Settings…`  
   - Platform: **Android**  
   - Add your main scene to **Scenes in Build** (if not already).  
   - Optionally check **Development Build** and **Autoconnect Profiler** for debugging.

3. **Player Settings**
   - `Player Settings → Other Settings → Configuration`:
     - **Internet Access** = `Auto` or `Require` (especially if you use API mode later).  
     - For HTTP (non-HTTPS) APIs in dev: set insecure HTTP / **Allow downloads over HTTP** to `Always Allowed` or `Development Only`.

4. **Build & Run**
   - In the **Build Settings** window, click **Build and Run**.  
   - Choose a folder or file name for the APK.  
   - Unity will build and install the app on the connected Quest.  
   - Put on the headset and select the app from **Apps → Unknown Sources** (if not visible on the main app list).

---

## Configuration

### Manual Elevator Mode (Controller-Driven)

This is the safer, standalone mode used for demos when you don’t want to rely on a live network/API.

In `ElevatorController`:

- Ensure this line in `Start()` is **commented out or removed**:

    // StartCoroutine(PollApiRoutine());

- Keep the API methods commented out:

    // [System.Serializable]
    // private class ApiResponse { ... }

    // IEnumerator PollApiRoutine() { ... }
    // IEnumerator GetFloorFromApi() { ... }
    // void HandleApiFloor(int apiFloor) { ... }

**Behavior in manual mode (typical legacy mapping):**

- **Left controller A/B** (primary/secondary)  
  → Calls `ChangeUserFloor(-1/+1)` to move **your floor** up/down.  
  → This shifts the entire rig vertically so that the chosen floor aligns with the real floor.

- **Right controller A/B**  
  → Calls `TryMoveElevator(-1/+1)` to move the **elevator cab** down/up one floor.  
  → The cab animates over `travelTimePerFloor`, updating HUD floor numbers as it passes intermediate floors.

You can keep or adjust these mappings depending on your input preferences.

---

### Live API Mode (Edge-AI–Driven)

To drive the elevator cab from the edge AI camera and backend API:

1. **Uncomment the API types and methods** in `ElevatorController`:

    [System.Serializable]
    private class ApiResponse
    {
        public int value;
        public long ts;
    }

    IEnumerator PollApiRoutine() { ... }
    IEnumerator GetFloorFromApi() { ... }
    void HandleApiFloor(int apiFloor) { ... }

2. In `Start()` of `ElevatorController`, **enable polling**:

    StartCoroutine(PollApiRoutine());

3. Set the API URL and polling interval:

    public string apiUrl = "http://<your-server>/number.json";
    public float pollInterval = 0.5f; // in seconds

4. Ensure **Android HTTP access** is properly configured:

   - `Edit → Project Settings → Player → Android → Other Settings → Configuration`:
     - **Internet Access** = `Auto` or `Require`.  
     - If you’re using `http://` (not `https://`), allow insecure HTTP for development.

**Runtime behavior in API mode:**

- The headset periodically sends `GET` requests to `apiUrl`.  
- Each valid JSON response is parsed; `value` is clamped to `[minFloor, maxFloor]`.  
- If the requested floor differs from `currentElevatorFloor` and the cab isn’t already moving, it calls `TryMoveElevator()` to animate the cab to the new floor.  
- The HUD and direction indicator update accordingly.

You can keep the **manual controls** active in parallel (for example, for testing), or disable them if you want the elevator motion to come strictly from the API.

---

## Testing Without a Quest

You can test basic logic in the Unity Editor:

- Press **Play** and:
  - Watch the elevator cab move in response to:
    - Left/right controller button presses (if XR simulation is enabled).  
    - Keyboard input, if you add an editor-only input path (for example, arrow keys or WASD inside `#if UNITY_EDITOR`).  
  - Check that:
    - Cab moves the correct distance per floor (`floorHeight`).  
    - HUD floor numbers (`userFloorText`, `elevatorFloorText`) update correctly.  
    - Direction text switches between `↑`, `↓`, and `-`.  
    - User floor changes cause the rig to shift vertically and the user floor panel to reposition near head height.

Limitations of Editor-only testing:

- No real passthrough (you’ll see the scene background instead).  
- No real Quest controllers unless you use a Quest link / XR simulation setup.  
- Network calls will work, but latency and behavior won’t fully mimic the device environment.

---

## Limitations

- **Single elevator shaft**: The current VR implementation is focused on one elevator at a time. Multi-shaft support would require per-elevator identification and visualization.  
- **Manual alignment**: Elevator rig alignment with the real-world shaft is manual; incorrect placement can cause visual mismatch.  
- **Timing mismatches**: In API mode, the cab motion is smooth and idealized; real elevators can have delays (doors, human movement) that introduce slight desynchronization.  
- **Network dependency (API mode)**: Live mode depends on stable network connectivity to the backend endpoint.  
- **Passthrough variations**: Behavior and appearance may differ slightly depending on the exact Meta XR/OpenXR integration and SDK version used.

---

## Future Work

- Add support for **multiple elevators**, each with its own rig, color coding, and label.  
- Display **crowding levels** or occupancy estimates based on additional sensors or vision models.  
- Integrate **audio cues** and **haptics** for blind/low-vision users.  
- Improve automatic **rig calibration**, for example, by detecting door edges or using known markers.  
- Replace vision-based floor detection in some setups with **shaft-mounted range sensors** for improved accuracy and robustness.

---

## Citation

If you use this VR portion in an academic or technical context, please cite the associated paper:

> S. Masic, N. Sabbour, A. Vicol, I. Cheema, E. Zaidi, N. Mehanathan, and S. Mann,  
> **“Wearable Technology to Mitigate Bad Elevator Design,”**  
> International Conference on Consumer Electronics (ICCE), 2026.

For research-only use:

> This code is provided for research and educational purposes.  
> For commercial use or redistribution, please contact the authors.

