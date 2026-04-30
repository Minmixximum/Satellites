using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SatelliteEdgeComputing.Visualization
{
    /// <summary>
    /// 卫星可视化器
    /// </summary>
    public class SatelliteVisualizer : MonoBehaviour
    {
        [Header("卫星设置")]
        [SerializeField] private GameObject satellitePrefab;
        [SerializeField] private float satelliteScale = 10000f;
        [SerializeField] private bool showLabels = true;
        [SerializeField] private bool showStatusIndicators = true;
        [SerializeField] private Color idleColor = Color.green;
        [SerializeField] private Color busyColor = Color.yellow;
        [SerializeField] private Color overloadedColor = Color.red;

        [Header("标签设置")]
        [SerializeField] private GameObject labelPrefab;
        [SerializeField] private float labelOffset = 20000f;
        [SerializeField] private Font labelFont;
        [SerializeField] private int labelFontSize = 14;
        [SerializeField] private Color labelColor = Color.white;

        [Header("状态指示器")]
        [SerializeField] private GameObject statusIndicatorPrefab;
        [SerializeField] private float indicatorScale = 2000f;

        [Header("轨道设置")]
        [SerializeField] private bool showOrbit = true;
        [SerializeField] private Material orbitMaterial;
        [SerializeField] private float orbitWidth = 0.5f;
        [SerializeField] private Color orbitColor = new Color(0.3f, 0.6f, 1f, 0.5f);
        [SerializeField] private int orbitSegments = 60;

        // 卫星实例字典
        private Dictionary<int, SatelliteInstance> satelliteInstances = new Dictionary<int, SatelliteInstance>();
        private EarthRenderer earthRenderer;
        private Transform canvasTransform;

        /// <summary>
        /// 卫星实例封装
        /// </summary>
        private class SatelliteInstance
        {
            public Core.Satellite data;
            public GameObject gameObject;
            public Renderer renderer;
            public GameObject label;
            public RectTransform labelRect;
            public Text labelText;
            public GameObject statusIndicator;
            public LineRenderer orbitLine;
            public List<Vector3> orbitPoints = new List<Vector3>();
            public Vector3 worldPosition;

            public void UpdateVisualization(EarthRenderer earthRenderer, float scale,
                Color idleColor, Color busyColor, Color overloadedColor)
            {
                if (gameObject == null || earthRenderer == null) return;

                // 更新位置
                worldPosition = data.GetWorldPosition(earthRenderer.GetEarthRadius());
                gameObject.transform.position = worldPosition;

                // 朝向地球中心
                gameObject.transform.LookAt(Vector3.zero);
                gameObject.transform.Rotate(90, 0, 0); // 调整方向

                // 更新颜色
                if (renderer != null)
                {
                    Color statusColor = data.GetStatusColor();
                    if (statusColor == Color.green) statusColor = idleColor;
                    else if (statusColor == Color.yellow) statusColor = busyColor;
                    else statusColor = overloadedColor;

                    renderer.material.color = statusColor;
                }

                // 更新标签文本
                if (label != null && labelText != null)
                {
                    labelText.text = $"{data.name}\n负载: {data.LoadRate:P0}\n任务: {data.taskCount}";
                }

                // 更新轨道点
                orbitPoints.Add(worldPosition);
                if (orbitPoints.Count > 100) // 保留最近100个点
                {
                    orbitPoints.RemoveAt(0);
                }
            }

            public void UpdateOrbitLine(bool showOrbit, Material material, float width, Color color)
            {
                if (orbitLine == null) return;

                orbitLine.enabled = showOrbit && orbitPoints.Count > 1;
                if (!orbitLine.enabled) return;

                orbitLine.positionCount = orbitPoints.Count;
                orbitLine.SetPositions(orbitPoints.ToArray());
                orbitLine.startColor = color;
                orbitLine.endColor = color;
                orbitLine.startWidth = width;
                orbitLine.endWidth = width;
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(EarthRenderer earthRenderer, Transform canvasTransform)
        {
            this.earthRenderer = earthRenderer;
            this.canvasTransform = canvasTransform;

            // 创建默认卫星预制体（如果未提供）
            if (satellitePrefab == null)
            {
                satellitePrefab = CreateDefaultSatellitePrefab();
            }

            // 创建默认标签预制体（如果未提供）
            if (labelPrefab == null && showLabels)
            {
                labelPrefab = CreateDefaultLabelPrefab();
            }

            // 创建默认状态指示器预制体（如果未提供）
            if (statusIndicatorPrefab == null && showStatusIndicators)
            {
                statusIndicatorPrefab = CreateDefaultStatusIndicatorPrefab();
            }

            Debug.Log("Satellite visualizer initialized.");
        }

        /// <summary>
        /// 更新卫星可视化
        /// </summary>
        public void UpdateSatellites(List<Core.Satellite> satellites)
        {
            Debug.Log($"[SatelliteVisualizer] UpdateSatellites被调用, 卫星数量={satellites?.Count ?? 0}, earthRenderer={(earthRenderer != null ? "有效" : "null")}");

            if (satellites == null || satellites.Count == 0)
            {
                Debug.LogWarning("[SatelliteVisualizer] UpdateSatellites: 卫星列表为空");
                return;
            }

            // 移除不存在的卫星
            List<int> toRemove = new List<int>();
            foreach (var kvp in satelliteInstances)
            {
                if (!satellites.Exists(s => s.id == kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (int id in toRemove)
            {
                DestroySatelliteInstance(id);
            }

            // 更新或创建卫星实例
            int updated = 0, created = 0;
            foreach (var satellite in satellites)
            {
                if (satelliteInstances.ContainsKey(satellite.id))
                {
                    // 更新现有实例
                    var instance = satelliteInstances[satellite.id];
                    instance.data = satellite;
                    instance.UpdateVisualization(earthRenderer, satelliteScale,
                        idleColor, busyColor, overloadedColor);
                    instance.UpdateOrbitLine(showOrbit, orbitMaterial, orbitWidth, orbitColor);
                    updated++;
                }
                else
                {
                    // 创建新实例
                    CreateSatelliteInstance(satellite);
                    created++;
                }
            }
            Debug.Log($"[SatelliteVisualizer] UpdateSatellites完成: 更新{updated}个, 新建{created}个");
        }

        /// <summary>
        /// 创建卫星实例
        /// </summary>
        private void CreateSatelliteInstance(Core.Satellite satellite)
        {
            if (earthRenderer == null)
            {
                Debug.LogWarning("[SatelliteVisualizer] CreateSatelliteInstance: EarthRenderer is null, cannot create satellite instance.");
                return;
            }

            Debug.Log($"[SatelliteVisualizer] CreateSatelliteInstance: 创建卫星 {satellite.name}, prefab={(satellitePrefab != null ? "有效" : "null")}");

            // 创建卫星游戏对象
            GameObject satelliteObj = Instantiate(satellitePrefab, transform);
            satelliteObj.name = $"Satellite_{satellite.id}_{satellite.name}";
            satelliteObj.SetActive(true);  // 激活对象（预制体默认禁用）

            // 设置位置
            float earthRadius = earthRenderer.GetEarthRadius();
            Vector3 position = satellite.GetWorldPosition(earthRadius);
            satelliteObj.transform.position = position;
            satelliteObj.transform.localScale = Vector3.one * satelliteScale;

            // 调试信息
            Debug.Log($"[SatelliteVisualizer] 卫星 {satellite.name}: 地球半径={earthRadius}, 卫星高度={satellite.altitude}, 位置={position}, 缩放={satelliteScale}");

#if UNITY_EDITOR && DEBUG_SATELLITE_POSITION
            // 临时调试：创建一个大的红色球体来验证位置
            GameObject debugSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            debugSphere.name = $"Debug_{satellite.name}";
            debugSphere.transform.position = position;
            debugSphere.transform.localScale = Vector3.one * 500000f; // 更大的缩放以便可见
            var debugRenderer = debugSphere.GetComponent<Renderer>();
            if (debugRenderer != null)
            {
                debugRenderer.material.color = Color.red;
            }
            Debug.Log($"[SatelliteVisualizer] 创建调试球体在位置 {position}, 缩放 500000");
#endif

            // 获取渲染器
            Renderer renderer = satelliteObj.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = satelliteObj.GetComponentInChildren<Renderer>();
            }

            // 创建标签
            GameObject label = null;
            RectTransform labelRect = null;
            Text labelText = null;
            if (showLabels && labelPrefab != null && canvasTransform != null)
            {
                label = Instantiate(labelPrefab, canvasTransform);
                labelRect = label.GetComponent<RectTransform>();
                if (labelRect == null)
                {
                    labelRect = label.AddComponent<RectTransform>();
                }
                labelText = label.GetComponentInChildren<Text>();
                if (labelText != null)
                {
                    labelText.text = satellite.name;
                    labelText.font = labelFont;
                    labelText.fontSize = labelFontSize;
                    labelText.color = labelColor;
                }
            }

            // 创建状态指示器
            GameObject statusIndicator = null;
            if (showStatusIndicators && statusIndicatorPrefab != null && canvasTransform != null)
            {
                statusIndicator = Instantiate(statusIndicatorPrefab, canvasTransform);
                var indicatorRect = statusIndicator.GetComponent<RectTransform>();
                if (indicatorRect == null)
                {
                    statusIndicator.AddComponent<RectTransform>();
                }
            }

            // 创建轨道线
            LineRenderer orbitLine = null;
            if (showOrbit)
            {
                GameObject orbitObj = new GameObject($"Orbit_{satellite.id}");
                orbitObj.transform.SetParent(transform);
                orbitLine = orbitObj.AddComponent<LineRenderer>();

                if (orbitMaterial != null)
                {
                    orbitLine.material = orbitMaterial;
                }
                else
                {
                    orbitLine.material = new Material(Shader.Find("Sprites/Default"));
                }

                orbitLine.startColor = orbitColor;
                orbitLine.endColor = orbitColor;
                orbitLine.startWidth = orbitWidth;
                orbitLine.endWidth = orbitWidth;
                orbitLine.useWorldSpace = true;
                orbitLine.positionCount = 0;
            }

            // 创建实例
            var instance = new SatelliteInstance
            {
                data = satellite,
                gameObject = satelliteObj,
                renderer = renderer,
                label = label,
                labelRect = labelRect,
                labelText = labelText,
                statusIndicator = statusIndicator,
                orbitLine = orbitLine,
                worldPosition = position
            };

            satelliteInstances[satellite.id] = instance;
            instance.UpdateVisualization(earthRenderer, satelliteScale, idleColor, busyColor, overloadedColor);
        }

        /// <summary>
        /// 销毁卫星实例
        /// </summary>
        private void DestroySatelliteInstance(int satelliteId)
        {
            if (satelliteInstances.TryGetValue(satelliteId, out var instance))
            {
                if (instance.gameObject != null)
                    Destroy(instance.gameObject);
                if (instance.label != null)
                    Destroy(instance.label);
                if (instance.statusIndicator != null)
                    Destroy(instance.statusIndicator);
                if (instance.orbitLine != null && instance.orbitLine.gameObject != null)
                    Destroy(instance.orbitLine.gameObject);

                satelliteInstances.Remove(satelliteId);
            }
        }

        /// <summary>
        /// 创建默认卫星预制体
        /// </summary>
        private GameObject CreateDefaultSatellitePrefab()
        {
            GameObject prefab = new GameObject("DefaultSatellitePrefab");
            prefab.SetActive(false);

            // 主体（长方体）
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(prefab.transform);
            body.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            body.transform.localPosition = Vector3.zero;

            // 太阳能板（两个扁平的长方体）
            GameObject solarPanel1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solarPanel1.transform.SetParent(prefab.transform);
            solarPanel1.transform.localScale = new Vector3(2f, 0.1f, 1f);
            solarPanel1.transform.localPosition = new Vector3(1f, 0, 0);

            GameObject solarPanel2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solarPanel2.transform.SetParent(prefab.transform);
            solarPanel2.transform.localScale = new Vector3(2f, 0.1f, 1f);
            solarPanel2.transform.localPosition = new Vector3(-1f, 0, 0);

            // 天线（圆柱体）
            GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antenna.transform.SetParent(prefab.transform);
            antenna.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
            antenna.transform.localPosition = new Vector3(0, 0.5f, 0);
            antenna.transform.Rotate(90, 0, 0);

            // 设置材质
            var material = new Material(Shader.Find("Standard"));
            material.color = Color.gray;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
            {
                renderer.material = material;
            }

            return prefab;
        }

        /// <summary>
        /// 创建默认标签预制体
        /// </summary>
        private GameObject CreateDefaultLabelPrefab()
        {
            GameObject prefab = new GameObject("LabelPrefab");

            // 根对象需要 RectTransform
            RectTransform rootRect = prefab.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(200, 50);
            rootRect.pivot = new Vector2(0.5f, 0f); // 锚点在底部中心

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(prefab.transform);

            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;
            rectTransform.anchoredPosition = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return prefab;
        }

        /// <summary>
        /// 创建默认状态指示器预制体
        /// </summary>
        private GameObject CreateDefaultStatusIndicatorPrefab()
        {
            GameObject prefab = new GameObject("StatusIndicatorPrefab");

            // 根对象需要 RectTransform
            RectTransform rootRect = prefab.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(20, 20);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            // 使用 UI Image 而不是 3D Quad
            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(prefab.transform, false);

            RectTransform iconRect = icon.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero;

            UnityEngine.UI.Image image = icon.AddComponent<UnityEngine.UI.Image>();
            image.color = Color.white;

            return prefab;
        }

        /// <summary>
        /// 显示/隐藏标签
        /// </summary>
        public void SetLabelsVisible(bool visible)
        {
            showLabels = visible;
            foreach (var instance in satelliteInstances.Values)
            {
                if (instance.label != null)
                {
                    instance.label.SetActive(visible);
                }
            }
        }

        /// <summary>
        /// 显示/隐藏状态指示器
        /// </summary>
        public void SetStatusIndicatorsVisible(bool visible)
        {
            showStatusIndicators = visible;
            foreach (var instance in satelliteInstances.Values)
            {
                if (instance.statusIndicator != null)
                {
                    instance.statusIndicator.SetActive(visible);
                }
            }
        }

        /// <summary>
        /// 显示/隐藏轨道线
        /// </summary>
        public void SetOrbitsVisible(bool visible)
        {
            showOrbit = visible;
            foreach (var instance in satelliteInstances.Values)
            {
                if (instance.orbitLine != null)
                {
                    instance.orbitLine.enabled = visible;
                }
            }
        }

        /// <summary>
        /// 清除所有卫星
        /// </summary>
        public void ClearAll()
        {
            foreach (var id in new List<int>(satelliteInstances.Keys))
            {
                DestroySatelliteInstance(id);
            }
            satelliteInstances.Clear();
        }

        void Update()
        {
            // 确保相机存在
            if (Camera.main == null || canvasTransform == null) return;

            Camera cam = Camera.main;
            RectTransform canvasRect = canvasTransform as RectTransform;

            // 更新标签位置（将世界坐标转换为屏幕坐标）
            foreach (var instance in satelliteInstances.Values)
            {
                if (instance.label != null && instance.labelRect != null)
                {
                    // 将世界坐标转换为屏幕坐标
                    Vector3 screenPos = cam.WorldToScreenPoint(instance.worldPosition);

                    // 检查是否在相机前方（z > 0）
                    if (screenPos.z > 0 && screenPos.x >= 0 && screenPos.x <= Screen.width &&
                        screenPos.y >= 0 && screenPos.y <= Screen.height)
                    {
                        instance.label.SetActive(true);

                        // 将屏幕坐标转换为 Canvas 局部坐标
                        Vector2 localPos;
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                            canvasRect, screenPos, null, out localPos))
                        {
                            instance.labelRect.anchoredPosition = localPos + Vector2.up * 30f;
                        }
                    }
                    else
                    {
                        // 在相机背后或屏幕外时隐藏标签
                        instance.label.SetActive(false);
                    }
                }

                if (instance.statusIndicator != null)
                {
                    Vector3 screenPos = cam.WorldToScreenPoint(instance.worldPosition);
                    if (screenPos.z > 0 && screenPos.x >= 0 && screenPos.x <= Screen.width &&
                        screenPos.y >= 0 && screenPos.y <= Screen.height)
                    {
                        instance.statusIndicator.SetActive(true);
                        var rect = instance.statusIndicator.GetComponent<RectTransform>();
                        if (rect != null && canvasRect != null)
                        {
                            Vector2 localPos;
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                                canvasRect, screenPos, null, out localPos))
                            {
                                rect.anchoredPosition = localPos + Vector2.up * 50f;
                            }
                        }
                    }
                    else
                    {
                        instance.statusIndicator.SetActive(false);
                    }
                }
            }
        }

        void OnDestroy()
        {
            ClearAll();
        }
    }
}
