// VfxManager.cs (전체)
using System.Collections.Generic;
using UnityEngine;

public class VfxManager : MonoBehaviour
{
    [Header("Block Break Particles")]
    public Material material;       // Sprites/Default 기반 템플릿 1개
    public ParticleSystem psPrefab; // 파티클 시스템 프리팹

    [Header("Loop VFX Prefabs")]
    // 키는 string으로 고정: "Smoke", "Fire_01", "Fire_02"
    public GameObject smokePrefab;
    public GameObject fire01Prefab;
    public GameObject fire02Prefab;

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

        // 플레이어 세팅 시, 현재 존재하는 루프 VFX들 culling 한 번 적용
        CullAllLoopVfx();
    }

    // ─────────────────────────────
    // Loop VFX API
    // ─────────────────────────────

    /// <summary>
    /// 루프 VFX를 켜거나 끄고, 켜는 경우 위치를 갱신한다.
    /// - on=false: 인스턴스가 있으면 비활성
    /// - on=true : 거리 조건 만족 시 생성/활성/위치갱신, 아니면 비활성
    /// </summary>
    public void SetLoopVfx(int ownerInstId, string vfxKey, bool on, Vector3 worldPos)
    {
        var key = (ownerInstId, vfxKey);

        if (!on)
        {
            if (_loop.TryGetValue(key, out var go) && go != null)
                go.SetActive(false);
            return;
        }

        // on=true 인데 플레이어 거리로 비활 조건이면 끔
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

    /// <summary>
    /// 특정 owner 멀티블럭의 모든 루프 VFX 삭제
    /// </summary>
    public void DespawnAllForOwner(int ownerInstId)
    {
        // 안전하게 스냅샷 후 삭제
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

    /// <summary>
    /// 플레이어 거리 기반으로 현재 생성된 모든 루프 VFX를 culling한다.
    /// - SetLoopVfx 호출이 없는 VFX도 멀어지면 꺼지고 가까워지면 켜져야 한다면
    ///   이 함수는 "현재 기록된 마지막 위치"가 필요하다.
    /// - 지금 구조는 SetLoopVfx가 매 틱/프레임 호출되어 위치가 들어온다는 전제라
    ///   여기서는 '현재 인스턴스 위치' 기준으로만 ON/OFF 한다.
    /// </summary>
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
        float du = r.width / tw / grid;
        float dv = r.height / th / grid;
        float u0 = r.x / tw, v0 = r.y / th;

        List<Mesh> list = new List<Mesh>(grid * grid);
        for (int gy = 0; gy < grid; gy++)
        for (int gx = 0; gx < grid; gx++)
        {
            float ua = u0 + du * gx, ub = ua + du;
            float va = v0 + dv * gy, vb = va + dv;

            Mesh m = new Mesh();
            m.vertices = new[] {
                new Vector3(-0.5f,-0.5f,0), new Vector3( 0.5f,-0.5f,0),
                new Vector3( 0.5f, 0.5f,0), new Vector3(-0.5f, 0.5f,0)
            };
            m.uv = new[] { new Vector2(ua,va), new Vector2(ub,va), new Vector2(ub,vb), new Vector2(ua,vb) };
            m.triangles = new[] { 0,1,2, 0,2,3 };
            list.Add(m);
        }

        arr = list.ToArray();
        _meshCache[key] = arr;
        return arr;
    }

    Material GetMat(Texture tex)
    {
        if (_matByTex.TryGetValue(tex, out var mat)) return mat;
        mat = new Material(material) { mainTexture = tex };
        _matByTex[tex] = mat;
        return mat;
    }
}
