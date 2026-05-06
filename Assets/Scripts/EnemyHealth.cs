using UnityEngine;
using UnityEngine.Events;
using FishNet.Object;
using FishNet.Object.Synchronizing;

public class EnemyHealth : NetworkBehaviour
{
    private const int HistorySize = 64;

    [SerializeField] float _maxHp = 100f;
    [SerializeField] GameObject _healthBarPrefab;

    public UnityEvent OnDeath = new UnityEvent(); // ← нэмэх

    readonly SyncVar<float> _hp = new SyncVar<float>();
    readonly Vector3[] _positionHistory = new Vector3[HistorySize];
    readonly float[] _positionHistoryTimes = new float[HistorySize];
    EnemyHealthBar _healthBar;
    int _historyIndex;
    bool _hasHistory;
    float _previewUntilTime;

    public override void OnStartServer()
    {
        base.OnStartServer();
        var ctrl = GetComponent<EnemyController>();
        _maxHp = ctrl?.data?.maxHealth ?? _maxHp;
        _hp.Value = _maxHp;
        RecordServerPosition();
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (_healthBarPrefab != null)
        {
            GameObject bar = Instantiate(_healthBarPrefab,
                transform.position + Vector3.up * 1.5f,
                Quaternion.identity);
            bar.transform.SetParent(transform);
            bar.transform.localPosition = Vector3.up * 1.5f;
            _healthBar = bar.GetComponent<EnemyHealthBar>();
        }
        _hp.OnChange += OnHpChanged;
        _healthBar?.UpdateHealth(_hp.Value, _maxHp);
    }

    void OnHpChanged(float prev, float next, bool asServer)
    {
        if (_healthBar == null)
            _healthBar = GetComponentInChildren<EnemyHealthBar>();

        _previewUntilTime = 0f;
        _healthBar?.UpdateHealth(next, _maxHp);
    }

    void Update()
    {
        if (IsServerStarted)
        {
            RecordServerPosition();
            return;
        }

        if (_previewUntilTime > 0f && Time.time >= _previewUntilTime)
            ClearPreview();
    }

    public void PreviewDamage(float damage)
    {
        if (_healthBar == null)
            _healthBar = GetComponentInChildren<EnemyHealthBar>();

        float previewHp = Mathf.Max(0f, _hp.Value - damage);
        _previewUntilTime = Time.time + 0.35f;
        _healthBar?.UpdateHealth(previewHp, _maxHp);
    }

    public void ClearPreview()
    {
        if (_healthBar == null)
            _healthBar = GetComponentInChildren<EnemyHealthBar>();

        _previewUntilTime = 0f;
        _healthBar?.UpdateHealth(_hp.Value, _maxHp);
    }

    public Vector3 GetLagCompensatedPosition(float secondsAgo)
    {
        if (!_hasHistory)
            return transform.position;

        float targetTime = Time.time - secondsAgo;
        Vector3 newestPosition = transform.position;
        float newestTime = -1f;

        for (int i = 0; i < HistorySize; i++)
        {
            int index = (_historyIndex - 1 - i + HistorySize) % HistorySize;
            float sampleTime = _positionHistoryTimes[index];

            if (sampleTime <= 0f)
                continue;

            if (newestTime < 0f)
            {
                newestTime = sampleTime;
                newestPosition = _positionHistory[index];
            }

            if (sampleTime <= targetTime)
                return _positionHistory[index];
        }

        return newestPosition;
    }

    private void RecordServerPosition()
    {
        _positionHistory[_historyIndex] = transform.position;
        _positionHistoryTimes[_historyIndex] = Time.time;
        _historyIndex = (_historyIndex + 1) % HistorySize;
        _hasHistory = true;
    }

    public void TakeDamage(float damage)
    {
        if (!IsServerStarted) return;
        _hp.Value -= damage;
        if (_hp.Value <= 0)
        {
            OnDeath?.Invoke(); // ← үхэх үед дуудах
            ServerManager.Despawn(gameObject);
        }
    }
}
