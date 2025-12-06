using System.Collections.Generic;
using UnityEngine;

//
// 엔티티 매니저
// - 청크 단위 시뮬레이션 on/off
// - Y 기준만 완전삭제
// - 엔티티 등록/해제 및 Spawn 헬퍼
//

public class EntityManager : MonoBehaviour
{
    [Header("References")]
    public Transform player;

    [Header("Chunk Culling (Simulation Chunks)")]
    [Tooltip("청크 타일 크기 (WorldManager.ChunkSize와 동일하게 유지)")]
    public int chunkSize = 16;

    [Tooltip("로딩되는 청크 반경 (WorldManager.ChunkRadius 동일)")]
    public int loadChunkRadius = 7;

    [Tooltip("시뮬레이션 청크 범위 = loadChunkRadius - simChunkMargin")]
    public int simChunkMargin = 4;

    [Tooltip("시뮬레이션 청크 컬링 활성화 여부")]
    public bool enableChunkCulling = true;

    [Tooltip("컬링 체크 주기(sec)")]
    public float checkInterval = 0.25f;
    float _timer;

    [Header("World Bounds Cleanup (완전삭제)")]
    [Tooltip("이 Y 값보다 낮으면 엔티티 완전 삭제")]
    public float minY = -50f;

    readonly List<Entity> _entities = new List<Entity>();
    public IReadOnlyList<Entity> Entities => _entities;


    //────────────────────────────────────────────
    // 엔티티 등록/해제
    //────────────────────────────────────────────

    public void Register(Entity e)
    {
        if (e == null) return;
        if (!_entities.Contains(e))
            _entities.Add(e);
    }

    public void Unregister(Entity e)
    {
        if (e == null) return;
        _entities.Remove(e);
    }

    void OnDestroy()
    {
        if (_entities == null) return;

        for (int i = _entities.Count - 1; i >= 0; i--)
        {
            if (_entities[i] == null)
                _entities.RemoveAt(i);
        }
    }


    //────────────────────────────────────────────
    // Spawn 헬퍼
    //────────────────────────────────────────────

    public T Spawn<T>(T prefab, Vector3 pos) where T : Entity
    {
        T inst = Instantiate(prefab, pos, Quaternion.identity, transform);
        Register(inst);
        return inst;
    }


    //────────────────────────────────────────────
    // Update 루프
    //────────────────────────────────────────────

    void Update()
    {
        if (_entities.Count == 0)
            return;

        _timer += Time.deltaTime;
        if (_timer < checkInterval)
            return;
        _timer = 0f;

        if (enableChunkCulling) ChunkCulling();
        Cleanup(); // Y 아래로 떨어진 엔티티 삭제
    }


    //────────────────────────────────────────────
    // 청크 기반 시뮬레이션 활성/비활성
    //
    // 엔티티의 청크 위치 (ecx, ecy)
    // 플레이어 청크 위치 (pcx, pcy)
    //
    // |ecx - pcx| <= simRadius && |ecy - pcy| <= simRadius
    //────────────────────────────────────────────

    void ChunkCulling()
    {
        if (player == null) return;
        if (chunkSize <= 0) return;

        int simRadius = loadChunkRadius - simChunkMargin;
        if (simRadius < 0) simRadius = 0;

        Vector3 p = player.position;
        int pcx = Mathf.FloorToInt(p.x / chunkSize);
        int pcy = Mathf.FloorToInt(p.y / chunkSize);

        for (int i = 0; i < _entities.Count; i++)
        {
            Entity e = _entities[i];
            if (e == null) continue;

            Vector3 pos = e.transform.position;
            int ecx = Mathf.FloorToInt(pos.x / chunkSize);
            int ecy = Mathf.FloorToInt(pos.y / chunkSize);

            int dx = ecx - pcx; if (dx < 0) dx = -dx;
            int dy = ecy - pcy; if (dy < 0) dy = -dy;

            bool active = (dx <= simRadius) && (dy <= simRadius);

            if (e.IsSimActive != active)
                e.SetSimActive(active);
        }
    }


    //────────────────────────────────────────────
    // Y 기준 완전 삭제
    //────────────────────────────────────────────

    void Cleanup()
    {
        for (int i = _entities.Count - 1; i >= 0; i--)
        {
            Entity e = _entities[i];

            if (e == null)
            {
                _entities.RemoveAt(i);
                continue;
            }

            Vector3 pos = e.transform.position;

            // Y 아래로 떨어지면 완전 삭제
            if (pos.y < minY)
            {
                Destroy(e.gameObject);
                _entities.RemoveAt(i);
            }
        }
    }
}
