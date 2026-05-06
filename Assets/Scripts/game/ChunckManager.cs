using System.Collections;
using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;

public class ChunkManager : MonoBehaviour
{
    public static bool IsLoaded { get; private set; }
    public static bool IsMapReady { get; private set; } = false;
    public static event System.Action OnMapReady;

    [Header("Chunk тохиргоо")]
    public int chunkSize = 50;
    public int resolution = 30;
    public float heightVariation = 5f;
    public float noiseScale = 0.05f;
    public int worldSeed = 20260506;
    public int viewDistance = 2;
    public Material groundMaterial;

    [Header("Мод")]
    public GameObject[] treePrefabs;
    public float treeChance = 0.3f;
    public float arenaRadius = 15f;

    [Header("NavMesh")]
    public NavMeshSurface navMeshSurface;

    Transform _player;
    Dictionary<Vector2Int, GameObject> _chunks = new();
    Vector2Int _lastPlayerChunk = new Vector2Int(int.MaxValue, 0);
    float _seed;
    bool _mapReadyFired;

    void Awake()
    {
        IsLoaded = false;
        IsMapReady = false;
        OnMapReady = null;
        _mapReadyFired = false;
        _seed = worldSeed;
        Debug.Log($"[ChunkManager] World seed = {worldSeed}");
    }

    void Start()
    {
        // Player хүлээхгүйгээр (0,0,0) гараас chunk үүсгэнэ
        _lastPlayerChunk = WorldToChunk(Vector3.zero);
        UpdateChunks();
        StartCoroutine(BuildNavMeshAndFireEvent());
    }

    IEnumerator BuildNavMeshAndFireEvent()
    {
        // Chunk-ууд үүсэх хүртэл нэг frame хүлээх
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame();

        if (navMeshSurface != null)
        {
            navMeshSurface.BuildNavMesh();
            Debug.Log("NavMesh баригдлаа!");
        }

        IsLoaded = true;
        IsMapReady = true;
        Debug.Log("Map бэлэн! Player spawn хийж болно.");
        OnMapReady?.Invoke();
        // Player гарсны дараа chunk update хийх
        StartCoroutine(TrackPlayer());
    }

    IEnumerator TrackPlayer()
    {
        while (true)
        {
            if (_player == null)
            {
                var p = GameObject.FindGameObjectWithTag("Player");
                if (p != null) _player = p.transform;
            }
            else
            {
                Vector2Int cur = WorldToChunk(_player.position);
                if (cur != _lastPlayerChunk)
                {
                    _lastPlayerChunk = cur;
                    UpdateChunks();
                }
            }
            yield return new WaitForSeconds(0.5f);
        }
    }

