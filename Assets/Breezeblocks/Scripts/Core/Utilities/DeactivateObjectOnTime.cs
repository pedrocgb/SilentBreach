using System.Collections;
using Breezeblocks.WeaponSystem;
using Sirenix.OdinInspector;
using UnityEngine;

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Utility/Deactivate Object On Time")]
public class DeactivateObjectOnTime : MonoBehaviour
{
    [FoldoutGroup("References"), Tooltip("Optional target to deactivate. If empty, this GameObject is used.")]
    [SerializeField] private GameObject targetObject;

    [FoldoutGroup("Timing")]
    [SerializeField] private bool startTimerOnEnable = true;

    [FoldoutGroup("Timing"), MinValue(0f), SuffixLabel("s", true)]
    [SerializeField] private float deactivateDelay = 1f;

    [FoldoutGroup("Timing")]
    [SerializeField] private bool useUnscaledTime;

    [FoldoutGroup("Behavior")]
    [SerializeField] private bool returnToPoolIfAvailable = true;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public bool IsTimerRunning => lifetimeRoutine != null;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly, SuffixLabel("s", true)]
    public float DeactivateDelay => deactivateDelay;

    private Coroutine lifetimeRoutine;

    private void Reset()
    {
        if (targetObject == null)
            targetObject = gameObject;
    }

    private void OnEnable()
    {
        if (!startTimerOnEnable)
            return;

        BeginTimer();
    }

    private void OnDisable()
    {
        CancelTimer();
    }

    private void OnValidate()
    {
        deactivateDelay = Mathf.Max(0f, deactivateDelay);
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Actions")]
    public void BeginTimer()
    {
        BeginTimer(deactivateDelay);
    }

    public void BeginTimer(float delay)
    {
        CancelTimer();
        lifetimeRoutine = StartCoroutine(LifetimeRoutine(Mathf.Max(0f, delay)));
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Actions")]
    public void CancelTimer()
    {
        if (lifetimeRoutine == null)
            return;

        StopCoroutine(lifetimeRoutine);
        lifetimeRoutine = null;
    }

    [Button(ButtonSizes.Small)]
    [FoldoutGroup("Actions")]
    public void DeactivateNow()
    {
        CancelTimer();
        ReturnToPoolOrDeactivate();
    }

    private IEnumerator LifetimeRoutine(float delay)
    {
        if (useUnscaledTime)
            yield return new WaitForSecondsRealtime(delay);
        else
            yield return new WaitForSeconds(delay);

        lifetimeRoutine = null;
        ReturnToPoolOrDeactivate();
    }

    private void ReturnToPoolOrDeactivate()
    {
        GameObject resolvedTarget = targetObject != null ? targetObject : gameObject;
        if (resolvedTarget == null)
            return;

        if (returnToPoolIfAvailable)
        {
            GlobalPooledObject pooledObject = resolvedTarget.GetComponent<GlobalPooledObject>();
            if (pooledObject != null)
            {
                pooledObject.ReturnToPool();
                return;
            }
        }

        resolvedTarget.SetActive(false);
    }
}
