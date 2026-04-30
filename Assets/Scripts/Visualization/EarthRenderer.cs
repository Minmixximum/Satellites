using System.Collections;
using UnityEditor;
using UnityEngine;

namespace SatelliteEdgeComputing.Visualization
{
    /// <summary>
    /// 地球渲染器
    /// </summary>
    public class EarthRenderer : MonoBehaviour
    {
        [Header("地球设置")]
        [SerializeField] private float earthRadius = 6378135f; // Unity单位（对应真实地球比例）
        [SerializeField] private float rotationSpeed = 10f; // 自转速度（度/秒，用于可视化）- 仅在 useSimTime = false 时使用
        [SerializeField] private bool enableRotation = true;
        [SerializeField] private bool useSimTime = true; // 是否使用模拟时间驱动地球旋转
        [SerializeField] private bool enableAtmosphere = false;

        [Header("引用")]
        [SerializeField] private GameObject earthSphere;
        [SerializeField] private GameObject atmosphereSphere;
        [SerializeField] private Material earthMaterial;
        [SerializeField] private Material atmosphereMaterial;
        [SerializeField] private Texture2D earthTexture;
        [SerializeField] private Texture2D nightTexture;

        [Header("昼夜效果")]
        [SerializeField] private Light sunLight;
        [SerializeField] private float dayNightCycleSpeed = 1.0f;
        [SerializeField] private bool enableDayNightCycle = true;

        private float currentRotation = 0f;
        private float dayNightProgress = 0f;
        private GameObject earthContainer; // 地球容器，用于统一旋转

        // 地球旋转参数
        private double earthRotationAngleRad = 0.0; // 当前地球旋转角度（弧度）
        private float speedFactor = 1200.0f; // 时间加速因子
        private bool useSmoothRotation = true; // 是否使用平滑旋转
        private double lastSyncAngle = 0.0; // 上次同步的角度
        private const double EARTH_ANGULAR_VELOCITY = 7.292115e-5; // 地球角速度 rad/s

        /// <summary>
        /// 获取地球容器的Transform，用于子对象（如地面站）跟随地球旋转
        /// </summary>
        public Transform EarthContainerTransform => earthContainer?.transform;

        private bool isInitialized = false;

        /// <summary>
        /// 初始化地球
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("初始化地球渲染器...");

            // 安全检查：确保地球半径合理（真实地球半径约6378km）
            if (earthRadius < 100000f)
            {
                Debug.LogWarning($"地球半径 {earthRadius} 太小，使用默认值 6378135");
                earthRadius = 6378135f;
            }

            Debug.Log($"地球渲染器初始化 - earthRadius: {earthRadius}");

            // 创建地球容器（如果不存在）
            if (earthContainer == null)
            {
                earthContainer = new GameObject("EarthContainer");
                earthContainer.transform.SetParent(transform);
                earthContainer.transform.localPosition = Vector3.zero;
                earthContainer.transform.localScale = Vector3.one;
            }

            // 创建地球球体（如果不存在或无效）
            bool needCreateSphere = (earthSphere == null);
            if (!needCreateSphere)
            {
                // 检查现有的 earthSphere 是否有有效的 Renderer
                var existingRenderer = earthSphere.GetComponent<Renderer>();
                if (existingRenderer == null)
                {
                    Debug.LogWarning($"earthSphere '{earthSphere.name}' 没有 Renderer 组件，将重新创建");
                    DestroyImmediate(earthSphere);
                    needCreateSphere = true;
                }
            }

