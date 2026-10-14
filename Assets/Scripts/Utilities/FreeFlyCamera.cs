using UnityEngine;
using UnityEngine.InputSystem;

public class FreeFlyCamera : MonoBehaviour
{
    [Header("Flight Speed")]
    [SerializeField] private float normalSpeed = 40f;
    [SerializeField] private float sprintSpeed = 120f;
    [SerializeField] private float lookSensitivity = 0.15f;
    [SerializeField] private float zoomSpeed = 5f;

    private float yaw = 0f;
    private float pitch = 0f;

    private void Start()
    {
        Vector3 angles = transform.eulerAngles;
        yaw = angles.y;
        pitch = angles.x;
    }

    private void Update()
    {
        var mouse = Mouse.current;
        var keyboard = Keyboard.current;
        if (mouse == null || keyboard == null) return;

        // 1. Mouse Look (Hold Right Mouse Button)
        if (mouse.rightButton.isPressed)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            yaw += delta.x;
            pitch -= delta.y;
            pitch = Mathf.Clamp(pitch, -85f, 85f);

            transform.eulerAngles = new Vector3(pitch, yaw, 0f);
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        // 2. Keyboard Movement (WASD + Q/E)
        float speed = keyboard.leftShiftKey.isPressed ? sprintSpeed : normalSpeed;

        Vector3 move = Vector3.zero;
        if (keyboard.wKey.isPressed) move += transform.forward;
        if (keyboard.sKey.isPressed) move -= transform.forward;
        if (keyboard.dKey.isPressed) move += transform.right;
        if (keyboard.aKey.isPressed) move -= transform.right;
        if (keyboard.eKey.isPressed) move += Vector3.up;
        if (keyboard.qKey.isPressed) move -= Vector3.up;

        transform.position += move * speed * Time.deltaTime;

        // 3. Scroll Wheel Zoom
        float scrollY = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) > 0.01f)
        {
            transform.position += transform.forward * Mathf.Sign(scrollY) * zoomSpeed * 3f;
        }
    }
}