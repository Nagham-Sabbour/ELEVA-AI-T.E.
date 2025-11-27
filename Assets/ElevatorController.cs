using UnityEngine;
using UnityEngine.XR;
using TMPro;
// using UnityEngine.InputSystem;

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

    private int currentElevatorFloor = 0;
    private int userFloor = 0;
    private bool isMoving = false;

    // For button edge detection
    private bool leftPrimaryLast = false;
    private bool leftSecondaryLast = false;
    private bool rightPrimaryLast = false;
    private bool rightSecondaryLast = false;

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
    }

    void Update()
    {
        HandleLeftController();
        HandleRightController();
    }

    void HandleLeftController()
    {
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool primaryPressed = false;
        bool secondaryPressed = false;

        leftHand.TryGetFeatureValue(CommonUsages.primaryButton, out primaryPressed);
        leftHand.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryPressed);

        // primary = floor up, secondary = floor down (your chosen floor)
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

    void HandleRightController()
    {
        if (isMoving) return; // Don't accept new commands while moving

        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        bool primaryPressed = false;
        bool secondaryPressed = false;

        rightHand.TryGetFeatureValue(CommonUsages.primaryButton, out primaryPressed);
        rightHand.TryGetFeatureValue(CommonUsages.secondaryButton, out secondaryPressed);

        // primary = elevator up, secondary = elevator down
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

    void ChangeUserFloor(int delta)
    {
        int oldFloor = userFloor;
        int newFloor = Mathf.Clamp(userFloor + delta, minFloor, maxFloor);

        int floorDelta = newFloor - oldFloor;

        if (floorDelta == 0) return;

        // Move the entire rig vertically so that the new floor aligns with the real floor.
        // Increasing user floor (e.g. 0 -> 3) should move the rig down by 3 * floorHeight.
        Vector3 pos = transform.position;
        pos.y -= floorDelta * floorHeight;
        transform.position = pos;

        userFloor = newFloor;
        UpdateUI();
    }

    void TryMoveElevator(int direction)
    {
        int targetFloor = currentElevatorFloor + direction;
        if (targetFloor < minFloor || targetFloor > maxFloor) return;

        StopAllCoroutines();
        StartCoroutine(MoveElevatorToFloor(targetFloor, direction));
    }

    System.Collections.IEnumerator MoveElevatorToFloor(int targetFloor, int direction)
    {
        isMoving = true;
        if (directionText != null)
        {
            directionText.text = direction > 0 ? "↑" : "↓";
        }

        float startY = elevatorCab.localPosition.y;
        float targetY = baseHeight + targetFloor * floorHeight;
        float elapsed = 0f;

        while (elapsed < travelTimePerFloor)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / travelTimePerFloor);
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

    // void Update()
    // {
    //     #if UNITY_EDITOR
    //     var kb = Keyboard.current;
    //     if (kb != null)
    //     {
    //         if (kb.upArrowKey.wasPressedThisFrame)
    //             TryMoveElevator(+1);
    //         if (kb.downArrowKey.wasPressedThisFrame)
    //             TryMoveElevator(-1);
    //     }
    //     #endif
    // }

}