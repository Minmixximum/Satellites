using System.Collections.Generic;
using UnityEngine;

namespace SatelliteEdgeComputing.Utils
{
    /// <summary>
    /// 对象池基类
    /// </summary>
    /// <typeparam name="T">池化对象的类型</typeparam>
    public abstract class ObjectPool<T> where T : class
    {
        protected Stack<T> pool = new Stack<T>();
        protected int maxSize;

        public ObjectPool(int maxSize = 100)
        {
            this.maxSize = maxSize;
        }

        /// <summary>
        /// 获取对象
        /// </summary>
        public virtual T Get()
        {
            if (pool.Count > 0)
            {
                return pool.Pop();
            }
            return CreateNew();
        }

        /// <summary>
        /// 回收对象
        /// </summary>
        public virtual void Return(T obj)
        {
            if (pool.Count < maxSize)
            {
                pool.Push(obj);
            }
            else
            {
                Destroy(obj);
            }
        }

        /// <summary>
        /// 清空对象池
        /// </summary>
        public virtual void Clear()
        {
            while (pool.Count > 0)
            {
                Destroy(pool.Pop());
            }
            pool.Clear();
        }

        /// <summary>
        /// 获取池中对象数量
        /// </summary>
        public int Count => pool.Count;

        /// <summary>
        /// 创建新对象
        /// </summary>
        protected abstract T CreateNew();

        /// <summary>
        /// 销毁对象
        /// </summary>
        protected abstract void Destroy(T obj);
    }

    /// <summary>
    /// GameObject对象池
    /// </summary>
    public class GameObjectPool : ObjectPool<GameObject>
    {
        private GameObject prefab;
        private Transform parent;

        public GameObjectPool(GameObject prefab, Transform parent = null, int maxSize = 100) : base(maxSize)
        {
            this.prefab = prefab;
            this.parent = parent;
        }

        protected override GameObject CreateNew()
        {
            GameObject obj = GameObject.Instantiate(prefab, parent);
            obj.SetActive(false);
            return obj;
        }

        protected override void Destroy(GameObject obj)
        {
            GameObject.Destroy(obj);
        }

        public override GameObject Get()
        {
            GameObject obj = base.Get();
            obj.SetActive(true);
            return obj;
        }

        public override void Return(GameObject obj)
        {
            obj.SetActive(false);
            base.Return(obj);
        }
    }

    /// <summary>
    /// Component对象池
    /// </summary>
    /// <typeparam name="T">Component类型</typeparam>
    public class ComponentPool<T> : ObjectPool<T> where T : Component
    {
        private T prefab;
        private Transform parent;

        public ComponentPool(T prefab, Transform parent = null, int maxSize = 100) : base(maxSize)
        {
            this.prefab = prefab;
            this.parent = parent;
        }

        protected override T CreateNew()
        {
            T component = GameObject.Instantiate(prefab, parent);
            component.gameObject.SetActive(false);
            return component;
        }

        protected override void Destroy(T obj)
        {
            GameObject.Destroy(obj.gameObject);
        }

        public override T Get()
        {
            T component = base.Get();
            component.gameObject.SetActive(true);
            return component;
        }

        public override void Return(T component)
        {
            component.gameObject.SetActive(false);
            base.Return(component);
        }
    }

    /// <summary>
    /// 粒子系统对象池
    /// </summary>
    public class ParticleSystemPool : ObjectPool<ParticleSystem>
    {
        private ParticleSystem prefab;
        private Transform parent;

        public ParticleSystemPool(ParticleSystem prefab, Transform parent = null, int maxSize = 50) : base(maxSize)
        {
            this.prefab = prefab;
            this.parent = parent;
        }

        protected override ParticleSystem CreateNew()
        {
            ParticleSystem ps = GameObject.Instantiate(prefab, parent);
            ps.gameObject.SetActive(false);
            return ps;
        }

        protected override void Destroy(ParticleSystem obj)
        {
            GameObject.Destroy(obj.gameObject);
        }

        public override ParticleSystem Get()
        {
            ParticleSystem ps = base.Get();
            ps.gameObject.SetActive(true);
            ps.Play();
            return ps;
        }

