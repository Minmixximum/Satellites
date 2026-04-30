using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;
using UnityEngine.Networking;

namespace SatelliteEdgeComputing.Network
{
    /// <summary>
    /// API客户端管理器
    /// </summary>
    public class ApiClient : MonoBehaviour
    {
        private static ApiClient _instance;
        public static ApiClient Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("ApiClient");
                    _instance = go.AddComponent<ApiClient>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        [Header("API配置")]
        [SerializeField] private string apiBaseUrl = "http://localhost:5000/api";
        [SerializeField] private float requestTimeout = 10f;
        [SerializeField] private int maxRetries = 3;
        [SerializeField] private float retryDelay = 1f;

        [Header("Status")]
        [SerializeField] private bool isConnected = false;
        [SerializeField] private string lastError = "";

        // 事件
        public event Action OnConnectionStatusChanged;
        public event Action<string> OnError;

        public bool IsConnected => isConnected;
        public string LastError => lastError;
        private const double KmToM = 1000.0;

        
        /// <summary>
        /// 测试API连接
        /// </summary>
        public IEnumerator TestConnection(Action<bool> callback = null)
        {
            string url = $"{apiBaseUrl}/health";
            bool handled = false;

            yield return SendRequest(url, UnityWebRequest.kHttpVerbGET, null,
                (responseText) =>
                {
                    handled = true;
                    if (!TryParseJson(responseText, out HealthResponse healthResponse, out string parseError))
                    {
                        UpdateConnectionState(false, parseError);
                        callback?.Invoke(false);
                        return;
                    }

                    if (healthResponse != null && healthResponse.success)
                    {
                        UpdateConnectionState(true);
                        Debug.Log("API连接成功");
                        callback?.Invoke(true);
                    }
                    else
                    {
                        string error = GetApiErrorMessage(healthResponse?.message, healthResponse?.error, "Health check response invalid");
                        UpdateConnectionState(false, error);
                        callback?.Invoke(false);
                    }
                },
                (error) =>
                {
                    handled = true;
                    callback?.Invoke(false);
                });

            if (!handled)
            {
                callback?.Invoke(isConnected);
            }
        }

        /// <summary>
        /// 获取卫星列表
        /// </summary>
        public IEnumerator GetSatellites(Action<List<Core.Satellite>> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/satellite/all";
            yield return SendRequest(url, UnityWebRequest.kHttpVerbGET, null,
                (responseText) => {
                    if (!TryParseJson(responseText, out BackendSatelliteListResponse response, out string parseError))
                    {
                        errorCallback?.Invoke(parseError);
                        callback?.Invoke(GetMockSatellites());
                        return;
                    }

                    if (response == null || !response.success)
                    {
                        string error = GetApiErrorMessage(response?.message, response?.error, "获取卫星列表失败");
                        errorCallback?.Invoke(error);
                        callback?.Invoke(GetMockSatellites());
                        return;
                    }

                    var converted = new List<Core.Satellite>();
                    if (response.data != null)
                    {
                        foreach (var backendSat in response.data)
                        {
                            converted.Add(ConvertBackendSatellite(backendSat));
                        }
                    }
                    callback?.Invoke(converted);
                },
                (error) => {
                    errorCallback?.Invoke(error);
                    // 返回模拟数据用于测试
                    callback?.Invoke(GetMockSatellites());
                });
        }

        /// <summary>
        /// 获取地面站列表�?        /// </summary>
        public IEnumerator GetGroundStations(Action<List<Core.GroundStation>> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/groundstation/all";
            yield return SendRequest(url, UnityWebRequest.kHttpVerbGET, null,
                (responseText) => {
                    if (!TryParseJson(responseText, out BackendGroundStationListResponse response, out string parseError))
                    {
                        errorCallback?.Invoke(parseError);
                        callback?.Invoke(GetMockGroundStations());
                        return;
                    }

                    if (response == null || !response.success)
                    {
                        string error = GetApiErrorMessage(response?.message, response?.error, "Failed to get ground station list");
                        errorCallback?.Invoke(error);
                        callback?.Invoke(GetMockGroundStations());
                        return;
                    }

                    var converted = new List<Core.GroundStation>();
                    if (response.data != null)
                    {
                        foreach (var backendGs in response.data)
                        {
                            converted.Add(ConvertBackendGroundStation(backendGs));
                        }
                    }
                    callback?.Invoke(converted);
                },
                (error) => {
                    errorCallback?.Invoke(error);
                    // 返回模拟数据用于测试
                    callback?.Invoke(GetMockGroundStations());
                });
        }

