using UnityEngine;

namespace SatelliteEdgeComputing.Core
{
    /// <summary>
    /// 场景自动设置脚本
    /// </summary>
    public class SceneSetup : MonoBehaviour
    {
        [Header("设置选项")]
        [SerializeField] private bool autoSetupOnStart = true;
        [SerializeField] private bool createEarth = true;
        [SerializeField] private bool createCameraController = true;
        [SerializeField] private bool createVisualizers = true;
        [SerializeField] private bool createUI = true;
        [SerializeField] private bool createLighting = true;

        [Header("地球设置")]
        [SerializeField] private float earthRadius = 6378135f;

        [Header("Skybox Settings")]
        [SerializeField] public Material skyMat;

        [Header("相机设置")]
        [SerializeField] private float cameraDistance = 15000000f;

        [Header("UI设置")]
        [SerializeField] private GameObject uiPrefab;

        void Start()
        {
            if (autoSetupOnStart)
            {
                SetupScene();
            }
        }

        /// <summary>
        /// 设置场景
        /// </summary>
        public void SetupScene()
        {
            Debug.Log("开始自动设置场景...");

            // 创建照明
            if (createLighting)
            {
                CreateLighting();
            }

            // 创建地球
            if (createEarth)
            {
                CreateEarth();
            }

            // 创建相机控制器
            if (createCameraController)
            {
                CreateCameraController();
            }

            // 创建可视化器
            if (createVisualizers)
            {
                CreateVisualizers();
            }

            // 创建UI
            if (createUI)
            {
                CreateUI();
            }

            // 初始化仿真管理器
            InitializeSimulationManager();

            Debug.Log("场景设置完成");
        }

        /// <summary>
        /// 创建照明
        /// </summary>
        private void CreateLighting()
        {
            // 创建方向光（太阳）
            GameObject sunLight = new GameObject("Sun");
            Light light = sunLight.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.8f;
            sunLight.transform.rotation = Quaternion.Euler(50, -30, 0);

            // 设置环境光
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            Color baseColor = new Color(0.2f, 0.2f, 0.2f);
            float intensity = 6.0f;
            RenderSettings.ambientLight = baseColor * intensity;

            // 创建天空盒材质
            if (skyMat == null)
            {
                RenderSettings.skybox = new Material(Shader.Find("Skybox/Procedural"));
            }
            else
            {
                RenderSettings.skybox = skyMat;
            }


            RenderSettings.fog = false;

            Debug.Log("照明设置完成");
        }

        /// <summary>
        /// 创建地球
        /// </summary>
        private void CreateEarth()
        {
            // 安全检查：确保地球半径合理（真实地球半径约6378km）
            float actualRadius = earthRadius;
            if (actualRadius < 100000f)
            {
                Debug.LogWarning($"场景中的地球半径 {actualRadius} 太小，使用默认值 6378135");
                actualRadius = 6378135f;
            }

            // 检查是否已存在地球对象
            Visualization.EarthRenderer existingRenderer = FindObjectOfType<Visualization.EarthRenderer>();
            if (existingRenderer != null)
            {
                Debug.Log("发现已存在的地球渲染器，更新半径设置");
                existingRenderer.SetEarthRadius(actualRadius);
                existingRenderer.Initialize();
                return;
            }

            GameObject earth = new GameObject("Earth");
            earth.transform.position = Vector3.zero;

            // 添加地球渲染器组件
            var earthRenderer = earth.AddComponent<Visualization.EarthRenderer>();
            earthRenderer.SetEarthRadius(actualRadius);
            // 立即初始化地球渲染器，确保EarthContainer在可视化器初始化前创建
            earthRenderer.Initialize();

            Debug.Log($"地球创建完成，半径: {actualRadius}");
        }

