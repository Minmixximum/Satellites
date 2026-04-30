using System;
using UnityEngine;
using UnityEngine.UI;
using SatelliteEdgeComputing.Network;
using System.Collections;

namespace SatelliteEdgeComputing.UI
{
    /// <summary>
    /// 主UI控制器
    /// </summary>
    public class MainUIController : MonoBehaviour
    {
        [Header("UI面板")]
        [SerializeField] private GameObject controlPanel;
        [SerializeField] private GameObject statisticsPanel;
        [SerializeField] private GameObject taskPanel;
        [SerializeField] private GameObject infoPanel;
        [SerializeField] private GameObject connectionPanel;

        [Header("UI元素")]
        [SerializeField] private Text statusText;
        [SerializeField] private Text timeText;
        [SerializeField] private Text fpsText;
        [SerializeField] private Button startButton;
        [SerializeField] private Button pauseButton;
        [SerializeField] private Button resetButton;
        [SerializeField] private Dropdown algorithmDropdown;
        [SerializeField] private Slider timeScaleSlider;
        [SerializeField] private Text timeScaleValueText;
        [SerializeField] private Toggle showOrbitsToggle;
        [SerializeField] private Toggle showLinksToggle;
        [SerializeField] private Toggle showLabelsToggle;
        [SerializeField] private Text connectionStatusText;
        [SerializeField] private Button reconnectButton;
        [SerializeField] private Button initializeDemoButton;
        [SerializeField] private Button clearTasksButton;

        [Header("统计信息")]
        [SerializeField] private Text satellitesCountText;
        [SerializeField] private Text tasksCountText;
        [SerializeField] private Text processedTasksText;
        [SerializeField] private Text successRateText;
        [SerializeField] private Text avgProcessingTimeText;
        [SerializeField] private Text activeLinksText;
        [SerializeField] private Text avgLoadText;

        [Header("任务列表")]
        [SerializeField] private Transform taskListContent;
        [SerializeField] private GameObject taskItemPrefab;
        [SerializeField] private ScrollRect taskScrollRect;

        [Header("信息面板")]
        [SerializeField] private Text selectedObjectText;
        [SerializeField] private Text objectDetailsText;
        [SerializeField] private Button closeInfoButton;

        [Header("设置")]
        [SerializeField] private float uiUpdateInterval = 0.5f; // UI更新间隔（秒）
        [SerializeField] private bool autoCreateUI = true; // 是否自动创建UI（false则使用Inspector赋值的UI）

        // 引用
        private Core.SimulationManager simulationManager;
        private Visualization.CameraController cameraController;
        private Visualization.SatelliteVisualizer satelliteVisualizer;
        private Visualization.GroundStationVisualizer groundStationVisualizer;
        private Visualization.LinkVisualizer linkVisualizer;

        private float lastUIUpdateTime = 0f;
        private int frameCount = 0;
        private float fpsUpdateTime = 0f;
        private float currentFPS = 60f;
        private bool isInitialized = false;  // 防止重复初始化

        #region 初始化
        /// <summary>
        /// 初始化UI
        /// </summary>
        public void Initialize(
            Core.SimulationManager simManager,
            Visualization.CameraController camController,
            Visualization.SatelliteVisualizer satVisualizer,
            Visualization.GroundStationVisualizer gsVisualizer,
            Visualization.LinkVisualizer lnkVisualizer)
        {
            Debug.Log($"[MainUIController] Initialize called. isInitialized={isInitialized}, autoCreateUI={autoCreateUI}");

            // 总是更新可视化器引用（如果提供了有效引用）
            bool wasSimManagerNull = (simulationManager == null);
            if (satVisualizer != null) satelliteVisualizer = satVisualizer;
            if (gsVisualizer != null) groundStationVisualizer = gsVisualizer;
            if (lnkVisualizer != null) linkVisualizer = lnkVisualizer;
            if (simManager != null) simulationManager = simManager;
            if (camController != null) cameraController = camController;

            // 如果 simulationManager 从null变为有效，需要订阅事件
            if (wasSimManagerNull && simulationManager != null)
            {
                Debug.Log("[MainUIController] 订阅 SimulationManager 事件");
                SubscribeToSimulationEvents();
            }

            if (isInitialized)
            {
                Debug.Log("[MainUIController] Initialize: 已初始化，更新引用后返回");
                return;  // 防止重复初始化UI和事件
            }

            Debug.Log($"[MainUIController] Initialize: taskPanel={taskPanel}, taskListContent={taskListContent}");

            // 确保 UI 元素已创建（可能在 Start() 之前被调用）
            if (autoCreateUI)
            {
                Debug.Log("[MainUIController] Initialize: 调用 FindOrCreateUIElements");
                FindOrCreateUIElements();
            }
            else
            {
                Debug.LogWarning("[MainUIController] Initialize: autoCreateUI=false，跳过UI创建");
            }

            SetupEventListeners();
            UpdateUIElements();

            // 初始化算法下拉框
            if (algorithmDropdown != null)
            {
                algorithmDropdown.ClearOptions();
                algorithmDropdown.AddOptions(new System.Collections.Generic.List<string>
                {
                    "FCFS (先来先服务)",
                    "SJF (最短作业优先)",
                    "EDD (最早截止时间优先)",
                    "Max-Visibility (最大可见性)"
                });
                algorithmDropdown.value = 0;
            }

            // 初始化时间滑块
            if (timeScaleSlider != null)
            {
                timeScaleSlider.minValue = 1200f;
                timeScaleSlider.maxValue = 3600f;
                timeScaleSlider.SetValueWithoutNotify(simulationManager != null ? simulationManager.SpeedFactor : 1200f);
            }

            isInitialized = true;
            Debug.Log("主UI控制器初始化完成");
        }

        /// <summary>
        /// 设置事件监听器
        /// </summary>
        private void SetupEventListeners()
        {
            if (startButton != null)
                startButton.onClick.AddListener(OnStartButtonClick);

            if (pauseButton != null)
                pauseButton.onClick.AddListener(OnPauseButtonClick);

            if (resetButton != null)
                resetButton.onClick.AddListener(OnResetButtonClick);

            if (algorithmDropdown != null)
                algorithmDropdown.onValueChanged.AddListener(OnAlgorithmChanged);

            if (timeScaleSlider != null)
                timeScaleSlider.onValueChanged.AddListener(OnTimeScaleChanged);

            if (timeScaleValueText != null)
                timeScaleValueText.text = $"x{(simulationManager != null ? simulationManager.SpeedFactor : 1200f):F1}";

            if (showOrbitsToggle != null)
                showOrbitsToggle.onValueChanged.AddListener(OnShowOrbitsChanged);

            if (showLinksToggle != null)
                showLinksToggle.onValueChanged.AddListener(OnShowLinksChanged);

            if (showLabelsToggle != null)
                showLabelsToggle.onValueChanged.AddListener(OnShowLabelsChanged);

            if (reconnectButton != null)
                reconnectButton.onClick.AddListener(OnReconnectButtonClick);

            if (initializeDemoButton != null)
                initializeDemoButton.onClick.AddListener(OnInitializeDemoButtonClick);

            if (clearTasksButton != null)
                clearTasksButton.onClick.AddListener(OnClearTasksButtonClick);

            if (closeInfoButton != null)
                closeInfoButton.onClick.AddListener(OnCloseInfoButtonClick);

            // 监听仿真事件
            SubscribeToSimulationEvents();

            Network.ApiClient.Instance.OnConnectionStatusChanged += UpdateConnectionStatus;
            Network.ApiClient.Instance.OnError += OnApiError;

            // 启动协程等待数据加载完成
            StartCoroutine(WaitForDataAndRefresh());
        }

