using UnityEngine;
using UnityEngine.XR;
using TMPro;

public class UserFloorPanelController : MonoBehaviour
{
    [Header("References")]
    public ElevatorController elevator;  // ElevatorRig’s ElevatorController
    public TextMeshPro floorText;
    public Transform upButton;           // UpButton transform
    public Transform downButton;         // DownButton transform

    [Header("Ray Settings")]
    public float maxRayDistance = 5f;
    public LineRenderer rayLine;

    private bool leftTriggerLast = false;

    void Update()
    {
        // Keep text in sync with current user floor
        if (elevator != null && floorText != null)
        {
            floorText.text = elevator.CurrentUserFloor.ToString();
        }

        // Get left-hand device
        InputDevice leftHand = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);

        bool triggerPressed = false;
        leftHand.TryGetFeatureValue(CommonUsages.triggerButton, out triggerPressed);

        Vector3 handPos;
        Quaternion handRot;
        bool hasPos = leftHand.TryGetFeatureValue(CommonUsages.devicePosition, out handPos);
        bool hasRot = leftHand.TryGetFeatureValue(CommonUsages.deviceRotation, out handRot);

        // Draw a visible ray
        if (hasPos && hasRot && rayLine != null)
        {
            // Origin slightly in front of the controller and comes out from the front of the controller
            Vector3 rayOrigin = handPos + handRot * new Vector3(0f, -0.02f, 0.05f);
            Vector3 rayDir = handRot * Vector3.down;

            rayLine.enabled = true;
            rayLine.positionCount = 2;
            rayLine.SetPosition(0, rayOrigin);
            rayLine.SetPosition(1, rayOrigin + rayDir * maxRayDistance);
        } else if (rayLine != null)
        {
            rayLine.enabled = false;
        }

        // On left trigger click, try to click a button
        if (triggerPressed && !leftTriggerLast)
        {
            if (hasPos && hasRot)
            {
                TryClickButton(handPos, handRot);
            }
        }

        leftTriggerLast = triggerPressed;
    }

    void TryClickButton(Vector3 handPos, Quaternion handRot)
    {
        if (elevator == null || upButton == null || downButton == null)
            return;

        Vector3 rayOrigin = handPos + handRot * new Vector3(0f, -0.02f, 0.05f);
        Vector3 rayDir = handRot * Vector3.down;

        if (Physics.Raycast(rayOrigin, rayDir, out RaycastHit hit, maxRayDistance))
        {
            if (hit.transform == upButton)
            {
                elevator.ChangeUserFloor(+1);
            }
            else if (hit.transform == downButton)
            {
                elevator.ChangeUserFloor(-1);
            }
        }
    }
}