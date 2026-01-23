// VfxManager.cs (전체 교체본)
// - 기존 Smoke/Fire loop VFX 유지
// - Wooden Cogwheel, Big Wooden Cogwheel, Waterwheel, Windmill 프리팹 추가
// - rpm/rotationDir 세팅해서 지정 좌표에 소환하는 API 추가
using System.Collections.Generic;
using UnityEngine;

public class VfxManager : MonoBehaviour
{
    [Header("Block Break Particles")]
    public Material material;       // Sprites/Default 기반 템플릿 1개
    public ParticleSystem psPrefab; // 파티클 시스템 프리팹

    [Header("Loop VFX Prefabs")]
    // 키는 string으로 고정
    // 기존: "Smoke", "Fire_01", "Fire_02"
    public GameObject smokePrefab;
    public GameObject fire01Prefab;
    public GameObject fire02Prefab;

    [Header("Rotating VFX Prefabs")]
    // 신규: 회전 VFX (프리팹에는 RotatingVfx 컴포넌트가 붙어있어야 함)
    // 키: "WoodenCogwheel", "BigWoodenCogwheel", "Waterwheel", "Windmill"
    public GameObject woodenCogwheelPrefab;
    public GameObject bigWoodenCogwheelPrefab;
    public GameObject waterwheelPrefab;
    public GameObject windmillPrefab;

    [Header("Loop VFX Culling")]
    public float activeRange = 40f;  // 플레이어 기준 거리 비활/활

    readonly Dictionary<(Sprite, int), Mesh[]> _meshCache = new Dictionary<(Sprite, int), Mesh[]>();
    readonly Dictionary<Texture, Material> _matByTex = new Dictionary<Texture, Material>();

    // 루프 VFX 인스턴스 관리: (ownerInstId, vfxKey) -> GameObject
    readonly Dictionary<(int, string), GameObject> _loop = new Dictionary<(int, string), GameObject>();

    Transform _player;

    public void SetPlayer(Transform player)
    {
        _player = player;
        CullAllLoopVfx();
    }

    // ─────────────────────────────
    // Loop VFX API (기존)
    // ─────────────────────────────
    public void SetLoopVfx(int ownerInstId, string vfxKey, bool on, Vector3 worldPos)
    {
        var key = (ownerInstId, vfxKey);

        if (!on)
        {
            if (_loop.TryGetValue(key, out var go) && go != null)
                go.SetActive(false);
            return;
        }

        if (!IsInRange(worldPos))
        {
            if (_loop.TryGetValue(key, out var go) && go != null)
                go.SetActive(false);
            return;
        }

        if (!_loop.TryGetValue(key, out var inst) || inst == null)
        {
            var prefab = GetLoopPrefab(vfxKey);
            if (prefab == null) return;

            inst = Instantiate(prefab, worldPos, Quaternion.identity);
            inst.name = $"{vfxKey}(Owner#{ownerInstId})";
            _loop[key] = inst;
        }

        inst.transform.position = worldPos;
        if (!inst.activeSelf) inst.SetActive(true);
    }

    // ─────────────────────────────
    // Rotating Loop VFX API (신규)
    // ─────────────────────────────
    public void SetRotatingLoopVfx(int ownerInstId, string vfxKey, bool on, Vector3 worldPos, float rpm, int rotationDir)
    {
        var key = (ownerInstId, vfxKey);

        if (!on)
        {
            if (_loop.TryGetValue(key, out var go) && go != null)
                go.SetActive(false);
            return;
        }

        if (!IsInRange(worldPos))
        {
            if (_loop.TryGetValue(key, out var go) && go != null)
                go.SetActive(false);
            return;
        }

        if (!_loop.TryGetValue(key, out var inst) || inst == null)
        {
            var prefab = GetRotatingPrefab(vfxKey);
            if (prefab == null) return;

            inst = Instantiate(prefab, worldPos, Quaternion.identity);
            inst.name = $"{vfxKey}(Owner#{ownerInstId})";
            _loop[key] = inst;
        }

        inst.transform.position = worldPos;

        var rv = inst.GetComponent<RotatingVfx>();
        if (rv != null)
            rv.Set(rpm, rotationDir);

        if (!inst.activeSelf) inst.SetActive(true);
    }

