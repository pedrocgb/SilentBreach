using Sirenix.OdinInspector;
using UnityEngine;

namespace Breezeblocks.Missions
{

[DisallowMultipleComponent]
[AddComponentMenu("Breezeblocks/Missions/Actor World UI Fixed Rotation")]
public class ActorWorldUiFixedRotation : MonoBehaviour
{
    [FoldoutGroup("Rotation")]
    [SerializeField] private bool captureCurrentWorldRotationOnEnable = true;

    [FoldoutGroup("Rotation"), ShowIf("@!captureCurrentWorldRotationOnEnable")]
    [SerializeField] private Vector3 fixedWorldEulerAngles;

    [FoldoutGroup("State"), ShowInInspector, ReadOnly]
    public Quaternion LockedWorldRotation => lockedWorldRotation;

    private Quaternion lockedWorldRotation = Quaternion.identity;

    private void Awake()
    {
        RefreshLockedRotation();
        ApplyLockedRotation();
    }

    private void OnEnable()
    {
        RefreshLockedRotation();
        ApplyLockedRotation();
    }

    private void LateUpdate()
    {
        ApplyLockedRotation();
    }

    [Button(ButtonSizes.Small)]
    public void RefreshLockedRotation()
    {
        lockedWorldRotation = captureCurrentWorldRotationOnEnable
            ? transform.rotation
            : Quaternion.Euler(fixedWorldEulerAngles);
    }

    private void ApplyLockedRotation()
    {
        transform.rotation = lockedWorldRotation;
    }
}

}