        /// <summary>
        /// 创建相机控制器
        /// </summary>
        private void CreateCameraController()
        {
            // 检查是否已存在相机控制器
            Visualization.CameraController existingController = FindObjectOfType<Visualization.CameraController>();
            if (existingController != null)
            {
                Debug.Log("发现已存在的相机控制器");
                existingController.Initialize();
                return;
            }

            // 确保有主相机
            Camera mainCamera = Camera.main;
            if (mainCamera == null)
            {
                GameObject cameraObj = new GameObject("Main Camera");
                cameraObj.tag = "MainCamera";
                mainCamera = cameraObj.AddComponent<Camera>();
                cameraObj.AddComponent<AudioListener>();
            }

            // 设置相机初始位置
            mainCamera.transform.position = new Vector3(0, 0, -cameraDistance);
            mainCamera.transform.LookAt(Vector3.zero);
            mainCamera.fieldOfView = 60f;
            mainCamera.farClipPlane = 100000000f;

            // 添加相机控制器
            var cameraController = mainCamera.gameObject.AddComponent<Visualization.CameraController>();
            cameraController.Initialize(mainCamera);

            Debug.Log("Camera controller created.");
        }

        /// <summary>
        /// 创建可视化器
        /// </summary>
        private void CreateVisualizers()
        {
            // 检查是否已存在可视化器
            Visualization.SatelliteVisualizer satelliteVisualizer = FindObjectOfType<Visualization.SatelliteVisualizer>();
            Visualization.GroundStationVisualizer groundStationVisualizer = FindObjectOfType<Visualization.GroundStationVisualizer>();
            Visualization.LinkVisualizer linkVisualizer = FindObjectOfType<Visualization.LinkVisualizer>();

            // 创建卫星可视化器（如果不存在）
            if (satelliteVisualizer == null)
            {
                GameObject satVisualizerObj = new GameObject("SatelliteVisualizer");
                satelliteVisualizer = satVisualizerObj.AddComponent<Visualization.SatelliteVisualizer>();
            }

            // 创建地面站可视化器（如果不存在）
            if (groundStationVisualizer == null)
            {
                GameObject gsVisualizerObj = new GameObject("GroundStationVisualizer");
                groundStationVisualizer = gsVisualizerObj.AddComponent<Visualization.GroundStationVisualizer>();
            }

            // 创建链路可视化器（如果不存在）
            if (linkVisualizer == null)
            {
                GameObject linkVisualizerObj = new GameObject("LinkVisualizer");
                linkVisualizer = linkVisualizerObj.AddComponent<Visualization.LinkVisualizer>();
            }

            // 获取地球渲染器
            Visualization.EarthRenderer earthRenderer = FindObjectOfType<Visualization.EarthRenderer>();
            if (earthRenderer == null)
            {
                Debug.LogWarning("Earth renderer not found, visualizers may not work.");
                return;
            }

            // 获取UI Canvas（如果存在）
            Canvas canvas = FindObjectOfType<Canvas>();
            Transform canvasTransform = canvas != null ? canvas.transform : null;

            // 初始化可视化器
            satelliteVisualizer.Initialize(earthRenderer, canvasTransform);
            groundStationVisualizer.Initialize(earthRenderer, canvasTransform);
            linkVisualizer.Initialize(earthRenderer);

            Debug.Log("可视化器创建完成");
        }

        /// <summary>
        /// 创建UI
        /// </summary>
        private void CreateUI()
        {
            // 如果提供了UI预制体，则使用它
            if (uiPrefab != null)
            {
                Instantiate(uiPrefab);
                Debug.Log("UI预制体实例化完成");
                return;
            }

            // 否则创建基本UI
            CreateBasicUI();

            Debug.Log("基本UI创建完成");
        }

        /// <summary>
        /// 创建基本UI
        /// </summary>
        private void CreateBasicUI()
        {
            // 创建Canvas
            GameObject canvasObj = new GameObject("Canvas");
            Canvas canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
            canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

            // 创建EventSystem
            GameObject eventSystemObj = new GameObject("EventSystem");
            eventSystemObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystemObj.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();

            // 创建控制面板
            // CreateControlPanel(canvas.transform);

            // 创建状态面板
            CreateStatusPanel(canvas.transform);

            Debug.Log("基本UI元素创建完成");
        }