        /// <summary>
        /// 订阅仿真管理器事件
        /// </summary>
        private void SubscribeToSimulationEvents()
        {
            if (simulationManager != null)
            {
                simulationManager.OnSimulationStarted += OnSimulationStarted;
                simulationManager.OnSimulationPaused += OnSimulationPaused;
                simulationManager.OnSimulationReset += OnSimulationReset;
                simulationManager.OnDataUpdated += OnDataUpdated;
                simulationManager.OnAlgorithmChanged += OnAlgorithmChangedEvent;
                simulationManager.OnSimulationInitialized += OnSimulationInitialized;
                Debug.Log("[MainUIController] 已订阅SimulationManager事件");
            }
        }

        /// <summary>
        /// 等待数据加载完成后刷新UI
        /// </summary>
        private IEnumerator WaitForDataAndRefresh()
        {
            // 等待初始化完成
            int maxWaitFrames = 300; // 最多等待5秒（60fps * 5）
            int waitFrames = 0;

            while (!simulationManager.IsInitialized && waitFrames < maxWaitFrames)
            {
                yield return null;
                waitFrames++;
            }

            Debug.Log($"[MainUIController] 初始化等待完成, IsInitialized={simulationManager.IsInitialized}, 等待帧数={waitFrames}");

            // 等待更长时间确保数据完全加载到UI线程
            yield return new WaitForSeconds(0.2f);

            // 多次尝试更新任务列表
            int retryCount = 0;
            int maxRetries = 5;
            while (retryCount < maxRetries)
            {
                if (simulationManager.Tasks.Count > 0)
                {
                    Debug.Log($"[MainUIController] 数据已加载，更新任务列表，任务数: {simulationManager.Tasks.Count}");
                    // 重置更新频率限制，强制更新
                    lastUIUpdateTime = 0f;
                    UpdateTaskList();
                    break;
                }
                else
                {
                    Debug.LogWarning($"[MainUIController] 第{retryCount + 1}次检查：没有任务数据，等待重试...");
                    yield return new WaitForSeconds(0.5f);
                    retryCount++;
                }
            }

            UpdateUIElements();
        }

        /// <summary>
        /// 更新UI元素
        /// </summary>
        private void UpdateUIElements()
        {
            // 更新状态
            if (statusText != null)
            {
                string status = simulationManager.IsRunning ? "运行中" : "已暂停";
                statusText.text = $"状态: {status}";
            }

            // 更新时间
            if (timeText != null)
            {
                float time = simulationManager.SimulationTime;
                timeText.text = $"仿真时间: {FormatTime(time)}";
            }

            // 更新按钮状态
            if (startButton != null)
                startButton.interactable = !simulationManager.IsRunning;

            if (pauseButton != null)
                pauseButton.interactable = simulationManager.IsRunning;

            // 更新连接状态
            UpdateConnectionStatus();

            // 更新统计信息
            UpdateStatistics();
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics()
        {
            if (simulationManager == null) return;

            if (satellitesCountText != null)
                satellitesCountText.text = $"卫星数量: {simulationManager.Satellites.Count}";

            if (tasksCountText != null)
                tasksCountText.text = $"任务数量: {simulationManager.Tasks.Count}";

            if (processedTasksText != null)
                processedTasksText.text = $"已处理: {simulationManager.TotalTasksProcessed}";

            if (successRateText != null)
                successRateText.text = $"成功率: {simulationManager.SuccessRate:P1}";

            if (avgProcessingTimeText != null)
                avgProcessingTimeText.text = $"平均处理时间: {simulationManager.AverageProcessingTime:F1}s";

            // 计算平均负载
            if (avgLoadText != null && simulationManager.Satellites.Count > 0)
            {
                float totalLoad = 0f;
                foreach (var satellite in simulationManager.Satellites)
                {
                    totalLoad += satellite.LoadRate;
                }
                float avgLoad = totalLoad / simulationManager.Satellites.Count;
                avgLoadText.text = $"平均负载: {avgLoad:P1}";
            }

            // 计算活跃链路（简化）
            if (activeLinksText != null)
            {
                activeLinksText.text = "活跃链路: 计算中...";
            }
        }

        /// <summary>
        /// 确保任务列表容器存在
        /// </summary>
        private void EnsureTaskListContent()
        {
            if (taskListContent != null) return;

            Debug.Log("[MainUIController] EnsureTaskListContent: 开始创建任务列表容器");

            // 如果 taskPanel 为null，尝试查找或创建
            if (taskPanel == null)
            {
                // 尝试在场景中查找已有的TaskPanel
                var existingPanel = GameObject.Find("TaskPanel");
                if (existingPanel != null)
                {
                    taskPanel = existingPanel;
                    Debug.Log($"[MainUIController] 找到已存在的 TaskPanel: {taskPanel.name}");
                }
                else
                {
                    // 创建Canvas（如果不存在）
                    Canvas canvas = FindObjectOfType<Canvas>();
                    if (canvas == null)
                    {
                        GameObject canvasGO = new GameObject("MainCanvas");
                        canvas = canvasGO.AddComponent<Canvas>();
                        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                        canvasGO.AddComponent<CanvasScaler>();
                        canvasGO.AddComponent<GraphicRaycaster>();
                        Debug.Log($"[MainUIController] 创建了新的Canvas: {canvasGO.name}");
                    }

                    // 创建TaskPanel
                    taskPanel = CreatePanel(canvas.transform, "TaskPanel", new Vector2(400, -165), new Vector2(200, 300));
                    Debug.Log($"[MainUIController] 创建了新的TaskPanel: {(taskPanel != null ? taskPanel.name : "null")}");
                }
            }

            // 再次检查taskPanel是否有效
            if (taskPanel == null)
            {
                Debug.LogError("[MainUIController] EnsureTaskListContent: taskPanel 创建失败！");
                return;
            }

            // 在taskPanel下创建TaskListContent
            GameObject contentGO = new GameObject("TaskListContent");
            contentGO.transform.SetParent(taskPanel.transform, false);

            RectTransform contentRect = contentGO.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 0);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = new Vector2(0, -25);
            contentRect.sizeDelta = new Vector2(0, -100);

            // 添加一个半透明背景以便调试
            Image bgImage = contentGO.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.3f, 0.2f, 0.5f);

