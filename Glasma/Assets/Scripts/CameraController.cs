
using System.Collections.Generic;
using UnityEngine;

namespace Glasma.Behaviours
{
    public class CameraController : MonoBehaviour
    {
        [Header("Movement")]
        public float damping = 1f;
        public float movementSpeed = 10f;
        public float fastMovementSpeed = 100f;

        [Header("Look & Orbit")]
        public float freeLookSensitivity = 3f;
        public float orbitSensitivity = 2f;
        
        [Header("Zoom")]
        public float zoomSensitivity = 10f;
        public bool followMouseWhileZooming;
        
        [Header("Orbit Settings")]
        public Vector3 orbitPoint = Vector3.zero;

        [Header("Orbit Behavior")]
        public bool enableOrbitInOrthographic = true;
        
        private bool looking;
        private bool orbiting;
        private Transform camTransform;
        private Camera cam;

        private Vector3 clickPoint;
        private float targetSize;
        private bool dragging;
        
        // Orbit state
        private Vector3 orbitCenter;
        private float orbitDistance;
        private float orbitYaw;
        private float orbitPitch;
        private float targetFov;
        public bool IsPerspective => !cam.orthographic;

        private void Awake()
        {
            cam = Camera.main;
            camTransform = transform;
            targetSize = cam.orthographicSize;
            targetFov = cam.fieldOfView;
            // Initialize orbit parameters
            UpdateOrbitCenter();
            CalculateOrbitFromCurrentPosition();
            FocusOnTarget();
        }

        private void UpdateOrbitCenter()
        {
            orbitCenter = orbitPoint;
        }
        private List<(float pitch, float yaw)> snapOrientations = new List<(float pitch, float yaw)>
        {
            (90f, 0),
            (-90f, 0),
            (0f, 0f),
            (0f, 180f), 
            (0f, 90f),
            (0f, -90f),

            (-45, 0),
            (-45, 90),
            (-45, -90),
            (-45, 180),
            
            (45, 0),
            (45, 90),
            (45, -90),
            (45, 180),
            
            (0, 45f),
            (0, 135f),
            (0, 225f),
            (0, 315f),
            
            (35.3f, 45f),
            (35.3f, 135f), 
            (35.3f, 225f),
            (35.3f, 315f),
            
            (-35.3f, 45f),
            (-35.3f, 135f),
            (-35.3f, 225f),
            (-35.3f, 315f)
        };
        public void SnapToClosestDiscreteView()
        {
            float snappedYaw = Mathf.Round(orbitYaw / 90f) * 90f;
            snapOrientations[0] = (90f, snappedYaw);
            snapOrientations[1] = (-90f, snappedYaw);
            float minDistance = float.MaxValue;
            float bestPitch = orbitPitch;
            float bestYaw = orbitYaw;
    
            foreach (var orientation in snapOrientations)
            {
                float targetPitch = orientation.pitch;
                float targetYaw = orientation.yaw;

                float pitchDiff = Mathf.Abs(targetPitch - orbitPitch);
                float yawDiff = Mathf.Abs(Mathf.DeltaAngle(orbitYaw, targetYaw));
        

                float totalDistance = pitchDiff + yawDiff;
        
                if (totalDistance < minDistance)
                {
                    minDistance = totalDistance;
                    bestPitch = targetPitch;
                    bestYaw = targetYaw;
                }
            }

            orbitPitch = bestPitch;
            orbitYaw = bestYaw;
            ApplyOrbitPosition();
        }
        
        private void CalculateOrbitFromCurrentPosition()
        {
            UpdateOrbitCenter();
            Vector3 offset = camTransform.position - orbitCenter;
            orbitDistance = offset.magnitude;
            orbitYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            orbitPitch = Mathf.Asin(offset.y / orbitDistance) * Mathf.Rad2Deg;
        }
        
        public void SetOrbitPoint(Vector3 point)
        {
            orbitPoint = point;
            CalculateOrbitFromCurrentPosition();
        }

        public void SetSize(float size)
        {
            cam.orthographicSize = size;
            targetSize = size;
        }

        private void ProcessPerspective()
        {
            var fastMode = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var speed = fastMode ? fastMovementSpeed : movementSpeed;
            speed /= damping;
            var delta = speed * Time.deltaTime;
            
            if (Input.GetKey(KeyCode.A))
            {
                camTransform.position = transform.position + (-camTransform.right * delta);
            }
        
            if (Input.GetKey(KeyCode.D))
            {
                camTransform.position = transform.position + (camTransform.right * delta);
            }

            if (Input.GetKey(KeyCode.W))
            {
                camTransform.position = transform.position + (camTransform.forward * delta);

            }

            if (Input.GetKey(KeyCode.S))
            {
                camTransform.position = transform.position + (-camTransform.forward * delta);

            }

            if (Input.GetKeyDown(KeyCode.Mouse1))
            {
                StartLooking();
            }
            else if (Input.GetKeyUp(KeyCode.Mouse1))
            {
                StopLooking();
            }
            
            var axis = Input.mouseScrollDelta.y;
            targetFov += axis * zoomSensitivity * targetFov;
            targetFov = Mathf.Clamp(targetFov, 1, 150);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, Time.deltaTime * 5);
            
