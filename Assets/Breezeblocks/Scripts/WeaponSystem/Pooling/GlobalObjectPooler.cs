using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/System/Global Object Pooler")]
public class GlobalObjectPooler : MonoBehaviour
{
#pragma warning disable 0649
    [Serializable]
    private class PoolRegistration
    {
        [AssetsOnly, Required]
        public GameObject prefab;

        [MinValue(0)]
        public int prewarmCount = 8;
    }
#pragma warning restore 0649

    private sealed class RuntimePool
    {
        public readonly GameObject Prefab;
        public readonly Transform Container;
        public readonly List<GameObject> Instances = new();

        public RuntimePool(GameObject prefab, Transform container)
        {
            Prefab = prefab;
            Container = container;
        }
    }

    public static GlobalObjectPooler Instance
    {
        get
        {
            if (_instance == null)
            {
                _instance = FindFirstObjectByType<GlobalObjectPooler>();
                if (_instance == null)
                {
                    GameObject poolerObject = new GameObject("Global Object Pooler");
                    _instance = poolerObject.AddComponent<GlobalObjectPooler>();
                }
            }

            return _instance;
        }
    }

    [FoldoutGroup("Lifecycle")]
    [SerializeField] private bool dontDestroyOnLoad = true;

    [FoldoutGroup("Setup")]
    [ListDrawerSettings(ShowFoldout = true, DefaultExpandedState = true)]
    [SerializeField] private List<PoolRegistration> startupPools = new();

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public int RegisteredPoolCount => _runtimePools.Count;

    [FoldoutGroup("Debug"), ShowInInspector, ReadOnly]
    public int TotalInstanceCount
    {
        get
        {
            int total = 0;
            foreach (RuntimePool pool in _runtimePools.Values)
                total += pool.Instances.Count;

            return total;
        }
    }

    private static GlobalObjectPooler _instance;
    private readonly Dictionary<GameObject, RuntimePool> _runtimePools = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStaticState()
    {
        _instance = null;
    }

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;

        if (dontDestroyOnLoad)
            DontDestroyOnLoad(gameObject);

        RegisterStartupPools();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Debug")]
    public void RegisterStartupPools()
    {
        if (startupPools == null)
            return;

        for (int i = 0; i < startupPools.Count; i++)
        {
            PoolRegistration registration = startupPools[i];
            if (registration == null || registration.prefab == null)
                continue;

            RegisterPrefab(registration.prefab, registration.prewarmCount);
        }
    }

    public void RegisterPrefab(GameObject prefab, int prewarmCount = 0)
    {
        RuntimePool pool = GetOrCreatePool(prefab);
        Prewarm(pool, Mathf.Max(0, prewarmCount));
    }

    public T Spawn<T>(T prefab, Vector3 position, Quaternion rotation, Transform parentOverride = null, int prewarmCount = 0) where T : Component
    {
        if (prefab == null)
            return null;

        GameObject instance = Spawn(prefab.gameObject, position, rotation, parentOverride, prewarmCount);
        return instance != null ? instance.GetComponent<T>() : null;
    }

    public GameObject Spawn(GameObject prefab, Vector3 position, Quaternion rotation, Transform parentOverride = null, int prewarmCount = 0)
    {
        RuntimePool pool = GetOrCreatePool(prefab);
        Prewarm(pool, Mathf.Max(0, prewarmCount));

        GameObject instance = GetAvailableInstance(pool);
        if (instance == null)
            return null;

        Transform targetParent = parentOverride != null ? parentOverride : null;
        instance.transform.SetParent(targetParent, false);
        instance.transform.SetPositionAndRotation(position, rotation);
        instance.SetActive(true);
        return instance;
    }

    public void Return(GameObject instance)
    {
        if (instance == null)
            return;

        GlobalPooledObject pooledObject = instance.GetComponent<GlobalPooledObject>();
        if (pooledObject != null)
        {
            pooledObject.ReturnToPool();
            return;
        }

        instance.SetActive(false);
    }

    internal void Return(GameObject instance, Transform poolContainer)
    {
        if (instance == null)
            return;

        instance.SetActive(false);
        instance.transform.SetParent(poolContainer, false);
    }

    public void ResetRuntimeState()
    {
        foreach (RuntimePool pool in _runtimePools.Values)
        {
            if (pool == null)
                continue;

            for (int i = pool.Instances.Count - 1; i >= 0; i--)
            {
                GameObject instance = pool.Instances[i];
                if (instance == null)
                {
                    pool.Instances.RemoveAt(i);
                    continue;
                }

                instance.SetActive(false);
                instance.transform.SetParent(pool.Container, false);
            }
        }
    }

    private RuntimePool GetOrCreatePool(GameObject prefab)
    {
        if (prefab == null)
            return null;

        if (_runtimePools.TryGetValue(prefab, out RuntimePool existingPool))
            return existingPool;

        GameObject containerObject = new GameObject(prefab.name + " Pool");
        containerObject.transform.SetParent(transform, false);

        RuntimePool newPool = new RuntimePool(prefab, containerObject.transform);
        _runtimePools.Add(prefab, newPool);
        return newPool;
    }

    private void Prewarm(RuntimePool pool, int targetCount)
    {
        if (pool == null || pool.Prefab == null)
            return;

        while (pool.Instances.Count < targetCount)
            CreateInstance(pool);
    }

    private GameObject GetAvailableInstance(RuntimePool pool)
    {
        if (pool == null)
            return null;

        for (int i = 0; i < pool.Instances.Count; i++)
        {
            GameObject instance = pool.Instances[i];
            if (instance != null && !instance.activeSelf)
                return instance;
        }

        return CreateInstance(pool);
    }

    private GameObject CreateInstance(RuntimePool pool)
    {
        if (pool == null || pool.Prefab == null)
            return null;

        GameObject instance = Instantiate(pool.Prefab, pool.Container);
        instance.SetActive(false);

        GlobalPooledObject pooledObject = instance.GetComponent<GlobalPooledObject>();
        if (pooledObject == null)
            pooledObject = instance.AddComponent<GlobalPooledObject>();

        pooledObject.Assign(this, pool.Container);
        pool.Instances.Add(instance);
        return instance;
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode loadMode)
    {
        ResetRuntimeState();
    }
}
}