            taskListContent = contentGO.transform;
            Debug.Log($"[MainUIController] EnsureTaskListContent 完成: taskListContent={taskListContent}");
        }

        /// <summary>
        /// 更新任务列表
        /// </summary>
        private void UpdateTaskList()
        {
            // 确保taskListContent存在
            EnsureTaskListContent();

            if (taskListContent == null)
            {
                Debug.LogWarning($"[MainUIController] UpdateTaskList: 无法创建taskListContent");
                return;
            }

            if (taskItemPrefab == null)
            {
                // 动态创建taskItemPrefab
                taskItemPrefab = CreateDefaultTaskItemPrefab();
                Debug.Log($"[MainUIController] 动态创建了taskItemPrefab: {taskItemPrefab}");
            }

            // 清除现有任务项
            foreach (Transform child in taskListContent)
            {
                Destroy(child.gameObject);
            }

            // 检查任务数量
            int taskCount = simulationManager.Tasks.Count;
            Debug.Log($"[MainUIController] UpdateTaskList: 共有 {taskCount} 个任务需要显示");

            // 如果没有任务，显示提示
            if (taskCount == 0)
            {
                GameObject emptyItem = new GameObject("EmptyTask");
                emptyItem.transform.SetParent(taskListContent, false);

                RectTransform rect = emptyItem.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(180, 30);

                Text text = emptyItem.AddComponent<Text>();
                text.text = "暂无任务！";
                text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                text.fontSize = 14;
                text.color = Color.gray;
                text.alignment = TextAnchor.MiddleCenter;
                return;
            }

            // 添加新任务项，垂直排列
            int maxTasks = 20; // 限制显示数量
            int count = 0;
            float itemHeight = 65f; // 每个任务项的高度
            float startY = -5f; // 从顶部开始，留一点间距

            foreach (var task in simulationManager.Tasks)
            {
                if (count >= maxTasks) break;

                GameObject taskItem = Instantiate(taskItemPrefab, taskListContent);
                taskItem.SetActive(true); // 确保启用
                taskItem.name = $"TaskItem_{task.id}"; // 设置有意义的名称

                // 设置任务项位置
                RectTransform itemRect = taskItem.GetComponent<RectTransform>();
                if (itemRect != null)
                {
                    Debug.Log("更新任务列表中......");
                    itemRect.anchorMin = new Vector2(0, 1);
                    itemRect.anchorMax = new Vector2(1, 1);
                    itemRect.pivot = new Vector2(0.5f, 1);
                    itemRect.anchoredPosition = new Vector2(0, startY - count * itemHeight);
                    itemRect.sizeDelta = new Vector2(-10, itemHeight - 5);
                }

                TaskItemUI taskUI = taskItem.GetComponent<TaskItemUI>();
                if (taskUI != null)
                {
                    taskUI.Initialize(task);
                }

                count++;
            }

            Debug.Log($"[MainUIController] UpdateTaskList: 已创建 {count} 个任务项UI");
        }

        /// <summary>
        /// 格式化时间显示
        /// </summary>
        private string FormatTime(float seconds)
        {
            int hours = (int)(seconds / 3600);
            int minutes = (int)((seconds % 3600) / 60);
            int secs = (int)(seconds % 60);

            if (hours > 0)
                return $"{hours:D2}:{minutes:D2}:{secs:D2}";
            else
                return $"{minutes:D2}:{secs:D2}";
        }

        #endregion

        #region 事件处理
        private void OnStartButtonClick()
        {
            simulationManager.StartSimulation();
        }

        private void OnPauseButtonClick()
        {
            simulationManager.PauseSimulation();
        }

        private void OnResetButtonClick()
        {
            simulationManager.ResetSimulation();
        }

        private void OnAlgorithmChanged(int index)
        {
            string[] algorithms = { "fcfs", "sjf", "edd", "max_visibility" };
            if (index >= 0 && index < algorithms.Length)
            {
                simulationManager.SetAlgorithm(algorithms[index]);
            }
        }

        private void OnTimeScaleChanged(float value)
        {
            value = Mathf.Max(1200f, value);
            simulationManager.SetSpeedFactor(value);
            if (timeScaleValueText != null)
            {
                timeScaleValueText.text = $"x{value:F1}";
            }
        }

        private void OnShowOrbitsChanged(bool show)
        {
            if (satelliteVisualizer != null)
            {
                satelliteVisualizer.SetOrbitsVisible(show);
            }
        }

        private void OnShowLinksChanged(bool show)
        {
            if (linkVisualizer != null)
            {
                linkVisualizer.SetLinksVisible(show);
            }
        }

        private void OnShowLabelsChanged(bool show)
        {
            if (satelliteVisualizer != null)
            {
                satelliteVisualizer.SetLabelsVisible(show);
            }
            if (groundStationVisualizer != null)
            {
                groundStationVisualizer.SetLabelsVisible(show);
            }
        }

        private void OnReconnectButtonClick()
        {
            if (connectionStatusText != null)
            {
                connectionStatusText.text = "API: 重新连接中...";
                connectionStatusText.color = Color.yellow;
            }

            StartCoroutine(Network.ApiClient.Instance.TestConnection((connected) =>
            {
                if (simulationManager != null)
                {
                    simulationManager.SetBackendEnabled(connected);
                    if (connected && simulationManager.IsInitialized)
                    {
                        simulationManager.ResetSimulation();
                    }
                }

                UpdateConnectionStatus();
            }));
        }

        private void OnInitializeDemoButtonClick()
        {
            if (simulationManager == null) return;

            // 调用API初始化演示数据
            StartCoroutine(Network.ApiClient.Instance.InitializeDemo(
                (success) => {
                    if (success)
                    {
                        Debug.Log("演示数据初始化成功");
                        // 刷新任务列表
                        StartCoroutine(RefreshTasks());
                        // 更新UI
                        UpdateUIElements();
                    }
                    else
                    {
                        Debug.LogError("演示数据初始化失败");
                    }
                },
                (error) => {
                    Debug.LogError($"初始化演示数据时出错: {error}");
                }
            ));
        }

        private void OnClearTasksButtonClick()
        {
            if (simulationManager == null) return;

            // 调用API清除所有任务
            StartCoroutine(Network.ApiClient.Instance.ClearTasks(
                (success) => {
                    if (success)
                    {
                        Debug.Log("任务清除成功");
                        // 刷新任务列表
                        StartCoroutine(RefreshTasks());
                        // 更新UI
                        UpdateUIElements();
                    }
                    else
                    {
                        Debug.LogError("任务清除失败");
                    }
                },
                (error) => {
                    Debug.LogError($"清除任务时出错: {error}");
                }
            ));
        }

        private IEnumerator RefreshTasks()
        {
            if (simulationManager != null)
            {
                simulationManager.RefreshTasks();
                // 等待数据刷新完成（估计时间）
                yield return new WaitForSeconds(1f);
            }
        }

