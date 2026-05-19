using UnityEngine;

namespace Breezeblocks.WeaponSystem
{

[DisallowMultipleComponent]
public class GlobalPooledObject : MonoBehaviour
{
    private GlobalObjectPooler _owner;
    private Transform _poolContainer;

    internal void Assign(GlobalObjectPooler owner, Transform poolContainer)
    {
        _owner = owner;
        _poolContainer = poolContainer;
    }

    public void ReturnToPool()
    {
        if (_owner != null)
        {
            _owner.Return(gameObject, _poolContainer);
            return;
        }

        gameObject.SetActive(false);
    }
}
}