        public override void Return(ParticleSystem ps)
        {
            ps.Stop();
            ps.gameObject.SetActive(false);
            base.Return(ps);
        }
    }

    /// <summary>
    /// 对象池管理器（单例）
    /// </summary>
    public class ObjectPoolManager : MonoBehaviour
    {
        private static ObjectPoolManager _instance;
        public static ObjectPoolManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("ObjectPoolManager");
                    _instance = go.AddComponent<ObjectPoolManager>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        private Dictionary<string, GameObjectPool> gameObjectPools = new Dictionary<string, GameObjectPool>();
        private Dictionary<string, object> componentPools = new Dictionary<string, object>();

        /// <summary>
        /// 获取或创建GameObject对象池
        /// </summary>
        public GameObjectPool GetGameObjectPool(GameObject prefab, Transform parent = null, int maxSize = 100)
        {
            string key = $"{prefab.name}_{(parent != null ? parent.name : "root")}";

            if (!gameObjectPools.ContainsKey(key))
            {
                gameObjectPools[key] = new GameObjectPool(prefab, parent, maxSize);
            }

            return gameObjectPools[key];
        }

        /// <summary>
        /// 获取或创建Component对象池
        /// </summary>
        public ComponentPool<T> GetComponentPool<T>(T prefab, Transform parent = null, int maxSize = 100) where T : Component
        {
            string key = $"{typeof(T).Name}_{prefab.name}_{(parent != null ? parent.name : "root")}";

            if (!componentPools.ContainsKey(key))
            {
                componentPools[key] = new ComponentPool<T>(prefab, parent, maxSize);
            }

            return (ComponentPool<T>)componentPools[key];
        }

        /// <summary>
        /// 获取或创建粒子系统对象池
        /// </summary>
        public ParticleSystemPool GetParticleSystemPool(ParticleSystem prefab, Transform parent = null, int maxSize = 50)
        {
            string key = $"ParticleSystem_{prefab.name}_{(parent != null ? parent.name : "root")}";

            if (!componentPools.ContainsKey(key))
            {
                componentPools[key] = new ParticleSystemPool(prefab, parent, maxSize);
            }

            return (ParticleSystemPool)componentPools[key];
        }

        /// <summary>
        /// 清空所有对象池
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in gameObjectPools.Values)
            {
                pool.Clear();
            }
            gameObjectPools.Clear();

            foreach (var pool in componentPools.Values)
            {
                if (pool is GameObjectPool gameObjectPool)
                {
                    gameObjectPool.Clear();
                }
                else if (pool is ComponentPool<Component> componentPool)
                {
                    componentPool.Clear();
                }
                else if (pool is ParticleSystemPool particlePool)
                {
                    particlePool.Clear();
                }
            }
            componentPools.Clear();
        }

        /// <summary>
        /// 预加载对象池
        /// </summary>
        public void PreloadPool<T>(T prefab, Transform parent, int count, int maxSize = 100) where T : Component
        {
            var pool = GetComponentPool(prefab, parent, maxSize);
            List<T> preloaded = new List<T>();

            for (int i = 0; i < count; i++)
            {
                T obj = pool.Get();
                preloaded.Add(obj);
            }

            foreach (T obj in preloaded)
            {
                pool.Return(obj);
            }
        }

        /// <summary>
        /// 获取池统计信息
        /// </summary>
        public Dictionary<string, int> GetPoolStatistics()
        {
            var stats = new Dictionary<string, int>();

            foreach (var kvp in gameObjectPools)
            {
                stats[$"GameObject_{kvp.Key}"] = kvp.Value.Count;
            }

            foreach (var kvp in componentPools)
            {
                int count = 0;
                if (kvp.Value is GameObjectPool gameObjectPool)
                {
                    count = gameObjectPool.Count;
                }
                else if (kvp.Value is ComponentPool<Component> componentPool)
                {
                    count = componentPool.Count;
                }
                else if (kvp.Value is ParticleSystemPool particlePool)
                {
                    count = particlePool.Count;
                }
                stats[$"Component_{kvp.Key}"] = count;
            }

            return stats;
        }

        void OnDestroy()
        {
            ClearAllPools();
        }
    }
}