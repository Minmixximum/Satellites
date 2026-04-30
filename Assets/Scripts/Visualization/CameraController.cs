using UnityEngine;

namespace SatelliteEdgeComputing.Visualization
{
    /// <summary>
    /// 相机控制器
    /// </summary>
    public class CameraController : MonoBehaviour
    {
        [Header("相机设置")]
        [SerializeField] private Camera targetCamera;
        [SerializeField] private Transform cameraTransform;
        [SerializeField] private float defaultDistance = 15000000f; // 默认距离
        [SerializeField] private Vector3 defaultRotation = new Vector3(30f, 0f, 0f); // 默认角度

        [Header("旋转控制")]
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private float rotationSpeed = 1.0f;
        [SerializeField] private float smoothRotation = 5.0f;
        [SerializeField] private float minVerticalAngle = -85f;
        [SerializeField] private float maxVerticalAngle = 85f;

        [Header("平移控制")]
        [SerializeField] private bool enablePanning = true;
        [SerializeField] private float panSpeed = 0.5f;
        [SerializeField] private KeyCode panKey = KeyCode.Mouse2; // 鼠标中键

        [Header("缩放控制")]
        [SerializeField] private bool enableZoom = true;
        [SerializeField] private float zoomSpeed = 500000f;
        [SerializeField] private float minZoomDistance = 6500000f; // 地球表面附近
        [SerializeField] private float maxZoomDistance = 50000000f; // 远距离离视图
        [SerializeField] private float smoothZoom = 5.0f;

        [Header("聚焦控制")]
        [SerializeField] private bool enableFocus = true;
        [SerializeField] private float focusSpeed = 5.0f;
        [SerializeField] private float focusDistanceMultiplier = 1.5f;
        [SerializeField] private LayerMask focusableLayers = -1;

        [Header("Inertia Settings")]
        [SerializeField] private bool enableInertia = true;
        [SerializeField] private float rotationInertia = 0.95f;
        [SerializeField] private float zoomInertia = 0.9f;

        [Header("Clipping Planes")]
        [SerializeField] private float nearClipPlane = 1000f;
        [SerializeField] private float farClipPlane = 100000000f; // 足够远以看到整个地球和卫星

        // 状态变量
        private Vector3 currentRotation;
        private float currentDistance;
        private Vector3 targetPosition;
        private Quaternion targetRotation;
        private Vector3 rotationVelocity;
        private float zoomVelocity;
        private bool isRotating = false;
        private bool isPanning = false;
        private Vector3 lastMousePosition;
        private Transform focusTarget;
        private Vector3 focusOffset;
        private bool isFocusing = false;
        private float focusProgress = 0f;

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(Camera camera = null)
        {
            if (camera != null)
            {
                targetCamera = camera;
            }
            else if (targetCamera == null)
            {
                targetCamera = Camera.main;
                if (targetCamera == null)
                {
                    Debug.LogError("Main camera not found.");
                    return;
                }
            }

            cameraTransform = targetCamera.transform;

            // 设置相机裁剪面（根据地球比例）
            targetCamera.nearClipPlane = nearClipPlane;
            targetCamera.farClipPlane = farClipPlane;

            // 设置初始位置和旋转
            currentRotation = defaultRotation;
            currentDistance = defaultDistance;

            UpdateTargetTransform();

            // 立即应用
            cameraTransform.rotation = targetRotation;
            cameraTransform.position = targetPosition;

            Debug.Log("相机控制器初始化完成");
        }

        /// <summary>
        /// 重置相机到默认位置
        /// </summary>
        public void ResetCamera()
        {
            currentRotation = defaultRotation;
            currentDistance = defaultDistance;
            focusTarget = null;
            isFocusing = false;
            UpdateTargetTransform();
        }

        /// <summary>
        /// 聚焦到目标
        /// </summary>
        public void FocusOnTarget(Transform target, float customDistance = -1)
        {
            if (target == null) return;

            focusTarget = target;
            isFocusing = true;
            focusProgress = 0f;

            // 计算聚焦距离
            if (customDistance > 0)
            {
                currentDistance = Mathf.Clamp(customDistance, minZoomDistance, maxZoomDistance);
            }
            else
            {
                // 根据目标大小自动计算距离
                Renderer renderer = target.GetComponent<Renderer>();
                float targetSize = 1f;
                if (renderer != null)
                {
                    targetSize = renderer.bounds.size.magnitude;
                }
                currentDistance = targetSize * focusDistanceMultiplier;
                currentDistance = Mathf.Clamp(currentDistance, minZoomDistance, maxZoomDistance);
            }

            // 计算聚焦偏移（使目标在视野中心）
            focusOffset = Vector3.zero;
        }