            if (looking)
            {
                var euler = camTransform.eulerAngles;
                var newRotationX = euler.y + Input.GetAxis("Mouse X") * freeLookSensitivity;
                var newRotationY = euler.x - Input.GetAxis("Mouse Y") * freeLookSensitivity;
                camTransform.localEulerAngles = new Vector3(newRotationY, newRotationX, 0f);
            }
        }
        
        private void StartLooking()
        {
            looking = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void StopLooking()
        {
            looking = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        private void ProcessOrthographic()
        {
            // Middle mouse for panning
            if (Input.GetMouseButtonDown(2))
            {
                clickPoint = cam.ScreenToWorldPoint(Input.mousePosition);
                dragging = true;
            }
            
            if (Input.GetMouseButtonUp(2))
            {
                dragging = false;
            }
            if (Input.GetKeyDown(KeyCode.Q))
            {
                SnapToClosestDiscreteView();
            }
            if (dragging)
            {
                Vector3 difference = clickPoint - cam.ScreenToWorldPoint(Input.mousePosition);
                camTransform.position += difference;
                orbitPoint += difference;
                if (!orbiting)
                {
                    CalculateOrbitFromCurrentPosition();
                }
            }
     
            // Mouse scroll for zoom
            var axis = Input.mouseScrollDelta.y;
            if (axis != 0)
            {
                targetSize -= (axis * zoomSensitivity * 0.1f);
                targetSize = Mathf.Max(0.1f, targetSize);
            }
            var mouseOffsetBefore = cam.ScreenToWorldPoint(Input.mousePosition);
            cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, Time.deltaTime * 10f);
            var mouseOffsetAfter = cam.ScreenToWorldPoint(Input.mousePosition);
            if (followMouseWhileZooming)
            {
                camTransform.position += (mouseOffsetBefore - mouseOffsetAfter);
                orbitPoint += mouseOffsetBefore - mouseOffsetAfter;
            }

            if (enableOrbitInOrthographic)
            {
                if (Input.GetMouseButtonDown(1))
                {
                    StartOrbiting();
                }
                else if (Input.GetMouseButtonUp(1))
                {
                    StopOrbiting();
                }
                
                if (orbiting)
                {
                    ProcessOrbiting();
                }
            }
        }

        private void ProcessOrbiting()
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");
            
            // Update orbit angles based on mouse movement
            orbitYaw += mouseX * orbitSensitivity;
            orbitPitch -= mouseY * orbitSensitivity;
            
            // Clamp pitch to prevent flipping
            orbitPitch = Mathf.Clamp(orbitPitch, -89.99f, 89.9f);
            
            ApplyOrbitPosition();
        }

        private void ApplyOrbitPosition()
        {
            UpdateOrbitCenter();
            float yawRad = orbitYaw * Mathf.Deg2Rad;
            float pitchRad = orbitPitch * Mathf.Deg2Rad;
            
            Vector3 offset = new Vector3(
                Mathf.Sin(yawRad) * Mathf.Cos(pitchRad),
                Mathf.Sin(pitchRad),
                Mathf.Cos(yawRad) * Mathf.Cos(pitchRad)
            ) * orbitDistance;
            
            camTransform.position = orbitCenter + offset;
            
            camTransform.LookAt(orbitCenter);
        }
        
        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                Application.Quit();
            }

            if (cam.orthographic)
            {
                ProcessOrthographic();
            }
            else
            {
                ProcessPerspective();
            }
        }

        private void OnDisable()
        {
            StopOrbiting();
        }

        private void StartOrbiting()
        {
            orbiting = true;
            looking = true;
            Cursor.visible = false;
            Cursor.lockState = CursorLockMode.Locked;
            
            CalculateOrbitFromCurrentPosition();
        }

        private void StopOrbiting()
        {
            orbiting = false;
            looking = false;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }

        public void ToggleMode()
        {
            cam.orthographic = !cam.orthographic;
            cam.nearClipPlane = cam.orthographic ? -10f : 0.01f;
            
            if (cam.orthographic)
            {
                ResetOrbitView();
                CalculateOrbitFromCurrentPosition();
            }
        }
        
        public void RotateAroundX(float degrees)
        {
            orbitPitch += degrees;
            orbitPitch = Mathf.Clamp(orbitPitch, -89f, 89f);
            ApplyOrbitPosition();
        }
        
        public void RotateAroundY(float degrees)
        {
            orbitYaw += degrees;
            ApplyOrbitPosition();
        }
        
        public void FocusOnTarget()
        {
            ApplyOrbitPosition();
        }
        
        public void ResetOrbitView()
        {
            ApplyOrbitPosition();
        }
        
        public void FrameBounds(Bounds bounds)
        {
            orbitPoint = bounds.center;
            float distance = bounds.size.magnitude;
            if (cam.orthographic)
            {
                cam.orthographicSize = bounds.size.magnitude * 0.5f;
                targetSize = cam.orthographicSize;
            }
            else
            {
                orbitDistance = distance * 1.5f;
            }
            
            ApplyOrbitPosition();
        }
    }
}