            if (needCreateSphere)
            {
                earthSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                earthSphere.name = "Earth";
                earthSphere.transform.SetParent(earthContainer.transform);
                earthSphere.transform.localPosition = Vector3.zero;
                earthSphere.transform.localScale = Vector3.one * earthRadius * 2; // 直径
                Debug.Log($"创建新地球球体，缩放: {earthSphere.transform.localScale}");

                // 应用材质到新创建的球体
                var renderer = earthSphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    if (earthMaterial != null)
                    {
                        Debug.Log($"应用用户设置的地球材质: {earthMaterial.name}");
                        var material = new Material(earthMaterial);
                        material.doubleSidedGI = true;
                        material.SetFloat("_Cull", 0f);
                        material.EnableKeyword("_CULL_OFF");
                        renderer.material = material;
                    }
                    else
                    {
                        // 尝试加载默认材质
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Mat-Earth/earth_mat.mat");
                        if (mat != null)
                        {
                            Debug.Log("加载默认地球材质");
                            var material = new Material(mat);
                            material.doubleSidedGI = true;
                            material.SetFloat("_Cull", 0f);
                            material.EnableKeyword("_CULL_OFF");
                            renderer.material = material;
                        }
                        else
                        {
                            // 使用纯色材质
                            Debug.LogWarning("未找到地球材质，使用默认蓝色");
                            var material = new Material(Shader.Find("Standard"));
                            material.color = Color.blue;
                            material.doubleSidedGI = true;
                            material.SetFloat("_Cull", 0f);
                            material.EnableKeyword("_CULL_OFF");
                            renderer.material = material;
                        }
                    }
                }
            }
            else
            {
                // 地球球体已存在，确保材质正确应用
                earthSphere.transform.localScale = Vector3.one * earthRadius * 2; // 更新缩放
                Debug.Log($"地球球体已存在，更新缩放: {earthSphere.transform.localScale}");

                var renderer = earthSphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    // 如果设置了材质，应用它
                    if (earthMaterial != null)
                    {
                        Debug.Log($"应用用户设置的地球材质: {earthMaterial.name}");
                        var material = new Material(earthMaterial);
                        material.doubleSidedGI = true;
                        material.SetFloat("_Cull", 0f);
                        material.EnableKeyword("_CULL_OFF");
                        renderer.material = material;
                    }
                    else
                    {
                        // 尝试加载默认材质
                        Material mat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Models/Mat-Earth/earth_mat.mat");
                        if (mat != null)
                        {
                            Debug.Log("加载默认地球材质");
                            var material = new Material(mat);
                            material.doubleSidedGI = true;
                            material.SetFloat("_Cull", 0f);
                            material.EnableKeyword("_CULL_OFF");
                            renderer.material = material;
                        }
                        else
                        {
                            // 使用纯色材质
                            Debug.LogWarning("未找到地球材质，使用默认蓝色");
                            var material = new Material(Shader.Find("Standard"));
                            material.color = Color.blue;
                            material.doubleSidedGI = true;
                            material.SetFloat("_Cull", 0f);
                            material.EnableKeyword("_CULL_OFF");
                            renderer.material = material;
                        }
                    }
                }
                else
                {
                    Debug.LogError("地球球体没有 Renderer 组件！");
                }
            }

            // 确保球体网格法线正确（可选修复）
            // 注释掉，Unity原始球体法线已经正确
            // var meshFilter = earthSphere.GetComponent<MeshFilter>();
            // if (meshFilter != null && meshFilter.mesh != null)
            // {
            //     meshFilter.mesh.RecalculateNormals();
            // }

            // 创建大气层（如果启用）
            if (enableAtmosphere && atmosphereSphere == null)
            {
                atmosphereSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                atmosphereSphere.name = "Atmosphere";
                atmosphereSphere.transform.SetParent(earthContainer.transform);
                atmosphereSphere.transform.localPosition = Vector3.zero;
                atmosphereSphere.transform.localScale = Vector3.one * (earthRadius * 2 + 50000); // 稍大于地球

                // 设置大气层材质
                var renderer = atmosphereSphere.GetComponent<Renderer>();
                if (atmosphereMaterial != null)
                {
                    // 使用提供的材质，但确保双面渲染和透明设置
                    var material = new Material(atmosphereMaterial);
                    material.doubleSidedGI = true;
                    material.SetFloat("_Cull", 0f);
                    material.EnableKeyword("_CULL_OFF");
                    // 确保透明设置正确
                    material.SetFloat("_Mode", 3);
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = 3000;
                    renderer.material = material;
                }
                else
                {
                    // 创建半透明蓝色材质
                    var material = new Material(Shader.Find("Standard"));
                    material.color = new Color(0.1f, 0.3f, 0.8f, 0.1f);
                    material.SetFloat("_Mode", 3); // 透明模式
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = 3000;
                    // 启用双面渲染
                    material.doubleSidedGI = true;
                    material.SetFloat("_Cull", 0f); // 0 = Off (双面渲染)
                    material.EnableKeyword("_CULL_OFF");
                    renderer.material = material;
                }
            }
            else if (atmosphereSphere != null && enableAtmosphere)
            {
                // 大气层已存在，确保材质双面渲染和透明设置
                var renderer = atmosphereSphere.GetComponent<Renderer>();
                if (renderer != null)
                {
                    var material = renderer.material; // 获取当前材质（会创建实例）
                    material.doubleSidedGI = true;
                    material.SetFloat("_Cull", 0f);
                    material.EnableKeyword("_CULL_OFF");
                    // 确保透明设置正确
                    material.SetFloat("_Mode", 3);
                    material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    material.SetInt("_ZWrite", 0);
                    material.DisableKeyword("_ALPHATEST_ON");
                    material.EnableKeyword("_ALPHABLEND_ON");
                    material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                    material.renderQueue = 3000;
                }
            }

            // 设置太阳光（如果未指定）
            if (sunLight == null)
            {
                sunLight = FindObjectOfType<Light>();
                if (sunLight == null || sunLight.type != LightType.Directional)
                {
                    // 创建方向光
                    GameObject lightObj = new GameObject("Sun");
                    lightObj.transform.SetParent(transform);
                    lightObj.transform.rotation = Quaternion.Euler(45, 45, 0);
                    sunLight = lightObj.AddComponent<Light>();
                    sunLight.type = LightType.Directional;
                    sunLight.intensity = 1.0f;
                    sunLight.shadows = LightShadows.Soft;
                }
            }

            // 添加经纬度网格（可选）
            CreateLatLongGrid();

            isInitialized = true;
            Debug.Log("地球渲染器初始化完成");
        }

        /// <summary>
        /// 创建经纬度网格
        /// </summary>
        private void CreateLatLongGrid()
        {
            int interval_lon = 15;
            int interval_lat = 20;
            // 创建经线（子午线）
            for (int lon = 0; lon < 360; lon += interval_lon)
            {
                CreateMeridian(lon);
            }

            // 创建纬线（平行圈）
            for (int lat = -80; lat <= 80; lat += interval_lat)
            {
                CreateParallel(lat);
            }
        }

        /// <summary>
        /// 创建经线
        /// </summary>
        private void CreateMeridian(float longitude)
        {
            GameObject lineObj = new GameObject($"Meridian_{longitude}");
            lineObj.transform.SetParent(earthContainer.transform);

            var lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = new Color(1.0f, 0f, 0f, 0.7f);
            lineRenderer.endColor = new Color(1.0f, 0f, 0f, 0.7f);
            lineRenderer.startWidth = 4500f;
            lineRenderer.endWidth = 4500f;
            lineRenderer.useWorldSpace = false;

            // 生成经线上的点
            int segments = 36;
            Vector3[] points = new Vector3[segments];

            for (int i = 0; i < segments; i++)
            {
                float lat = -90 + (180f * i / (segments - 1));
                Vector3 point = Core.CoordinateConverter.GeodeticToWorld(lat, longitude, 0, earthRadius);
                points[i] = point;
            }

            lineRenderer.positionCount = segments;
            lineRenderer.SetPositions(points);
        }

        /// <summary>
        /// 创建纬线
        /// </summary>
        private void CreateParallel(float latitude)
        {
            GameObject lineObj = new GameObject($"Parallel_{latitude}");
            lineObj.transform.SetParent(earthContainer.transform);

            var lineRenderer = lineObj.AddComponent<LineRenderer>();
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = new Color(1.0f, 0f, 0f, 0.7f);
            lineRenderer.endColor = new Color(1.0f, 0f, 0f, 0.7f);
            lineRenderer.startWidth = 4500f;
            lineRenderer.endWidth = 4500f;
            lineRenderer.useWorldSpace = false;

            // 生成纬线上的点
            int segments = 72;
            Vector3[] points = new Vector3[segments];

            for (int i = 0; i < segments; i++)
            {
                float lon = -180 + (360f * i / (segments - 1));
                Vector3 point = Core.CoordinateConverter.GeodeticToWorld(latitude, lon, 0, earthRadius);
                points[i] = point;
            }

            lineRenderer.positionCount = segments;
            lineRenderer.SetPositions(points);
        }

        /// <summary>
        /// 设置地球半径
        /// </summary>
        public void SetEarthRadius(float radius)
        {
            // 安全检查：如果半径太小，使用默认值（真实地球半径约6378km）
            if (radius < 100000f)
            {
                Debug.LogWarning($"地球半径值 {radius} 太小，使用默认值 6378135");
                radius = 6378135f;
            }
            earthRadius = radius;
            if (earthSphere != null)
            {
                earthSphere.transform.localScale = Vector3.one * earthRadius * 2;
            }
            if (atmosphereSphere != null)
            {
                atmosphereSphere.transform.localScale = Vector3.one * (earthRadius * 2 + 50000);
            }
        }

        /// <summary>
        /// 设置自转速度
        /// </summary>
        public void SetRotationSpeed(float speed)
        {
            rotationSpeed = speed;
        }

        /// <summary>
        /// 启用/禁用自转
        /// </summary>
        public void SetRotationEnabled(bool enabled)
        {
            enableRotation = enabled;
        }

        /// <summary>
        /// 启用/禁用昼夜循环
        /// </summary>
        public void SetDayNightCycleEnabled(bool enabled)
        {
            enableDayNightCycle = enabled;
        }

        /// <summary>
        /// 启用/禁用大气层
        /// </summary>
        public void SetAtmosphereEnabled(bool enabled)
        {
            enableAtmosphere = enabled;
            if (atmosphereSphere != null)
            {
                atmosphereSphere.SetActive(enabled);
            }
        }

        void Start()
        {
            Initialize();

            // 订阅 SimulationManager 的地球旋转事件
            if (useSimTime && Core.SimulationManager.Instance != null)
            {
                Core.SimulationManager.Instance.OnEarthRotationUpdated += OnEarthRotationUpdated;
            }
        }

        void OnDestroy()
        {
            // 取消订阅事件
            if (Core.SimulationManager.Instance != null)
            {
                Core.SimulationManager.Instance.OnEarthRotationUpdated -= OnEarthRotationUpdated;
            }
        }

        /// <summary>
        /// 当地球旋转角度更新时调用（从SimulationManager同步）
        /// </summary>
        private void OnEarthRotationUpdated(double angleRadians)
        {
            if (!useSimTime || earthContainer == null) return;

            // 同步角度（用于与后端同步）
            lastSyncAngle = angleRadians;
            earthRotationAngleRad = angleRadians;
        }

        /// <summary>
        /// 设置时间加速因子
        /// </summary>
        public void SetSpeedFactor(float factor)
        {
            speedFactor = Mathf.Clamp(factor, 1200f, 3600f);
        }

        private int debugFrameCount = 0;  // 调试用
        
        void Update()
        {
            if (earthContainer == null)
            {
                Debug.LogWarning("[EarthRenderer] earthContainer is null!");
                return;
            }

            // 使用模拟时间驱动的平滑旋转
            if (useSimTime && useSmoothRotation)
            {
                // 从 SimulationManager 获取当前的 speedFactor
                if (Core.SimulationManager.Instance != null)
                {
                    speedFactor = Core.SimulationManager.Instance.SpeedFactor;
                }

                // 每帧根据速度因子更新旋转角度
                // delta_angle = angular_velocity * delta_time * speed_factor
                double deltaAngle = EARTH_ANGULAR_VELOCITY * Time.deltaTime * speedFactor;
                earthRotationAngleRad += deltaAngle;
                
                // 每秒输出一次调试信息
                debugFrameCount++;
                if (debugFrameCount >= 60)
                {
                    Debug.Log($"[EarthRenderer] 旋转中: speedFactor={speedFactor}, deltaAngle={deltaAngle:E4}, angle={earthRotationAngleRad:F4} rad");
                    debugFrameCount = 0;
                }

                // 归一化到 [0, 2π)
                earthRotationAngleRad = earthRotationAngleRad % (2.0 * System.Math.PI);
                if (earthRotationAngleRad < 0) earthRotationAngleRad += 2.0 * System.Math.PI;

                // 应用旋转
                float angleDegrees = (float)(earthRotationAngleRad * 180.0 / System.Math.PI);
                earthContainer.transform.rotation = Quaternion.Euler(0, -angleDegrees, 0);
            }
            // 如果不使用模拟时间，使用传统的旋转方式
            else if (!useSimTime && enableRotation)
            {
                // 使用度/秒作为旋转速度单位，便于可视化
                earthContainer.transform.Rotate(Vector3.up, rotationSpeed * Time.deltaTime, Space.Self);
            }

            // 昼夜循环
            if (enableDayNightCycle && sunLight != null)
            {
                dayNightProgress += Time.deltaTime * dayNightCycleSpeed / 86400f; // 一天=86400秒
                dayNightProgress %= 1f;

                // 根据时间调整太阳角度
                float sunAngle = 360f * dayNightProgress;
                sunLight.transform.rotation = Quaternion.Euler(sunAngle, -45f, 0);

                // 调整光照强度（模拟黎明和黄昏）
                float angleFromHorizon = Mathf.Abs(Mathf.Sin(sunAngle * Mathf.Deg2Rad));
                float intensity = Mathf.Clamp01(angleFromHorizon * 2f);
                sunLight.intensity = intensity;
            }
        }

        /// <summary>
        /// 获取地球表面点
        /// </summary>
        public Vector3 GetSurfacePoint(float latitude, float longitude)
        {
            return Core.CoordinateConverter.GeodeticToWorld(latitude, longitude, 0, earthRadius);
        }

        /// <summary>
        /// 获取地球半径
        /// </summary>
        public float GetEarthRadius()
        {
            return earthRadius;
        }
    }
}
