using UnityEngine;

namespace GLaDE.UI
{
    public class MouseSwayCamera : MonoBehaviour
    {
        public Transform cam;  // Assign the cam's transform to this field.
        public float sensitivity = 2.0f; // Adjust this to control the mouse sensitivity.
        public float smoothSpeed = 5.0f; // Adjust this to control the smoothness of the camera movement.
        public float initialVerticalOffset = 5.0f; // Initial vertical offset to look slightly upwards.
        
        private Vector2 currentRotation = Vector2.zero;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None; // Unlock the cursor.
            currentRotation.y = initialVerticalOffset; // Apply the initial vertical offset.
        }

        void Update()
        {
            // Get mouse input.
            float mouseX = Input.GetAxis("Mouse X") * (sensitivity * 0.4f);
            float mouseY = Input.GetAxis("Mouse Y") * sensitivity;

            // Update the camera rotation based on mouse input.
            currentRotation.y -= mouseY;
            currentRotation.x += mouseX;

            // Clamp the vertical rotation to prevent flipping.
            currentRotation.y = Mathf.Clamp(currentRotation.y, -90f, 90f);

            // Smoothly interpolate the camera's rotation.
            Quaternion targetRotation = Quaternion.Euler(currentRotation.y, currentRotation.x, 0);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * smoothSpeed);

            // Rotate the cam's body horizontally (left and right).
            cam.Rotate(Vector3.up * mouseX);
        }
    }
}