        /// <summary>
        /// 获取任务列表
        /// </summary>
        public IEnumerator GetTasks(Action<List<Core.Task>> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/tasks/list";
            Debug.Log($"[ApiClient.GetTasks] 开始请求: {url}");
            yield return SendRequest(url, UnityWebRequest.kHttpVerbGET, null,
                (responseText) => {
                    Debug.Log($"[ApiClient.GetTasks] 收到响应, 长度={responseText?.Length ?? 0}");
                    if (!TryParseTasksResponse(responseText, out BackendTask[] backendTasks, out string responseError))
                    {
                         Debug.LogWarning($"[ApiClient.GetTasks] 解析失败: {responseError}");
                        errorCallback?.Invoke(responseError);
                        callback?.Invoke(GetMockTasks());
                        return;
                    }

                    var converted = new List<Core.Task>();
                    if (backendTasks != null)
                    {
                        Debug.Log($"[ApiClient.GetTasks] 解析成功, 任务数组长度={backendTasks.Length}");
                        foreach (var backendTask in backendTasks)
                        {
                            if (backendTask != null)
                            {
                                converted.Add(ConvertBackendTask(backendTask));
                            }
                        }
                    }

                    if (converted.Count == 0)
                    {
                        Debug.LogWarning($"[ApiClient.GetTasks] 转换后任务数为0. Raw response: {TruncateForLog(responseText)}");
                    }
                    else
                    {
                        Debug.Log($"[ApiClient.GetTasks] 成功转换 {converted.Count} 个任务");
                    }

                    callback?.Invoke(converted);
                },
                (error) => {
                    Debug.LogError($"[ApiClient.GetTasks] 请求失败: {error}");
                    errorCallback?.Invoke(error);
                    // 返回模拟数据用于测试
                    callback?.Invoke(GetMockTasks());
                });
        }

        /// <summary>
        /// 启动仿真
        /// </summary>
        public IEnumerator StartSimulation(string algorithm = "fcfs", int maxTasks = 100, float speedFactor = 60.0f,
            Action<bool> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/simulation/start";
            var requestData = new StartSimulationRequest
            {
                algorithm = algorithm,
                max_tasks = maxTasks,
                speed_factor = speedFactor,
                // Legacy request fields retained for compatibility.
                time_scale = speedFactor,
                time_speed = speedFactor
            };

            string json = JsonUtility.ToJson(requestData);
            yield return SendBooleanRequest(url, UnityWebRequest.kHttpVerbPOST, json, callback, errorCallback);
        }

        /// <summary>
        /// 暂停仿真
        /// </summary>
        public IEnumerator PauseSimulation(Action<bool> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/simulation/pause";
            yield return SendBooleanRequest(url, UnityWebRequest.kHttpVerbPOST, null, callback, errorCallback);
        }

        /// <summary>
        /// 设置调度算法
        /// </summary>
        public IEnumerator SetAlgorithm(string algorithm, Action<bool> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/simulation/algorithm";
            var requestData = new SetAlgorithmRequest { algorithm = algorithm };
            string json = JsonUtility.ToJson(requestData);

            yield return SendBooleanRequest(url, UnityWebRequest.kHttpVerbPOST, json, callback, errorCallback);
        }

        /// <summary>
        /// 获取模拟时间信息
        /// </summary>
        public IEnumerator GetSimulationTime(Action<TimeInfoData> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/simulation/time";
            yield return SendRequest(url, UnityWebRequest.kHttpVerbGET, null,
                (responseText) => {
                    if (!TryParseJson(responseText, out TimeInfoResponse response, out string parseError))
                    {
                        errorCallback?.Invoke(parseError);
                        callback?.Invoke(null);
                        return;
                    }

                    if (response == null || !response.success)
                    {
                        string error = GetApiErrorMessage(response?.message, response?.error, "获取时间信息失败");
                        errorCallback?.Invoke(error);
                        callback?.Invoke(null);
                        return;
                    }

                    callback?.Invoke(response.data);
                },
                (error) => {
                    errorCallback?.Invoke(error);
                    callback?.Invoke(null);
                });
        }

