using System.Collections.Generic;
using UnityEngine;

namespace SatelliteEdgeComputing.Visualization
{
    /// <summary>
    /// 通信链路可视化器
    /// </summary>
    public class LinkVisualizer : MonoBehaviour
    {
        [Header("链路设置")]
        [SerializeField] private bool showLinks = true;
        [SerializeField] private Material linkMaterial;
        [SerializeField] private float linkWidth = 200f;
        [SerializeField] private Color activeLinkColor = new Color(0, 1, 0, 0.8f); // 绿色
        [SerializeField] private Color inactiveLinkColor = new Color(1, 0, 0, 0.3f); // 红色
        [SerializeField] private Color potentialLinkColor = new Color(1, 1, 0, 0.2f); // 黄色
        [SerializeField] private float maxLinkDistance = 2000000f; // 最大通信距离（米）

        [Header("粒子效果")]
        [SerializeField] private bool useParticles = true;
        [SerializeField] private ParticleSystem linkParticlePrefab;
        [SerializeField] private float particleSpeed = 10f;
        [SerializeField] private float particleEmissionRate = 5f;

        [Header("性能优化")]
        [SerializeField] private int maxLinks = 50;
        [SerializeField] private float updateInterval = 0.5f; // 更新间隔（秒）

        // 链路实例字典
        private Dictionary<string, LinkInstance> linkInstances = new Dictionary<string, LinkInstance>();
        private EarthRenderer earthRenderer;
        private float lastUpdateTime = 0f;

        /// <summary>
        /// 链路实例封装
        /// </summary>
        private class LinkInstance
        {
            public string linkId;
            public int satelliteId;
            public int groundStationId;
            public bool isActive;
            public bool isPotential;
            public float strength; // 信号强度 0-1
            public GameObject gameObject;
            public LineRenderer lineRenderer;
            public ParticleSystem particleSystem;

