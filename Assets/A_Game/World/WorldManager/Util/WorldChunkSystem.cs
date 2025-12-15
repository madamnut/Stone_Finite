using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// 청크 풀 / 로드 / 언로드 / 타일맵 리프레시 / 라이트 메쉬까지 담당하는 시스템
/// (WorldManager에서 의존성만 주입해서 사용)
///
/// WorldData 구조 전제:
///   bg / solid / fluid / light
///
/// Chunk 컴포넌트 전제(필드명):
///   - Tilemap bgTilemap;
///   - Tilemap solidTilemap;
///   - Tilemap fluidTilemap;
///   - TileBase[] bgBuffer, solidBuffer, fluidBuffer;
///   - bool bgDirty, solidDirty, fluidDirty, lightDirty;
///   - lightMeshFilter/lightColors 등은 기존과 동일
///
/// Fluid Mask 전제(Chunk.cs에 추가된 필드):
///   - Texture2D fluidTypeTex, fluidAmountTex (16x16)
///   - Color32[] fluidTypePixels, fluidAmtPixels (256)
///   - MaterialPropertyBlock fluidMpb
///   - TilemapRenderer fluidRenderer
///
/// Fluid 스프라이트 전제:
///   Water_1 ~ Water_16 (amount 1..128 → 1..16, 8단위)
///
/// 타일 제공 정책(고정):
/// - BG: collider 없음
/// - Solid(FG): collidable=true면 collider Sprite, 아니면 None
/// - Fluid: collider 항상 있음(Trigger는 TilemapCollider2D 인스펙터에서)
/// - 타일 생성/캐싱은 CellLibrary가 담당 (WorldChunkSystem은 Tile을 “요청”만)
/// </summary>
public class WorldChunkSystem
{
    // ───────── 기본 참조/설정 ─────────
    private readonly int worldWidth;
    private readonly int worldHeight;
    private readonly int chunkSize;
    private readonly int chunkRadius;
    private readonly int maxLoadsPerFrame;

    private readonly WorldData worldMap;
    private readonly GameObject chunkPrefab;
    private readonly Transform chunkRoot;

    private readonly CellLibrary cellLibrary;

    // (현재 스크립트 내에선 직접 호출하진 않지만, 외부 정책상 유지)
    private readonly System.Action<int, int> recalcLightAt;

    // 라이트 메쉬 계산용 상수 (0~15)
    private const byte NAT_MAX = 15;
    private const byte ART_MAX = 15;

    // 시간에 따라 바뀌는 전역 밝기 오프셋 (0~15)
    private byte globalBrightnessOffset = 0;

    // ───────── 풀 / 로드 큐 / 활성 청크 ─────────
    private readonly Queue<GameObject> chunkPool = new();
    private readonly List<Vector2Int> loadList = new();
    private int loadIndex = 0;
    private readonly List<Vector2Int> unloadList = new();
    private readonly HashSet<Vector2Int> currentNeeded = new();
    private readonly Dictionary<Vector2Int, GameObject> activeChunks = new();

    // ───────── 더티 청크 집합 ─────────
    private readonly HashSet<Vector2Int> solidDirtyChunks = new();
    private readonly HashSet<Vector2Int> fluidDirtyChunks = new();
    private readonly HashSet<Vector2Int> bgDirtyChunks = new();
    private readonly HashSet<Vector2Int> lightDirtyChunks = new();

    private bool isLoading = false;
    private Vector2Int lastPlayerChunk = Vector2Int.zero;

    public IReadOnlyDictionary<Vector2Int, GameObject> ActiveChunks => activeChunks;