        /// <summary>
        /// 聚焦到地球
        /// </summary>
        public void FocusOnEarth(float distance = -1)
        {
            GameObject earth = GameObject.Find("Earth");
            if (earth != null)
            {
                FocusOnTarget(earth.transform, distance);
            }
            else
            {
                // 聚焦到原点（地球中心）
                focusTarget = null;
                isFocusing = true;
                focusProgress = 0f;
                focusOffset = Vector3.zero;

                if (distance > 0)
                {
                    currentDistance = Mathf.Clamp(distance, minZoomDistance, maxZoomDistance);
                }
                else
                {
                    currentDistance = defaultDistance;
                }
            }
        }

        /// <summary>
        /// 更新目标变换
        /// </summary>
        private void UpdateTargetTransform()
        {
            // 计算目标旋转
            targetRotation = Quaternion.Euler(currentRotation.x, currentRotation.y, 0);

            // 计算目标位置
            Vector3 direction = targetRotation * Vector3.forward;
            targetPosition = -direction * currentDistance;
        }

        void Start()
        {
            Initialize();
        }

        void Update()
        {
            HandleInput();
            UpdateFocus();
            ApplyTransform();
        }

        /// <summary>
        /// 处理输入
        /// </summary>
        private void HandleInput()
        {
            // 旋转
            if (enableRotation && !isPanning)
            {
                HandleRotation();
            }

            // 平移
            if (enablePanning)
            {
                HandlePanning();
            }

            // 缩放
            if (enableZoom)
            {
                HandleZoom();
            }

            // 聚焦
            if (enableFocus && Input.GetMouseButtonDown(0) && Input.GetKey(KeyCode.LeftControl))
            {
                HandleClickFocus();
            }

            // 重置
            if (Input.GetKeyDown(KeyCode.R))
            {
                ResetCamera();
            }
        }

        /// <summary>
        /// 处理旋转输入
        /// </summary>
        private void HandleRotation()
        {
            if (Input.GetMouseButtonDown(0) && !Input.GetKey(KeyCode.LeftControl))
            {
                isRotating = true;
                lastMousePosition = Input.mousePosition;
            }

            if (Input.GetMouseButtonUp(0))
            {
                isRotating = false;
            }

            if (isRotating)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                lastMousePosition = Input.mousePosition;

                // 应用旋转
                currentRotation.y += delta.x * rotationSpeed;
                currentRotation.x -= delta.y * rotationSpeed;
                currentRotation.x = Mathf.Clamp(currentRotation.x, minVerticalAngle, maxVerticalAngle);

                // 记录速度（用于惯性）
                rotationVelocity = delta * rotationSpeed;

                // 取消聚焦
                isFocusing = false;
                focusTarget = null;

                UpdateTargetTransform();
            }
            else if (enableInertia && rotationVelocity.magnitude > 0.01f)
            {
                // 应用惯性
                currentRotation.y += rotationVelocity.x;
                currentRotation.x -= rotationVelocity.y;
                currentRotation.x = Mathf.Clamp(currentRotation.x, minVerticalAngle, maxVerticalAngle);

                rotationVelocity *= rotationInertia;

                UpdateTargetTransform();
            }
        }

        /// <summary>
        /// 处理平移输入
        /// </summary>
        private void HandlePanning()
        {
            if (Input.GetKeyDown(panKey))
            {
                isPanning = true;
                lastMousePosition = Input.mousePosition;
            }

            if (Input.GetKeyUp(panKey))
            {
                isPanning = false;
            }

            if (isPanning)
            {
                Vector3 delta = Input.mousePosition - lastMousePosition;
                lastMousePosition = Input.mousePosition;

                // 将屏幕delta转换为世界空间平移
                Vector3 right = cameraTransform.right;
                Vector3 up = cameraTransform.up;

                Vector3 panDelta = (-right * delta.x + -up * delta.y) * panSpeed * currentDistance / 1000f;
                targetPosition += panDelta;

                // 取消聚焦
                isFocusing = false;
                focusTarget = null;
            }
        }

