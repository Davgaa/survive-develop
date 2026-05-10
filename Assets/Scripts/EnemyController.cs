using UnityEngine;
using UnityEngine.AI;
using FishNet.Object;

public class EnemyController : NetworkBehaviour
{
    [SerializeField] public EnemyData data;
    [SerializeField] private float targetRefreshInterval = 0.5f;
    [SerializeField] private float animationSyncInterval = 0.15f;
    [SerializeField] private float destinationRefreshInterval = 0.25f;
    [SerializeField] private float destinationMoveThreshold = 0.75f;
    [SerializeField] private float _attackCooldown = 1.5f;

    NavMeshAgent _agent;
    Transform _target;
    Animator _animator;
    float _nextTargetRefreshTime;
    float _nextAnimationSyncTime;
    float _nextDestinationRefreshTime;
    float _lastSyncedSpeed = -1f;
    float _lastAttackTime;
    Vector3 _lastDestination;
    bool _lastSyncedAttack;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _animator = GetComponentInChildren<Animator>();
        _nextDestinationRefreshTime = Time.time + Random.Range(0f, destinationRefreshInterval);
    }

    public override void OnStartServer()
    {
        base.OnStartServer();
        FindTarget();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsServerStarted)
        {
            NavMeshAgent agent = GetComponent<NavMeshAgent>();
            if (agent != null)
                agent.enabled = false;
        }
    }

    [Server]
    void FindTarget()
    {
        GameObject[] players = GameObject.FindGameObjectsWithTag("Player");
        float closest = Mathf.Infinity;

        foreach (var p in players)
        {
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < closest)
            {
                closest = dist;
                _target = p.transform;
            }
        }

        _nextTargetRefreshTime = Time.time + targetRefreshInterval;
    }

    void Update()
    {
        if (!IsServerStarted)
            return;

        if ((_target == null || !_target.gameObject.activeInHierarchy) && Time.time >= _nextTargetRefreshTime)
            FindTarget();

        if (_target == null)
        {
            if (_agent != null && _agent.enabled && _agent.hasPath)
                _agent.ResetPath();

            SyncAnimation(0f, false);
            return;
        }

        if (_agent == null || !_agent.enabled)
            return;

        float dist = Vector3.Distance(transform.position, _target.position);
        float range = data != null ? data.attackRange : 1.5f;

        if (dist > range)
        {
            UpdateDestinationIfNeeded();
            SyncAnimation(1f, false);
        }
        else
        {
            if (_agent.hasPath)
                _agent.ResetPath();

            SyncAnimation(0f, true);
            TryAttack();
        }
    }

    [Server]
    void TryAttack()
    {
        if (Time.time < _lastAttackTime + _attackCooldown) return;
        _lastAttackTime = Time.time;

        PlayerHealth health = _target.GetComponent<PlayerHealth>();
        if (health != null)
        {
            float dmg = data != null ? data.damage : 10f;
            health.TakeDamage(dmg);
        }
    }

    [Server]
    void UpdateDestinationIfNeeded()
    {
        if (Time.time < _nextDestinationRefreshTime)
            return;

        Vector3 targetPosition = _target.position;
        _nextDestinationRefreshTime = Time.time + destinationRefreshInterval;

        if (_agent.hasPath && Vector3.Distance(_lastDestination, targetPosition) < destinationMoveThreshold)
            return;

        _lastDestination = targetPosition;
        _agent.SetDestination(targetPosition);
    }

    [Server]
    void SyncAnimation(float speed, bool attack)
    {
        bool speedChanged = !Mathf.Approximately(speed, _lastSyncedSpeed);
        bool attackChanged = attack && !_lastSyncedAttack;
        bool intervalElapsed = Time.time >= _nextAnimationSyncTime;

        if (!speedChanged && !attackChanged && !intervalElapsed)
            return;

        _lastSyncedSpeed = speed;
        _lastSyncedAttack = attack;
        _nextAnimationSyncTime = Time.time + animationSyncInterval;
        UpdateAnimationRpc(speed, attack);
    }

    [ObserversRpc]
    void UpdateAnimationRpc(float speed, bool attack)
    {
        if (_animator == null)
            _animator = GetComponentInChildren<Animator>();

        _animator?.SetFloat("Speed", speed, 0.1f, Time.deltaTime);
        if (attack)
            _animator?.SetTrigger("Attack");
    }
}