    // ───────── 생성자 ─────────
    public WorldChunkSystem(
        int worldWidth,
        int worldHeight,
        int chunkSize,
        int chunkRadius,
        int maxLoadsPerFrame,
        WorldData worldMap,
        GameObject chunkPrefab,
        Transform chunkRoot,
        CellLibrary cellLibrary,
        System.Action<int, int> recalcLightAt
    )
    {
        this.worldWidth = worldWidth;
        this.worldHeight = worldHeight;
        this.chunkSize = chunkSize;
        this.chunkRadius = chunkRadius;
        this.maxLoadsPerFrame = maxLoadsPerFrame;

        this.worldMap = worldMap;
        this.chunkPrefab = chunkPrefab;
        this.chunkRoot = chunkRoot;

        this.cellLibrary = cellLibrary;
        this.recalcLightAt = recalcLightAt;
    }

    // ───────── 외부에서 세팅/호출할 API ─────────

    /// <summary>초기 청크 풀 생성 (WorldManager.Awake 에서 호출)</summary>
    public void InitializePool(int initialPoolSize)
    {
        for (int i = 0; i < initialPoolSize; i++)
        {
            var go = Object.Instantiate(chunkPrefab, chunkRoot);
            go.SetActive(false);
            chunkPool.Enqueue(go);
        }
    }

    /// <summary>Awake 후, 현재 플레이어 위치 기준으로 lastPlayerChunk 초기화</summary>
    public void ResetLastPlayerChunk(Vector3 playerPosition)
    {
        lastPlayerChunk = GetPlayerChunk(playerPosition);
    }

    /// <summary>시간에 따른 전역 밝기 오프셋(WorldManager에서 계산) 값을 반영</summary>
    public void SetGlobalBrightnessOffset(byte offset)
    {
        globalBrightnessOffset = offset;
    }

    /// <summary>
    /// 플레이어 위치 기준으로 청크 로드/언로드 큐를 구성하고,
    /// 필요 시 코루틴으로 실제 로드를 진행한다.
    /// </summary>
    public void UpdateVisibleChunks(Vector3 playerPosition, MonoBehaviour coroutineHost)
    {
        Vector2Int playerChunk = GetPlayerChunk(playerPosition);

        // 같은 청크에 머물러 있고, 로드 큐도 비었고, 이미 청크가 떠 있으면 계산 스킵
        if (playerChunk == lastPlayerChunk &&
            loadList.Count == 0 &&
            activeChunks.Count > 0)
            return;

        // 순간이동 수준으로 멀리 이동했으면 로드 큐 초기화
        if ((playerChunk - lastPlayerChunk).sqrMagnitude > (chunkRadius * chunkRadius * 4))
        {
            loadList.Clear();
            loadIndex = 0;
        }
        lastPlayerChunk = playerChunk;

        // 유효 청크 범위 계산
        int cxMin = 0;
        int cyMin = 0;
        int cxMax = Mathf.Max(0, (worldWidth - 1) / chunkSize);
        int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);

        // 필요한 청크 집합 구성
        currentNeeded.Clear();
        for (int dy = -chunkRadius; dy <= chunkRadius; dy++)
        for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
        {
            int cx = playerChunk.x + dx;
            int cy = playerChunk.y + dy;
            if (cx < cxMin || cy < cyMin || cx > cxMax || cy > cyMax) continue;
            currentNeeded.Add(new Vector2Int(cx, cy));
        }

        // 언로드 대상 계산
        unloadList.Clear();
        foreach (var coord in activeChunks.Keys)
            if (!currentNeeded.Contains(coord)) unloadList.Add(coord);

        foreach (var coord in unloadList)
        {
            ReturnToPool(activeChunks[coord]);
            activeChunks.Remove(coord);

            // 혹시 남아있을 수 있는 더티 기록 제거
            bgDirtyChunks.Remove(coord);
            solidDirtyChunks.Remove(coord);
            fluidDirtyChunks.Remove(coord);
            lightDirtyChunks.Remove(coord);
        }

        // 로드 대상 리스트 재구성 (아직 없는 청크만)
        loadList.Clear();
        foreach (var c in currentNeeded)
        {
            if (!activeChunks.ContainsKey(c))
                loadList.Add(c);
        }

