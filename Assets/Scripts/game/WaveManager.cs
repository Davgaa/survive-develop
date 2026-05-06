using UnityEngine;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.Events;

public class WaveManager : NetworkBehaviour
{
    [Header("Enemy Prefabs")]
    [SerializeField] private NetworkObject _banditPrefab;
    [SerializeField] private NetworkObject _knightPrefab;
    [SerializeField] private NetworkObject _skeletonPrefab;

    [Header("Газар илрүүлэлт")]
    [SerializeField] private LayerMask _groundMask;

    [Header("Wave тохиргоо")]
    [SerializeField] private float _timeBetweenWaves = 15f;
    [SerializeField] private float _spawnInterval = 0.5f;
    [SerializeField] private bool _useStressSpawnCount = true;
    [SerializeField] private int _stressEnemyCount = 50;
    [SerializeField] private float _stressTestSpawnInterval = 0.1f;
    [SerializeField] private float _spawnMinDistance = 3f;
    [SerializeField] private float _spawnRingMinDistance = 22f;
    [SerializeField] private float _spawnRingMaxDistance = 45f;
    [SerializeField] private int _spawnPositionAttempts = 24;

    private readonly SyncVar<int> _currentWave = new SyncVar<int>();
    private readonly SyncVar<int> _enemiesAlive = new SyncVar<int>();
    private readonly List<Vector3> _waveSpawnPositions = new List<Vector3>();

    public int CurrentWave => _currentWave.Value;
    public int EnemiesAlive => _enemiesAlive.Value;

    private bool _canStartWaves = false;

    public override void OnStartServer()
    {
        base.OnStartServer();
        Debug.Log("[WaveManager] OnStartServer called");

        if (ChunkManager.IsLoaded)
        {
            HandleMapReady();
        }
        else
        {
            ChunkManager.OnMapReady += HandleMapReady;
        }
    }

    public override void OnStopServer()
    {
        base.OnStopServer();
        ChunkManager.OnMapReady -= HandleMapReady;
    }

    private void HandleMapReady()
    {
        if (_canStartWaves)
            return;

        _canStartWaves = true;
        Debug.Log("[WaveManager] Map ready. Starting waves.");
        StartCoroutine(WaveLoop());
    }

    private IEnumerator WaveLoop()
    {
        yield return new WaitForSeconds(2f);

        while (_canStartWaves)
        {
            _currentWave.Value++;
            _waveSpawnPositions.Clear();
            Debug.Log($"[WaveManager] Starting wave {_currentWave.Value}");

            yield return StartCoroutine(SpawnWave(_currentWave.Value));

            while (_enemiesAlive.Value > 0)
                yield return new WaitForSeconds(1f);

            Debug.Log($"[WaveManager] Wave {_currentWave.Value} дууслаа!");
            yield return new WaitForSeconds(_timeBetweenWaves);
        }
    }

    private IEnumerator SpawnWave(int wave)
    {
        bool stressTestWave = _useStressSpawnCount && wave == 1;
        if (stressTestWave)
        {
            Debug.Log($"[WaveManager] Stress wave {wave}: total enemies={_stressEnemyCount}, interval={_stressTestSpawnInterval:0.###}s");

            for (int i = 0; i < _stressEnemyCount; i++)
            {
                SpawnEnemy(GetStressTestPrefab(i), i % 3);
                yield return new WaitForSeconds(_stressTestSpawnInterval);
            }

            yield break;
        }

        int banditCount = 3 + wave * 2;
        int knightCount = wave >= 3 ? wave : 0;
        int skeletonCount = wave >= 5 ? wave - 3 : 0;
        float spawnInterval = _spawnInterval;
        Debug.Log($"[WaveManager] Wave {wave} counts: bandit={banditCount}, knight={knightCount}, skeleton={skeletonCount}, interval={spawnInterval:0.###}s");

        for (int i = 0; i < banditCount; i++)
        {
            SpawnEnemy(_banditPrefab, 0);
            yield return new WaitForSeconds(spawnInterval);
        }

        for (int i = 0; i < knightCount; i++)
        {
            SpawnEnemy(_knightPrefab, 1);
            yield return new WaitForSeconds(spawnInterval);
        }

        for (int i = 0; i < skeletonCount; i++)
        {
            SpawnEnemy(_skeletonPrefab, 2);
            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private NetworkObject GetStressTestPrefab(int index)
    {
        NetworkObject[] prefabs = { _banditPrefab, _knightPrefab, _skeletonPrefab };
        int available = 0;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] != null)
                available++;
        }

        if (available == 0)
            return null;

        int wanted = index % available;
        int seen = 0;

        for (int i = 0; i < prefabs.Length; i++)
        {
            if (prefabs[i] == null)
                continue;

            if (seen == wanted)
                return prefabs[i];

            seen++;
        }

        return _banditPrefab;
    }

    [Server]
    private void SpawnEnemy(NetworkObject prefab, int spawnSector)
    {
        if (prefab == null)
        {
            Debug.LogWarning("[WaveManager] Enemy prefab is null.");
            return;
        }

        Vector3 spawnPos = GetSpawnPosition(spawnSector);
        if (spawnPos == Vector3.zero)
        {
            Debug.LogWarning("[WaveManager] Could not find valid spawn position.");
            return;
        }

        NetworkObject enemy = Instantiate(prefab, spawnPos, Quaternion.identity);
        ServerManager.Spawn(enemy);
        _waveSpawnPositions.Add(spawnPos);

        _enemiesAlive.Value++;
        EnemyHealth health = enemy.GetComponent<EnemyHealth>();
        if (health != null)
        {
            health.OnDeath.AddListener(() =>
            {
                _enemiesAlive.Value = Mathf.Max(0, _enemiesAlive.Value - 1);
            });
        }
        else
        {
            Debug.LogWarning("[WaveManager] EnemyHealth component missing on enemy prefab.");
        }
    }

    private Vector3 GetSpawnPosition(int spawnSector)
    {
        for (int i = 0; i < _spawnPositionAttempts; i++)
        {
            Vector3 candidate = GetSpawnCandidate(spawnSector);
            if (candidate == Vector3.zero)
                continue;

            if (IsFarEnoughFromOtherSpawns(candidate))
                return candidate;
        }

        return GetSpawnCandidate(spawnSector);
    }

    private Vector3 GetSpawnCandidate(int spawnSector)
    {
        float sectorSize = 120f;
        float angleStart = spawnSector * sectorSize;
        float angle = Random.Range(angleStart, angleStart + sectorSize) * Mathf.Deg2Rad;
        float dist = Random.Range(_spawnRingMinDistance, _spawnRingMaxDistance);

        float x = Mathf.Cos(angle) * dist;
        float z = Mathf.Sin(angle) * dist;

        Vector3 rayStart = new Vector3(x, 50f, z);

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 100f, _groundMask))
            return hit.point + Vector3.up * 1f;

        return Vector3.zero;
    }

    private bool IsFarEnoughFromOtherSpawns(Vector3 candidate)
    {
        float minSqrDistance = _spawnMinDistance * _spawnMinDistance;

        for (int i = 0; i < _waveSpawnPositions.Count; i++)
        {
            if ((candidate - _waveSpawnPositions[i]).sqrMagnitude < minSqrDistance)
                return false;
        }

        return true;
    }
}