        /// <summary>
        /// 设置时间加速因子
        /// </summary>
        public IEnumerator SetSpeedFactor(float speedFactor, Action<float> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/simulation/speed";
            var requestData = new SetSpeedFactorRequest { speed_factor = speedFactor };
            string json = JsonUtility.ToJson(requestData);

            yield return SendRequest(url, UnityWebRequest.kHttpVerbPOST, json,
                (responseText) => {
                    if (!TryParseJson(responseText, out SetSpeedFactorResponse response, out string parseError))
                    {
                        errorCallback?.Invoke(parseError);
                        callback?.Invoke(speedFactor);
                        return;
                    }

                    if (response == null || !response.success)
                    {
                        string error = GetApiErrorMessage(response?.message, response?.error, "设置速度因子失败");
                        errorCallback?.Invoke(error);
                        callback?.Invoke(speedFactor);
                        return;
                    }

                    callback?.Invoke(response.data.speed_factor);
                },
                (error) => {
                    errorCallback?.Invoke(error);
                    callback?.Invoke(speedFactor);
                });
        }

        /// <summary>
        /// 通用请求发送方法（支持重试）�?        /// </summary>
        private IEnumerator SendRequest(string url, string method, string jsonData,
            Action<string> successCallback, Action<string> errorCallback)
        {
            int retryCount = 0;
            bool success = false;
            string error = "";

            while (retryCount < maxRetries && !success)
            {
                using (UnityWebRequest request = new UnityWebRequest(url, method))
                {
                    request.timeout = (int)requestTimeout;
                    request.downloadHandler = new DownloadHandlerBuffer();
                    request.SetRequestHeader("Accept", "application/json");

                    if (!string.IsNullOrEmpty(jsonData))
                    {
                        request.uploadHandler = new UploadHandlerRaw(System.Text.Encoding.UTF8.GetBytes(jsonData));
                        request.SetRequestHeader("Content-Type", "application/json");
                    }

                    yield return request.SendWebRequest();

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        success = true;
                        UpdateConnectionState(true);
                        successCallback?.Invoke(request.downloadHandler.text);
                    }
                    else
                    {
                        error = $"请求失败 ({retryCount + 1}/{maxRetries}): {request.error}";
                        retryCount++;

                        if (retryCount < maxRetries)
                        {
                            yield return new WaitForSeconds(retryDelay);
                        }
                        else
                        {
                            UpdateConnectionState(false, error);
                            errorCallback?.Invoke(error);
                        }
                    }
                }
            }
        }

        private IEnumerator SendBooleanRequest(string url, string method, string jsonData,
            Action<bool> callback, Action<string> errorCallback)
        {
            yield return SendRequest(url, method, jsonData,
                (responseText) =>
                {
                    if (!TryParseJson(responseText, out BasicApiResponse response, out string parseError))
                    {
                        errorCallback?.Invoke(parseError);
                        callback?.Invoke(false);
                        return;
                    }

                    if (response != null && response.success)
                    {
                        callback?.Invoke(true);
                        return;
                    }

                    string error = GetApiErrorMessage(response?.message, response?.error, "请求失败");
                    errorCallback?.Invoke(error);
                    callback?.Invoke(false);
                },
                (error) =>
                {
                    errorCallback?.Invoke(error);
                    callback?.Invoke(false);
                });
        }

        private bool TryParseJson<T>(string json, out T result, out string error) where T : class
        {
            try
            {
                result = JsonUtility.FromJson<T>(json);
                error = "";
                return result != null;
            }
            catch (Exception e)
            {
                result = default(T);
                error = $"JSON解析失败: {e.Message}";
                return false;
            }
        }

                private bool TryParseTasksResponse(string json, out BackendTask[] tasks, out string error)
        {
            tasks = null;
            error = "";

            string standardParseError = "";
            if (TryParseJson(json, out BackendTaskListResponse standard, out standardParseError) &&
                standard != null)
            {
                if (!standard.success)
                {
                    error = GetApiErrorMessage(standard.message, standard.error, "Failed to get task list");
                    return false;
                }

                if (standard.data != null)
                {
                    tasks = standard.data;
                    return true;
                }
            }

            string altParseError = "";
            if (TryParseJson(json, out BackendTaskListAltResponse alt, out altParseError) &&
                alt != null)
            {
                if (!alt.success)
                {
                    error = GetApiErrorMessage(alt.message, alt.error, "Failed to get task list");
                    return false;
                }

                if (alt.tasks != null)
                {
                    tasks = alt.tasks;
                    return true;
                }
            }

            string nestedParseError = "";
            if (TryParseJson(json, out BackendTaskListNestedResponse nested, out nestedParseError) &&
                nested != null)
            {
                if (!nested.success)
                {
                    error = GetApiErrorMessage(nested.message, nested.error, "Failed to get task list");
                    return false;
                }

                if (nested.data != null && nested.data.tasks != null)
                {
                    tasks = nested.data.tasks;
                    return true;
                }
            }

            tasks = Array.Empty<BackendTask>();
            error = $"Task response parsed but no supported tasks field found. Standard: {standardParseError}; Alt: {altParseError}; Nested: {nestedParseError}";
            return true;
        }
        private string GetApiErrorMessage(string message, string error, string fallback)
        {
            if (!string.IsNullOrWhiteSpace(message))
                return message;

            if (!string.IsNullOrWhiteSpace(error))
                return error;

            return fallback;
        }