        // 플레이어와의 거리 기준 정렬 (가장 가까운 청크부터)
        loadList.Sort((a, b) =>
        {
            int ax = a.x - playerChunk.x;
            int ay = a.y - playerChunk.y;
            int bx = b.x - playerChunk.x;
            int by = b.y - playerChunk.y;

            int da2 = ax * ax + ay * ay;
            int db2 = bx * bx + by * by;
            return da2.CompareTo(db2);
        });

        loadIndex = 0;

        if (!isLoading && loadList.Count > 0)
            coroutineHost.StartCoroutine(ProcessLoadQueue());
    }

    /// <summary>
    /// 더티 플래그가 켜진 청크의 타일/라이트를 실제로 다시 세팅.
    /// (WorldManager의 FixedUpdate 등에서 호출)
    /// </summary>
    public void ProcessDirtyChunks()
    {
        if (solidDirtyChunks.Count == 0 &&
            fluidDirtyChunks.Count == 0 &&
            bgDirtyChunks.Count == 0 &&
            lightDirtyChunks.Count == 0)
            return;

        // BG
        if (bgDirtyChunks.Count > 0)
        {
            foreach (var coord in bgDirtyChunks)
            {
                if (!activeChunks.TryGetValue(coord, out var go)) continue;
                var c = go.GetComponent<Chunk>();
                if (!c.bgDirty) continue;

                RefreshChunkLayer(coord, LayerType.BG);
                c.bgDirty = false;
            }
            bgDirtyChunks.Clear();
        }

        // Solid
        if (solidDirtyChunks.Count > 0)
        {
            foreach (var coord in solidDirtyChunks)
            {
                if (!activeChunks.TryGetValue(coord, out var go)) continue;
                var c = go.GetComponent<Chunk>();
                if (!c.solidDirty) continue;

                RefreshChunkLayer(coord, LayerType.Solid);
                c.solidDirty = false;
            }
            solidDirtyChunks.Clear();
        }

        // Fluid
        if (fluidDirtyChunks.Count > 0)
        {
            foreach (var coord in fluidDirtyChunks)
            {
                if (!activeChunks.TryGetValue(coord, out var go)) continue;
                var c = go.GetComponent<Chunk>();
                if (!c.fluidDirty) continue;

                RefreshChunkLayer(coord, LayerType.Fluid);
                c.fluidDirty = false;
            }
            fluidDirtyChunks.Clear();
        }

        // Light
        if (lightDirtyChunks.Count > 0)
        {
            foreach (var coord in lightDirtyChunks)
            {
                if (!activeChunks.TryGetValue(coord, out var go)) continue;
                var c = go.GetComponent<Chunk>();
                if (!c.lightDirty) continue;

                RefreshLightLayer(coord);
                c.lightDirty = false;
            }
            lightDirtyChunks.Clear();
        }
    }

    /// <summary>
    /// 월드 좌표 기준으로 해당하는 청크를 dirty 표시.
    /// </summary>
    public void MarkChunkDirty(
        int worldX,
        int worldY,
        bool markSolid,
        bool markBG = false,
        bool markFluid = false
    )
    {
        int cx = Mathf.FloorToInt(worldX / (float)chunkSize);
        int cy = Mathf.FloorToInt(worldY / (float)chunkSize);
        var coord = new Vector2Int(cx, cy);

        if (!activeChunks.TryGetValue(coord, out var go)) return;
        var c = go.GetComponent<Chunk>();

        if (markSolid)
        {
            c.solidDirty = true;
            solidDirtyChunks.Add(coord);
        }
        if (markFluid)
        {
            c.fluidDirty = true;
            fluidDirtyChunks.Add(coord);
        }
        if (markBG)
        {
            c.bgDirty = true;
            bgDirtyChunks.Add(coord);
        }
    }

    /// <summary>
    /// 월드 좌표 1칸이 속한 청크의 lightDirty를 켠다.
    /// </summary>
    public void MarkLightDirtyCell(int worldX, int worldY)
    {
        int x = Mathf.Clamp(worldX, 0, worldWidth - 1);
        int y = Mathf.Clamp(worldY, 0, worldHeight - 1);

        int cx = x / chunkSize;
        int cy = y / chunkSize;

        var coord = new Vector2Int(cx, cy);
        if (!activeChunks.TryGetValue(coord, out var go)) return;

        var c = go.GetComponent<Chunk>();
        c.lightDirty = true;
        lightDirtyChunks.Add(coord);
    }

    public void MarkLightDirtyCells(List<Vector2Int> cells)
    {
        for (int i = 0; i < cells.Count; i++)
        {
            var p = cells[i];
            MarkLightDirtyCell(p.x, p.y);
        }
    }

    public void MarkLightDirtyRect(int x, int y, int w, int h)
    {
        int x0 = Mathf.Clamp(x, 0, worldWidth - 1);
        int y0 = Mathf.Clamp(y, 0, worldHeight - 1);
        int x1 = Mathf.Clamp(x + w - 1, 0, worldWidth - 1);
        int y1 = Mathf.Clamp(y + h - 1, 0, worldHeight - 1);

        int cx0 = x0 / chunkSize, cy0 = y0 / chunkSize;
        int cx1 = x1 / chunkSize, cy1 = y1 / chunkSize;

        for (int cy = cy0; cy <= cy1; cy++)
        for (int cx = cx0; cx <= cx1; cx++)
        {
            var coord = new Vector2Int(cx, cy);
            if (!activeChunks.TryGetValue(coord, out var go)) continue;

            var c = go.GetComponent<Chunk>();
            c.lightDirty = true;
            lightDirtyChunks.Add(coord);
        }
    }

    public void MarkAllChunksLightDirty()
    {
        foreach (var kv in activeChunks)
        {
            var coord = kv.Key;
            var c = kv.Value.GetComponent<Chunk>();
            c.lightDirty = true;
            lightDirtyChunks.Add(coord);
        }
    }

    // ───────── 내부 구현 ─────────

    private IEnumerator ProcessLoadQueue()
    {
        isLoading = true;

        int cxMin = 0, cyMin = 0;
        int cxMax = Mathf.Max(0, (worldWidth - 1) / chunkSize);
        int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);

        int loads = 0;
        while (loads < maxLoadsPerFrame && loadIndex < loadList.Count)
        {
            var coord = loadList[loadIndex++];
            if (!currentNeeded.Contains(coord)) continue;
            if (coord.x < cxMin || coord.y < cyMin || coord.x > cxMax || coord.y > cyMax) continue;

            CreateChunk(coord);
            loads++;
        }

        if (loadIndex >= loadList.Count)
        {
            loadList.Clear();
            loadIndex = 0;
        }

        yield return null;
        isLoading = false;
    }

    private Vector2Int GetPlayerChunk(Vector3 p)
    {
        return new Vector2Int(
            Mathf.FloorToInt(p.x / chunkSize),
            Mathf.FloorToInt(p.y / chunkSize)
        );
    }

    private GameObject GetFromPool()
    {
        if (chunkPool.Count > 0) return chunkPool.Dequeue();
        var go = Object.Instantiate(chunkPrefab, chunkRoot);
        go.SetActive(false);
        return go;
    }

    private void ReturnToPool(GameObject go)
    {
        go.SetActive(false);
        chunkPool.Enqueue(go);
    }

    private void CreateChunk(Vector2Int coord)
    {
        int cxMax = Mathf.Max(0, (worldWidth - 1) / chunkSize);
        int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);
        if (coord.x < 0 || coord.y < 0 || coord.x > cxMax || coord.y > cyMax) return;

        var go = GetFromPool();
        go.SetActive(true);
        go.name = $"Chunk_{coord.x}_{coord.y}";
        go.transform.localPosition = new Vector3(coord.x * chunkSize, coord.y * chunkSize, 0f);

        var c = go.GetComponent<Chunk>();

        var bgBuf = c.bgBuffer;
        var solidBuf = c.solidBuffer;
        var fluidBuf = c.fluidBuffer;

        int size = chunkSize * chunkSize;
        for (int i = 0; i < size; i++)
        {
            bgBuf[i] = null;
            solidBuf[i] = null;
            fluidBuf[i] = null;
        }

        var bounds = new BoundsInt(0, 0, 0, chunkSize, chunkSize, 1);

        for (int y = 0; y < chunkSize; y++)
        for (int x = 0; x < chunkSize; x++)
        {
            int wx = coord.x * chunkSize + x;
            int wy = coord.y * chunkSize + y;
            int idx = y * chunkSize + x;

            if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight)
                continue;

            // BG: 항상 collider 없음
            bgBuf[idx] = cellLibrary.GetBgTile(worldMap.bg[wx, wy]);

            // Solid
            var s = worldMap.solid[wx, wy];
            solidBuf[idx] = (s.id != 0) ? cellLibrary.GetSolidTile(s.id, s.meta) : null;

            // Fluid: collidable solid이면 표시 안 함
            bool solidCollidable = false;
            if (s.id != 0)
                solidCollidable = (cellLibrary.GetSolidFlags(s.id) & CellLibrary.SolidFlags.Collidable) != 0;

            if (solidCollidable)
            {
                fluidBuf[idx] = null;
            }
            else
            {
                var f = worldMap.fluid[wx, wy];
                fluidBuf[idx] = (f.id != 0 && f.amount > 0) ? cellLibrary.GetFluidTile(f.id, f.amount) : null;
            }
        }

        c.bgTilemap.SetTilesBlock(bounds, bgBuf);
        c.solidTilemap.SetTilesBlock(bounds, solidBuf);
        c.fluidTilemap.SetTilesBlock(bounds, fluidBuf);

        // ===== Fluid Mask: 초기 1회 굽기 + MPB 적용 =====
        for (int y = 0; y < chunkSize; y++)
        for (int x = 0; x < chunkSize; x++)
        {
            int wx = coord.x * chunkSize + x;
            int wy = coord.y * chunkSize + y;
            int idx = y * chunkSize + x;

            if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight)
            {
                c.fluidTypePixels[idx] = new Color32(0, 0, 0, 255);
                c.fluidAmtPixels[idx] = new Color32(0, 0, 0, 255);
                continue;
            }

            var s2 = worldMap.solid[wx, wy];
            bool s2Collidable = false;
            if (s2.id != 0)
                s2Collidable = (cellLibrary.GetSolidFlags(s2.id) & CellLibrary.SolidFlags.Collidable) != 0;

            if (s2Collidable)
            {
                c.fluidTypePixels[idx] = new Color32(0, 0, 0, 255);
                c.fluidAmtPixels[idx] = new Color32(0, 0, 0, 255);
                continue;
            }

            var f2 = worldMap.fluid[wx, wy];
            byte type = (byte)((f2.id != 0 && f2.amount > 0) ? Mathf.Min((int)f2.id, 255) : 0); // fluidId == typeIndex
            byte amt  = (byte)((f2.id != 0 && f2.amount > 0) ? f2.amount : 0);

            c.fluidTypePixels[idx] = new Color32(type, 0, 0, 255);
            c.fluidAmtPixels[idx]  = new Color32(amt, 0, 0, 255);
        }

        c.fluidTypeTex.SetPixels32(c.fluidTypePixels);
        c.fluidTypeTex.Apply(false, false);

        c.fluidAmountTex.SetPixels32(c.fluidAmtPixels);
        c.fluidAmountTex.Apply(false, false);

        Vector3 origin = go.transform.position;

        c.fluidRenderer.GetPropertyBlock(c.fluidMpb);
        c.fluidMpb.SetTexture("_TypeTex", c.fluidTypeTex);
        c.fluidMpb.SetTexture("_AmountTex", c.fluidAmountTex);
        c.fluidMpb.SetVector("_ChunkOriginWS", new Vector4(origin.x, origin.y, 0f, 0f));
        c.fluidRenderer.SetPropertyBlock(c.fluidMpb);

        // Solid collider 갱신
        var solidColl = c.solidTilemap.GetComponent<TilemapCollider2D>();
        if (solidColl != null)
        {
            c.solidTilemap.RefreshAllTiles();
            solidColl.ProcessTilemapChanges();
        }

        // Fluid collider 갱신 (Trigger는 인스펙터에서)
        var fluidColl = c.fluidTilemap.GetComponent<TilemapCollider2D>();
        if (fluidColl != null)
        {
            c.fluidTilemap.RefreshAllTiles();
            fluidColl.ProcessTilemapChanges();
        }

        activeChunks[coord] = go;

        // 라이트 메쉬 초기화
        RefreshLightLayer(coord);

        c.bgDirty = false;
        c.solidDirty = false;
        c.fluidDirty = false;
        c.lightDirty = false;
    }

    private enum LayerType { BG, Solid, Fluid }

    private void RefreshChunkLayer(Vector2Int coord, LayerType type)
    {
        if (!activeChunks.TryGetValue(coord, out var go)) return;

        var c = go.GetComponent<Chunk>();
        var bounds = new BoundsInt(0, 0, 0, chunkSize, chunkSize, 1);

        switch (type)
        {
            case LayerType.BG:
            {
                var buf = c.bgBuffer;
                for (int y = 0; y < chunkSize; y++)
                for (int x = 0; x < chunkSize; x++)
                {
                    int wx = coord.x * chunkSize + x;
                    int wy = coord.y * chunkSize + y;
                    int idx = y * chunkSize + x;
                    if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; continue; }
                    buf[idx] = cellLibrary.GetBgTile(worldMap.bg[wx, wy]);
                }
                c.bgTilemap.SetTilesBlock(bounds, buf);
                break;
            }

            case LayerType.Solid:
            {
                var buf = c.solidBuffer;
                for (int y = 0; y < chunkSize; y++)
                for (int x = 0; x < chunkSize; x++)
                {
                    int wx = coord.x * chunkSize + x;
                    int wy = coord.y * chunkSize + y;
                    int idx = y * chunkSize + x;
                    if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; continue; }

                    var s = worldMap.solid[wx, wy];
                    buf[idx] = (s.id != 0) ? cellLibrary.GetSolidTile(s.id, s.meta) : null;
                }

                c.solidTilemap.SetTilesBlock(bounds, buf);

                var solidColl = c.solidTilemap.GetComponent<TilemapCollider2D>();
                if (solidColl != null)
                {
                    c.solidTilemap.RefreshAllTiles();
                    solidColl.ProcessTilemapChanges();
                }

                break;
            }

            case LayerType.Fluid:
            {
                var buf = c.fluidBuffer;
                for (int y = 0; y < chunkSize; y++)
                for (int x = 0; x < chunkSize; x++)
                {
                    int wx = coord.x * chunkSize + x;
                    int wy = coord.y * chunkSize + y;
                    int idx = y * chunkSize + x;
                    if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; continue; }

                    var s = worldMap.solid[wx, wy];
                    bool solidCollidable = false;
                    if (s.id != 0)
                        solidCollidable = (cellLibrary.GetSolidFlags(s.id) & CellLibrary.SolidFlags.Collidable) != 0;

                    if (solidCollidable)
                    {
                        buf[idx] = null;
                        continue;
                    }

                    var f = worldMap.fluid[wx, wy];
                    buf[idx] = (f.id != 0 && f.amount > 0) ? cellLibrary.GetFluidTile(f.id, f.amount) : null;
                }

                c.fluidTilemap.SetTilesBlock(bounds, buf);

                // ===== Fluid Mask: Dirty 갱신 + MPB 적용 =====
                for (int y = 0; y < chunkSize; y++)
                for (int x = 0; x < chunkSize; x++)
                {
                    int wx = coord.x * chunkSize + x;
                    int wy = coord.y * chunkSize + y;
                    int idx = y * chunkSize + x;

                    if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight)
                    {
                        c.fluidTypePixels[idx] = new Color32(0, 0, 0, 255);
                        c.fluidAmtPixels[idx] = new Color32(0, 0, 0, 255);
                        continue;
                    }

                    var s2 = worldMap.solid[wx, wy];
                    bool s2Collidable = false;
                    if (s2.id != 0)
                        s2Collidable = (cellLibrary.GetSolidFlags(s2.id) & CellLibrary.SolidFlags.Collidable) != 0;

                    if (s2Collidable)
                    {
                        c.fluidTypePixels[idx] = new Color32(0, 0, 0, 255);
                        c.fluidAmtPixels[idx] = new Color32(0, 0, 0, 255);
                        continue;
                    }

                    var f2 = worldMap.fluid[wx, wy];
                    byte type2 = (byte)((f2.id != 0 && f2.amount > 0) ? Mathf.Min((int)f2.id, 255) : 0);
                    byte amt2  = (byte)((f2.id != 0 && f2.amount > 0) ? f2.amount : 0);

                    c.fluidTypePixels[idx] = new Color32(type2, 0, 0, 255);
                    c.fluidAmtPixels[idx]  = new Color32(amt2, 0, 0, 255);
                }

                c.fluidTypeTex.SetPixels32(c.fluidTypePixels);
                c.fluidTypeTex.Apply(false, false);

                c.fluidAmountTex.SetPixels32(c.fluidAmtPixels);
                c.fluidAmountTex.Apply(false, false);

                Vector3 origin2 = go.transform.position;

                c.fluidRenderer.GetPropertyBlock(c.fluidMpb);
                c.fluidMpb.SetTexture("_TypeTex", c.fluidTypeTex);
                c.fluidMpb.SetTexture("_AmountTex", c.fluidAmountTex);
                c.fluidMpb.SetVector("_ChunkOriginWS", new Vector4(origin2.x, origin2.y, 0f, 0f));
                c.fluidRenderer.SetPropertyBlock(c.fluidMpb);

                // Fluid collider 갱신 (Trigger는 인스펙터에서)
                var fluidColl = c.fluidTilemap.GetComponent<TilemapCollider2D>();
                if (fluidColl != null)
                {
                    c.fluidTilemap.RefreshAllTiles();
                    fluidColl.ProcessTilemapChanges();
                }

                break;
            }
        }
    }

    private void RefreshLightLayer(Vector2Int coord)
    {
        if (!activeChunks.TryGetValue(coord, out var go)) return;
        var c = go.GetComponent<Chunk>();

        var mesh = c.lightMeshFilter.sharedMesh;
        if (mesh == null) return;

        int vW = chunkSize + 1, vH = chunkSize + 1, vCount = vW * vH;
        var cols = (c.lightColors != null && c.lightColors.Length == vCount)
            ? c.lightColors : new Color32[vCount];

        int sx = coord.x * chunkSize;
        int sy = coord.y * chunkSize;

        for (int vy = 0; vy <= chunkSize; vy++)
        {
            for (int vx = 0; vx <= chunkSize; vx++)
            {
                int gx = sx + vx;
                int gy = sy + vy;

                float sum = 0f;
                int samples = 0;

                // 3x3 샘플
                for (int oy = -1; oy <= 1; oy++)
                for (int ox = -1; ox <= 1; ox++)
                {
                    int x = Mathf.Clamp(gx + ox, 0, worldWidth - 1);
                    int y = Mathf.Clamp(gy + oy, 0, worldHeight - 1);

                    var L = worldMap.light[x, y];

                    int ns = L.natural - globalBrightnessOffset;
                    if (ns < 0) ns = 0;

                    float n01 = ns / (float)NAT_MAX;
                    float a01 = L.artificial / (float)ART_MAX;

                    sum += Mathf.Max(n01, a01);
                    samples++;
                }

                float avg = (samples > 0) ? (sum / samples) : 0f;
                avg = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(avg));

                float A01 = 1f - avg;
                byte Ab = (byte)Mathf.RoundToInt(Mathf.Clamp01(A01) * 255f);

                cols[vy * vW + vx] = new Color32(0, 0, 0, Ab);
            }
        }

        c.lightColors = cols;
        mesh.colors32 = cols;
    }
}
