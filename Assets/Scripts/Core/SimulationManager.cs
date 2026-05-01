using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SatelliteEdgeComputing.Core
{
    /// <summary>
    /// 仿真管理器（单例）
    /// </summary>
    public class SimulationManager : MonoBehaviour
    {
        private static SimulationManager _instance;
        private const float DefaultUpdateInterval = 0.2f;
        private const float DefaultSpeedFactor = 60.0f;
        private const int DefaultTargetFrameRate = 90;
        private const int MaxTasksPerSatellite = 10;
        public static SimulationManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("SimulationManager");
                    _instance = go.AddComponent<SimulationManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            updateInterval = DefaultUpdateInterval;
            speedFactor = DefaultSpeedFactor;
            Application.targetFrameRate = DefaultTargetFrameRate;
        }

        [Header("仿真状态")]
        [SerializeField] private bool isInitialized = false;
        [SerializeField] private bool isRunning = false;
        [SerializeField] private float simulationTime = 0f;
        [SerializeField] private float updateInterval = DefaultUpdateInterval; // 数据更新间隔（秒）
        [SerializeField] private SimulationConfig config = new SimulationConfig();

        [Header("时间加速")]
        [SerializeField] private float speedFactor = DefaultSpeedFactor; // 默认60倍速
        [SerializeField] private double earthRotationAngle = 0.0; // 地球旋转角度（弧度）
        private DateTime? simStartTime = null; // 模拟开始时间
        private DateTime? simCurrentTime = null; // 当前模拟时间

        [Header("数据")]
        [SerializeField] private List<Satellite> satellites = new List<Satellite>();
        [SerializeField] private List<GroundStation> groundStations = new List<GroundStation>();
        [SerializeField] private List<Task> tasks = new List<Task>();

        [Header("性能统计")]
        [SerializeField] private int totalTasksProcessed = 0;
        [SerializeField] private int tasksCompleted = 0;
        [SerializeField] private int tasksFailed = 0;
        [SerializeField] private float averageProcessingTime = 0f;

        // 事件
        public event Action OnSimulationInitialized;
        public event Action OnSimulationStarted;
        public event Action OnSimulationPaused;
        public event Action OnSimulationReset;
        public event Action OnDataUpdated;
        public event Action<string> OnAlgorithmChanged;
        public event Action<float> OnSpeedFactorChanged;
        public event Action<double> OnEarthRotationUpdated;

        // 属性
        public bool IsRunning => isRunning;
        public bool IsInitialized => isInitialized;
        public float SimulationTime => simulationTime;
        public SimulationConfig Config => config;
        public List<Satellite> Satellites => satellites;
        public List<GroundStation> GroundStations => groundStations;
        public List<Task> Tasks => tasks;

        // 时间加速属性
        public float SpeedFactor => speedFactor;
        public double EarthRotationAngle => earthRotationAngle;
        public DateTime? SimCurrentTime => simCurrentTime;
        public DateTime? SimStartTime => simStartTime;

        // 统计属性
        public int TotalTasksProcessed => totalTasksProcessed;
        public int TasksCompleted => tasksCompleted;
        public int TasksFailed => tasksFailed;
        public float SuccessRate => totalTasksProcessed > 0 ? (float)tasksCompleted / totalTasksProcessed : 0f;
        public float AverageProcessingTime => averageProcessingTime;
        public bool UseBackend => useBackend;

        private Coroutine updateCoroutine;

        [Header("网络设置")]
        [SerializeField] private bool useBackend = true; // 是否使用后端API

        /// <summary>
        /// 初始化仿真
        /// </summary>
        public void Initialize()
        {
            if (isInitialized) return;

            Debug.Log("初始化仿真管理器...");

            // 加载配置
            config = new SimulationConfig
            {
                algorithm = "fcfs",
                timeScale = DefaultSpeedFactor,
                maxTasks = 100,
                showOrbits = true,
                showLinks = true
            };

            // 测试API连接
            StartCoroutine(Network.ApiClient.Instance.TestConnection((connected) =>
            {
                useBackend = connected;
                if (connected)
                {
                    Debug.Log("API连接成功，开始加载数据...");
                    LoadInitialData();
                }
                else
                {
                    Debug.LogWarning("API连接失败，使用模拟数据...");
                    LoadMockData();
                }
            }));
        }

        /// <summary>
        /// 加载初始数据
        /// </summary>
        private void LoadInitialData()
        {
            if (!useBackend || !Network.ApiClient.Instance.IsConnected)
            {
                LoadMockData();
                return;
            }

            StartCoroutine(LoadAllData());
        }

        private IEnumerator LoadAllData()
        {
            // 并行加载所有数据
            bool satellitesLoaded = false;
            bool stationsLoaded = false;
            bool tasksLoaded = false;

            StartCoroutine(Network.ApiClient.Instance.GetSatellites(
                (satelliteList) =>
                {
                    satellites = satelliteList;
                    satellitesLoaded = true;
                    Debug.Log($"加载了{satelliteList.Count}颗卫星");
                },
                (error) => { Debug.LogError($"加载卫星失败: {error}，使用模拟数据"); satellites = Network.ApiClient.Instance.GetMockSatellites(); satellitesLoaded = true; }
            ));

            StartCoroutine(Network.ApiClient.Instance.GetGroundStations(
                (stationList) =>
                {
                    groundStations = stationList;
                    stationsLoaded = true;
                    Debug.Log($"加载了{stationList.Count}个地面站");
                },
                (error) => { Debug.LogError($"加载地面站失败: {error}，使用模拟数据"); groundStations = Network.ApiClient.Instance.GetMockGroundStations(); stationsLoaded = true; }
            ));

            StartCoroutine(Network.ApiClient.Instance.GetTasks(
                (taskList) =>
                {
                    tasks = taskList;
                    tasksLoaded = true;
                    Debug.Log($"加载了{taskList.Count}个任务");
                },
                (error) => { Debug.LogError($"加载任务失败: {error}，使用模拟数据"); tasks = Network.ApiClient.Instance.GetMockTasks(); tasksLoaded = true; }
            ));

            // 等待所有数据加载完成
            yield return new WaitUntil(() => satellitesLoaded && stationsLoaded && tasksLoaded);

            isInitialized = true;
            OnSimulationInitialized?.Invoke();
            OnDataUpdated?.Invoke();
            Debug.Log("仿真初始化完成");
        }

        /// <summary>
        /// 开始仿真
        /// </summary>
        public void StartSimulation()
        {
            if (!isInitialized)
            {
                Debug.LogWarning("仿真未初始化，先调用Initialize()");
                return;
            }

            if (isRunning) return;

            Debug.Log("开始仿真...");
            isRunning = true;

            // 初始化模拟开始时间
            simStartTime = DateTime.UtcNow;
            simCurrentTime = simStartTime;
            earthRotationAngle = 0.0;

            // 如果使用后端，通知后端开始仿真
            if (useBackend && Network.ApiClient.Instance.IsConnected)
            {
                StartCoroutine(Network.ApiClient.Instance.StartSimulation(
                    config.algorithm,
                    config.maxTasks,
                    speedFactor,
                    (success) =>
                    {
                        if (success)
                        {
                            Debug.Log("后端仿真已启动");
                        }
                        else
                        {
                            useBackend = false;
                            PrepareLocalFallbackState();
                            UpdateLocalTaskSimulation();
                            Debug.LogWarning("后端仿真启动失败，继续本地仿真");
                        }
                    }
                ));
            }
            else
            {
                PrepareLocalFallbackState();
                UpdateLocalTaskSimulation();
                Debug.Log("使用本地仿真模式");
            }

            // 启动数据更新协程
            if (updateCoroutine != null)
                StopCoroutine(updateCoroutine);

            updateCoroutine = StartCoroutine(UpdateSimulationData());

            OnSimulationStarted?.Invoke();
        }

        /// <summary>
        /// 暂停仿真
        /// </summary>
        public void PauseSimulation()
        {
            if (!isRunning) return;

            Debug.Log("暂停仿真...");
            isRunning = false;

            // 如果使用后端，通知后端暂停仿真
            if (useBackend && Network.ApiClient.Instance.IsConnected)
            {
                StartCoroutine(Network.ApiClient.Instance.PauseSimulation(
                    (success) =>
                    {
                        if (success)
                        {
                            Debug.Log("后端仿真已暂停");
                        }
                    }
                ));
            }

            // 停止数据更新协程
            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
                updateCoroutine = null;
            }

            OnSimulationPaused?.Invoke();
        }

        /// <summary>
        /// 重置仿真
        /// </summary>
        public void ResetSimulation()
        {
            Debug.Log("Reset simulation...");

            if (isRunning)
            {
                PauseSimulation();
            }

            simulationTime = 0f;
            totalTasksProcessed = 0;
            tasksCompleted = 0;
            tasksFailed = 0;
            averageProcessingTime = 0f;
            simStartTime = null;
            simCurrentTime = null;
            earthRotationAngle = 0.0;
            tasks.Clear();

            if (useBackend && Network.ApiClient.Instance.IsConnected)
            {
                StartCoroutine(Network.ApiClient.Instance.ResetSimulation(
                    (success) =>
                    {
                        if (success)
                        {
                            RefreshTasks();
                        }
                        else
                        {
                            Debug.LogError("Backend simulation reset failed");
                        }
                    },
                    (error) => Debug.LogError($"Backend simulation reset error: {error}")
                ));
            }
            else if (isInitialized)
            {
                LoadInitialData();
            }

            OnSimulationReset?.Invoke();
        }

        public void ClearTasksLocal()
        {
            tasks.Clear();
            totalTasksProcessed = 0;
            tasksCompleted = 0;
            tasksFailed = 0;
            averageProcessingTime = 0f;
            OnDataUpdated?.Invoke();
        }

        /// <summary>
        /// Set scheduling algorithm.
        /// </summary>
        public void SetAlgorithm(string algorithm)
        {
            if (config.algorithm == algorithm) return;

            config.algorithm = algorithm;
            Debug.Log($"切换调度算法: {algorithm}");

            // 通知后端
            if (isRunning && useBackend && Network.ApiClient.Instance.IsConnected)
            {
                StartCoroutine(Network.ApiClient.Instance.SetAlgorithm(algorithm,
                    (success) =>
                    {
                        if (success)
                        {
                            Debug.Log($"后端算法已切换为: {algorithm}");
                        }
                    }
                ));
            }

            OnAlgorithmChanged?.Invoke(algorithm);
        }

        /// <summary>
        /// 设置时间缩放
        /// </summary>
        public void SetTimeScale(float timeScale)
        {
            // Backward-compatible wrapper: timeScale now maps to speedFactor.
            SetSpeedFactor(timeScale);
            config.timeScale = speedFactor;
            Debug.Log($"Time scale set to: {config.timeScale} (mapped to speed_factor)");
        }

        /// <summary>
        /// 设置时间加速因子
        /// </summary>
        public void SetSpeedFactor(float factor)
        {
            speedFactor = Mathf.Clamp(factor, DefaultSpeedFactor, 3600f);
            Debug.Log($"时间加速因子设置为: {speedFactor}x");

            // 通知后端
            if (useBackend && Network.ApiClient.Instance.IsConnected)
            {
                StartCoroutine(Network.ApiClient.Instance.SetSpeedFactor(speedFactor,
                    (newFactor) =>
                    {
                        speedFactor = Mathf.Clamp(newFactor, DefaultSpeedFactor, 3600f);
                        Debug.Log($"后端速度因子已设置为: {newFactor}x");
                    },
                    (error) => Debug.LogWarning($"设置后端速度因子失败: {error}")
                ));
            }

            OnSpeedFactorChanged?.Invoke(speedFactor);
        }

        /// <summary>
        /// 更新仿真数据（定期调用）
        /// </summary>
        private IEnumerator UpdateSimulationData()
        {
            while (isRunning)
            {
                yield return new WaitForSeconds(updateInterval);

                // 更新仿真时间
                simulationTime += updateInterval * speedFactor;

                if (!useBackend || !Network.ApiClient.Instance.IsConnected)
                {
                    // 本地模式：基于 speed_factor 更新模拟时间
                    UpdateLocalSimTime();
                    UpdateLocalTaskSimulation();
                    UpdateStatistics(tasks);
                    OnDataUpdated?.Invoke();
                    continue;
                }

                // 从后端获取时间信息
                StartCoroutine(Network.ApiClient.Instance.GetSimulationTime(
                    (timeInfo) => {
                        if (timeInfo != null)
                        {
                            earthRotationAngle = timeInfo.earth_rotation_angle_rad;
                            if (DateTime.TryParse(timeInfo.sim_time, out DateTime parsedTime))
                            {
                                simCurrentTime = parsedTime;
                            }
                            OnEarthRotationUpdated?.Invoke(earthRotationAngle);
                        }
                    },
                    (error) => Debug.LogWarning($"获取时间信息失败: {error}")
                ));

                // 从后端获取最新数据
                StartCoroutine(Network.ApiClient.Instance.GetSatellites(
                    (satelliteList) => {
                        satellites = satelliteList;
                        Debug.Log($"[SimulationManager] GetSatellites回调: 获取到 {satelliteList.Count} 颗卫星");
                        OnDataUpdated?.Invoke();
                        Debug.Log($"[SimulationManager] OnDataUpdated事件已触发");
                    },
                    (error) =>
                    {
                        useBackend = false;
                        PrepareLocalFallbackState();
                        Debug.LogWarning($"更新卫星数据失败: {error}");
                    }
                ));

                StartCoroutine(Network.ApiClient.Instance.GetTasks(
                    (taskList) => {
                        tasks = taskList;
                        Debug.Log($"[SimulationManager] GetTasks回调: 获取到 {taskList.Count} 个任务");
                        // 更新统计信息
                        UpdateStatistics(taskList);
                        OnDataUpdated?.Invoke();
                        Debug.Log($"[SimulationManager] OnDataUpdated事件已触发");
                    },
                    (error) =>
                    {
                        useBackend = false;
                        PrepareLocalFallbackState();
                        Debug.LogWarning($"更新任务数据失败: {error}");
                    }
                ));
            }
        }

        /// <summary>
        /// 本地模式更新模拟时间
        /// </summary>
        private void UpdateLocalSimTime()
        {
            // Earth angular velocity: 7.292115e-5 rad/s
            const double EarthAngularVelocity = 7.292115e-5;

            // Update earth rotation angle based on speed factor
            double deltaSeconds = updateInterval * speedFactor;
            earthRotationAngle += deltaSeconds * EarthAngularVelocity;

            // Normalize angle to [0, 2π)
            earthRotationAngle = earthRotationAngle % (2.0 * Math.PI);
            if (earthRotationAngle < 0) earthRotationAngle += 2.0 * Math.PI;

            // Update sim current time
            if (simStartTime.HasValue)
            {
                simCurrentTime = simStartTime.Value.AddSeconds(simulationTime);
            }

            OnEarthRotationUpdated?.Invoke(earthRotationAngle);
        }

        private void PrepareLocalFallbackState()
        {
            if (tasks == null)
                tasks = new List<Task>();

            if (satellites == null)
                satellites = new List<Satellite>();

            foreach (var task in tasks)
            {
                if (task == null)
                    continue;

                task.status = NormalizeLocalStatus(task.status);
                task.progress = Mathf.Clamp01(task.progress);

                if (task.status == "completed" || task.status == "failed")
                {
                    task.progress = 1f;
                    continue;
                }

                if ((task.status == "assigned" || task.status == "running") && task.assignedSatelliteId > 0)
                {
                    if (task.scheduledStartTime < 0f || task.scheduledEndTime <= task.scheduledStartTime)
                        InitializeLocalTaskTimeline(task, FindSatellite(task.assignedSatelliteId));
                    continue;
                }

                ResetLocalTask(task);
            }

            ResetSatelliteTaskCountsFromTasks();
        }

        private void UpdateLocalTaskSimulation()
        {
            PrepareLocalFallbackState();
            UpdateActiveLocalTasks();
            AssignPendingLocalTasks();
            UpdateActiveLocalTasks();
            ResetSatelliteTaskCountsFromTasks();
        }

        private void UpdateActiveLocalTasks()
        {
            foreach (var task in tasks)
            {
                if (task == null)
                    continue;

                task.status = NormalizeLocalStatus(task.status);

                switch (task.status)
                {
                    case "pending":
                        if (task.deadline > 0f && simulationTime >= task.deadline)
                            FailLocalTask(task);
                        break;
                    case "assigned":
                        if (task.deadline > 0f && simulationTime >= task.deadline)
                        {
                            FailLocalTask(task);
                            break;
                        }

                        if (task.assignedSatelliteId <= 0)
                        {
                            ResetLocalTask(task);
                            break;
                        }

                        if (task.scheduledStartTime < 0f || task.scheduledEndTime <= task.scheduledStartTime)
                            InitializeLocalTaskTimeline(task, FindSatellite(task.assignedSatelliteId));

                        task.progress = 0f;
                        if (simulationTime >= task.scheduledStartTime)
                        {
                            task.status = "running";
                            UpdateRunningTaskProgress(task);
                        }
                        break;
                    case "running":
                        if (task.deadline > 0f && simulationTime >= task.deadline && simulationTime < task.scheduledEndTime)
                        {
                            FailLocalTask(task);
                            break;
                        }

                        if (task.scheduledStartTime < 0f || task.scheduledEndTime <= task.scheduledStartTime)
                            InitializeLocalTaskTimeline(task, FindSatellite(task.assignedSatelliteId));

                        UpdateRunningTaskProgress(task);
                        if (simulationTime >= task.scheduledEndTime)
                            CompleteLocalTask(task);
                        break;
                    case "completed":
                    case "failed":
                        task.progress = 1f;
                        break;
                    default:
                        ResetLocalTask(task);
                        break;
                }
            }
        }

        private void AssignPendingLocalTasks()
        {
            if (tasks == null || satellites == null || satellites.Count == 0)
                return;

            var projectedCounts = new Dictionary<int, int>();
            foreach (var satellite in satellites)
            {
                if (satellite != null)
                    projectedCounts[satellite.id] = 0;
            }

            foreach (var task in tasks)
            {
                if (task == null)
                    continue;

                string status = NormalizeLocalStatus(task.status);
                if ((status == "assigned" || status == "running") && task.assignedSatelliteId > 0)
                {
                    if (!projectedCounts.ContainsKey(task.assignedSatelliteId))
                        projectedCounts[task.assignedSatelliteId] = 0;

                    projectedCounts[task.assignedSatelliteId]++;
                }
            }

            List<Task> pendingTasks = GetSortedPendingTasks();
            foreach (var task in pendingTasks)
            {
                if (task.deadline > 0f && simulationTime >= task.deadline)
                {
                    FailLocalTask(task);
                    continue;
                }

                Satellite satellite = SelectLocalSatellite(task, projectedCounts);
                if (satellite == null)
                    continue;

                float duration = EstimateLocalTaskDuration(task, satellite);
                int queueDepth = projectedCounts.TryGetValue(satellite.id, out int count) ? count : 0;
                float startTime = simulationTime + queueDepth * duration;

                task.assignedSatelliteId = satellite.id;
                task.status = "assigned";
                task.progress = 0f;
                task.scheduledStartTime = startTime;
                task.scheduledEndTime = startTime + duration;
                projectedCounts[satellite.id] = queueDepth + 1;
            }
        }

        private List<Task> GetSortedPendingTasks()
        {
            var pendingTasks = new List<Task>();
            foreach (var task in tasks)
            {
                if (task != null && NormalizeLocalStatus(task.status) == "pending")
                    pendingTasks.Add(task);
            }

            string algorithm = config != null ? (config.algorithm ?? "fcfs").ToLowerInvariant() : "fcfs";
            pendingTasks.Sort((left, right) =>
            {
                switch (algorithm)
                {
                    case "sjf":
                        return CompareThenById(left.computation.CompareTo(right.computation), left.id, right.id);
                    case "edd":
                        return CompareThenById(left.deadline.CompareTo(right.deadline), left.id, right.id);
                    case "max_visibility":
                        int priorityCompare = right.priority.CompareTo(left.priority);
                        return priorityCompare != 0 ? priorityCompare : CompareThenById(left.deadline.CompareTo(right.deadline), left.id, right.id);
                    default:
                        return left.id.CompareTo(right.id);
                }
            });

            return pendingTasks;
        }

        private Satellite SelectLocalSatellite(Task task, Dictionary<int, int> projectedCounts)
        {
            Satellite bestSatellite = null;
            float bestScore = float.MinValue;
            string algorithm = config != null ? (config.algorithm ?? "fcfs").ToLowerInvariant() : "fcfs";

            foreach (var satellite in satellites)
            {
                if (satellite == null)
                    continue;

                int queueDepth = projectedCounts.TryGetValue(satellite.id, out int count) ? count : 0;
                if (queueDepth >= MaxTasksPerSatellite)
                    continue;

                if (task.computation > satellite.capacity)
                    continue;

                float score = satellite.capacity - queueDepth * 25f;
                if (algorithm == "max_visibility")
                    score += satellite.power * 0.1f;
                else if (algorithm == "edd")
                    score -= queueDepth * 5f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestSatellite = satellite;
                }
            }

            return bestSatellite;
        }

        private float EstimateLocalTaskDuration(Task task, Satellite satellite)
        {
            float capacity = satellite != null ? Mathf.Max(1f, satellite.capacity) : 100f;
            float baseDuration = Mathf.Max(1f, task.computation / capacity);
            float minVisibleDuration = 5f;
            return Mathf.Max(baseDuration, minVisibleDuration);
        }

        private void InitializeLocalTaskTimeline(Task task, Satellite satellite)
        {
            float duration = EstimateLocalTaskDuration(task, satellite);
            float clampedProgress = Mathf.Clamp01(task.progress);
            task.scheduledStartTime = Mathf.Max(0f, simulationTime - duration * clampedProgress);
            task.scheduledEndTime = task.scheduledStartTime + duration;

            if (task.scheduledEndTime <= simulationTime)
            {
                task.scheduledEndTime = simulationTime + Mathf.Max(1f, duration * (1f - clampedProgress));
            }
        }

        private void UpdateRunningTaskProgress(Task task)
        {
            float duration = Mathf.Max(1f, task.scheduledEndTime - task.scheduledStartTime);
            task.progress = Mathf.Clamp01((simulationTime - task.scheduledStartTime) / duration);
        }

        private void CompleteLocalTask(Task task)
        {
            task.status = "completed";
            task.progress = 1f;
        }

        private void FailLocalTask(Task task)
        {
            task.status = "failed";
            task.progress = 1f;
        }

        private void ResetLocalTask(Task task)
        {
            task.status = "pending";
            task.assignedSatelliteId = 0;
            task.progress = 0f;
            task.scheduledStartTime = -1f;
            task.scheduledEndTime = -1f;
        }

        private void ResetSatelliteTaskCountsFromTasks()
        {
            foreach (var satellite in satellites)
            {
                if (satellite == null)
                    continue;

                satellite.taskCount = 0;
                satellite.status = "idle";
            }

            foreach (var task in tasks)
            {
                if (task == null || task.assignedSatelliteId <= 0)
                    continue;

                string status = NormalizeLocalStatus(task.status);
                if (status != "assigned" && status != "running")
                    continue;

                Satellite satellite = FindSatellite(task.assignedSatelliteId);
                if (satellite == null)
                    continue;

                satellite.taskCount++;
            }

            foreach (var satellite in satellites)
            {
                if (satellite == null)
                    continue;

                if (satellite.taskCount == 0)
                    satellite.status = "idle";
                else if (satellite.taskCount >= MaxTasksPerSatellite)
                    satellite.status = "overloaded";
                else
                    satellite.status = "busy";
            }
        }

        private string NormalizeLocalStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "pending";

            string normalized = status.Trim().ToLowerInvariant();
            return normalized == "processing" ? "running" : normalized;
        }

        private int CompareThenById(int comparison, int leftId, int rightId)
        {
            return comparison != 0 ? comparison : leftId.CompareTo(rightId);
        }

        /// <summary>
        /// 更新统计信息
        /// </summary>
        private void UpdateStatistics(List<Task> currentTasks)
        {
            int completed = 0;
            int failed = 0;
            float totalProcessingTime = 0f;

            foreach (var task in currentTasks)
            {
                if (task.status == "completed")
                {
                    completed++;
                    // 这里可以添加处理时间计算
                }
                else if (task.status == "failed")
                {
                    failed++;
                }
            }

            tasksCompleted = completed;
            tasksFailed = failed;
            totalTasksProcessed = completed + failed;

            if (completed > 0)
            {
                // 简化计算平均处理时间
                averageProcessingTime = simulationTime / completed;
            }
        }

        /// <summary>
        /// 查找卫星
        /// </summary>
        public Satellite FindSatellite(int id)
        {
            return satellites.Find(s => s.id == id);
        }

        /// <summary>
        /// 查找地面站
        /// </summary>
        public GroundStation FindGroundStation(int id)
        {
            return groundStations.Find(g => g.id == id);
        }

        /// <summary>
        /// 查找任务
        /// </summary>
        public Task FindTask(int id)
        {
            return tasks.Find(t => t.id == id);
        }

        void Start()
        {
            // 自动初始化
            Initialize();
        }

        void OnDestroy()
        {
            if (updateCoroutine != null)
            {
                StopCoroutine(updateCoroutine);
            }
        }

        /// <summary>
        /// 刷新任务数据
        /// </summary>
        public void RefreshTasks()
        {
            if (!useBackend || !Network.ApiClient.Instance.IsConnected)
            {
                PrepareLocalFallbackState();
                if (isRunning)
                    UpdateLocalTaskSimulation();
                UpdateStatistics(tasks);
                OnDataUpdated?.Invoke();
                return;
            }

            StartCoroutine(Network.ApiClient.Instance.GetTasks(
                (taskList) =>
                {
                    tasks = taskList;
                    Debug.Log($"刷新了{taskList.Count}个任务");
                    UpdateStatistics(tasks);
                    OnDataUpdated?.Invoke();
                },
                (error) => Debug.LogError($"刷新任务失败: {error}")
            ));
        }

        public void SetBackendEnabled(bool enabled)
        {
            useBackend = enabled;
        }

        private void LoadMockData()
        {
            satellites = Network.ApiClient.Instance.GetMockSatellites();
            groundStations = Network.ApiClient.Instance.GetMockGroundStations();
            tasks = Network.ApiClient.Instance.GetMockTasks();
            PrepareLocalFallbackState();
            UpdateStatistics(tasks);

            isInitialized = true;
            OnSimulationInitialized?.Invoke();
            OnDataUpdated?.Invoke();
        }
    }
}