        /// <summary>
        /// 处理缩放输入
        /// </summary>
        private void HandleZoom()
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.01f)
            {
                // 计算缩放
                float zoomDelta = scroll * zoomSpeed;
                currentDistance -= zoomDelta;
                currentDistance = Mathf.Clamp(currentDistance, minZoomDistance, maxZoomDistance);

                // 记录速度（用于惯性）
                zoomVelocity = zoomDelta;

                // 取消聚焦
                isFocusing = false;
                focusTarget = null;

                UpdateTargetTransform();
            }
            else if (enableInertia && Mathf.Abs(zoomVelocity) > 0.1f)
            {
                // 应用惯性
                currentDistance -= zoomVelocity;
                currentDistance = Mathf.Clamp(currentDistance, minZoomDistance, maxZoomDistance);

                zoomVelocity *= zoomInertia;

                UpdateTargetTransform();
            }
        }

        /// <summary>
        /// 处理点击聚焦
        /// </summary>
        private void HandleClickFocus()
        {
            Ray ray = targetCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, focusableLayers))
            {
                Transform hitTransform = hit.transform;

                // 检查是否是可聚焦的目标（卫星、地面站等）
                if (hitTransform.name.Contains("Satellite") || hitTransform.name.Contains("GroundStation") ||
                    hitTransform.name.Contains("Earth"))
                {
                    FocusOnTarget(hitTransform);
                }
            }
        }

        /// <summary>
        /// 更新聚焦动画
        /// </summary>
        private void UpdateFocus()
        {
            if (!isFocusing || focusProgress >= 1f) return;

            focusProgress += Time.deltaTime * focusSpeed;

            if (focusTarget != null)
            {
                // 计算目标位置（围绕焦点目标）
                Vector3 targetFocusPosition = focusTarget.position + focusOffset;
                Vector3 desiredPosition = targetFocusPosition - cameraTransform.forward * currentDistance;

                // 平滑插值
                targetPosition = Vector3.Lerp(targetPosition, desiredPosition, focusProgress);

                // 使相机朝向目标
                Vector3 directionToTarget = (targetFocusPosition - cameraTransform.position).normalized;
                Quaternion desiredRotation = Quaternion.LookRotation(directionToTarget);
                targetRotation = Quaternion.Slerp(targetRotation, desiredRotation, focusProgress);
            }
            else
            {
                // 聚焦到原点（地球中心）
                Vector3 desiredPosition = -cameraTransform.forward * currentDistance;
                targetPosition = Vector3.Lerp(targetPosition, desiredPosition, focusProgress);
                targetRotation = Quaternion.Slerp(targetRotation, Quaternion.Euler(currentRotation), focusProgress);
            }

            if (focusProgress >= 1f)
            {
                isFocusing = false;
            }
        }

        /// <summary>
        /// 应用变换
        /// </summary>
        private void ApplyTransform()
        {
            if (cameraTransform == null) return;

            // 平滑插值
            cameraTransform.rotation = Quaternion.Slerp(cameraTransform.rotation, targetRotation, Time.deltaTime * smoothRotation);
            cameraTransform.position = Vector3.Lerp(cameraTransform.position, targetPosition, Time.deltaTime * smoothZoom);
        }

        /// <summary>
        /// 设置旋转是否启用
        /// </summary>
        public void SetRotationEnabled(bool enabled)
        {
            enableRotation = enabled;
            if (!enabled)
            {
                isRotating = false;
            }
        }

        /// <summary>
        /// 设置平移是否启用
        /// </summary>
        public void SetPanningEnabled(bool enabled)
        {
            enablePanning = enabled;
            if (!enabled)
            {
                isPanning = false;
            }
        }

        /// <summary>
        /// 设置缩放是否启用
        /// </summary>
        public void SetZoomEnabled(bool enabled)
        {
            enableZoom = enabled;
        }

        /// <summary>
        /// 设置聚焦是否启用
        /// </summary>
        public void SetFocusEnabled(bool enabled)
        {
            enableFocus = enabled;
        }

        /// <summary>
        /// 设置相机距离
        /// </summary>
        public void SetCameraDistance(float distance)
        {
            currentDistance = Mathf.Clamp(distance, minZoomDistance, maxZoomDistance);
            UpdateTargetTransform();
        }

        /// <summary>
        /// 获取当前相机距离
        /// </summary>
        public float GetCameraDistance()
        {
            return currentDistance;
        }
    }
}
