using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SatelliteEdgeComputing.Visualization
{
    /// <summary>
    /// 地面站可视化器
    /// </summary>
    public class GroundStationVisualizer : MonoBehaviour
    {
        [Header("地面站设置")]
        [SerializeField] private GameObject groundStationPrefab;
        [SerializeField] private float stationScale = 20000f;
        [SerializeField] private bool showCoverageArea = true;
        [SerializeField] private bool showLabels = true;
        [SerializeField] private Color mainStationColor = Color.green;
        [SerializeField] private Color backupStationColor = Color.yellow;

        [Header("覆盖区域设置")]
        [SerializeField] private Material coverageMaterial;
        [SerializeField] private Color coverageColor = new Color(0.2f, 0.8f, 0.2f, 0.1f);
        [SerializeField] private float coverageOpacity = 0.1f;

        [Header("标签设置")]
        [SerializeField] private GameObject labelPrefab;
        [SerializeField] private float labelOffset = 30000f;
        [SerializeField] private Font labelFont;
        [SerializeField] private int labelFontSize = 14;
        [SerializeField] private Color labelColor = Color.white;

        // 地面站实例字典
        private Dictionary<int, GroundStationInstance> stationInstances = new Dictionary<int, GroundStationInstance>();
        private EarthRenderer earthRenderer;
        private Transform canvasTransform;

        /// <summary>
        /// 地面站实例封装
        /// </summary>
        private class GroundStationInstance
        {
            public Core.GroundStation data;
            public GameObject gameObject;
            public Renderer renderer;
            public GameObject coverageArea;
            public GameObject label;
            public RectTransform labelRect;
            public Text labelText;
            public Vector3 worldPosition;

            public void UpdateVisualization(EarthRenderer earthRenderer, float scale,
                Color mainColor, Color backupColor, bool showCoverage, Material coverageMat,
                Color coverageCol, float coverageOpacity,float labelOffset)
            {
                if (gameObject == null || earthRenderer == null) return;

                // 更新位置（在地球表面）
                // 由于地面站是地球容器的子对象，使用localPosition以跟随地球旋转
                worldPosition = data.GetWorldPosition(earthRenderer.GetEarthRadius());
                gameObject.transform.localPosition = worldPosition;

                // 朝向地球中心（使用本地坐标系的反向）
                gameObject.transform.localRotation = Quaternion.LookRotation(-worldPosition.normalized, Vector3.up);
                gameObject.transform.Rotate(-90, 0, 0); // 调整方向使天线向上
                // 设置颜色
                if (renderer != null)
                {
                    Color stationColor = data.type == "main" ? mainColor : backupColor;
                    renderer.material.color = stationColor;
                }

                // 更新覆盖区域
                if (coverageArea != null)
                {
                    coverageArea.SetActive(showCoverage);
                    if (showCoverage)
                    {
                        // 覆盖区域是地面站的子对象，使用localPosition=zero保持在地面站位置
                        coverageArea.transform.localPosition = Vector3.zero;
                        coverageArea.transform.localScale = Vector3.one * data.coverageRadius * 2;

                        var coverageRenderer = coverageArea.GetComponent<Renderer>();
                        if (coverageRenderer != null)
                        {
                            coverageCol.a = coverageOpacity;
                            coverageRenderer.material.color = coverageCol;
                        }
                    }
                }

                // 更新标签文本
                if (label != null && labelText != null)
                {
                    labelText.text = $"{data.name}\n覆盖半径: {Core.DataConverters.FormatDistance(data.coverageRadius)}\n类型: {data.type}";
                }
            }
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public void Initialize(EarthRenderer earthRenderer, Transform canvasTransform)
        {
            this.earthRenderer = earthRenderer;
            this.canvasTransform = canvasTransform;

            // 创建默认地面站预制体（如果未提供）
            if (groundStationPrefab == null)
            {
                groundStationPrefab = CreateDefaultGroundStationPrefab();
            }

            // 创建默认标签预制体（如果未提供）
            if (labelPrefab == null && showLabels)
            {
                labelPrefab = CreateDefaultLabelPrefab();
            }

            Debug.Log("地面站可视化器初始化完成");
        }

        /// <summary>
        /// 更新地面站可视化
        /// </summary>
        public void UpdateGroundStations(List<Core.GroundStation> groundStations)
        {
            // 移除不存在的地面站
            List<int> toRemove = new List<int>();
            foreach (var kvp in stationInstances)
            {
                if (!groundStations.Exists(g => g.id == kvp.Key))
                {
                    toRemove.Add(kvp.Key);
                }
            }

            foreach (int  id in toRemove)
            {
                DestroyGroundStationInstance(id);
            }

            // 更新或创建地面站实例
            foreach (var station in groundStations)
            {
                if (stationInstances.ContainsKey(station.id))
                {
                    // 更新现有实例
                    var instance = stationInstances[station.id];
                    instance.data = station;
                    instance.UpdateVisualization(earthRenderer, stationScale,
                        mainStationColor, backupStationColor, showCoverageArea,
                        coverageMaterial, coverageColor, coverageOpacity, labelOffset);
                }
                else
                {
                    // 创建新实例
                    CreateGroundStationInstance(station);
                }
            }
        }

        /// <summary>
        /// 创建地面站实例
        /// </summary>
        private void CreateGroundStationInstance(Core.GroundStation station)
        {
            if (earthRenderer == null)
            {
                Debug.LogWarning("EarthRenderer is null, cannot create ground station instance.");
                return;
            }

            // 获取地球容器，地面站应作为地球容器的子对象以跟随地球旋转
            Transform parentTransform = earthRenderer.EarthContainerTransform;
            if (parentTransform == null)
            {
                parentTransform = transform; // 回退到自身的transform
            }

            // 创建地面站游戏对象
            GameObject stationObj = Instantiate(groundStationPrefab, parentTransform);
            stationObj.name = $"GroundStation_{station.id}_{station.name}";
            stationObj.SetActive(true);  // 激活对象（预制体默认禁用）

            // 设置位置（使用localPosition因为地面站是地球容器的子对象）
            Vector3 position = station.GetWorldPosition(earthRenderer.GetEarthRadius());
            stationObj.transform.localPosition = position;
            stationObj.transform.localScale = Vector3.one * stationScale;

            // 朝向地球中心（使用本地坐标系）
            stationObj.transform.localRotation = Quaternion.LookRotation(-position.normalized, Vector3.up);
            stationObj.transform.Rotate(-90, 0, 0);

            // 获取渲染器
            Renderer renderer = stationObj.GetComponent<Renderer>();
            if (renderer == null)
            {
                renderer = stationObj.GetComponentInChildren<Renderer>();
            }

            // 设置颜色
            if (renderer != null)
            {
                Color stationColor = station.type == "main" ? mainStationColor : backupStationColor;
                renderer.material.color = stationColor;
            }

            // 创建覆盖区域
            GameObject coverageArea = null;
            if (showCoverageArea)
            {
                coverageArea = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                coverageArea.name = $"Coverage_{station.id}";
                coverageArea.transform.SetParent(stationObj.transform);
                coverageArea.transform.localPosition = Vector3.zero;
                coverageArea.transform.localScale = Vector3.one * station.coverageRadius * 2;

                var coverageRenderer = coverageArea.GetComponent<Renderer>();
                if (coverageMaterial != null)
                {
                    coverageRenderer.material = coverageMaterial;
                }
                else
                {
                    coverageRenderer.material = new Material(Shader.Find("Standard"));
                }

                coverageColor.a = coverageOpacity;
                coverageRenderer.material.color = coverageColor;

                // 设置为半透明
                var material = coverageRenderer.material;
                material.SetFloat("_Mode", 3); // 透明模式
                material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetInt("_ZWrite", 0);
                material.DisableKeyword("_ALPHATEST_ON");
                material.EnableKeyword("_ALPHABLEND_ON");
                material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
                material.renderQueue = 3000;
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
                    labelText.text = station.name;
                    labelText.font = labelFont;
                    labelText.fontSize = labelFontSize;
                    labelText.color = labelColor;
                }
            }

            // 创建实例
            var instance = new GroundStationInstance
            {
                data = station,
                gameObject = stationObj,
                renderer = renderer,
                coverageArea = coverageArea,
                label = label,
                labelRect = labelRect,
                labelText = labelText,
                worldPosition = position
            };

            stationInstances[station.id] = instance;
            instance.UpdateVisualization(earthRenderer, stationScale,
                mainStationColor, backupStationColor, showCoverageArea,
                coverageMaterial, coverageColor, coverageOpacity,labelOffset);
        }

        /// <summary>
        /// 销毁地面站实例
        /// </summary>
        private void DestroyGroundStationInstance(int stationId)
        {
            if (stationInstances.TryGetValue(stationId, out var instance))
            {
                if (instance.gameObject != null)
                    Destroy(instance.gameObject);
                if (instance.label != null)
                    Destroy(instance.label);

                stationInstances.Remove(stationId);
            }
        }

        /// <summary>
        /// 创建默认地面站预制体
        /// </summary>
        private GameObject CreateDefaultGroundStationPrefab()
        {
            GameObject prefab = new GameObject("DefaultGroundStationPrefab");
            prefab.SetActive(false);

            // 底座（长方体）
            GameObject baseObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            baseObj.transform.SetParent(prefab.transform);
            baseObj.transform.localScale = new Vector3(1f, 0.2f, 1f);
            baseObj.transform.localPosition = Vector3.zero;

            // 塔体（圆柱体）
            GameObject tower = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tower.transform.SetParent(prefab.transform);
            tower.transform.localScale = new Vector3(0.3f, 1f, 0.3f);
            tower.transform.localPosition = new Vector3(0, 0.6f, 0);

            // 天线（胶囊体）
            GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            antenna.transform.SetParent(prefab.transform);
            antenna.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            antenna.transform.localPosition = new Vector3(0, 1.5f, 0);
            antenna.transform.Rotate(180, 0, 0);

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
        /// 显示/隐藏覆盖区域
        /// </summary>
        public void SetCoverageAreasVisible(bool visible)
        {
            showCoverageArea = visible;
            foreach (var instance in stationInstances.Values)
            {
                if (instance.coverageArea != null)
                {
                    instance.coverageArea.SetActive(visible);
                }
            }
        }

        /// <summary>
        /// 显示/隐藏标签
        /// </summary>
        public void SetLabelsVisible(bool visible)
        {
            showLabels = visible;
            foreach (var instance in stationInstances.Values)
            {
                if (instance.label != null)
                {
                    instance.label.SetActive(visible);
                }
            }
        }

        /// <summary>
        /// 设置覆盖区域透明度
        /// </summary>
        public void SetCoverageOpacity(float opacity)
        {
            coverageOpacity = Mathf.Clamp01(opacity);
            coverageColor.a = coverageOpacity;

            foreach (var instance in stationInstances.Values)
            {
                if (instance.coverageArea != null)
                {
                    var renderer = instance.coverageArea.GetComponent<Renderer>();
                    if (renderer != null)
                    {
                        coverageColor.a = coverageOpacity;
                        renderer.material.color = coverageColor;
                    }
                }
            }
        }

        /// <summary>
        /// 清除所有地面站
        /// </summary>
        public void ClearAll()
        {
            foreach (var id in new List<int>(stationInstances.Keys))
            {
                DestroyGroundStationInstance(id);
            }
            stationInstances.Clear();
        }

        void Update()
        {
            // 确保相机存在
            if (Camera.main == null || canvasTransform == null) return;

            Camera cam = Camera.main;
            RectTransform canvasRect = canvasTransform as RectTransform;

            // 更新标签位置（将世界坐标转换为屏幕坐标）
            foreach (var instance in stationInstances.Values)
            {
                if (instance.label != null && instance.labelRect != null)
                {
                    // 将世界坐标转换为屏幕坐标
                    Vector3 screenPos = cam.WorldToScreenPoint(instance.worldPosition);

                    // 检查是否在相机前方且在屏幕内
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
            }
        }

        void OnDestroy()
        {
            ClearAll();
        }
    }
}
