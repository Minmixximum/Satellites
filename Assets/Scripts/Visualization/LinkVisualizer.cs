using System.Collections.Generic;
using UnityEngine;

namespace SatelliteEdgeComputing.Visualization
{
    /// <summary>
    /// Communication link visualizer.
    /// </summary>
    public class LinkVisualizer : MonoBehaviour
    {
        [Header("Link Settings")]
        [SerializeField] private bool showLinks = true;
        [SerializeField] private Material linkMaterial;
        [SerializeField] private float linkWidth = 350f;
        [SerializeField] private float minVisibleLinkWidth = 80f;
        [SerializeField] private Color activeLinkColor = new Color(0f, 1f, 0.15f, 0.9f);
        [SerializeField] private Color inactiveLinkColor = new Color(1f, 0.15f, 0.05f, 0.25f);
        [SerializeField] private Color potentialLinkColor = new Color(1f, 0.9f, 0f, 0.5f);
        [SerializeField] private float maxLinkDistance = 5000000f;
        [SerializeField] private bool requireLineOfSight = true;

        [Header("Particles")]
        [SerializeField] private bool useParticles = false;
        [SerializeField] private ParticleSystem linkParticlePrefab;
        [SerializeField] private float particleSpeed = 10f;
        [SerializeField] private float particleEmissionRate = 5f;

        [Header("Performance")]
        [SerializeField] private int maxLinks = 50;
        [SerializeField] private float updateInterval = 0.5f;

        private readonly Dictionary<string, LinkInstance> linkInstances = new Dictionary<string, LinkInstance>();
        private readonly List<LinkCandidate> candidates = new List<LinkCandidate>();
        private readonly HashSet<string> desiredLinkIds = new HashSet<string>();
        private readonly List<string> linksToRemove = new List<string>();
        private EarthRenderer earthRenderer;
        private float lastUpdateTime = -999f;
        private int activeLinkCount;
        private int visibleLinkCount;

        public int ActiveLinkCount => activeLinkCount;
        public int VisibleLinkCount => visibleLinkCount;

        private struct LinkCandidate
        {
            public string linkId;
            public Core.Satellite satellite;
            public Core.GroundStation station;
            public Vector3 satellitePosition;
            public Vector3 stationPosition;
            public float distance;
            public float strength;
            public bool isActive;
            public bool isPotential;
        }

        private class LinkInstance
        {
            public string linkId;
            public int satelliteId;
            public int groundStationId;
            public bool isActive;
            public bool isPotential;
            public float strength;
            public GameObject gameObject;
            public LineRenderer lineRenderer;
            public ParticleSystem particleSystem;

            public void UpdateVisualization(Vector3 satellitePos, Vector3 groundStationPos,
                bool showLinks, float width, float minWidth,
                Color activeColor, Color inactiveColor, Color potentialColor,
                bool useParticles, float particleSpeed)
            {
                if (gameObject == null || lineRenderer == null) return;

                lineRenderer.enabled = showLinks;
                if (!showLinks) return;

                lineRenderer.SetPosition(0, satellitePos);
                lineRenderer.SetPosition(1, groundStationPos);

                Color linkColor = inactiveColor;
                if (isActive)
                    linkColor = Color.Lerp(potentialColor, activeColor, Mathf.Clamp01(strength));
                else if (isPotential)
                    linkColor = Color.Lerp(inactiveColor, potentialColor, Mathf.Clamp01(strength));

                float visibleWidth = Mathf.Max(minWidth, width * Mathf.Clamp01(strength));
                lineRenderer.startColor = linkColor;
                lineRenderer.endColor = linkColor;
                lineRenderer.startWidth = visibleWidth;
                lineRenderer.endWidth = Mathf.Max(minWidth * 0.75f, visibleWidth * 0.65f);

                if (particleSystem == null) return;

                bool particlesVisible = useParticles && isActive && strength > 0.3f;
                particleSystem.gameObject.SetActive(particlesVisible);
                if (!particlesVisible)
                {
                    if (particleSystem.isPlaying)
                        particleSystem.Stop();
                    return;
                }

                if (!particleSystem.isPlaying)
                    particleSystem.Play();

                particleSystem.transform.position = satellitePos;
                var shape = particleSystem.shape;
                shape.rotation = Quaternion.LookRotation(groundStationPos - satellitePos).eulerAngles;

                var main = particleSystem.main;
                main.startSpeed = particleSpeed * Mathf.Clamp01(strength);
            }
        }

