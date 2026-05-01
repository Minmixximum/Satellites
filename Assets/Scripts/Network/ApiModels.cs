using System;
using System.Collections.Generic;

namespace SatelliteEdgeComputing.Network
{
    /// <summary>
    /// API响应基础类
    /// </summary>
    [Serializable]
    public class ApiResponse<T>
    {
        public bool success;
        public string message;
        public string error;
        public T data;
        public long timestamp;
    }

    [Serializable]
    public class BasicApiResponse
    {
        public bool success;
        public string message;
        public string error;
        public long timestamp;
    }

    /// <summary>
    /// 卫星列表响应
    /// </summary>
    [Serializable]
    public class SatellitesResponse
    {
        public List<Core.Satellite> satellites;
    }

    /// <summary>
    /// 地面站列表响应
    /// </summary>
    [Serializable]
    public class GroundStationsResponse
    {
        public List<Core.GroundStation> ground_stations;
    }

    /// <summary>
    /// 任务列表响应
    /// </summary>
    [Serializable]
    public class TasksResponse
    {
        public List<Core.Task> tasks;
    }

    /// <summary>
    /// 仿真状态响应
    /// </summary>
    [Serializable]
    public class SimulationStatusResponse
    {
        public bool success;
        public string message;
        public string error;
        public SimulationStatusData data;
    }

    [Serializable]
    public class SimulationStatusData
    {
        public bool is_running;
        public bool is_paused;
        public string current_time;
        public string start_time;
        public float time_speed;
        public string active_algorithm;
    }

    /// <summary>
    /// 启动仿真请求
    /// </summary>
    [Serializable]
    public class StartSimulationRequest
    {
        public string algorithm;
        public int max_tasks;
        public float speed_factor;
        public float time_scale;
        public float time_speed;
    }

    /// <summary>
    /// 设置算法请求
    /// </summary>
    [Serializable]
    public class SetAlgorithmRequest
    {
        public string algorithm;
    }

    /// <summary>
    /// 健康检查响应
    /// </summary>
    [Serializable]
    public class HealthResponse
    {
        public bool success;
        public string message;
        public string error;
        public string timestamp;
    }

    [Serializable]
    public class BackendSatelliteListResponse
    {
        public bool success;
        public string message;
        public string error;
        public BackendSatellite[] data;
        public int count;
    }

    [Serializable]
    public class BackendGroundStationListResponse
    {
        public bool success;
        public string message;
        public string error;
        public BackendGroundStation[] data;
        public int count;
    }

    [Serializable]
    public class BackendTaskListResponse
    {
        public bool success;
        public string message;
        public string error;
        public BackendTask[] data;
        public int count;
    }

    [Serializable]
    public class BackendTaskListAltResponse
    {
        public bool success;
        public string message;
        public string error;
        public BackendTask[] tasks;
        public int count;
    }

    [Serializable]
    public class BackendTaskListNestedResponse
    {
        public bool success;
        public string message;
        public string error;
        public BackendTaskDataContainer data;
    }

    [Serializable]
    public class BackendTaskDataContainer
    {
        public BackendTask[] tasks;
        public int count;
    }

    /// <summary>
    /// 场景初始化请求
    /// </summary>
    [Serializable]
    public class InitializeScenarioRequest
    {
        // 可留空，使用默认配置
    }

    /// <summary>
    /// 后端卫星数据模型（用于反序列化）
    /// </summary>
    [Serializable]
    public class BackendSatellite
    {
        public string id;
        public string name;
        public string tle_line1;
        public string tle_line2;
        public float capacity;
        public float storage;
        public float max_power;
        public SatellitePosition position;
        public float current_power;
        public float current_load;
        public bool is_visible;
        public int task_queue_length;
        public int completed_tasks;
        public int failed_tasks;
        public string last_update;
    }

    [Serializable]
    public class SatellitePosition
    {
        public double lat;
        public double lon;
        public double alt;
    }

    /// <summary>
    /// 后端地面站数据模型
    /// </summary>
    [Serializable]
    public class BackendGroundStation
    {
        public string id;
        public string name;
        public double latitude;
        public double longitude;
        public double altitude;
        public double min_elevation;
        public double max_range;
        public double communication_speed;
        public List<string> connected_satellites;
        public bool is_active;
    }

    /// <summary>
    /// 后端任务数据模型
    /// </summary>
    [Serializable]
    public class BackendTask
    {
        public string id;
        public float size;
        public int priority;
        public string deadline;
        public string arrival_time;
        public string status;
        public string assigned_satellite;
        public string actual_start;
        public string actual_end;
        public float progress = -1f;
        public double source_lat;
        public double source_lon;
        public string task_type;
        public float input_data_size;
        public float output_data_size;
    }

    /// <summary>
    /// 模拟时间信息响应
    /// </summary>
    [Serializable]
    public class TimeInfoResponse
    {
        public bool success;
        public string message;
        public string error;
        public TimeInfoData data;
    }

    [Serializable]
    public class TimeInfoData
    {
        public string sim_time;
        public string start_time;
        public float speed_factor;
        public double earth_rotation_angle_rad;
        public double earth_rotation_angle_deg;
        public bool is_running;
        public bool is_paused;
    }

    /// <summary>
    /// 设置速度因子请求
    /// </summary>
    [Serializable]
    public class SetSpeedFactorRequest
    {
        public float speed_factor;
    }

    /// <summary>
    /// 设置速度因子响应
    /// </summary>
    [Serializable]
    public class SetSpeedFactorResponse
    {
        public bool success;
        public string message;
        public string error;
        public SpeedFactorData data;
    }

    [Serializable]
    public class SpeedFactorData
    {
        public float speed_factor;
        public string sim_time;
    }
}