            public void UpdateVisualization(Vector3 satellitePos, Vector3 groundStationPos,
                bool showLinks, float width,
                Color activeColor, Color inactiveColor, Color potentialColor,
                bool useParticles, float particleSpeed, float maxDistance)
            {
                if (gameObject == null || lineRenderer == null) return;

                lineRenderer.enabled = showLinks;
                if (!showLinks) return;

                // 设置位置
                lineRenderer.SetPosition(0, satellitePos);
                lineRenderer.SetPosition(1, groundStationPos);

                // 计算距离和信号强度
                float distance = Vector3.Distance(satellitePos, groundStationPos);
                strength = Mathf.Clamp01(1 - (distance / maxDistance));

                // 设置颜色和宽度
                Color linkColor;
                if (isActive)
                {
                    linkColor = Color.Lerp(inactiveColor, activeColor, strength);
                }
                else if (isPotential)
                {
                    linkColor = Color.Lerp(inactiveColor, potentialColor, strength * 0.5f);
                }
                else
                {
                    linkColor = inactiveColor;
                }

                lineRenderer.startColor = linkColor;
                lineRenderer.endColor = linkColor;
                lineRenderer.startWidth = width * strength;
                lineRenderer.endWidth = width * strength * 0.5f;

                // 更新粒子系统
                if (particleSystem != null)
                {
                    particleSystem.gameObject.SetActive(useParticles && isActive && strength > 0.3f);

                    if (particleSystem.isPlaying)
                    {
                        // 设置粒子方向
                        var shape = particleSystem.shape;
                        shape.position = satellitePos;
                        shape.rotation = Quaternion.LookRotation(groundStationPos - satellitePos).eulerAngles;

                        // 设置粒子速度
                        var main = particleSystem.main;
                        main.startSpeed = particleSpeed * strength;

                        // 设置粒子颜色
                        var colorOverLifetime = particleSystem.colorOverLifetime;
                        Gradient gradient = new Gradient();
                        gradient.SetKeys(
                            new GradientColorKey[] { new GradientColorKey(activeColor, 0f), new GradientColorKey(activeColor, 1f) },
                            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
                        );
                        colorOverLifetime.color = gradient;
                    }
                }
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(EarthRenderer earthRenderer)
        {
            this.earthRenderer = earthRenderer;

            // 创建默认粒子系统预制体（如果未提供）
            if (linkParticlePrefab == null && useParticles)
            {
                linkParticlePrefab = CreateDefaultParticlePrefab();
            }

            Debug.Log("Link visualizer initialized.");
        }

        /// <summary>
        /// 更新通信链路
        /// </summary>
        public void UpdateLinks(List<Core.Satellite> satellites, List<Core.GroundStation> groundStations)
        {
            if (earthRenderer == null) return;

            // 限制更新频率
            if (Time.time - lastUpdateTime < updateInterval)
                return;

            lastUpdateTime = Time.time;

            // 清理不存在的链路
            List<string> toRemove = new List<string>();
            foreach (var kvp in linkInstances)
            {
                bool satelliteExists = satellites.Exists(s => s.id == kvp.Value.satelliteId);
                bool stationExists = groundStations.Exists(g => g.id == kvp.Value.groundStationId);

                if (!satelliteExists || !stationExists)
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (string linkId in toRemove)
            {
                DestroyLinkInstance(linkId);
            }

            // 限制链路数量
            if (linkInstances.Count >= maxLinks)
                return;

            // 计算新的链路
            foreach (var satellite in satellites)
            {
                foreach (var station in groundStations)
                {
                    string linkId = $"{satellite.id}_{station.id}";

                    // 如果链路已存在，更新它
                    if (linkInstances.ContainsKey(linkId))
                    {
                        UpdateLinkInstance(linkId, satellite, station);
                    }
                    else if (linkInstances.Count < maxLinks) // 创建新链路
                    {
                        CreateLinkInstance(linkId, satellite, station);
                    }
                }
            }

            // 更新所有链路的可视化
            float earthRadius = earthRenderer.GetEarthRadius();
            foreach (var kvp in linkInstances)
            {
                var instance = kvp.Value;
                var satellite = satellites.Find(s => s.id == instance.satelliteId);
                var station = groundStations.Find(g => g.id == instance.groundStationId);

                if (satellite != null && station != null)
                {
                    Vector3 satPos = satellite.GetWorldPosition(earthRadius);
                    Vector3 stationPos = station.GetWorldPosition(earthRadius);

                    instance.UpdateVisualization(satPos, stationPos,
                        showLinks, linkWidth,
                        activeLinkColor, inactiveLinkColor, potentialLinkColor,
                        useParticles, particleSpeed, maxLinkDistance);
                }
            }
        }

        /// <summary>
        /// 创建链路实例
        /// </summary>
        private void CreateLinkInstance(string linkId, Core.Satellite satellite, Core.GroundStation station)
        {
            if (earthRenderer == null) return;

            // 计算链路状态
            bool isActive = CheckLinkActive(satellite, station);
            bool isPotential = CheckLinkPotential(satellite, station);

            // 创建游戏对象
            GameObject linkObj = new GameObject($"Link_{linkId}");
            linkObj.transform.SetParent(transform);

            // 创建LineRenderer
            LineRenderer lineRenderer = linkObj.AddComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;

            if (linkMaterial != null)
            {
                lineRenderer.material = linkMaterial;
            }
            else
            {
                lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            }

            // 创建粒子系统（如果需要）
            ParticleSystem particleSystem = null;
            if (useParticles && linkParticlePrefab != null)
            {
                particleSystem = Instantiate(linkParticlePrefab, linkObj.transform);
                particleSystem.transform.position = satellite.GetWorldPosition(earthRenderer.GetEarthRadius());

                // 配置粒子系统
                var main = particleSystem.main;
                main.startSpeed = particleSpeed;
                main.startLifetime = 5f;

                var emission = particleSystem.emission;
                emission.rateOverTime = particleEmissionRate;

                var shape = particleSystem.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 5f;

                particleSystem.Stop();
                if (isActive)
                {
                    particleSystem.Play();
                }
            }

            // 创建实例
            var instance = new LinkInstance
            {
                linkId = linkId,
                satelliteId = satellite.id,
                groundStationId = station.id,
                isActive = isActive,
                isPotential = isPotential,
                strength = 1f,
                gameObject = linkObj,
                lineRenderer = lineRenderer,
                particleSystem = particleSystem
            };

            linkInstances[linkId] = instance;
        }

        /// <summary>
        /// 更新链路实例状态
        /// </summary>
        private void UpdateLinkInstance(string linkId, Core.Satellite satellite, Core.GroundStation station)
        {
            if (linkInstances.TryGetValue(linkId, out var instance))
            {
                instance.isActive = CheckLinkActive(satellite, station);
                instance.isPotential = CheckLinkPotential(satellite, station);

                // 控制粒子系统
                if (instance.particleSystem != null)
                {
                    if (instance.isActive && !instance.particleSystem.isPlaying)
                    {
                        instance.particleSystem.Play();
                    }
                    else if (!instance.isActive && instance.particleSystem.isPlaying)
                    {
                        instance.particleSystem.Stop();
                    }
                }
            }
        }

        /// <summary>
        /// 销毁链路实例
        /// </summary>
        private void DestroyLinkInstance(string linkId)
        {
            if (linkInstances.TryGetValue(linkId, out var instance))
            {
                if (instance.gameObject != null)
                    Destroy(instance.gameObject);

                linkInstances.Remove(linkId);
            }
        }

        /// <summary>
        /// 检查链路是否活跃
        /// </summary>
        private bool CheckLinkActive(Core.Satellite satellite, Core.GroundStation station)
        {
            if (earthRenderer == null) return false;

            // 简化：根据卫星负载和距离判断
            Vector3 satPos = satellite.GetWorldPosition(earthRenderer.GetEarthRadius());
            Vector3 stationPos = station.GetWorldPosition(earthRenderer.GetEarthRadius());
            float distance = Vector3.Distance(satPos, stationPos);

            return distance <= maxLinkDistance && satellite.LoadRate < 0.8f;
        }

        /// <summary>
        /// 检查链路是否潜在可用
        /// </summary>
        private bool CheckLinkPotential(Core.Satellite satellite, Core.GroundStation station)
        {
            if (earthRenderer == null) return false;

            Vector3 satPos = satellite.GetWorldPosition(earthRenderer.GetEarthRadius());
            Vector3 stationPos = station.GetWorldPosition(earthRenderer.GetEarthRadius());
            float distance = Vector3.Distance(satPos, stationPos);

            return distance <= maxLinkDistance * 1.2f && satellite.LoadRate < 0.9f;
        }

        /// <summary>
        /// 创建默认粒子系统预制体
        /// </summary>
        private ParticleSystem CreateDefaultParticlePrefab()
        {
            GameObject particleObj = new GameObject("LinkParticles");
            ParticleSystem particleSystem = particleObj.AddComponent<ParticleSystem>();

            // 设置主模块
            var main = particleSystem.main;
            main.startSpeed = particleSpeed;
            main.startLifetime = 3f;
            main.startSize = 0.1f;
            main.startColor = activeLinkColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;

            // 设置发射模块
            var emission = particleSystem.emission;
            emission.rateOverTime = particleEmissionRate;

            // 设置形状模块
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.1f;

            // 设置颜色随生命周期变化
            var colorOverLifetime = particleSystem.colorOverLifetime;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.green, 0f), new GradientColorKey(Color.yellow, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = gradient;

            // 设置渲染器
            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

            particleObj.SetActive(false);
            return particleSystem;
        }

        /// <summary>
        /// 显示/隐藏所有链路
        /// </summary>
        public void SetLinksVisible(bool visible)
        {
            showLinks = visible;
            foreach (var instance in linkInstances.Values)
            {
                if (instance.lineRenderer != null)
                {
                    instance.lineRenderer.enabled = visible;
                }
            }
        }

        /// <summary>
        /// 启用/禁用粒子效果
        /// </summary>
        public void SetParticlesEnabled(bool enabled)
        {
            useParticles = enabled;
            foreach (var instance in linkInstances.Values)
            {
                if (instance.particleSystem != null)
                {
                    instance.particleSystem.gameObject.SetActive(enabled && instance.isActive);
                }
            }
        }

        /// <summary>
        /// 设置最大通信距离
        /// </summary>
        public void SetMaxLinkDistance(float distance)
        {
            maxLinkDistance = Mathf.Max(1000f, distance);
        }

        /// <summary>
        /// 设置链路宽度
        /// </summary>
        public void SetLinkWidth(float width)
        {
            linkWidth = Mathf.Max(0.1f, width);
        }

        /// <summary>
        /// 清除所有链路
        /// </summary>
        public void ClearAll()
        {
            foreach (var linkId in new List<string>(linkInstances.Keys))
            {
                DestroyLinkInstance(linkId);
            }
            linkInstances.Clear();
        }

        void OnDestroy()
        {
            ClearAll();
        }
    }
}