    System.Collections.IEnumerator WaitForPlayer()
    {
        while (true)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p != null) { _player = p.transform; break; }
            yield return new WaitForSeconds(0.5f);
        }
        UpdateChunks();

        // NavMesh bake
        yield return new WaitForSeconds(0.5f);
        if (navMeshSurface != null)
            navMeshSurface.BuildNavMesh();

        if (!_mapReadyFired)
        {
            _mapReadyFired = true;
            IsLoaded = true;
            OnMapReady?.Invoke();
            Debug.Log("Map бэлэн боллоо!");
        }
    }

    

    Vector2Int WorldToChunk(Vector3 pos) =>
        new Vector2Int(
            Mathf.FloorToInt(pos.x / chunkSize),
            Mathf.FloorToInt(pos.z / chunkSize));

    void UpdateChunks()
    {
        var needed = new HashSet<Vector2Int>();

        for (int x = -viewDistance; x <= viewDistance; x++)
            for (int z = -viewDistance; z <= viewDistance; z++)
            {
                var coord = new Vector2Int(
                    _lastPlayerChunk.x + x,
                    _lastPlayerChunk.y + z);
                needed.Add(coord);
                if (!_chunks.ContainsKey(coord))
                    CreateChunk(coord);
            }

        var remove = new List<Vector2Int>();
        foreach (var kv in _chunks)
            if (!needed.Contains(kv.Key)) remove.Add(kv.Key);
        foreach (var c in remove)
        {
            Destroy(_chunks[c]);
            _chunks.Remove(c);
        }
    }

    void CreateChunk(Vector2Int coord)
    {
        Vector3 origin = new Vector3(
            coord.x * chunkSize, 0, coord.y * chunkSize);

        var go = new GameObject($"Chunk_{coord.x}_{coord.y}");
        go.transform.position = origin;
        go.transform.SetParent(transform);
        go.layer = LayerMask.NameToLayer("Ground");

        var mf = go.AddComponent<MeshFilter>();
        var mr = go.AddComponent<MeshRenderer>();
        var mc = go.AddComponent<MeshCollider>();

        if (groundMaterial) mr.material = groundMaterial;

        var mesh = BuildMesh(coord, origin);
        mf.mesh = mesh;
        mc.sharedMesh = mesh;

        SpawnTrees(go, coord, origin);
        _chunks[coord] = go;
    }
    float OctaveNoise(float wx, float wz)
    {
        // Domain Warping — байгалийн уул шиг харагдуулна
        float warpX = Mathf.PerlinNoise(
            (wx + _seed) * noiseScale * 0.5f,
            (wz + _seed) * noiseScale * 0.5f) * 30f;
        float warpZ = Mathf.PerlinNoise(
            (wx + _seed + 50f) * noiseScale * 0.5f,
            (wz + _seed + 50f) * noiseScale * 0.5f) * 30f;

        // Үндсэн том давалгаа — уул
        float hills = Mathf.PerlinNoise(
            (wx + warpX + _seed) * noiseScale,
            (wz + warpZ + _seed) * noiseScale);

        // Дунд давалгаа — толгод
        float mid = Mathf.PerlinNoise(
            (wx + _seed) * noiseScale * 2.5f,
            (wz + _seed) * noiseScale * 2.5f) * 0.3f;

        // Жижиг давалгаа — нарийн хэлбэр
        float detail = Mathf.PerlinNoise(
            (wx + _seed) * noiseScale * 6f,
            (wz + _seed) * noiseScale * 6f) * 0.1f;

        // Нэгтгэх
        float combined = hills * 0.6f + mid + detail;

        // Уул гэдэг мэдрэмж нэмэх — доод хэсгийг хавтгай болгох
        combined = Mathf.Pow(Mathf.Clamp01(combined), 1.2f);

        return combined * heightVariation;
    }
    Mesh BuildMesh(Vector2Int coord, Vector3 origin)
    {
        var mesh = new Mesh();
        int r = resolution;
        float s = chunkSize;

        var verts = new Vector3[(r + 1) * (r + 1)];
        var uvs = new Vector2[verts.Length];
        var tris = new int[r * r * 6];

        for (int i = 0, z = 0; z <= r; z++)
            for (int x = 0; x <= r; x++, i++)
            {
                float lx = (float)x / r * s;
                float lz = (float)z / r * s;
                float wx = origin.x + lx;
                float wz = origin.z + lz;

                float y = OctaveNoise(wx, wz);

                // Arena дунд хэсгийг хавтгай байлгах — зөв арга
                float dist = new Vector2(wx, wz).magnitude;
                float flatBlend = Mathf.Clamp01((dist - arenaRadius) / 10f);
                y *= flatBlend;

                verts[i] = new Vector3(lx, y, lz);
                uvs[i] = new Vector2((float)x / r, (float)z / r);
            }

        int v = 0, t = 0;
        for (int z = 0; z < r; z++)
        {
            for (int x = 0; x < r; x++)
            {
                tris[t] = v;
                tris[t + 1] = v + r + 1;
                tris[t + 2] = v + 1;
                tris[t + 3] = v + 1;
                tris[t + 4] = v + r + 1;
                tris[t + 5] = v + r + 2;
                v++; t += 6;
            }
            v++;
        }

        mesh.vertices = verts;
        mesh.triangles = tris;
        mesh.uv = uvs;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    void SpawnTrees(GameObject chunk, Vector2Int coord, Vector3 origin)
    {
        if (treePrefabs == null || treePrefabs.Length == 0) return;

        var old = Random.state;
        int treeSeed = unchecked(worldSeed ^ (coord.x * 73856093) ^ (coord.y * 19349663));
        Random.InitState(treeSeed);

        int count = Mathf.Clamp(
            Mathf.RoundToInt(chunkSize * treeChance * 0.05f), 1, 8);

        for (int i = 0; i < count; i++)
        {
            float lx = Random.Range(2f, chunkSize - 2f);
            float lz = Random.Range(2f, chunkSize - 2f);
            float wx = origin.x + lx;
            float wz = origin.z + lz;

            if (new Vector2(wx, wz).magnitude < arenaRadius + 5f)
                continue;

            var ray = new Ray(new Vector3(wx, 100f, wz), Vector3.down);
            if (!Physics.Raycast(ray, out RaycastHit hit, 200f)) continue;

            var tree = Instantiate(
                treePrefabs[Random.Range(0, treePrefabs.Length)],
                hit.point,
                Quaternion.Euler(0, Random.Range(0f, 360f), 0));
            tree.transform.localScale =
                Vector3.one * Random.Range(0.8f, 1.5f);
            tree.transform.SetParent(chunk.transform);
        }

        Random.state = old;
    }
}
