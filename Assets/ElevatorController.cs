using UnityEngine;
using UnityEngine.XR;
using UnityEngine.Networking;
using TMPro;
using System.Collections;

public class ElevatorController : MonoBehaviour
{
    [Header("Elevator Setup")]
    public Transform elevatorCab;          // The moving box (cab)
    public float baseHeight = 1f;          // Center height for floor 0 (cab is 2m tall, so base at y=1)
    public float floorHeight = 4f;         // Distance in meters between floors (center to center)
    public float travelTimePerFloor = 5f;  // Seconds per floor
    public int minFloor = 0;
    public int maxFloor = 8;

    [Header("UI")]
    public TextMeshPro userFloorText;
    public TextMeshPro elevatorFloorText;
    public TextMeshPro directionText;
    public TextMeshPro apiDebugText;

    [Header("API Settings")]
    public string apiUrl = "http://178.128.234.40/number.json";
    public float pollInterval = 0.5f;      // seconds between requests

    private int currentElevatorFloor = 0;
    private int userFloor = 0;
    private bool isMoving = false;

    // For button edge detection
    private bool leftPrimaryLast = false;
    private bool leftSecondaryLast = false;
    private bool rightPrimaryLast = false;
    private bool rightSecondaryLast = false;

    // Track only the movement coroutine (don’t kill API polling)
    private Coroutine moveRoutine;

    [System.Serializable]
    private class ApiResponse
    {
        public int value;   // floor number
        public long ts;     // timestamp (not used)
    }

    void Start()
    {
        // Force cab to be at the correct base height for floor 0
        if (elevatorCab != null)
        {
            Vector3 pos = elevatorCab.localPosition;
            pos.y = baseHeight;
            elevatorCab.localPosition = pos;
        }

        UpdateUI();

        // Always start polling the API
        StartCoroutine(PollApiRoutine());
    }

    void Update()
    {
        // Left controller = user floor (rig moves in Y)
        HandleLeftController();

        // Manual elevator control via right controller (DISABLED – kept for reference)
        // HandleRightController();
    }

    void HandleLeftController()
    {
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool primaryPressed = false;
        bool secondaryPressed = false;

        leftHand.TryGetFeatureValue(CommonUsages.primaryButton, out primaryPressed);
        leftHand.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryPressed);

        // primary = floor down, secondary = floor up (your latest mapping)
        if (primaryPressed && !leftPrimaryLast)
        {
            ChangeUserFloor(-1);
        }
        if (secondaryPressed && !leftSecondaryLast)
        {
            ChangeUserFloor(+1);
        }

