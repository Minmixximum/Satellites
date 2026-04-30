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
        private const float DefaultSpeedFactor = 1200.0f;
        private const int DefaultTargetFrameRate = 90;
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
        [SerializeField] private float speedFactor = DefaultSpeedFactor; // 默认1200倍速
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
                            Debug.LogWarning("后端仿真启动失败，继续本地仿真");
                        }
                    }
                ));
            }
            else
            {
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
            Debug.Log("重置仿真...");

            // 停止仿真
            if (isRunning)
            {
                PauseSimulation();
            }

            // 重置状态
            simulationTime = 0f;
            totalTasksProcessed = 0;
            tasksCompleted = 0;
            tasksFailed = 0;
            averageProcessingTime = 0f;

            // 重新加载数据
            if (isInitialized)
            {
                LoadInitialData();
            }

            OnSimulationReset?.Invoke();
        }

        /// <summary>
        /// 设置调度算法
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
                UpdateStatistics(tasks);
                OnDataUpdated?.Invoke();
                return;
            }

            StartCoroutine(Network.ApiClient.Instance.GetTasks(
                (taskList) =>
                {
                    tasks = taskList;
                    Debug.Log($"刷新了{taskList.Count}个任务");
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
            UpdateStatistics(tasks);

            isInitialized = true;
            OnSimulationInitialized?.Invoke();
            OnDataUpdated?.Invoke();
        }
    }
}
