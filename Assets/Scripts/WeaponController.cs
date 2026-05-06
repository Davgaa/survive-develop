using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Connection;
using FishNet.Object;

public class WeaponController : NetworkBehaviour
{
    [SerializeField] float _damage = 25f;
    [SerializeField] float _range = 20f;
    [SerializeField] float _fireRate = 0.3f;
    [SerializeField] float _hitValidationRadius = 1.25f;
    [SerializeField] float _maxLagCompensationSeconds = 0.35f;
    [SerializeField] float _maxShotOriginDistance = 3f;

    float _nextFireTime;
    Camera _cam;
    Animator _animator;

    public override void OnStartClient()
    {
        base.OnStartClient();

        _animator = GetComponentInChildren<Animator>();
        if (!IsOwner)
            return;

        _cam = Camera.main;
    }

    void Update()
    {
        if (!IsOwner)
            return;

        if (_cam == null)
        {
            _cam = Camera.main;
            return;
        }

        bool shooting = Mouse.current != null && Mouse.current.leftButton.isPressed;
        if (!shooting || Time.time < _nextFireTime)
            return;

        _nextFireTime = Time.time + _fireRate;
        Shoot();
    }

    void Shoot()
    {
        PlayImmediateShotFeedback();

        Ray ray = _cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        int layerMask = ~LayerMask.GetMask("Player");
        NetworkObject predictedEnemy = null;

        if (Physics.Raycast(ray, out RaycastHit hit, _range, layerMask))
        {
            EnemyHealth enemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (enemy != null)
            {
                enemy.PreviewDamage(_damage);
                predictedEnemy = enemy.GetComponent<NetworkObject>();
            }
        }

        if (predictedEnemy == null || !predictedEnemy.IsSpawned)
            return;

        uint clientTick = TimeManager != null ? TimeManager.LocalTick : 0;
        DamageServerRpc(predictedEnemy.ObjectId, ray.origin, ray.direction.normalized, clientTick);
    }

    [ServerRpc(RequireOwnership = false)]
    void DamageServerRpc(int enemyObjectId, Vector3 shotOrigin, Vector3 shotDirection, uint clientTick, NetworkConnection sender = null)
    {
        if (!IsServerInitialized)
            return;

        if (!IsValidShooter(sender, shotOrigin) || !TryGetValidatedEnemy(enemyObjectId, shotOrigin, shotDirection, clientTick, sender, out EnemyHealth enemy))
            return;

        enemy.TakeDamage(_damage);
    }

    private void PlayImmediateShotFeedback()
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        _animator?.SetTrigger("Attack");
    }

    [Server]
    private bool IsValidShooter(NetworkConnection sender, Vector3 shotOrigin)
    {
        if (sender == null || !sender.IsActive)
            return false;

        if (Owner != sender)
            return false;

        if (sender.FirstObject == null)
            return false;

        return Vector3.Distance(sender.FirstObject.transform.position, shotOrigin) <= _maxShotOriginDistance;
    }

    [Server]
    private bool TryGetValidatedEnemy(int enemyObjectId, Vector3 shotOrigin, Vector3 shotDirection, uint clientTick, NetworkConnection sender, out EnemyHealth enemy)
    {
        enemy = null;

        if (!ServerManager.Objects.Spawned.TryGetValue(enemyObjectId, out NetworkObject claimedEnemy))
            return false;

        if (claimedEnemy == null || !claimedEnemy.IsSpawned)
            return false;

        if (!claimedEnemy.TryGetComponent(out enemy))
            return false;

        if (shotDirection.sqrMagnitude < 0.9f)
            return false;

        shotDirection.Normalize();
        float secondsAgo = EstimateShotAgeSeconds(clientTick, sender);
        Vector3 rewoundTarget = enemy.GetLagCompensatedPosition(secondsAgo);
        Vector3 toTarget = rewoundTarget - shotOrigin;
        float projectedDistance = Vector3.Dot(toTarget, shotDirection);

        if (projectedDistance < 0f || projectedDistance > _range)
            return false;

        float missDistance = Vector3.Cross(shotDirection, toTarget).magnitude;
        if (missDistance > _hitValidationRadius)
            return false;

        int layerMask = ~LayerMask.GetMask("Player");
        if (Physics.Raycast(shotOrigin, shotDirection, out RaycastHit hit, _range, layerMask))
        {
            EnemyHealth raycastEnemy = hit.collider.GetComponentInParent<EnemyHealth>();
            if (raycastEnemy == enemy)
                return true;

            if (raycastEnemy == null && hit.distance + _hitValidationRadius < projectedDistance)
                return false;
        }

        return true;
    }

    [Server]
    private float EstimateShotAgeSeconds(uint clientTick, NetworkConnection sender)
    {
        if (TimeManager == null || sender == null || sender.LocalTick.IsUnset || clientTick == 0)
            return 0f;

        uint estimatedClientNow = sender.LocalTick.Value(TimeManager);
        if (estimatedClientNow == FishNet.Managing.Timing.TimeManager.UNSET_TICK || estimatedClientNow < clientTick)
            return 0f;

        float secondsAgo = (float)((estimatedClientNow - clientTick) * TimeManager.TickDelta);
        return Mathf.Clamp(secondsAgo, 0f, _maxLagCompensationSeconds);
    }
}
