using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace SatelliteEdgeComputing.Visualization
{
    /// <summary>
    /// Satellite visualizer.
    /// </summary>
    public class SatelliteVisualizer : MonoBehaviour
    {
        [Header("Satellite Settings")]
        [SerializeField] private GameObject satellitePrefab;
        [SerializeField] private float satelliteScale = 50000f;
        [SerializeField] private bool showLabels = true;
        [SerializeField] private bool showStatusIndicators = true;
        [SerializeField] private Color idleColor = Color.green;
        [SerializeField] private Color busyColor = Color.yellow;
        [SerializeField] private Color overloadedColor = Color.red;

        [Header("Label Settings")]
        [SerializeField] private GameObject labelPrefab;
        [SerializeField] private float labelOffset = 20000f;
        [SerializeField] private Font labelFont;
        [SerializeField] private int labelFontSize = 14;
        [SerializeField] private Color labelColor = Color.white;

        [Header("Status Indicator")]
        [SerializeField] private GameObject statusIndicatorPrefab;
        [SerializeField] private float indicatorScale = 2000f;

        [Header("Orbit Settings")]
        [SerializeField] private bool showOrbit = true;
        [SerializeField] private Material orbitMaterial;
        [SerializeField] private float orbitWidth = 0.5f;
        [SerializeField] private Color orbitColor = new Color(0.3f, 0.6f, 1f, 0.5f);
        [SerializeField] private int orbitSegments = 96;
        [SerializeField] private int highLoadOrbitSegments = 48;
        [SerializeField] private float orbitRefreshInterval = 1f;

        [Header("Performance")]
        [SerializeField] private bool verboseLogging = false;
        [SerializeField] private int autoHideLabelsThreshold = 30;
        [SerializeField] private int highLoadOrbitThreshold = 80;

        private readonly Dictionary<int, SatelliteInstance> satelliteInstances = new Dictionary<int, SatelliteInstance>();
        private EarthRenderer earthRenderer;
        private Transform canvasTransform;

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
            public Vector3 worldPosition;
            public Vector3 previousWorldPosition;
            public Vector3 interpolationStartPosition;
            public Vector3 orbitNormal = Vector3.zero;
            public Vector3 lastOrbitCenterPosition = Vector3.zero;
            public Vector3[] orbitPositions;
            public float interpolationStartTime;
            public float lastOrbitRefreshTime = -999f;
            public bool hasPosition;

            public void UpdateVisualization(EarthRenderer earthRenderer, Color idleColor, Color busyColor, Color overloadedColor)
            {
                if (gameObject == null || earthRenderer == null) return;

                Vector3 nextPosition = data.GetWorldPosition(earthRenderer.GetEarthRadius());
                if (!hasPosition)
                {
                    worldPosition = nextPosition;
                    previousWorldPosition = nextPosition;
                    interpolationStartPosition = nextPosition;
                    interpolationStartTime = Time.time;
                    gameObject.transform.position = nextPosition;
                    hasPosition = true;
                }
                else if (nextPosition != worldPosition)
                {
                    previousWorldPosition = worldPosition;
                    interpolationStartPosition = gameObject.transform.position;
                    interpolationStartTime = Time.time;
                    worldPosition = nextPosition;

                    Vector3 inferredNormal = Vector3.Cross(previousWorldPosition.normalized, worldPosition.normalized);
                    if (inferredNormal.sqrMagnitude > 0.000001f)
                        orbitNormal = inferredNormal.normalized;
                }

                if (renderer != null)
                {
                    Color statusColor = data.GetStatusColor();
                    if (statusColor == Color.green) statusColor = idleColor;
                    else if (statusColor == Color.yellow) statusColor = busyColor;
                    else statusColor = overloadedColor;

                    renderer.material.color = statusColor;
                }

                if (label != null && labelText != null)
                    labelText.text = $"{data.name}\n负载: {data.LoadRate:P0}\n任务: {data.taskCount}";
            }

            public void UpdateOrbitLine(bool showOrbit, float width, Color color, int segments, float refreshInterval, bool forceRefresh = false)
            {
                if (orbitLine == null) return;

                orbitLine.enabled = showOrbit && hasPosition;
                if (!orbitLine.enabled) return;

                orbitLine.loop = true;
                orbitLine.startColor = color;
                orbitLine.endColor = color;
                orbitLine.startWidth = width;
                orbitLine.endWidth = width;

                bool needsRefresh =
                    forceRefresh ||
                    orbitPositions == null ||
                    orbitPositions.Length != segments ||
                    Time.time - lastOrbitRefreshTime >= refreshInterval ||
                    Vector3.Distance(lastOrbitCenterPosition, worldPosition) > Mathf.Max(1000f, worldPosition.magnitude * 0.001f);

                if (!needsRefresh)
                    return;

                BuildOrbitPositions(segments);
                orbitLine.positionCount = orbitPositions.Length;
                orbitLine.SetPositions(orbitPositions);
                lastOrbitRefreshTime = Time.time;
                lastOrbitCenterPosition = worldPosition;
            }

            private void BuildOrbitPositions(int segments)
            {
                segments = Mathf.Max(12, segments);
                if (orbitPositions == null || orbitPositions.Length != segments)
                    orbitPositions = new Vector3[segments];

                Vector3 radiusVector = worldPosition;
                float radius = Mathf.Max(1f, radiusVector.magnitude);
                Vector3 normal = orbitNormal.sqrMagnitude > 0.000001f ? orbitNormal.normalized : GetFallbackNormal(radiusVector);
                Vector3 axisA = Vector3.ProjectOnPlane(radiusVector, normal);
                if (axisA.sqrMagnitude < 0.000001f)
                    axisA = Vector3.Cross(normal, Vector3.up);
                if (axisA.sqrMagnitude < 0.000001f)
                    axisA = Vector3.Cross(normal, Vector3.right);

                axisA = axisA.normalized;
                Vector3 axisB = Vector3.Cross(normal, axisA).normalized;

                for (int i = 0; i < segments; i++)
                {
                    float angle = Mathf.PI * 2f * i / segments;
                    orbitPositions[i] = (axisA * Mathf.Cos(angle) + axisB * Mathf.Sin(angle)) * radius;
                }
            }

            private Vector3 GetFallbackNormal(Vector3 radiusVector)
            {
                Vector3 normal = Vector3.Cross(radiusVector.normalized, Vector3.up);
                if (normal.sqrMagnitude < 0.000001f)
                    normal = Vector3.Cross(radiusVector.normalized, Vector3.right);
                return normal.normalized;
            }
        }

        public void Initialize(EarthRenderer earthRenderer, Transform canvasTransform)
        {
            this.earthRenderer = earthRenderer;
            this.canvasTransform = canvasTransform;

            if (satellitePrefab == null)
                satellitePrefab = CreateDefaultSatellitePrefab();

            if (labelPrefab == null && showLabels)
                labelPrefab = CreateDefaultLabelPrefab();

            if (statusIndicatorPrefab == null && showStatusIndicators)
                statusIndicatorPrefab = CreateDefaultStatusIndicatorPrefab();

            Log("Satellite visualizer initialized.");
        }

        public void UpdateSatellites(List<Core.Satellite> satellites)
        {
            Log($"UpdateSatellites: count={satellites?.Count ?? 0}");

            if (satellites == null)
            {
                ClearAll();
                return;
            }

            var satelliteIds = new HashSet<int>();
            foreach (var satellite in satellites)
            {
                if (satellite != null)
                    satelliteIds.Add(satellite.id);
            }

            var toRemove = new List<int>();
            foreach (var kvp in satelliteInstances)
            {
                if (!satelliteIds.Contains(kvp.Key))
                    toRemove.Add(kvp.Key);
            }

            foreach (int id in toRemove)
                DestroySatelliteInstance(id);

            bool compactMode = satellites.Count >= autoHideLabelsThreshold;
            bool highLoadMode = satellites.Count >= highLoadOrbitThreshold;
            int selectedOrbitSegments = highLoadMode ? highLoadOrbitSegments : orbitSegments;

            int updated = 0;
            int created = 0;
            foreach (var satellite in satellites)
            {
                if (satellite == null)
                    continue;

                if (satelliteInstances.TryGetValue(satellite.id, out var instance))
                {
                    instance.data = satellite;
                    instance.UpdateVisualization(earthRenderer, idleColor, busyColor, overloadedColor);
                    instance.UpdateOrbitLine(showOrbit, orbitWidth, orbitColor, selectedOrbitSegments, orbitRefreshInterval);
                    updated++;
                }
                else
                {
                    CreateSatelliteInstance(satellite, compactMode, selectedOrbitSegments);
                    created++;
                }
            }

            Log($"UpdateSatellites done: updated={updated}, created={created}");
        }

        private void CreateSatelliteInstance(Core.Satellite satellite, bool compactMode, int selectedOrbitSegments)
        {
            if (earthRenderer == null)
            {
                Debug.LogWarning("EarthRenderer is null, cannot create satellite instance.");
                return;
            }

            GameObject satelliteObj = Instantiate(satellitePrefab, transform);
            satelliteObj.name = $"Satellite_{satellite.id}_{satellite.name}";
            satelliteObj.SetActive(true);

            float earthRadius = earthRenderer.GetEarthRadius();
            Vector3 position = satellite.GetWorldPosition(earthRadius);
            satelliteObj.transform.position = position;
            satelliteObj.transform.localScale = Vector3.one * satelliteScale;

            Renderer renderer = satelliteObj.GetComponent<Renderer>();
            if (renderer == null)
                renderer = satelliteObj.GetComponentInChildren<Renderer>();

            GameObject label = null;
            RectTransform labelRect = null;
            Text labelText = null;
            bool visibleLabels = showLabels && !compactMode && labelPrefab != null && canvasTransform != null;
            if (visibleLabels)
            {
                label = Instantiate(labelPrefab, canvasTransform);
                labelRect = label.GetComponent<RectTransform>() ?? label.AddComponent<RectTransform>();
                labelText = label.GetComponentInChildren<Text>();
                if (labelText != null)
                {
                    labelText.text = satellite.name;
                    labelText.font = labelFont;
                    labelText.fontSize = labelFontSize;
                    labelText.color = labelColor;
                }
            }

            GameObject statusIndicator = null;
            if (showStatusIndicators && !compactMode && statusIndicatorPrefab != null && canvasTransform != null)
            {
                statusIndicator = Instantiate(statusIndicatorPrefab, canvasTransform);
                if (statusIndicator.GetComponent<RectTransform>() == null)
                    statusIndicator.AddComponent<RectTransform>();
            }

            LineRenderer orbitLine = null;
            if (showOrbit)
            {
                GameObject orbitObj = new GameObject($"Orbit_{satellite.id}");
                orbitObj.transform.SetParent(transform, false);
                orbitLine = orbitObj.AddComponent<LineRenderer>();
                orbitLine.material = orbitMaterial != null ? orbitMaterial : new Material(Shader.Find("Sprites/Default"));
                orbitLine.useWorldSpace = true;
                orbitLine.positionCount = 0;
                orbitLine.loop = true;
                orbitLine.startColor = orbitColor;
                orbitLine.endColor = orbitColor;
                orbitLine.startWidth = orbitWidth;
                orbitLine.endWidth = orbitWidth;
            }

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
                worldPosition = position,
                previousWorldPosition = position,
                orbitNormal = Vector3.Cross(position.normalized, Vector3.up).sqrMagnitude > 0.000001f
                    ? Vector3.Cross(position.normalized, Vector3.up).normalized
                    : Vector3.Cross(position.normalized, Vector3.right).normalized
            };

            satelliteInstances[satellite.id] = instance;
            instance.UpdateVisualization(earthRenderer, idleColor, busyColor, overloadedColor);
            instance.UpdateOrbitLine(showOrbit, orbitWidth, orbitColor, selectedOrbitSegments, orbitRefreshInterval, true);
        }

        private GameObject CreateDefaultSatellitePrefab()
        {
            GameObject prefab = new GameObject("DefaultSatellitePrefab");
            prefab.SetActive(false);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.transform.SetParent(prefab.transform);
            body.transform.localScale = new Vector3(0.5f, 1f, 0.5f);
            body.transform.localPosition = Vector3.zero;

            GameObject solarPanel1 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solarPanel1.transform.SetParent(prefab.transform);
            solarPanel1.transform.localScale = new Vector3(2f, 0.1f, 1f);
            solarPanel1.transform.localPosition = new Vector3(1f, 0, 0);

            GameObject solarPanel2 = GameObject.CreatePrimitive(PrimitiveType.Cube);
            solarPanel2.transform.SetParent(prefab.transform);
            solarPanel2.transform.localScale = new Vector3(2f, 0.1f, 1f);
            solarPanel2.transform.localPosition = new Vector3(-1f, 0, 0);

            GameObject antenna = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            antenna.transform.SetParent(prefab.transform);
            antenna.transform.localScale = new Vector3(0.1f, 0.5f, 0.1f);
            antenna.transform.localPosition = new Vector3(0, 0.5f, 0);
            antenna.transform.Rotate(90, 0, 0);

            var material = new Material(Shader.Find("Standard"));
            material.color = Color.gray;
            foreach (var renderer in prefab.GetComponentsInChildren<Renderer>())
                renderer.material = material;

            return prefab;
        }

        private GameObject CreateDefaultLabelPrefab()
        {
            GameObject prefab = new GameObject("LabelPrefab");
            RectTransform rootRect = prefab.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(200, 50);
            rootRect.pivot = new Vector2(0.5f, 0f);

            GameObject textObj = new GameObject("Text");
            textObj.transform.SetParent(prefab.transform, false);

            RectTransform rectTransform = textObj.AddComponent<RectTransform>();
            rectTransform.anchorMin = Vector2.zero;
            rectTransform.anchorMax = Vector2.one;
            rectTransform.sizeDelta = Vector2.zero;

            Text text = textObj.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 14;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;

            return prefab;
        }

        private GameObject CreateDefaultStatusIndicatorPrefab()
        {
            GameObject prefab = new GameObject("StatusIndicatorPrefab");
            RectTransform rootRect = prefab.AddComponent<RectTransform>();
            rootRect.sizeDelta = new Vector2(20, 20);
            rootRect.pivot = new Vector2(0.5f, 0.5f);

            GameObject icon = new GameObject("Icon");
            icon.transform.SetParent(prefab.transform, false);

            RectTransform iconRect = icon.AddComponent<RectTransform>();
            iconRect.anchorMin = Vector2.zero;
            iconRect.anchorMax = Vector2.one;
            iconRect.sizeDelta = Vector2.zero;

            Image image = icon.AddComponent<Image>();
            image.color = Color.white;

            return prefab;
        }

        public void SetLabelsVisible(bool visible)
        {
            showLabels = visible;
            foreach (var instance in satelliteInstances.Values)
            {
                if (instance.label != null)
                    instance.label.SetActive(visible);
            }
        }

        public void SetStatusIndicatorsVisible(bool visible)
        {
            showStatusIndicators = visible;
            foreach (var instance in satelliteInstances.Values)
            {
                if (instance.statusIndicator != null)
                    instance.statusIndicator.SetActive(visible);
            }
        }

        public void SetOrbitsVisible(bool visible)
        {
            showOrbit = visible;
            foreach (var instance in satelliteInstances.Values)
            {
                if (instance.orbitLine != null)
                    instance.orbitLine.enabled = visible;
            }
        }

        public void ClearAll()
        {
            foreach (var id in new List<int>(satelliteInstances.Keys))
                DestroySatelliteInstance(id);

            satelliteInstances.Clear();
        }

        private void DestroySatelliteInstance(int satelliteId)
        {
            if (!satelliteInstances.TryGetValue(satelliteId, out var instance))
                return;

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

        private void Log(string message)
        {
            if (verboseLogging)
                Debug.Log($"[SatelliteVisualizer] {message}");
        }

        void Update()
        {
            if (Camera.main == null)
                return;

            Camera cam = Camera.main;
            RectTransform canvasRect = canvasTransform as RectTransform;
            const float interpolationDuration = 0.2f;

            foreach (var instance in satelliteInstances.Values)
            {
                if (instance.gameObject != null)
                {
                    float moveT = Mathf.Clamp01((Time.time - instance.interpolationStartTime) / interpolationDuration);
                    instance.gameObject.transform.position = Vector3.Lerp(
                        instance.interpolationStartPosition,
                        instance.worldPosition,
                        moveT
                    );
                    instance.gameObject.transform.LookAt(Vector3.zero);
                    instance.gameObject.transform.Rotate(90, 0, 0);
                }

                Vector3 currentPosition = instance.gameObject != null ? instance.gameObject.transform.position : instance.worldPosition;

                if (canvasRect != null && instance.label != null && instance.labelRect != null)
                {
                    Vector3 screenPos = cam.WorldToScreenPoint(currentPosition);
                    if (screenPos.z > 0 && screenPos.x >= 0 && screenPos.x <= Screen.width &&
                        screenPos.y >= 0 && screenPos.y <= Screen.height)
                    {
                        instance.label.SetActive(true);
                        Vector2 localPos;
                        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out localPos))
                            instance.labelRect.anchoredPosition = localPos + Vector2.up * labelOffset;
                    }
                    else
                    {
                        instance.label.SetActive(false);
                    }
                }

                if (canvasRect != null && instance.statusIndicator != null)
                {
                    Vector3 screenPos = cam.WorldToScreenPoint(currentPosition);
                    if (screenPos.z > 0 && screenPos.x >= 0 && screenPos.x <= Screen.width &&
                        screenPos.y >= 0 && screenPos.y <= Screen.height)
                    {
                        instance.statusIndicator.SetActive(true);
                        var rect = instance.statusIndicator.GetComponent<RectTransform>();
                        if (rect != null)
                        {
                            Vector2 localPos;
                            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPos, null, out localPos))
                                rect.anchoredPosition = localPos + Vector2.up * 50f;
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