        /// <summary>
        /// 创建控制面板
        /// </summary>
        private void CreateControlPanel(Transform parent)
        {
            // 创建面板
            GameObject panel = new GameObject("ControlPanel");
            panel.transform.SetParent(parent);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(10, -10);
            rect.sizeDelta = new Vector2(300, 200);

            // 添加背景
            UnityEngine.UI.Image bg = panel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            // 这里可以添加更多的UI元素...
        }

        /// <summary>
        /// 创建状态面板
        /// </summary>
        private void CreateStatusPanel(Transform parent)
        {
            // 创建面板
            GameObject panel = new GameObject("StatusPanel");
            panel.transform.SetParent(parent);

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(1, 1);
            rect.anchoredPosition = new Vector2(-10, -10);
            rect.sizeDelta = new Vector2(200, 30);

            // 添加背景
            UnityEngine.UI.Image bg = panel.AddComponent<UnityEngine.UI.Image>();
            bg.color = new Color(0, 0, 0, 0.7f);

            // 添加状态文本
            GameObject statusTextObj = new GameObject("StatusText");
            statusTextObj.transform.SetParent(panel.transform);

            RectTransform textRect = statusTextObj.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0, 1);
            textRect.anchorMax = new Vector2(1, 1);
            textRect.pivot = new Vector2(0.5f, 1);
            textRect.anchoredPosition = new Vector2(0, -10);
            textRect.sizeDelta = new Vector2(-20, 30);

            UnityEngine.UI.Text statusText = statusTextObj.AddComponent<UnityEngine.UI.Text>();
            statusText.text = "仿真状态: 就绪";
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 14;
            statusText.color = Color.white;
            statusText.alignment = TextAnchor.UpperCenter;
        }

        /// <summary>
        /// 初始化仿真管理器
        /// </summary>
        private void InitializeSimulationManager()
        {
            // 获取或创建仿真管理器
            var simulationManager = SimulationManager.Instance;

            // 获取可视化器引用
            var satelliteVisualizer = FindObjectOfType<Visualization.SatelliteVisualizer>();
            var groundStationVisualizer = FindObjectOfType<Visualization.GroundStationVisualizer>();
            var linkVisualizer = FindObjectOfType<Visualization.LinkVisualizer>();
            var cameraController = FindObjectOfType<Visualization.CameraController>();

            Debug.Log($"[SceneSetup] 可视化器引用: satelliteVisualizer={(satelliteVisualizer != null ? "有效" : "null")}, groundStationVisualizer={(groundStationVisualizer != null ? "有效" : "null")}");

            // 获取UI控制器
            var uiController = FindObjectOfType<UI.MainUIController>();

            if (uiController != null)
            {
                Debug.Log($"[SceneSetup] 找到UI控制器，调用Initialize");
                uiController.Initialize(simulationManager, cameraController,
                    satelliteVisualizer, groundStationVisualizer, linkVisualizer);
            }
            else
            {
                Debug.LogWarning("[SceneSetup] 未找到UI控制器!");
            }

            // 开始初始化仿真
            simulationManager.Initialize();

            Debug.Log("仿真管理器初始化完成");
        }

        /// <summary>
        /// 重置场景
        /// </summary>
        public void ResetScene()
        {
            // 销毁所有创建的对象（除了核心对象）
            var objects = FindObjectsOfType<GameObject>();
            foreach (var obj in objects)
            {
                if (obj.name.Contains("Earth") || obj.name.Contains("Main Camera") ||
                    obj.name.Contains("Canvas") || obj.name.Contains("EventSystem") ||
                    obj.name.Contains("Visualizer") || obj.name == "Sun")
                {
                    Destroy(obj);
                }
            }

            // 重新设置场景
            SetupScene();
        }

        /// <summary>
        /// 在编辑器中手动触发设置
        /// </summary>
        [ContextMenu("手动设置场景")]
        private void ManualSetup()
        {
            SetupScene();
        }

        /// <summary>
        /// 在编辑器中手动重置场景
        /// </summary>
        [ContextMenu("重置场景")]
        private void ManualReset()
        {
            ResetScene();
        }
    }
}