        public void Initialize(EarthRenderer earthRenderer)
        {
            this.earthRenderer = earthRenderer;

            if (linkParticlePrefab == null && useParticles)
                linkParticlePrefab = CreateDefaultParticlePrefab();

            Debug.Log("Link visualizer initialized.");
        }

        public void UpdateLinks(List<Core.Satellite> satellites, List<Core.GroundStation> groundStations)
        {
            if (earthRenderer == null || satellites == null || groundStations == null)
                return;

            if (Time.time - lastUpdateTime < updateInterval)
            {
                ApplyVisibilityOnly();
                return;
            }

            lastUpdateTime = Time.time;
            BuildCandidates(satellites, groundStations);
            ApplyTopCandidates();
        }

        private void BuildCandidates(List<Core.Satellite> satellites, List<Core.GroundStation> groundStations)
        {
            candidates.Clear();
            float earthRadius = earthRenderer.GetEarthRadius();

            foreach (var satellite in satellites)
            {
                if (satellite == null)
                    continue;

                Vector3 satellitePos = satellite.GetWorldPosition(earthRadius);

                foreach (var station in groundStations)
                {
                    if (station == null)
                        continue;

                    Vector3 stationPos = GetGroundStationWorldPosition(station, earthRadius);
                    float distance = Vector3.Distance(satellitePos, stationPos);
                    bool isPotential = distance <= maxLinkDistance * 1.2f;
                    if (!isPotential)
                        continue;

                    if (requireLineOfSight && !HasLineOfSight(satellitePos, stationPos, earthRadius))
                        continue;

                    float strength = Mathf.Clamp01(1f - (distance / Mathf.Max(1f, maxLinkDistance * 1.2f)));
                    bool isActive = distance <= maxLinkDistance && satellite.LoadRate < 0.8f;

                    candidates.Add(new LinkCandidate
                    {
                        linkId = $"{satellite.id}_{station.id}",
                        satellite = satellite,
                        station = station,
                        satellitePosition = satellitePos,
                        stationPosition = stationPos,
                        distance = distance,
                        strength = strength,
                        isActive = isActive,
                        isPotential = isPotential
                    });
                }
            }

            candidates.Sort((left, right) =>
            {
                int activeCompare = right.isActive.CompareTo(left.isActive);
                if (activeCompare != 0) return activeCompare;

                int strengthCompare = right.strength.CompareTo(left.strength);
                if (strengthCompare != 0) return strengthCompare;

                return left.distance.CompareTo(right.distance);
            });
        }

        private void ApplyTopCandidates()
        {
            desiredLinkIds.Clear();
            activeLinkCount = 0;
            visibleLinkCount = 0;

            int count = Mathf.Min(maxLinks, candidates.Count);
            for (int i = 0; i < count; i++)
            {
                LinkCandidate candidate = candidates[i];
                desiredLinkIds.Add(candidate.linkId);

                LinkInstance instance = GetOrCreateLinkInstance(candidate);
                instance.isActive = candidate.isActive;
                instance.isPotential = candidate.isPotential;
                instance.strength = candidate.strength;

                if (candidate.isActive)
                    activeLinkCount++;
                visibleLinkCount++;

                instance.UpdateVisualization(
                    candidate.satellitePosition,
                    candidate.stationPosition,
                    showLinks,
                    linkWidth,
                    minVisibleLinkWidth,
                    activeLinkColor,
                    inactiveLinkColor,
                    potentialLinkColor,
                    useParticles,
                    particleSpeed);
            }

            linksToRemove.Clear();
            foreach (var kvp in linkInstances)
            {
                if (!desiredLinkIds.Contains(kvp.Key))
                    linksToRemove.Add(kvp.Key);
            }

            foreach (string linkId in linksToRemove)
                DestroyLinkInstance(linkId);
        }