        private void OnCloseInfoButtonClick()
        {
            if (infoPanel != null)
            {
                infoPanel.SetActive(false);
            }
        }

        private void OnSimulationStarted()
        {
            UpdateUIElements();
        }

        private void OnSimulationInitialized()
        {
            int taskCount = (simulationManager != null && simulationManager.Tasks != null) ? simulationManager.Tasks.Count : 0;
            Debug.Log($"[MainUIController] OnSimulationInitialized 被调用，任务数: {taskCount}");
            // 重置更新频率限制，强制更新
            lastUIUpdateTime = 0f;
            UpdateTaskList();
            UpdateUIElements();
        }

        private void OnSimulationPaused()
        {
            UpdateUIElements();
        }

        private void OnSimulationReset()
        {
            UpdateUIElements();
            UpdateTaskList();
        }

        private void OnDataUpdated()
        {
            // 数据更新时不使用频率限制，因为后端已经控制了更新频率
            Debug.Log($"[MainUIController] OnDataUpdated被调用");

            int satCount = (simulationManager != null && simulationManager.Satellites != null) ? simulationManager.Satellites.Count : 0;
            int taskCount = (simulationManager != null && simulationManager.Tasks != null) ? simulationManager.Tasks.Count : 0;
            Debug.Log($"[MainUIController] OnDataUpdated: 卫星数{satCount}, 任务数{taskCount}");

            UpdateStatistics();
            UpdateTaskList();

            // 更新卫星和地面站可视化
            if (satelliteVisualizer != null && simulationManager != null)
            {
                Debug.Log($"[MainUIController] 调用 satelliteVisualizer.UpdateSatellites");
                satelliteVisualizer.UpdateSatellites(simulationManager.Satellites);
            }
            else
            {
                Debug.LogWarning($"[MainUIController] satelliteVisualizer为null, 无法更新卫星可视化");
            }
            if (groundStationVisualizer != null && simulationManager != null)
            {
                Debug.Log($"[MainUIController] 调用 groundStationVisualizer.UpdateGroundStations");
                groundStationVisualizer.UpdateGroundStations(simulationManager.GroundStations);
            }
            else
            {
                Debug.LogWarning($"[MainUIController] groundStationVisualizer为null, 无法更新地面站可视化");
            }
        }

        private void OnAlgorithmChangedEvent(string algorithm)
        {
            // 更新下拉框选择
            if (algorithmDropdown != null)
            {
                int index = GetAlgorithmIndex(algorithm);
                if (index >= 0)
                {
                    algorithmDropdown.value = index;
                }
            }
        }
        #endregion

        /// <summary>
        /// 获取算法索引
        /// </summary>
        private int GetAlgorithmIndex(string algorithm)
        {
            switch (algorithm.ToLower())
            {
                case "fcfs":
                    return 0;
                case "sjf":
                    return 1;
                case "edd":
                    return 2;
                case "max_visibility":
                    return 3;
                default:
                    return 0;
            }
        }

        /// <summary>
        /// 更新连接状态
        /// </summary>
        private void UpdateConnectionStatus()
        {
            if (connectionStatusText != null)
            {
                bool connected = Network.ApiClient.Instance.IsConnected;
                string lastError = Network.ApiClient.Instance.LastError;
                connectionStatusText.text = connected
                    ? "API: 已连接"
                    : string.IsNullOrWhiteSpace(lastError) ? "API: 未连接" : $"API: 未连接({lastError})";
                connectionStatusText.color = connected ? Color.green : Color.red;
            }
        }

        private void OnApiError(string error)
        {
            UpdateConnectionStatus();
        }

        /// <summary>
        /// 显示信息面板
        /// </summary>
        public void ShowInfoPanel(string title, string details)
        {
            if (infoPanel == null) return;

            if (selectedObjectText != null)
                selectedObjectText.text = title;

            if (objectDetailsText != null)
                objectDetailsText.text = details;

            infoPanel.SetActive(true);
        }

        void Start()
        {
            // 如果已经被Initialize()初始化过，跳过
            if (isInitialized)
            {
                Debug.Log("[MainUIController] Start: 已初始化，跳过");
                return;
            }

            Debug.Log($"[MainUIController] Start called. autoCreateUI = {autoCreateUI}");

            // 自动查找引用（如果未在Inspector中设置）
            if (simulationManager == null)
                simulationManager = Core.SimulationManager.Instance;

            if (cameraController == null)
                cameraController = FindObjectOfType<Visualization.CameraController>();

            if (satelliteVisualizer == null)
                satelliteVisualizer = FindObjectOfType<Visualization.SatelliteVisualizer>();

            if (groundStationVisualizer == null)
                groundStationVisualizer = FindObjectOfType<Visualization.GroundStationVisualizer>();

            if (linkVisualizer == null)
                linkVisualizer = FindObjectOfType<Visualization.LinkVisualizer>();

            // 自动查找或创建UI元素
            if (autoCreateUI)
            {
                Debug.Log("[MainUIController] autoCreateUI is true, creating UI elements...");
                FindOrCreateUIElements();
                Debug.Log("[MainUIController] UI elements creation completed.");
            }
            else
            {
                Debug.Log("[MainUIController] autoCreateUI is false, using Inspector-assigned UI only.");
            }

            Initialize(simulationManager, cameraController, satelliteVisualizer, groundStationVisualizer, linkVisualizer);
        }

        /// <summary>
        /// 自动查找或创建UI元素
        /// </summary>
        private void FindOrCreateUIElements()
        {
            Debug.Log("[MainUIController] FindOrCreateUIElements 开始执行");
            Debug.Log($"[MainUIController] 当前状态: taskPanel={taskPanel}, taskListContent={taskListContent}, taskItemPrefab={taskItemPrefab}");

            // 查找场景中已有的Canvas，或者创建新的
            Canvas canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                GameObject canvasGO = new GameObject("MainCanvas");
                canvas = canvasGO.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvasGO.AddComponent<CanvasScaler>();
                canvasGO.AddComponent<GraphicRaycaster>();
            }
            Transform canvasTransform = canvas.transform;
            Debug.Log($"[MainUIController] Canvas: {canvas.name}");

            // 创建或查找面板（只有在Inspector中未赋值时才创建）
            // 调整位置避免重叠：左上和右上放控制/统计，左下和右下放任务/连接
            if (controlPanel == null)
            {
                // 左上角：x负方向(左)，y正方向(上偏中间)
                controlPanel = CreatePanel(canvasTransform, "ControlPanel", new Vector2(-400, 80), new Vector2(200, 400));
                Debug.Log("[MainUIController] Created ControlPanel");
            }

            if (statisticsPanel == null)
            {
                // 右上角：x正方向(右)，y正方向(上偏中间)
                statisticsPanel = CreatePanel(canvasTransform, "StatisticsPanel", new Vector2(400, 115), new Vector2(200, 250));
                Debug.Log("[MainUIController] Created StatisticsPanel");
            }