    public void DespawnAllForOwner(int ownerInstId)
    {
        var toRemove = new List<(int, string)>();
        foreach (var kv in _loop)
        {
            if (kv.Key.Item1 == ownerInstId)
            {
                if (kv.Value != null) Destroy(kv.Value);
                toRemove.Add(kv.Key);
            }
        }

        for (int i = 0; i < toRemove.Count; i++)
            _loop.Remove(toRemove[i]);
    }

    public void CullAllLoopVfx()
    {
        if (_player == null) return;

        foreach (var kv in _loop)
        {
            var go = kv.Value;
            if (go == null) continue;

            bool inRange = IsInRange(go.transform.position);
            if (go.activeSelf != inRange)
                go.SetActive(inRange);
        }
    }

    GameObject GetLoopPrefab(string vfxKey)
    {
        return vfxKey switch
        {
            "Smoke"   => smokePrefab,
            "Fire_01" => fire01Prefab,
            "Fire_02" => fire02Prefab,
            _ => null
        };
    }

    GameObject GetRotatingPrefab(string vfxKey)
    {
        return vfxKey switch
        {
            "WoodenCogwheel"     => woodenCogwheelPrefab,
            "BigWoodenCogwheel"  => bigWoodenCogwheelPrefab,
            "Waterwheel"         => waterwheelPrefab,
            "Windmill"           => windmillPrefab,
            _ => null
        };
    }

    bool IsInRange(Vector3 p)
    {
        if (_player == null) return true;
        float r = Mathf.Max(0.01f, activeRange);
        return (_player.position - p).sqrMagnitude <= r * r;
    }

    // ─────────────────────────────
    // Block Break Particles (기존)  <<< 건들지 말라고 해서 그대로 둠
    // ─────────────────────────────
    public void EmitBlockAtCell(Sprite s, int cx, int cy, int cellSize, int grid = 2, int count = -1)
    {
        Vector3 pos = new Vector3(
            cx * cellSize + cellSize * 0.5f,
            cy * cellSize + cellSize * 0.5f,
            0f
        );

        ParticleSystem ps = Instantiate(psPrefab, pos, Quaternion.identity);
        var rend = (ParticleSystemRenderer)ps.GetComponent<Renderer>();
        rend.renderMode = ParticleSystemRenderMode.Mesh;
        rend.sharedMaterial = GetMat(s.texture);

        Mesh[] shards = GetShardMeshes(s, grid);

        if (count < 0)
        {
            for (int i = 0; i < shards.Length; i++) { rend.mesh = shards[i]; ps.Emit(1); }
        }
        else
        {
            for (int i = 0; i < count; i++) { rend.mesh = shards[Random.Range(0, shards.Length)]; ps.Emit(1); }
        }

        var main = ps.main;
        main.stopAction = ParticleSystemStopAction.Destroy;
    }

    Mesh[] GetShardMeshes(Sprite s, int grid)
    {
        var key = (s, grid);
        if (_meshCache.TryGetValue(key, out var arr)) return arr;

        Texture2D tex = s.texture;
        Rect r = s.textureRect;
        float tw = tex.width, th = tex.height;

        int gx = Mathf.Max(1, grid);
        int gy = Mathf.Max(1, grid);

        int total = gx * gy;
        var meshes = new Mesh[total];

        float cellW = r.width / gx;
        float cellH = r.height / gy;

        int idx = 0;
        for (int y = 0; y < gy; y++)
        for (int x = 0; x < gx; x++)
        {
            float px0 = r.x + x * cellW;
            float py0 = r.y + y * cellH;
            float px1 = px0 + cellW;
            float py1 = py0 + cellH;

            float u0 = px0 / tw;
            float v0 = py0 / th;
            float u1 = px1 / tw;
            float v1 = py1 / th;

            var m = new Mesh();
            m.vertices = new[]
            {
                new Vector3(-0.5f, -0.5f, 0f),
                new Vector3(-0.5f, +0.5f, 0f),
                new Vector3(+0.5f, +0.5f, 0f),
                new Vector3(+0.5f, -0.5f, 0f),
            };
            m.uv = new[]
            {
                new Vector2(u0, v0),
                new Vector2(u0, v1),
                new Vector2(u1, v1),
                new Vector2(u1, v0),
            };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            meshes[idx++] = m;
        }

        _meshCache[key] = meshes;
        return meshes;
    }

    Material GetMat(Texture tex)
    {
        if (_matByTex.TryGetValue(tex, out var m) && m != null)
            return m;

        var inst = new Material(material);
        inst.mainTexture = tex;
        _matByTex[tex] = inst;
        return inst;
    }
}