        private LinkInstance GetOrCreateLinkInstance(LinkCandidate candidate)
        {
            if (linkInstances.TryGetValue(candidate.linkId, out var instance))
                return instance;

            GameObject linkObj = new GameObject($"Link_{candidate.linkId}");
            linkObj.transform.SetParent(transform, false);

            LineRenderer lineRenderer = linkObj.AddComponent<LineRenderer>();
            lineRenderer.positionCount = 2;
            lineRenderer.useWorldSpace = true;
            lineRenderer.material = linkMaterial != null
                ? linkMaterial
                : new Material(Shader.Find("Sprites/Default"));

            ParticleSystem particleSystem = null;
            if (useParticles)
            {
                if (linkParticlePrefab == null)
                    linkParticlePrefab = CreateDefaultParticlePrefab();

                particleSystem = Instantiate(linkParticlePrefab, linkObj.transform);
                particleSystem.Stop();
            }

            instance = new LinkInstance
            {
                linkId = candidate.linkId,
                satelliteId = candidate.satellite.id,
                groundStationId = candidate.station.id,
                gameObject = linkObj,
                lineRenderer = lineRenderer,
                particleSystem = particleSystem
            };

            linkInstances[candidate.linkId] = instance;
            return instance;
        }

        private Vector3 GetGroundStationWorldPosition(Core.GroundStation station, float earthRadius)
        {
            Vector3 localPosition = station.GetWorldPosition(earthRadius);
            Transform earthTransform = earthRenderer != null ? earthRenderer.EarthContainerTransform : null;
            return earthTransform != null ? earthTransform.TransformPoint(localPosition) : localPosition;
        }

        private bool HasLineOfSight(Vector3 satellitePos, Vector3 stationPos, float earthRadius)
        {
            Vector3 stationNormal = stationPos.normalized;
            Vector3 toSatellite = (satellitePos - stationPos).normalized;
            if (Vector3.Dot(stationNormal, toSatellite) <= 0f)
                return false;

            Vector3 closestPoint = ClosestPointOnSegment(Vector3.zero, stationPos, satellitePos);
            return closestPoint.magnitude >= earthRadius * 0.98f;
        }

        private Vector3 ClosestPointOnSegment(Vector3 point, Vector3 start, Vector3 end)
        {
            Vector3 segment = end - start;
            float lengthSquared = segment.sqrMagnitude;
            if (lengthSquared <= Mathf.Epsilon)
                return start;

            float t = Mathf.Clamp01(Vector3.Dot(point - start, segment) / lengthSquared);
            return start + segment * t;
        }

        private void ApplyVisibilityOnly()
        {
            foreach (var instance in linkInstances.Values)
            {
                if (instance.lineRenderer != null)
                    instance.lineRenderer.enabled = showLinks;

                if (instance.particleSystem != null)
                    instance.particleSystem.gameObject.SetActive(showLinks && useParticles && instance.isActive);
            }
        }

        private void DestroyLinkInstance(string linkId)
        {
            if (!linkInstances.TryGetValue(linkId, out var instance))
                return;

            if (instance.gameObject != null)
                Destroy(instance.gameObject);

            linkInstances.Remove(linkId);
        }

        private ParticleSystem CreateDefaultParticlePrefab()
        {
            GameObject particleObj = new GameObject("LinkParticles");
            ParticleSystem particleSystem = particleObj.AddComponent<ParticleSystem>();

            var main = particleSystem.main;
            main.startSpeed = particleSpeed;
            main.startLifetime = 3f;
            main.startSize = 0.1f;
            main.startColor = activeLinkColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 100;

            var emission = particleSystem.emission;
            emission.rateOverTime = particleEmissionRate;

            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 5f;
            shape.radius = 0.1f;

            var renderer = particleSystem.GetComponent<ParticleSystemRenderer>();
            renderer.material = new Material(Shader.Find("Particles/Standard Unlit"));

            particleObj.SetActive(false);
            return particleSystem;
        }

        public void SetLinksVisible(bool visible)
        {
            showLinks = visible;
            ApplyVisibilityOnly();
        }

        public void SetParticlesEnabled(bool enabled)
        {
            useParticles = enabled;
            ApplyVisibilityOnly();
        }

        public void SetMaxLinkDistance(float distance)
        {
            maxLinkDistance = Mathf.Max(1000f, distance);
            lastUpdateTime = -999f;
        }

        public void SetLinkWidth(float width)
        {
            linkWidth = Mathf.Max(0.1f, width);
            ApplyVisibilityOnly();
        }

        public void ClearAll()
        {
            foreach (var linkId in new List<string>(linkInstances.Keys))
                DestroyLinkInstance(linkId);

            linkInstances.Clear();
            activeLinkCount = 0;
            visibleLinkCount = 0;
        }

        void OnDestroy()
        {
            ClearAll();
        }
    }
}