            if (taskPanel == null)
            {
                // 左下角：x负方向(左)，y负方向(下)
                taskPanel = CreatePanel(canvasTransform, "TaskPanel", new Vector2(400, -165), new Vector2(200, 300));
                Debug.Log($"[MainUIController] Created TaskPanel: {taskPanel}");
            }

            if (connectionPanel == null)
            {
                // 右下角：x正方向(右)，y负方向(下偏上一点，避免和统计面板太近)
                connectionPanel = CreatePanel(canvasTransform, "ConnectionPanel", new Vector2(-400, -175), new Vector2(200, 100));
                Debug.Log("[MainUIController] Created ConnectionPanel");
            }

            if (infoPanel == null)
            {
                // 中央位置（默认隐藏）
                infoPanel = CreatePanel(canvasTransform, "InfoPanel", new Vector2(0, 0), new Vector2(300, 200));
                infoPanel.SetActive(false);
                Debug.Log("[MainUIController] Created InfoPanel");
            }

            Debug.Log($"[MainUIController] 面板创建完成: taskPanel={taskPanel}");

            //-----------------------dropdown-----------------------------------

            if (algorithmDropdown != null)
            {
                // 确保dropdown的父级为canvas
                Transform dropdownTransform = algorithmDropdown.transform;
                if (dropdownTransform.parent != canvasTransform)
                {
                    dropdownTransform.SetParent(canvasTransform, false);
                }

                // 设置RectTransform使其在Canvas上正确显示
                RectTransform rect = dropdownTransform.GetComponent<RectTransform>();
                if (rect == null)
                {
                    rect = dropdownTransform.gameObject.AddComponent<RectTransform>();
                }

                // 设置锚点、位置和大小（示例：放在controlPanel下方或指定位置）
                rect.anchorMin = new Vector2(0.5f, 0.5f);   // 左上角锚点
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(-490, 50); // 相对于左上角的位置
                rect.sizeDelta = new Vector2(180, 30);         // 宽度180，高度30

                // 如果dropdown组件尚未添加，则添加（假设需要）
                Dropdown dropdownComp = dropdownTransform.GetComponent<Dropdown>();
                if (dropdownComp == null)
                {
                    dropdownComp = dropdownTransform.gameObject.AddComponent<Dropdown>();
                }

                // 可选：设置默认选项
                dropdownComp.options.Clear();
                dropdownComp.options.Add(new Dropdown.OptionData("算法 A"));
                dropdownComp.options.Add(new Dropdown.OptionData("算法 B"));
                dropdownComp.options.Add(new Dropdown.OptionData("算法 C"));

                Debug.Log("[MainUIController] algorithmDropdown added to canvas.");
            }
            else
            {
                Debug.LogWarning("[MainUIController] algorithmDropdown is null, cannot add to canvas.");
            }

            // 创建控制面板内容（只在元素为null时创建）
            Debug.Log("[MainUIController] 开始创建面板内容...");
            CreateControlPanelContent();

            // 创建统计面板内容
            CreateStatisticsPanelContent();

            // 创建任务面板内容
            CreateTaskPanelContent();

            // 创建连接面板内容
            CreateConnectionPanelContent();

