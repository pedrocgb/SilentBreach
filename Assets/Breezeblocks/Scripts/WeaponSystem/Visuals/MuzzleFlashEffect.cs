using System.Collections;
using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Weapons/Muzzle Flash Effect")]
public class MuzzleFlashEffect : MonoBehaviour
{
    [FoldoutGroup("References"), Tooltip("Optional separate root for scaling the flash visuals.")]
    [SerializeField] private Transform visualsRoot;

    [FoldoutGroup("References")]
    [SerializeField] private ParticleSystem[] particleSystems;

    private Coroutine _lifetimeRoutine;

    private void Reset()
    {
        visualsRoot = transform;
    }

    private void OnDisable()
    {
        if (_lifetimeRoutine != null)
        {
            StopCoroutine(_lifetimeRoutine);
            _lifetimeRoutine = null;
        }
    }

    public void Play(float size, float duration)
    {
        if (visualsRoot == null)
            visualsRoot = transform;

        visualsRoot.localScale = Vector3.one * Mathf.Max(0f, size);

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

        if (_lifetimeRoutine != null)
            StopCoroutine(_lifetimeRoutine);

        _lifetimeRoutine = StartCoroutine(LifetimeRoutine(Mathf.Max(0f, duration)));
    }

    private IEnumerator LifetimeRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        ReturnToPoolOrDisable();
        _lifetimeRoutine = null;
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