        leftPrimaryLast = primaryPressed;
        leftSecondaryLast = secondaryPressed;
    }

    // =====================================================================
    // LEGACY: manual elevator control with right controller (kept as comment)
    // =====================================================================
    /*
    void HandleRightController()
    {
        if (isMoving) return; // Don't accept new commands while moving

        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool primaryPressed = false;
        bool secondaryPressed = false;

        rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out primaryPressed);
        rightHand.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryPressed);

        // primary = elevator down, secondary = elevator up
        if (primaryPressed && !rightPrimaryLast)
        {
            TryMoveElevator(-1);
        }
        if (secondaryPressed && !rightSecondaryLast)
        {
            TryMoveElevator(+1);
        }

        rightPrimaryLast = primaryPressed;
        rightSecondaryLast = secondaryPressed;
    }
    */

    void ChangeUserFloor(int delta)
    {
        int oldFloor = userFloor;
        int newFloor = Mathf.Clamp(userFloor + delta, minFloor, maxFloor);
        int floorDelta = newFloor - oldFloor;

        if (floorDelta == 0) return;

        // Move entire rig vertically so new floor aligns with real floor
        Vector3 pos = transform.position;
        pos.y -= floorDelta * floorHeight;
        transform.position = pos;

        userFloor = newFloor;
        UpdateUI();
    }

    // ================== MOVEMENT (used by API + legacy controls) ==================

    void TryMoveElevator(int targetFloor)
    {
        if (targetFloor < minFloor || targetFloor > maxFloor) return;

        int direction = targetFloor > currentElevatorFloor ? +1 : -1;

        if (moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
        }
        moveRoutine = StartCoroutine(MoveElevatorToFloor(targetFloor, direction));
    }

    IEnumerator MoveElevatorToFloor(int targetFloor, int direction)
    {
        isMoving = true;

        if (directionText != null)
        {
            directionText.text = direction > 0 ? "↑" : "↓";
        }

        float startY = elevatorCab.localPosition.y;
        float targetY = baseHeight + targetFloor * floorHeight;
        float elapsed = 0f;

        int floorDelta = Mathf.Max(1, Mathf.Abs(targetFloor - currentElevatorFloor));
        float totalTime = travelTimePerFloor * floorDelta; // 5s per floor

        Debug.Log($"[Elevator] Moving from floor {currentElevatorFloor} to {targetFloor} in {totalTime:F2}s");

        while (elapsed < totalTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalTime);
            float newY = Mathf.Lerp(startY, targetY, t);
            Vector3 pos = elevatorCab.localPosition;
            pos.y = newY;
            elevatorCab.localPosition = pos;
            yield return null;
        }

        currentElevatorFloor = targetFloor;
        isMoving = false;

        if (directionText != null)
        {
            directionText.text = "-";
        }

        UpdateUI();
        moveRoutine = null;
    }

    void UpdateUI()
    {
        if (userFloorText != null)
        {
            userFloorText.text = "You: " + userFloor;
        }
        if (elevatorFloorText != null)
        {
            elevatorFloorText.text = "Elevator: " + currentElevatorFloor;
        }
    }

    // ========================== API POLLING ==========================

    IEnumerator PollApiRoutine()
    {
        Debug.Log("[Elevator] Starting API poll loop…");
        while (true)
        {
            yield return StartCoroutine(GetFloorFromApi());
            yield return new WaitForSeconds(pollInterval);
        }
    }

    IEnumerator GetFloorFromApi()
    {
        using (UnityWebRequest www = UnityWebRequest.Get(apiUrl))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                string msg = "[Elevator] API error: " + www.error;
                Debug.LogWarning(msg);
                if (apiDebugText != null) apiDebugText.text = msg;
                yield break;
            }

            string json = www.downloadHandler.text.Trim();
            string rawMsg = "[Elevator] Raw JSON: " + json;
            Debug.Log(rawMsg);
            if (apiDebugText != null) apiDebugText.text = rawMsg;

            ApiResponse resp;
            try
            {
                resp = JsonUtility.FromJson<ApiResponse>(json);
            }
            catch
            {
                string msg = "[Elevator] Failed to parse JSON: " + json;
                Debug.LogWarning(msg);
                if (apiDebugText != null) apiDebugText.text = msg;
                yield break;
            }

            int apiFloor = Mathf.Clamp(resp.value, minFloor, maxFloor);
            string floorMsg = $"[Elevator] API floor = {apiFloor}, current = {currentElevatorFloor}";
            Debug.Log(floorMsg);
            if (apiDebugText != null) apiDebugText.text = floorMsg;

            HandleApiFloor(apiFloor);
        }
    }

    void HandleApiFloor(int apiFloor)
    {
        if (apiFloor == currentElevatorFloor)
        {
            // nothing to do
            return;
        }

        if (isMoving)
        {
            string msg = $"[Elevator] Ignoring API floor {apiFloor} (already moving).";
            Debug.Log(msg);
            if (apiDebugText != null) apiDebugText.text = msg;
            return;
        }

        string acceptMsg = $"[Elevator] Accepting API floor {apiFloor}, starting movement.";
        Debug.Log(acceptMsg);
        if (apiDebugText != null) apiDebugText.text = acceptMsg;

        TryMoveElevator(apiFloor);
    }
}