            // 创建信息面板内容
            CreateInfoPanelContent();
            Debug.Log("[MainUIController] 所有面板内容创建完成");
        }

        /// <summary>
        /// 创建面板
        /// </summary>
        private GameObject CreatePanel(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject panel = new GameObject(name);
            panel.transform.SetParent(parent, false);

            RectTransform rectTransform = panel.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = panel.AddComponent<Image>();
            image.color = new Color(0.1f, 0.1f, 0.1f, 0.8f);

            return panel;
        }

        /// <summary>
        /// 创建文本组件
        /// </summary>
        private Text CreateText(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 size, int fontSize = 14)
        {
            GameObject textGO = new GameObject(name);
            textGO.transform.SetParent(parent, false);

            RectTransform rectTransform = textGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Text textComponent = textGO.AddComponent<Text>();
            textComponent.text = text;
            textComponent.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            textComponent.fontSize = fontSize;
            textComponent.color = Color.white;
            textComponent.alignment = TextAnchor.MiddleLeft;

            return textComponent;
        }

        /// <summary>
        /// 创建按钮
        /// </summary>
        private Button CreateButton(Transform parent, string name, string text, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject buttonGO = new GameObject(name);
            buttonGO.transform.SetParent(parent, false);

            RectTransform rectTransform = buttonGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = buttonGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.4f, 0.8f, 1f);

            Button button = buttonGO.AddComponent<Button>();

            // 创建按钮文本
            Text buttonText = CreateText(buttonGO.transform, "Text", text, Vector2.zero, size, 14);
            buttonText.alignment = TextAnchor.MiddleCenter;

            return button;
        }

        /// <summary>
        /// 创建滑块
        /// </summary>
        private Slider CreateSlider(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject sliderGO = new GameObject(name);
            sliderGO.transform.SetParent(parent, false);

            RectTransform rectTransform = sliderGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Slider slider = sliderGO.AddComponent<Slider>();

            // 创建背景
            GameObject background = new GameObject("Background");
            background.transform.SetParent(sliderGO.transform, false);
            RectTransform bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // 创建填充区域
            GameObject fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGO.transform, false);
            RectTransform fillAreaRect = fillArea.AddComponent<RectTransform>();
            fillAreaRect.anchorMin = Vector2.zero;
            fillAreaRect.anchorMax = Vector2.one;
            fillAreaRect.sizeDelta = Vector2.zero;

            GameObject fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fillRect = fill.AddComponent<RectTransform>();
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.sizeDelta = Vector2.zero;
            Image fillImage = fill.AddComponent<Image>();
            fillImage.color = new Color(0.2f, 0.6f, 1f, 1f);

            slider.fillRect = fillRect;

            return slider;
        }

        /// <summary>
        /// 创建下拉框
        /// </summary>
        private Dropdown CreateDropdown(Transform parent, string name, Vector2 anchoredPosition, Vector2 size)
        {
            GameObject dropdownGO = new GameObject(name);
            dropdownGO.transform.SetParent(parent, false);

            RectTransform rectTransform = dropdownGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = size;

            Image image = dropdownGO.AddComponent<Image>();
            image.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            Dropdown dropdown = dropdownGO.AddComponent<Dropdown>();

            // 创建标签文本
            Text labelText = CreateText(dropdownGO.transform, "Label", "选择...", Vector2.zero, new Vector2(size.x - 20, size.y), 14);
            labelText.alignment = TextAnchor.MiddleLeft;
            dropdown.captionText = labelText;

            return dropdown;
        }

        /// <summary>
        /// 创建开关
        /// </summary>
        private Toggle CreateToggle(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            GameObject toggleGO = new GameObject(name);
            toggleGO.transform.SetParent(parent, false);

            RectTransform rectTransform = toggleGO.AddComponent<RectTransform>();
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = anchoredPosition;
            rectTransform.sizeDelta = new Vector2(160, 20);

            Toggle toggle = toggleGO.AddComponent<Toggle>();

            // 创建背景
            GameObject background = new GameObject("Background");
            background.transform.SetParent(toggleGO.transform, false);
            RectTransform bgRect = background.AddComponent<RectTransform>();
            bgRect.anchorMin = new Vector2(0, 0.5f);
            bgRect.anchorMax = new Vector2(0, 0.5f);
            bgRect.pivot = new Vector2(0, 0.5f);
            bgRect.anchoredPosition = Vector2.zero;
            bgRect.sizeDelta = new Vector2(20, 20);
            Image bgImage = background.AddComponent<Image>();
            bgImage.color = Color.white;
            toggle.targetGraphic = bgImage;

            // 创建勾选标记
            GameObject checkmark = new GameObject("Checkmark");
            checkmark.transform.SetParent(background.transform, false);
            RectTransform checkRect = checkmark.AddComponent<RectTransform>();
            checkRect.anchorMin = Vector2.zero;
            checkRect.anchorMax = Vector2.one;
            checkRect.sizeDelta = Vector2.zero;
            Image checkImage = checkmark.AddComponent<Image>();
            checkImage.color = new Color(0.2f, 0.6f, 1f, 1f);
            toggle.graphic = checkImage;

            // 创建标签
            Text labelText = CreateText(toggleGO.transform, "Label", label, new Vector2(25, 0), new Vector2(130, 20), 12);
            labelText.alignment = TextAnchor.MiddleLeft;

            return toggle;
        }

        /// <summary>
        /// 创建控制面板内容
        /// </summary>
        private void CreateControlPanelContent()
        {
            if (controlPanel == null) return;

            // 面板尺寸 200x300，坐标从中心(0,0)开始，y范围大约 -150 到 +150
            // 标题
            CreateText(controlPanel.transform, "Title", "控制面板", new Vector2(0, 130), new Vector2(180, 25), 16);

            // 状态文本
            if (statusText == null)
                statusText = CreateText(controlPanel.transform, "StatusText", "状态: 已暂停", new Vector2(0, 105), new Vector2(180, 20), 12);

            // 时间文本
            if (timeText == null)
                timeText = CreateText(controlPanel.transform, "TimeText", "仿真时间: 00:00", new Vector2(0, 85), new Vector2(180, 20), 12);

            // FPS文本
            if (fpsText == null)
                fpsText = CreateText(controlPanel.transform, "FPSText", "FPS: 60", new Vector2(0, 65), new Vector2(180, 20), 12);

            // 开始按钮和暂停按钮并排
            if (startButton == null)
                startButton = CreateButton(controlPanel.transform, "StartButton", "开始", new Vector2(-48, 30), new Vector2(75, 28));

            if (pauseButton == null)
                pauseButton = CreateButton(controlPanel.transform, "PauseButton", "暂停", new Vector2(48, 30), new Vector2(75, 28));

            // 重置按钮
            if (resetButton == null)
                resetButton = CreateButton(controlPanel.transform, "ResetButton", "重置", new Vector2(0, -5), new Vector2(160, 28));

            // 算法下拉框
            if (algorithmDropdown == null)
                algorithmDropdown = CreateDropdown(controlPanel.transform, "AlgorithmDropdown", new Vector2(0, -40), new Vector2(160, 28));

            // 时间缩放滑块和值文本
            if (timeScaleSlider == null)
                timeScaleSlider = CreateSlider(controlPanel.transform, "TimeScaleSlider", new Vector2(-25, -75), new Vector2(100, 20));

            if (timeScaleValueText == null)
                timeScaleValueText = CreateText(controlPanel.transform, "TimeScaleValue", "x1.0", new Vector2(60, -75), new Vector2(45, 20), 12);

            // 显示开关 - 三个开关垂直排列
            if (showOrbitsToggle == null)
                showOrbitsToggle = CreateToggle(controlPanel.transform, "ShowOrbitsToggle", "显示轨道", new Vector2(0, -105));

            if (showLinksToggle == null)
                showLinksToggle = CreateToggle(controlPanel.transform, "ShowLinksToggle", "显示链路", new Vector2(0, -125));

            if (showLabelsToggle == null)
                showLabelsToggle = CreateToggle(controlPanel.transform, "ShowLabelsToggle", "显示标签", new Vector2(0, -145));
        }

        /// <summary>
        /// 创建统计面板内容
        /// </summary>
        private void CreateStatisticsPanelContent()
        {
            if (statisticsPanel == null) return;

            // 面板尺寸 200x250
            // 标题
            CreateText(statisticsPanel.transform, "Title", "统计信息", new Vector2(0, 105), new Vector2(180, 25), 16);

            // 各项统计文本 - 紧凑型垂直排列
            if (satellitesCountText == null)
                satellitesCountText = CreateText(statisticsPanel.transform, "SatellitesCount", "卫星数量: 0", new Vector2(0, 80), new Vector2(180, 20), 12);

            if (tasksCountText == null)
                tasksCountText = CreateText(statisticsPanel.transform, "TasksCount", "任务数量: 0", new Vector2(0, 60), new Vector2(180, 20), 12);

            if (processedTasksText == null)
                processedTasksText = CreateText(statisticsPanel.transform, "ProcessedTasks", "已处理: 0", new Vector2(0, 40), new Vector2(180, 20), 12);

            if (successRateText == null)
                successRateText = CreateText(statisticsPanel.transform, "SuccessRate", "成功率: 0%", new Vector2(0, 20), new Vector2(180, 20), 12);

            if (avgProcessingTimeText == null)
                avgProcessingTimeText = CreateText(statisticsPanel.transform, "AvgTime", "平均处理时间: 0s", new Vector2(0, 0), new Vector2(180, 20), 12);

            if (activeLinksText == null)
                activeLinksText = CreateText(statisticsPanel.transform, "ActiveLinks", "活跃链路: 0", new Vector2(0, -20), new Vector2(180, 20), 12);

            if (avgLoadText == null)
                avgLoadText = CreateText(statisticsPanel.transform, "AvgLoad", "平均负载: 0%", new Vector2(0, -40), new Vector2(180, 20), 12);
        }

        /// <summary>
        /// 创建任务面板内容
        /// </summary>
        private void CreateTaskPanelContent()
        {
            Debug.Log($"[MainUIController] CreateTaskPanelContent 开始: taskPanel={taskPanel}");

            if (taskPanel == null)
            {
                Debug.LogError("[MainUIController] CreateTaskPanelContent: taskPanel is null! 无法创建任务列表内容");
                return;
            }

            // 面板尺寸 200x200
            // 标题
            CreateText(taskPanel.transform, "Title", "任务列表", new Vector2(0, 80), new Vector2(180, 25), 16);

            // 清除任务按钮放在底部
            if (clearTasksButton == null)
                clearTasksButton = CreateButton(taskPanel.transform, "ClearTasksButton", "清除任务", new Vector2(0, -75), new Vector2(160, 28));

            // 任务列表内容容器（需要正确的RectTransform设置）
            if (taskListContent == null)
            {
                Debug.Log("[MainUIController] CreateTaskPanelContent: 创建新的taskListContent");
                GameObject contentGO = new GameObject("TaskListContent");
                contentGO.transform.SetParent(taskPanel.transform, false);
                taskListContent = contentGO.transform;

                // 添加并配置RectTransform，使任务项在正确位置显示
                RectTransform contentRect = contentGO.AddComponent<RectTransform>();
                contentRect.anchorMin = new Vector2(0, 0);
                contentRect.anchorMax = new Vector2(1, 1);
                contentRect.pivot = new Vector2(0.5f, 1);
                contentRect.anchoredPosition = new Vector2(0, -25); // 在标题下方
                contentRect.sizeDelta = new Vector2(0, -100); // 留出底部按钮空间

                // 添加一个半透明背景以便调试
                Image bgImage = contentGO.AddComponent<Image>();
                bgImage.color = new Color(0.2f, 0.3f, 0.2f, 0.5f); // 绿色调半透明背景

                Debug.Log($"[MainUIController] CreateTaskPanelContent: taskListContent 创建完成 = {taskListContent}");
            }
            else
            {
                Debug.Log($"[MainUIController] CreateTaskPanelContent: taskListContent 已存在 = {taskListContent}");
            }

            // 创建任务项预制体（如果未在Inspector中赋值）
            if (taskItemPrefab == null)
            {
                taskItemPrefab = CreateDefaultTaskItemPrefab();
                Debug.Log("[MainUIController] Created default taskItemPrefab");
            }

            Debug.Log($"[MainUIController] CreateTaskPanelContent 完成: taskListContent={taskListContent}, taskItemPrefab={taskItemPrefab}");
        }

        /// <summary>
        /// 创建默认的任务项预制体
        /// </summary>
        private GameObject CreateDefaultTaskItemPrefab()
        {
            GameObject prefab = new GameObject("TaskItemPrefab");
            prefab.SetActive(false); // 预制体默认禁用，实例化时启用

            // 根对象RectTransform
            RectTransform rootRect = prefab.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(180, 60);
            rootRect.pivot = new Vector2(0.5f, 1f);

            // 添加TaskItemUI组件
            TaskItemUI taskItemUI = prefab.AddComponent<TaskItemUI>();

            // 创建背景
            GameObject bgObj = new GameObject("Background");
            bgObj.transform.SetParent(prefab.transform, false);
            RectTransform bgRect = bgObj.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            Image bgImage = bgObj.AddComponent<Image>();
            bgImage.color = new Color(0.15f, 0.15f, 0.2f, 0.9f);

            // 任务名称文本
            GameObject nameObj = new GameObject("TaskName");
            nameObj.transform.SetParent(prefab.transform, false);
            RectTransform nameRect = nameObj.AddComponent<RectTransform>();
            nameRect.anchorMin = new Vector2(0, 1);
            nameRect.anchorMax = new Vector2(1, 1);
            nameRect.pivot = new Vector2(0, 1);
            nameRect.anchoredPosition = new Vector2(5, -5);
            nameRect.sizeDelta = new Vector2(-10, 18);
            Text nameText = nameObj.AddComponent<Text>();
            nameText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            nameText.fontSize = 12;
            nameText.color = Color.white;
            nameText.alignment = TextAnchor.MiddleLeft;

            // 状态和优先级（同一行）
            GameObject statusObj = new GameObject("Status");
            statusObj.transform.SetParent(prefab.transform, false);
            RectTransform statusRect = statusObj.AddComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0, 1);
            statusRect.anchorMax = new Vector2(0.5f, 1);
            statusRect.pivot = new Vector2(0, 1);
            statusRect.anchoredPosition = new Vector2(5, -24);
            statusRect.sizeDelta = new Vector2(0, 14);
            Text statusText = statusObj.AddComponent<Text>();
            statusText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            statusText.fontSize = 10;
            statusText.color = Color.gray;
            statusText.alignment = TextAnchor.MiddleLeft;

            GameObject priorityObj = new GameObject("Priority");
            priorityObj.transform.SetParent(prefab.transform, false);
            RectTransform priorityRect = priorityObj.AddComponent<RectTransform>();
            priorityRect.anchorMin = new Vector2(0.5f, 1);
            priorityRect.anchorMax = new Vector2(1, 1);
            priorityRect.pivot = new Vector2(0, 1);
            priorityRect.anchoredPosition = new Vector2(-5, -24);
            priorityRect.sizeDelta = new Vector2(0, 14);
            Text priorityText = priorityObj.AddComponent<Text>();
            priorityText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            priorityText.fontSize = 10;
            priorityText.color = Color.gray;
            priorityText.alignment = TextAnchor.MiddleRight;

            // 进度条背景
            GameObject progressBgObj = new GameObject("ProgressBackground");
            progressBgObj.transform.SetParent(prefab.transform, false);
            RectTransform progressBgRect = progressBgObj.AddComponent<RectTransform>();
            progressBgRect.anchorMin = new Vector2(0, 0);
            progressBgRect.anchorMax = new Vector2(1, 0);
            progressBgRect.pivot = new Vector2(0.5f, 0);
            progressBgRect.anchoredPosition = new Vector2(0, 5);
            progressBgRect.sizeDelta = new Vector2(-10, 12);
            Image progressBgImage = progressBgObj.AddComponent<Image>();
            progressBgImage.color = new Color(0.2f, 0.2f, 0.2f, 1f);

            // 进度条填充
            GameObject progressFillObj = new GameObject("ProgressFill");
            progressFillObj.transform.SetParent(progressBgObj.transform, false);
            RectTransform progressFillRect = progressFillObj.AddComponent<RectTransform>();
            progressFillRect.anchorMin = Vector2.zero;
            progressFillRect.anchorMax = new Vector2(1, 1);
            progressFillRect.pivot = new Vector2(0, 0.5f);
            progressFillRect.anchoredPosition = Vector2.zero;
            progressFillRect.sizeDelta = Vector2.zero;
            Image progressFillImage = progressFillObj.AddComponent<Image>();
            progressFillImage.color = new Color(0.2f, 0.6f, 1f, 1f);
            progressFillImage.type = Image.Type.Filled;
            progressFillImage.fillMethod = Image.FillMethod.Horizontal;

            // 进度文本
            GameObject progressTextObj = new GameObject("ProgressText");
            progressTextObj.transform.SetParent(progressBgObj.transform, false);
            RectTransform progressTextRect = progressTextObj.AddComponent<RectTransform>();
            progressTextRect.anchorMin = Vector2.zero;
            progressTextRect.anchorMax = Vector2.one;
            progressTextRect.sizeDelta = Vector2.zero;
            Text progressText = progressTextObj.AddComponent<Text>();
            progressText.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            progressText.fontSize = 9;
            progressText.color = Color.white;
            progressText.alignment = TextAnchor.MiddleCenter;

            // 使用反射设置TaskItemUI的私有字段（因为它们是[SerializeField]）
            System.Reflection.FieldInfo nameField = typeof(TaskItemUI).GetField("taskNameText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo statusField = typeof(TaskItemUI).GetField("taskStatusText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo priorityField = typeof(TaskItemUI).GetField("taskPriorityText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo progressTextField = typeof(TaskItemUI).GetField("taskProgressText",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            System.Reflection.FieldInfo progressBarField = typeof(TaskItemUI).GetField("progressBar",
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

            nameField?.SetValue(taskItemUI, nameText);
            statusField?.SetValue(taskItemUI, statusText);
            priorityField?.SetValue(taskItemUI, priorityText);
            progressTextField?.SetValue(taskItemUI, progressText);
            progressBarField?.SetValue(taskItemUI, progressFillImage);

            return prefab;
        }

        /// <summary>
        /// 创建连接面板内容
        /// </summary>
        private void CreateConnectionPanelContent()
        {
            if (connectionPanel == null) return;

            // 面板尺寸 200x100
            // 标题
            CreateText(connectionPanel.transform, "Title", "连接状态", new Vector2(0, 35), new Vector2(180, 20), 16);

            // 连接状态文本
            if (connectionStatusText == null)
                connectionStatusText = CreateText(connectionPanel.transform, "Status", "API: 未连接", new Vector2(0, 12), new Vector2(180, 20), 12);

            // 两个按钮并排放置
            if (reconnectButton == null)
                reconnectButton = CreateButton(connectionPanel.transform, "ReconnectButton", "重新连接", new Vector2(-48, -22), new Vector2(75, 26));

            if (initializeDemoButton == null)
                initializeDemoButton = CreateButton(connectionPanel.transform, "InitDemoButton", "初始化", new Vector2(48, -22), new Vector2(75, 26));
        }

        /// <summary>
        /// 创建信息面板内容
        /// </summary>
        private void CreateInfoPanelContent()
        {
            if (infoPanel == null) return;

            // 面板尺寸 300x200
            // 标题
            if (selectedObjectText == null)
                selectedObjectText = CreateText(infoPanel.transform, "Title", "对象信息", new Vector2(0, 80), new Vector2(280, 25), 16);

            // 详情文本 - 可滚动区域
            if (objectDetailsText == null)
            {
                objectDetailsText = CreateText(infoPanel.transform, "Details", "选择对象查看详情", new Vector2(0, 10), new Vector2(280, 100), 12);
                objectDetailsText.alignment = TextAnchor.UpperLeft;
            }

            // 关闭按钮
            if (closeInfoButton == null)
                closeInfoButton = CreateButton(infoPanel.transform, "CloseButton", "关闭", new Vector2(0, -75), new Vector2(100, 28));
        }

        void Update()
        {
            // 更新FPS
            UpdateFPS();

            // 定期更新基本UI元素（状态、时间等）
            if (Time.time - lastUIUpdateTime >= uiUpdateInterval)
            {
                // 只更新基本UI，不触发完整的数据更新
                if (statusText != null && simulationManager != null)
                {
                    string status = simulationManager.IsRunning ? "运行中" : "已暂停";
                    statusText.text = $"状态: {status}";
                }
                if (timeText != null && simulationManager != null)
                {
                    timeText.text = $"仿真时间: {FormatTime(simulationManager.SimulationTime)}";
                }
                lastUIUpdateTime = Time.time;
            }
        }

        /// <summary>
        /// 更新FPS显示
        /// </summary>
        private void UpdateFPS()
        {
            frameCount++;
            fpsUpdateTime += Time.unscaledDeltaTime;

            if (fpsUpdateTime >= 0.5f) // 每0.5秒更新一次
            {
                currentFPS = frameCount / fpsUpdateTime;
                frameCount = 0;
                fpsUpdateTime = 0f;

                if (fpsText != null)
                {
                    fpsText.text = $"FPS: {currentFPS:F1}";
                }
            }
        }

        void OnDestroy()
        {
            // 清理事件监听器
            if (simulationManager != null)
            {
                simulationManager.OnSimulationStarted -= OnSimulationStarted;
                simulationManager.OnSimulationPaused -= OnSimulationPaused;
                simulationManager.OnSimulationReset -= OnSimulationReset;
                simulationManager.OnDataUpdated -= OnDataUpdated;
                simulationManager.OnAlgorithmChanged -= OnAlgorithmChangedEvent;
                simulationManager.OnSimulationInitialized -= OnSimulationInitialized;
            }

            Network.ApiClient.Instance.OnConnectionStatusChanged -= UpdateConnectionStatus;
            Network.ApiClient.Instance.OnError -= OnApiError;
        }
    }

    /// <summary>
    /// 任务项UI组件
    /// </summary>
    public class TaskItemUI : MonoBehaviour
    {
        [SerializeField] private Text taskNameText;
        [SerializeField] private Text taskStatusText;
        [SerializeField] private Text taskPriorityText;
        [SerializeField] private Text taskProgressText;
        [SerializeField] private Image progressBar;
        [SerializeField] private Color pendingColor = Color.gray;
        [SerializeField] private Color assignedColor = Color.yellow;
        [SerializeField] private Color processingColor = Color.blue;
        [SerializeField] private Color completedColor = Color.green;
        [SerializeField] private Color failedColor = Color.red;

        private Core.Task taskData;

        public void Initialize(Core.Task task)
        {
            taskData = task;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (taskData == null) return;

            if (taskNameText != null)
                taskNameText.text = taskData.name;

            if (taskStatusText != null)
            {
                taskStatusText.text = GetStatusText(taskData.status);
                taskStatusText.color = GetStatusColor(taskData.status);
            }

            if (taskPriorityText != null)
                taskPriorityText.text = $"优先级: {taskData.priority}";

            if (taskProgressText != null)
                taskProgressText.text = $"{taskData.progress:P0}";

            if (progressBar != null)
            {
                progressBar.fillAmount = taskData.progress;
                progressBar.color = GetProgressColor(taskData.status, taskData.progress);
            }
        }

        private string GetStatusText(string status)
        {
            switch (status?.ToLower())
            {
                case "pending": return "等待中";
                case "assigned": return "已分配";
                case "running": return "处理中";
                case "processing": return "处理中";
                case "completed": return "已完成";
                case "failed": return "失败";
                default: return "未知";
            }
        }

        private Color GetStatusColor(string status)
        {
            switch (status?.ToLower())
            {
                case "pending": return pendingColor;
                case "assigned": return assignedColor;
                case "running": return processingColor;
                case "processing": return processingColor;
                case "completed": return completedColor;
                case "failed": return failedColor;
                default: return Color.white;
            }
        }

        private Color GetProgressColor(string status, float progress)
        {
            string normalizedStatus = status?.ToLower();
            if (normalizedStatus == "completed") return completedColor;
            if (normalizedStatus == "failed") return failedColor;

            // 根据进度渐变
            return Color.Lerp(processingColor, completedColor, progress);
        }
    }
}
