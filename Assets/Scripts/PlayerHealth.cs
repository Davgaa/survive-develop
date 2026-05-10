using UnityEngine;
using UnityEngine.Events;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class PlayerHealth : NetworkBehaviour
{
    [SerializeField] float _maxHp = 100f;

    public UnityEvent OnDeath = new UnityEvent();

    readonly SyncVar<float> _hp = new SyncVar<float>();

    public float CurrentHp => _hp.Value;
    public float MaxHp => _maxHp;

    public override void OnStartServer()
    {
        base.OnStartServer();
        _hp.Value = _maxHp;
    }

    public void TakeDamage(float damage)
    {
        if (!IsServerStarted) return;
        _hp.Value = Mathf.Max(0f, _hp.Value - damage);
        if (_hp.Value <= 0f)
            OnDeath?.Invoke();
    }
}
