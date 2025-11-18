using UnityEngine;
using UnityEngine.XR;

public class ElevatorPlacement : MonoBehaviour
{
    public Transform xrCamera;        // The VR camera (HMD)
    public float initialDistance = 2f;
    public float moveSpeed = 1f;      // meters per second when adjusting
    public bool placementDone = false;

    void Start()
    {
        if (xrCamera == null)
        {
            // Try to find the Main Camera under the XR Origin
            Camera cam = Camera.main;
            if (cam != null)
            {
                xrCamera = cam.transform;
            }
        }

        if (xrCamera != null)
        {
            // Place 2m in front of camera, on the floor (approx)
            Vector3 forward = xrCamera.forward;
            forward.y = 0f;
            forward.Normalize();

            Vector3 startPos = xrCamera.position + forward * initialDistance;
            // Assume head height ~1.6m -> elevator base at floor
            startPos.y = xrCamera.position.y - 1.6f;

            transform.position = startPos;

            // Ensure upright, no tilt
            Vector3 euler = transform.eulerAngles;
            euler.x = 0f;
            euler.z = 0f;
            transform.eulerAngles = euler;
        }
    }

    void Update()
    {
        if (placementDone) return;

        // Use right-hand joystick to slide elevator in X/Z
        InputDevice rightHand = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);

        Vector2 primary2DAxis;
        if (rightHand.TryGetFeatureValue(CommonUsages.primary2DAxis, out primary2DAxis))
        {
            // primary2DAxis.x -> left/right, .y -> forward/back
            Vector3 move = new Vector3(primary2DAxis.x, 0f, primary2DAxis.y);
            move *= moveSpeed * Time.deltaTime;

            // Move in world XZ only
            transform.position += move;
        }

        // Press right trigger to confirm placement and lock
        bool triggerPressed;
        if (rightHand.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed) && triggerPressed)
        {
            placementDone = true;
        }
    }
}