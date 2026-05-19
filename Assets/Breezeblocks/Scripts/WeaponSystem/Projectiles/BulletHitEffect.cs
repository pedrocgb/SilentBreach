using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Weapons/Bullet Hit Effect")]
public class BulletHitEffect : MonoBehaviour
{
    [FoldoutGroup("References"), Tooltip("Optional transform used to rotate or scale the visible impact.")]
    [SerializeField] private Transform visualsRoot;

    [FoldoutGroup("References")]
    [SerializeField] private ParticleSystem[] particleSystems;

    [FoldoutGroup("Playback"), MinValue(0f)]
    [SerializeField] private float fallbackDuration = 0.5f;

    private Coroutine lifetimeRoutine;

    private void Reset()
    {
        visualsRoot = transform;
        CacheParticleSystems();
    }

    private void Awake()
    {
        if (visualsRoot == null)
            visualsRoot = transform;

        CacheParticleSystems();
    }

    private void OnDisable()
    {
        if (lifetimeRoutine != null)
        {
            StopCoroutine(lifetimeRoutine);
            lifetimeRoutine = null;
        }
    }

    public void Play()
    {
        if (visualsRoot == null)
            visualsRoot = transform;

        CacheParticleSystems();

        if (particleSystems != null)
        {
            for (int i = 0; i < particleSystems.Length; i++)
            {
                if (particleSystems[i] == null)
                    continue;

                particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                particleSystems[i].Play(true);
            }
        }

        if (lifetimeRoutine != null)
            StopCoroutine(lifetimeRoutine);

        lifetimeRoutine = StartCoroutine(LifetimeRoutine(ResolveDuration()));
    }

    private IEnumerator LifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        ReturnToPoolOrDisable();
        lifetimeRoutine = null;
    }

    private float ResolveDuration()
    {
        float resolvedDuration = Mathf.Max(0f, fallbackDuration);

        if (particleSystems == null)
            return resolvedDuration;

        for (int i = 0; i < particleSystems.Length; i++)
        {
            ParticleSystem system = particleSystems[i];
            if (system == null)
                continue;

            ParticleSystem.MainModule main = system.main;
            float candidateDuration = main.duration + main.startLifetime.constantMax;
            resolvedDuration = Mathf.Max(resolvedDuration, candidateDuration);
        }

        return resolvedDuration;
    }

    private void CacheParticleSystems()
    {
        if (particleSystems == null || particleSystems.Length == 0)
            particleSystems = GetComponentsInChildren<ParticleSystem>(true);
    }

    private void ReturnToPoolOrDisable()
    {
        GlobalPooledObject pooledObject = GetComponent<GlobalPooledObject>();
        if (pooledObject != null)
            pooledObject.ReturnToPool();
        else
            gameObject.SetActive(false);
    }
}
}
