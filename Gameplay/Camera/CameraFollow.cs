using Photon.Pun;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MultiplayerFramework
{
    public class CameraFollow : MonoBehaviourPun
    {
        public enum CameraState
        {
            Follow,
            Orbit,
            POV
        }

        [Header("Target Settings")]
        public Transform target;
        public Transform cockpitPoint; 
        public Vector3 offset = new Vector3(0, 5, -7);
        public Vector3 lookOffset = new Vector3(0, 2, 0);

        [Header("Follow Settings")]
        public float followSpeed = 6f;
        public float rotationSmoothSpeed = 10f;

        [Header("Orbit Settings")]
        public float mouseSensitivity = 3f;
        public bool invertY = false;
        public float orbitCenterHeight = 2f;
        public float minZoom = 2f;
        public float maxZoom = 15f;
        public float zoomSpeed = 5f;
        public float followSmooth = 6f; // orbit pozíció finomításához

        private float yaw = 0f;
        private float pitch = 30f;
        private float currentZoom = 5f;
        private float yawSmoothVelocity;
        private float pitchSmoothVelocity;
        private float zoomSmoothVelocity;

        [Header("POV Settings")]
        public Vector3 povPosition = new Vector3(0, 2, 0);
        public Quaternion povRotation = Quaternion.Euler(10, 0, 0);
        public float minHeightAboveGround = 1f;
        public string groundLayerName = "L";

        public CameraState currentState = CameraState.Follow;

        void LateUpdate()
        {
            if (target == null) return;

            HandleCameraToggle();

            switch (currentState)
            {
                case CameraState.Follow:
                    UpdateFollowCam();
                    break;
                case CameraState.Orbit:
                    UpdateOrbitCam();
                    break;
                case CameraState.POV:
                    UpdatePOVCam();
                    break;
            }
        }

        void HandleCameraToggle()
        {
            if (Keyboard.current.cKey.wasPressedThisFrame)
            {
                currentState = (CameraState)(((int)currentState + 1) % 3);
            }
        }

        // ---- EGYSZERŰ FOLLOW CAM ----
        void UpdateFollowCam()
        {
            Quaternion rotation = Quaternion.Euler(0, target.eulerAngles.y, 0);
            Vector3 desiredPosition = target.position + rotation * offset;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSpeed * Time.deltaTime);

            Vector3 lookTarget = target.position + target.TransformDirection(lookOffset);
            Quaternion desiredRotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desiredRotation, rotationSmoothSpeed * Time.deltaTime);
        }

        // ---- EREDETI SIMA ORBIT CAM ----
        void UpdateOrbitCam()
        {
            if (Mouse.current.rightButton.isPressed)
            {
                float mouseX = Mouse.current.delta.x.ReadValue() * mouseSensitivity;
                float mouseY = Mouse.current.delta.y.ReadValue() * mouseSensitivity;

                yaw += mouseX;
                pitch += invertY ? mouseY : -mouseY;
                pitch = Mathf.Clamp(pitch, -30f, 80f);
            }

            float scroll = Mouse.current.scroll.ReadValue().y;
            currentZoom -= scroll * zoomSpeed * Time.deltaTime;
            currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);

            Vector3 orbitCenter = target.position + Vector3.up * orbitCenterHeight;
            Vector3 zoomedOffset = offset.normalized * currentZoom;

            Quaternion rotation = Quaternion.Euler(pitch, yaw, 0);
            Vector3 desiredPosition = orbitCenter + rotation * zoomedOffset;

            transform.position = Vector3.Lerp(transform.position, desiredPosition, followSmooth * Time.deltaTime);
            transform.LookAt(orbitCenter);
        }

        // ---- POV CAM ----
        void UpdatePOVCam()
        {
            Vector3 desiredPosition;

            if (cockpitPoint != null)
            {
                desiredPosition = cockpitPoint.position;
                transform.rotation = cockpitPoint.rotation;
            }
            else
            {
                desiredPosition = target.position + target.TransformVector(povPosition);
                transform.rotation = target.rotation * povRotation;
            }

            int groundLayer = LayerMask.GetMask(groundLayerName);
            Vector3 rayOrigin = desiredPosition + Vector3.up * 5f;
            float rayDistance = 20f;

            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, rayDistance, groundLayer))
            {
                desiredPosition.y = Mathf.Max(desiredPosition.y, hit.point.y + minHeightAboveGround);
            }
            else
            {
                desiredPosition.y = Mathf.Max(desiredPosition.y, minHeightAboveGround);
            }

            transform.position = desiredPosition;
        }

        public void SetTarget(Transform newTarget)
        {
            target = newTarget;
        }
    }
}