                private string TruncateForLog(string value, int maxLen = 300)
        {
            if (string.IsNullOrEmpty(value))
                return "";

            return value.Length <= maxLen ? value : value.Substring(0, maxLen) + "...";
        }
        private void UpdateConnectionState(bool connected, string error = "")
        {
            bool statusChanged = isConnected != connected;
            isConnected = connected;
            lastError = connected ? "" : error;

            if (!connected && !string.IsNullOrWhiteSpace(error))
            {
                Debug.LogError(error);
                OnError?.Invoke(error);
            }

            if (statusChanged)
            {
                OnConnectionStatusChanged?.Invoke();
            }
        }

        /// <summary>
        /// 初始化演示数据（生成8个示例任务）
        /// </summary>
        public IEnumerator InitializeDemo(Action<bool> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/initialize/demo";
            yield return SendBooleanRequest(url, UnityWebRequest.kHttpVerbPOST, null, callback, errorCallback);
        }

        /// <summary>
        /// 从场景文件初始化数据
        /// </summary>
        public IEnumerator InitializeScenario(Action<bool> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/initialize/scenario";
            yield return SendBooleanRequest(url, UnityWebRequest.kHttpVerbPOST, null, callback, errorCallback);
        }

        /// <summary>
        /// 清除所有任务�?        /// </summary>
        public IEnumerator ClearTasks(Action<bool> callback = null, Action<string> errorCallback = null)
        {
            string url = $"{apiBaseUrl}/initialize/clear";
            yield return SendBooleanRequest(url, UnityWebRequest.kHttpVerbPOST, null, callback, errorCallback);
        }

        #region 数据转换方法
        /// <summary>
        /// 将后端卫星数据转换为前端模型
        /// </summary>
        private Core.Satellite ConvertBackendSatellite(BackendSatellite backendSat)
        {
            var sat = new Core.Satellite();
            string satelliteId = backendSat?.id ?? "";
            // Parse string ID like sat_001; fallback to hash when not numeric.
            if (int.TryParse(satelliteId.Replace("sat_", ""), out int intId))
                sat.id = intId;
            else
                sat.id = satelliteId.GetHashCode();

            sat.name = backendSat.name;

            // 使用位置数据
            if (backendSat.position != null)
            {
                sat.latitude = backendSat.position.lat;
                sat.longitude = backendSat.position.lon;
                sat.altitude = backendSat.position.alt * KmToM;
            }
            else
            {
                // 默认�?                sat.latitude = 0;
                sat.longitude = 0;
                sat.altitude = 500 * KmToM;
            }

            sat.capacity = backendSat.capacity;
            sat.power = backendSat.current_power;
            sat.taskCount = backendSat.task_queue_length;
            sat.status = backendSat.is_visible ? "visible" : "idle";

            return sat;
        }

        /// <summary>
        /// 将后端地面站数据转换为前端模型�?        /// </summary>
        private Core.GroundStation ConvertBackendGroundStation(BackendGroundStation backendGs)
        {
            var gs = new Core.GroundStation();
            string groundStationId = backendGs?.id ?? "";

            if (int.TryParse(groundStationId.Replace("gs_", ""), out int intId))
                gs.id = intId;
            else
                gs.id = groundStationId.GetHashCode();

            gs.name = backendGs.name;
            gs.latitude = backendGs.latitude;
            gs.longitude = backendGs.longitude;
            gs.altitude = backendGs.altitude * KmToM;
            gs.coverageRadius = (float)(backendGs.max_range * KmToM);
            gs.type = backendGs.is_active ? "main" : "backup";

            return gs;
        }

