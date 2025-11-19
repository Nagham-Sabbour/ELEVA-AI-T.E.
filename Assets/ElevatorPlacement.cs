using UnityEngine;
using UnityEngine.XR;

public class ElevatorPlacement : MonoBehaviour
{
    [Header("References")]
    public Transform xrCamera;              // The VR camera (HMD)

    [Header("Settings")]
    public float initialDistance = 2f;      // How far in front of your head to place the rig
    public float assumedEyeHeight = 1.6f;   // Approx eye height above floor in meters
    public float moveSpeed = 1.5f;          // Speed for joystick movement on XZ plane

    [Header("State")]
    public bool placementDone = false;      // true = locked, false = can move/rotate

    // Internal state
    float baseFloorY;                       // Floor height for floor 0 at startup

    bool isRotating = false;
    float grabStartRigYaw;
    float grabStartHandYaw;

    bool rightTriggerLast = false;
    bool rightStickClickLast = false;

    bool leftTriggerLast = false;
    float lastLeftTriggerClickTime = -999f;
    const float doubleClickInterval = 0.3f;

    void Start()
    {
        // Auto-assign camera if not set
        if (xrCamera == null)
        {
            Camera cam = Camera.main;
            if (cam != null)
                xrCamera = cam.transform;
        }

        if (xrCamera != null)
        {
            baseFloorY = xrCamera.position.y - assumedEyeHeight;
        }
        else
        {
            baseFloorY = transform.position.y;
        }

        // Initial placement: floor 0 aligned, 2m in front of view
        PlaceInFrontOfCamera(keepY: false);
    }

    void Update()
    {
        if (xrCamera == null)
            return;

        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        InputDevice leftHand  = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool rightTriggerPressed = false;
        bool stickClick = false;
        Vector2 stickAxis = Vector2.zero;

        bool leftTriggerPressed = false;

        rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out rightTriggerPressed);
        rightHand.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out stickClick);
        rightHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out stickAxis);

        leftHand.TryGetFeatureValue(CommonUsages.triggerButton, out leftTriggerPressed);

        // -------- LEFT TRIGGER: LOCK / UNLOCK (DOUBLE CLICK) --------
        if (leftTriggerPressed && !leftTriggerLast)
        {
            float now = Time.time;
            if (now - lastLeftTriggerClickTime <= doubleClickInterval)
            {
                // Double-click -> toggle lock
                placementDone = !placementDone;
                isRotating = false; // stop any ongoing rotation
                lastLeftTriggerClickTime = -999f;
            }
            else
            {
                lastLeftTriggerClickTime = now;
            }
        }
        leftTriggerLast = leftTriggerPressed;

        // If locked, ignore all placement controls
        if (placementDone)
        {
            isRotating = false;
            rightTriggerLast = rightTriggerPressed;
            rightStickClickLast = stickClick;
            return;
        }

        // -------- RIGHT STICK CLICK: SNAP IN FRONT (KEEP CURRENT FLOOR) --------
        if (stickClick && !rightStickClickLast)
        {
            // Snap in front of camera, but keep current Y so the "current floor" alignment stays correct
            PlaceInFrontOfCamera(keepY: true);
        }
        rightStickClickLast = stickClick;

        // -------- RIGHT TRIGGER: ROTATE ONLY (NO TRANSLATION) --------
        if (rightTriggerPressed && !rightTriggerLast)
        {
            StartRotation(rightHand);
        }
        else if (!rightTriggerPressed && rightTriggerLast)
        {
            isRotating = false;
        }
        rightTriggerLast = rightTriggerPressed;

        if (isRotating)
        {
            UpdateRotation(rightHand);
        }

        // -------- RIGHT STICK AXIS: MOVE ON XZ PLANE (VIEW-RELATIVE) --------
        if (stickAxis.sqrMagnitude > 0.001f)
        {
            MoveWithStick(stickAxis);
        }
    }

    // Snap in front of camera; keepY = true to preserve current floor alignment
    void PlaceInFrontOfCamera(bool keepY)
    {
        if (xrCamera == null) return;

        // Camera forward flattened to XZ
        Vector3 forward = xrCamera.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = xrCamera.transform.forward;
            forward.y = 0f;
        }
        forward.Normalize();

        Vector3 pos = xrCamera.position + forward * initialDistance;

        if (keepY)
        {
            // Preserve current vertical offset so current floor stays aligned
            pos.y = transform.position.y;
        }
        else
        {
            // Initial placement: align floor 0 with real floor
            pos.y = baseFloorY;
        }

        transform.position = pos;

        // Yaw matches camera, stay perfectly upright
        Vector3 euler = transform.eulerAngles;
        euler.x = 0f;
        euler.y = xrCamera.eulerAngles.y;
        euler.z = 0f;
        transform.eulerAngles = euler;
    }

    void StartRotation(InputDevice rightHand)
    {
        Quaternion handRot;
        if (!rightHand.TryGetFeatureValue(CommonUsages.deviceRotation, out handRot))
            return;

        Vector3 handEuler = handRot.eulerAngles;
        grabStartHandYaw = handEuler.y;
        grabStartRigYaw = transform.eulerAngles.y;
        isRotating = true;
    }

    void UpdateRotation(InputDevice rightHand)
    {
        Quaternion handRot;
        if (!rightHand.TryGetFeatureValue(CommonUsages.deviceRotation, out handRot))
            return;

        Vector3 handEuler = handRot.eulerAngles;
        float currentHandYaw = handEuler.y;

        // DeltaAngle handles wrap-around at 0/360 properly
        float deltaYaw = Mathf.DeltaAngle(grabStartHandYaw, currentHandYaw);
        float newYaw = grabStartRigYaw + deltaYaw;

        Vector3 euler = transform.eulerAngles;
        euler.x = 0f;
        euler.y = newYaw;
        euler.z = 0f;
        transform.eulerAngles = euler;
    }

    void MoveWithStick(Vector2 stickAxis)
    {
        if (xrCamera == null) return;

        // Movement relative to your view, projected onto XZ plane
        Vector3 camForward = xrCamera.forward;
        camForward.y = 0f;
        camForward.Normalize();

        Vector3 camRight = xrCamera.right;
        camRight.y = 0f;
        camRight.Normalize();

        Vector3 moveDir = camRight * stickAxis.x + camForward * stickAxis.y;
        Vector3 pos = transform.position + moveDir * moveSpeed * Time.deltaTime;

        // Keep current height (floor alignment)
        pos.y = transform.position.y;

        transform.position = pos;
    }
}