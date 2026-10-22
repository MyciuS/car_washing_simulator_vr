using UnityEngine;
using UnityEngine.XR;
using UnityEngine.InputSystem;

public class VRFallbackController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 3.5f;

    [Header("Camera Settings")]
    public float mouseSensitivity = 0.1f;
    public Transform cameraTransform;

    private float rotationX = 0f;
    private float rotationY = 0f;
    private bool isVRActive = false;

    void Start()
    {
        // Disable non-VR controls if a VR head-mounted display is active
        if (XRSettings.isDeviceActive)
        {
            isVRActive = true;
            return;
        }

        // Auto-assign Main Camera if left empty in Inspector
        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        if (cameraTransform != null)
        {
            rotationX = cameraTransform.localEulerAngles.x;
            rotationY = transform.eulerAngles.y;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        if (isVRActive) return;

        HandleRotation();
        HandleMovement();

        // Toggle mouse lock mode using new Input System
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            Cursor.lockState = (Cursor.lockState == CursorLockMode.Locked)
                ? CursorLockMode.None
                : CursorLockMode.Locked;
            Cursor.visible = !Cursor.visible;
        }
    }

    private void HandleRotation()
    {
        if (Cursor.lockState != CursorLockMode.Locked || cameraTransform == null) return;

        // Read mouse delta using new Input System
        Vector2 mouseDelta = Mouse.current != null ? Mouse.current.delta.ReadValue() * mouseSensitivity : Vector2.zero;

        rotationX -= mouseDelta.y;
        rotationX = Mathf.Clamp(rotationX, -80f, 80f);

        rotationY += mouseDelta.x;

        // Rotate character body horizontally, and camera vertically
        transform.rotation = Quaternion.Euler(0f, rotationY, 0f);
        cameraTransform.localRotation = Quaternion.Euler(rotationX, 0f, 0f);
    }

    [Header("Components")]
    public CharacterController controller; // Assign in Inspector or GetComponent in Start

    private void HandleMovement()
    {
        float moveX = 0f;
        float moveZ = 0f;

        if (Keyboard.current != null)
        {
            if (Keyboard.current.wKey.isPressed) moveZ += 1f;
            if (Keyboard.current.sKey.isPressed) moveZ -= 1f;
            if (Keyboard.current.aKey.isPressed) moveX -= 1f;
            if (Keyboard.current.dKey.isPressed) moveX += 1f;
        }

        Vector3 moveDirection = (transform.right * moveX + transform.forward * moveZ).normalized;

        // Use CharacterController.Move to respect physical colliders
        if (controller != null)
        {
            controller.Move(moveDirection * moveSpeed * Time.deltaTime);
        }
    }
}