        /// <summary>
        /// 将后端任务数据转换为前端模型
        /// </summary>
        private Core.Task ConvertBackendTask(BackendTask backendTask)
        {
            var task = new Core.Task();
            string taskId = backendTask?.id ?? "";

            if (int.TryParse(taskId.Replace("task_", ""), out int intId))
                task.id = intId;
            else
                task.id = taskId.GetHashCode();

            task.name = $"Task-{taskId}";
            task.priority = backendTask.priority;

            // 将截止时间字符串转换为秒数（简化处理）
            if (TryParseUtcDateTime(backendTask.arrival_time, out DateTime arrivalUtc) &&
                TryParseUtcDateTime(backendTask.deadline, out DateTime deadlineUtc))
            {
                task.deadline = Mathf.Max(0f, (float)(deadlineUtc - arrivalUtc).TotalSeconds);
            }
            else if (TryParseUtcDateTime(backendTask.deadline, out DateTime fallbackDeadlineUtc))
            {
                task.deadline = Mathf.Max(0f, (float)(fallbackDeadlineUtc - DateTime.UtcNow).TotalSeconds);
            }
            else
            {
                task.deadline = 3600f;
            }

            task.computation = backendTask.size;
            task.dataSize = backendTask.input_data_size + backendTask.output_data_size;
            task.status = NormalizeTaskStatus(backendTask.status);

            if (int.TryParse(backendTask.assigned_satellite?.Replace("sat_", ""), out int satId))
                task.assignedSatelliteId = satId;
            else
                task.assignedSatelliteId = 0;

            task.progress = task.status switch
            {
                "completed" => 1f,
                "running" => 0.5f,
                "failed" => 1f,
                _ => 0f
            };

            return task;
        }

        private string NormalizeTaskStatus(string status)
        {
            if (string.IsNullOrWhiteSpace(status))
                return "pending";

            return status.ToLower() switch
            {
                "processing" => "running",
                "timeout" => "failed",
                _ => status.ToLower()
            };
        }

        private bool TryParseUtcDateTime(string value, out DateTime parsedUtc)
        {
            if (DateTimeOffset.TryParse(
                value,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset parsed))
            {
                parsedUtc = parsed.UtcDateTime;
                return true;
            }

            parsedUtc = default;
            return false;
        }
        #endregion

        #region 模拟数据生成（用于测试）
        public List<Core.Satellite> GetMockSatellites()
        {
            var satellites = new List<Core.Satellite>();
            int count = 15;

            for (int i = 0; i < count; i++)
            {
                satellites.Add(new Core.Satellite
                {
                    id = (i+1),
                    name = $"SAT-{i + 1:000}",
                    latitude = UnityEngine.Random.Range(-60f, 60f),
                    longitude = UnityEngine.Random.Range(-180f, 180f),
                    altitude = UnityEngine.Random.Range(500000f, 600000f),
                    capacity = UnityEngine.Random.Range(50f, 150f),
                    power = UnityEngine.Random.Range(30f, 100f),
                    taskCount = UnityEngine.Random.Range(0, 10),
                    status = "idle"
                });
            }

            return satellites;
        }

        public List<Core.GroundStation> GetMockGroundStations()
        {
            var stations = new List<Core.GroundStation>();

            stations.Add(new Core.GroundStation
            {
                id = 1,
                name = "Beijing Station",
                latitude = 39.9,
                longitude = 116.4,
                altitude = 44,
                coverageRadius = 1000000,
                type = "main"
            });

            stations.Add(new Core.GroundStation
            {
                id = 2,
                name = "Shanghai Station",
                latitude = 31.2,
                longitude = 121.5,
                altitude = 20,
                coverageRadius = 800000,
                type = "main"
            });

            stations.Add(new Core.GroundStation
            {
                id = 3,
                name = "Urumqi Station",
                latitude = 43.8,
                longitude = 87.6,
                altitude = 850,
                coverageRadius = 1200000,
                type = "backup"
            });

            return stations;
        }

        public List<Core.Task> GetMockTasks()
        {
            var tasks = new List<Core.Task>();
            int count = 5;

            for (int i = 0; i < count; i++)
            {
                tasks.Add(new Core.Task
                {
                    id = (i + 1),
                    name = $"Task-{i + 1:000}",
                    priority = UnityEngine.Random.Range(1, 6),
                    deadline = UnityEngine.Random.Range(60f, 600f),
                    computation = UnityEngine.Random.Range(10f, 100f),
                    dataSize = UnityEngine.Random.Range(1f, 50f),
                    status = UnityEngine.Random.Range(0, 3) switch
                    {
                        0 => "pending",
                        1 => "assigned",
                        _ => "running"
                    },
                    assignedSatelliteId = (UnityEngine.Random.Range(1, 16)),
                    progress = UnityEngine.Random.Range(0f, 1f)
                });
            }

            return tasks;
        }
        #endregion
    }
}




