using System;
using System.Collections.Generic;
using UnityEngine;

namespace SatelliteEdgeComputing.Core
{
    /// <summary>
    /// 卫星数据结构
    /// </summary>
    [Serializable]
    public class Satellite
    {
        public int id;
        public string name;
        public double latitude;     // 纬度（度）
        public double longitude;    // 经度（度）
        public double altitude;     // 高度（米）
        public float capacity;      // 计算容量
        public float power;         // 剩余电量
        public float storage;       // 存储容量
        public int taskCount;       // 当前任务数
        public string status;       // 状态：idle, busy, overloaded

        // 计算负载率（0-1）
        public float LoadRate => taskCount / Mathf.Max(1, capacity);

        // 位置转换为Unity世界坐标
        public Vector3 GetWorldPosition(float earthRadius = 6378135f)
        {
            return CoordinateConverter.GeodeticToWorld(latitude, longitude, altitude, earthRadius);
        }

        // 获取状态颜色
        public Color GetStatusColor()
        {
            float load = LoadRate;
            if (load < 0.3f) return Color.green;
            if (load < 0.7f) return Color.yellow;
            return Color.red;
        }
    }

    /// <summary>
    /// 地面站数据结构
    /// </summary>
    [Serializable]
    public class GroundStation
    {
        public int id;
        public string name;
        public double latitude;      // 纬度（度）
        public double longitude;     // 经度（度）
        public double altitude;      // 海拔高度（米）
        public float coverageRadius; // 覆盖半径（米）
        public string type;         // 类型：main, backup

        // 位置转换为Unity世界坐标
        public Vector3 GetWorldPosition(float earthRadius = 6378135f)
        {
            return CoordinateConverter.GeodeticToWorld(latitude, longitude, altitude, earthRadius);
        }
    }

    /// <summary>
    /// 计算任务数据结构
    /// </summary>
    [Serializable]
    public class Task
    {
        public int id;
        public string name;
        public int priority;        // 优先级（1-5）
        public float deadline;      // 截止时间（秒）
        public float computation;   // 计算需求
        public float dataSize;      // 数据大小（MB）
        public string status;       // 状态：pending, assigned, processing, completed
        public int assignedSatelliteId; // 分配的卫星ID
        public float progress;      // 进度（0-1）

        // 是否已分配
        public bool IsAssigned => !string.IsNullOrEmpty(status) && status != "pending";
    }

    /// <summary>
    /// 仿真配置
    /// </summary>
    [Serializable]
    public class SimulationConfig
    {
        public string algorithm= "fcfs";    // 调度算法：fcfs, sjf, edd, max_visibility
        public float timeScale = 1.0f; // 时间缩放因子
        public int maxTasks = 10;  // 最大任务数
        public bool showOrbits = true; // 显示轨道
        public bool showLinks = true; // 显示通信链路
    }

    /// <summary>
    /// 坐标转换工具
    /// </summary>
    public static class CoordinateConverter
    {
        /// <summary>
        /// 将大地坐标（纬度、经度、高度）转换为Unity世界坐标
        /// </summary>
        /// <param name="lat">纬度（度）</param>
        /// <param name="lon">经度（度）</param>
        /// <param name="alt">高度（米）</param>
        /// <param name="earthRadius">地球半径（Unity单位）</param>
        /// <returns>Unity世界坐标</returns>
        public static Vector3 GeodeticToWorld(double lat, double lon, double alt, float earthRadius = 6378135f)
        {
            // 将角度转换为弧度
            double latRad = lat * Mathf.Deg2Rad;
            double lonRad = lon * Mathf.Deg2Rad;

            // 计算球面坐标
            double radius = earthRadius + alt;
            double x = radius * Math.Cos(latRad) * Math.Cos(lonRad);
            double z = radius * Math.Cos(latRad) * Math.Sin(lonRad);
            double y = radius * Math.Sin(latRad);

            return new Vector3((float)x, (float)y, (float)z);
        }

        /// <summary>
        /// 计算两点之间的距离（米）
        /// </summary>
        public static float Distance(Vector3 a, Vector3 b, float earthRadius = 6378135f)
        {
            // 将Unity坐标转换回球面角度计算距离
            // 简化：使用直线距离近似
            return Vector3.Distance(a, b);
        }
    }
}
