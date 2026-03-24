using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Ï≤?Å¨ ?Ä / Î°úÎìú / ?∏Î°ú??/ ?Ä?ºÎßµ Î¶¨ÌîÑ?àÏãú / ?ºÏù¥???àÏù¥???çÏä§Ï≤?ÍπåÏ? ?¥Îãπ?òÎäî ?úÏä§??
/// (WorldManager?êÏÑú ?òÏ°¥?±Îßå Ï£ºÏûÖ?¥ÏÑú ?¨Ïö©)
///
/// WorldData Íµ¨Ï°∞(?†Í∑ú):
///   bg / utility(id+meta) / solid(id+meta) / fluid(id+amount) / naturalLight / artificialLight
///
/// Chunk Ïª¥Ìè¨?åÌä∏ ?ÑÏ†ú(?ÑÎìúÎ™?:
///   - Tilemap bgTilemap;
///   - Tilemap utilityTilemap;    // ??Ï∂îÍ?
///   - Tilemap solidTilemap;
///   - Tilemap platformTilemap;   // ??Ï∂îÍ? (ÏΩúÎùº?¥Îçî ?ÑÏö©, ?åÎçî OFF ?ÑÏ†ú)
///   - Tilemap liquidTilemap;     (?ÑÎìúÎ™??†Ï?)
///   - TileBase[] bgBuffer, utilityBuffer, solidBuffer, platformBuffer, liquidBuffer; // ??
///   - bool bgDirty, utilityDirty, solidDirty, platformDirty, liquidDirty, lightDirty; // ??
///
/// Liquid Mask ?ÑÏ†ú(Chunk.cs??Ï∂îÍ????ÑÎìú):
///   - Texture2D liquidTypeTex, liquidAmountTex (16x16)
///   - Color32[] liquidTypePixels, liquidAmtPixels (256)
///   - MaterialPropertyBlock liquidMpb
///   - TilemapRenderer liquidRenderer
///
/// Light Overlay ?ÑÏ†ú(Chunk.cs??Ï∂îÍ????ÑÎìú):
///   - MeshRenderer lightOverlayRenderer
///   - Texture2D lightTex (18x18)
///   - Color32[] lightPixels (18*18)
///   - MaterialPropertyBlock lightMpb
///
/// Tile ?ïÏ±Ö:
/// - BG ?Ä?? CellLibrary.GetBgTile(id)
/// - Utility ?Ä?? CellLibrary.GetUtilityTile(id, meta)                    // ??Ï∂îÍ?
/// - Solid ?Ä???úÍ∞Å): CellLibrary.GetSolidTile(id, meta)
/// - Platform ÏΩúÎùº?¥Îçî ?Ä?? CellLibrary.GetPlatformColliderTile(id, meta)  // ??Ï∂îÍ?
/// - Fluid ?Ä?? CellLibrary.GetFluidTile(fluidId, amount)
/// </summary>
using Game.Data;

namespace Game.World
{
    public class WorldChunkSystem
    {
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä Í∏∞Î≥∏ Ï∞∏Ï°∞/?§Ï†ï ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        private readonly int worldWidth;
        private readonly int worldHeight;
        private readonly int chunkSize;
        private readonly int chunkRadius;
        private readonly int maxLoadsPerFrame;
    
        private readonly WorldData worldMap;
        private readonly GameObject chunkPrefab;
        private readonly Transform chunkRoot;
    
        private readonly CellLibrary cellLibrary;
    
        // (?ÑÏû¨ ?§ÌÅ¨Î¶ΩÌä∏ ?¥Ïóê??ÏßÅÏ†ë ?∏Ï∂ú?òÏßÑ ?äÏ?Îß? ?∏Î? ?ïÏ±Ö???†Ï?)
        private readonly System.Action<int, int> recalcLightAt;
    
        // ?úÍ∞Ñ???∞Îùº Î∞îÎÄåÎäî ?ÑÏó≠ Î∞ùÍ∏∞ ?§ÌîÑ??(0~15)
        private ushort globalBrightnessOffset = 0;
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä ?Ä / Î°úÎìú ??/ ?úÏÑ± Ï≤?Å¨ ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        private readonly Queue<GameObject> chunkPool = new();
        private readonly List<Vector2Int> loadList = new();
        private int loadIndex = 0;
        private readonly List<Vector2Int> unloadList = new();
        private readonly HashSet<Vector2Int> currentNeeded = new();
        private readonly Dictionary<Vector2Int, GameObject> activeChunks = new();
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä ?îÌã∞ Ï≤?Å¨ ÏßëÌï© ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        private readonly HashSet<Vector2Int> solidDirtyChunks = new();
        private readonly HashSet<Vector2Int> platformDirtyChunks = new(); // ??Ï∂îÍ?
        private readonly HashSet<Vector2Int> liquidDirtyChunks = new();
        private readonly HashSet<Vector2Int> bgDirtyChunks = new();
        private readonly HashSet<Vector2Int> utilityDirtyChunks = new(); // ??Ï∂îÍ?
        private readonly HashSet<Vector2Int> lightDirtyChunks = new();
    
        private bool isLoading = false;
        private Vector2Int lastPlayerChunk = Vector2Int.zero;
    
        public IReadOnlyDictionary<Vector2Int, GameObject> ActiveChunks => activeChunks;
    
        const int LIGHT_MAX = 15;
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä ?ùÏÑ±???Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
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
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä ?∏Î??êÏÑú ?∏ÌåÖ/?∏Ï∂ú??API ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
    
        /// <summary>Ï¥àÍ∏∞ Ï≤?Å¨ ?Ä ?ùÏÑ± (WorldManager.Awake ?êÏÑú ?∏Ï∂ú)</summary>
        public void InitializePool(int initialPoolSize)
        {
            for (int i = 0; i < initialPoolSize; i++)
            {
                var go = Object.Instantiate(chunkPrefab, chunkRoot);
                go.SetActive(false);
                chunkPool.Enqueue(go);
            }
        }
    
        /// <summary>Awake ?? ?ÑÏû¨ ?åÎ†à?¥Ïñ¥ ?ÑÏπò Í∏∞Ï??ºÎ°ú lastPlayerChunk Ï¥àÍ∏∞??/summary>
        public void ResetLastPlayerChunk(Vector3 playerPosition)
        {
            lastPlayerChunk = GetPlayerChunk(playerPosition);
        }
    
        /// <summary>?úÍ∞Ñ???∞Î•∏ ?ÑÏó≠ Î∞ùÍ∏∞ ?§ÌîÑ??WorldManager?êÏÑú Í≥ÑÏÇ∞) Í∞íÏùÑ Î∞òÏòÅ</summary>
        public void SetGlobalBrightnessOffset(ushort offset)
        {
            globalBrightnessOffset = offset;
        }
    
        /// <summary>
        /// ?åÎ†à?¥Ïñ¥ ?ÑÏπò Í∏∞Ï??ºÎ°ú Ï≤?Å¨ Î°úÎìú/?∏Î°ú???êÎ? Íµ¨ÏÑ±?òÍ≥†,
        /// ?ÑÏöî ??ÏΩîÎ£®?¥ÏúºÎ°??§Ï†ú Î°úÎìúÎ•?ÏßÑÌñâ?úÎã§.
        /// </summary>
        public void UpdateVisibleChunks(Vector3 playerPosition, MonoBehaviour coroutineHost)
        {
            Vector2Int playerChunk = GetPlayerChunk(playerPosition);
    
            // Í∞ôÏ? Ï≤?Å¨??Î®∏Î¨º???àÍ≥†, Î°úÎìú ?êÎèÑ ÎπÑÏóàÍ≥? ?¥Î? Ï≤?Å¨Í∞Ä ???àÏúºÎ©?Í≥ÑÏÇ∞ ?§ÌÇµ
            if (playerChunk == lastPlayerChunk &&
                loadList.Count == 0 &&
                activeChunks.Count > 0)
                return;
    
            // ?úÍ∞Ñ?¥Îèô ?òÏ??ºÎ°ú Î©ÄÎ¶??¥Îèô?àÏúºÎ©?Î°úÎìú ??Ï¥àÍ∏∞??
            if ((playerChunk - lastPlayerChunk).sqrMagnitude > (chunkRadius * chunkRadius * 4))
            {
                loadList.Clear();
                loadIndex = 0;
            }
            lastPlayerChunk = playerChunk;
    
            // ?†Ìö® Ï≤?Å¨ Î≤îÏúÑ Í≥ÑÏÇ∞
            int cxMin = 0;
            int cyMin = 0;
            int cxMax = Mathf.Max(0, (worldWidth - 1) / chunkSize);
            int cyMax = Mathf.Max(0, (worldHeight - 1) / chunkSize);
    
            // ?ÑÏöî??Ï≤?Å¨ ÏßëÌï© Íµ¨ÏÑ±
            currentNeeded.Clear();
            for (int dy = -chunkRadius; dy <= chunkRadius; dy++)
            for (int dx = -chunkRadius; dx <= chunkRadius; dx++)
            {
                int cx = playerChunk.x + dx;
                int cy = playerChunk.y + dy;
                if (cx < cxMin || cy < cyMin || cx > cxMax || cy > cyMax) continue;
                currentNeeded.Add(new Vector2Int(cx, cy));
            }
    
            // ?∏Î°ú???Ä??Í≥ÑÏÇ∞
            unloadList.Clear();
            foreach (var coord in activeChunks.Keys)
                if (!currentNeeded.Contains(coord)) unloadList.Add(coord);
    
            foreach (var coord in unloadList)
            {
                ReturnToPool(activeChunks[coord]);
                activeChunks.Remove(coord);
    
                // ?πÏãú ?®ÏïÑ?àÏùÑ ???àÎäî ?îÌã∞ Í∏∞Î°ù ?úÍ±∞
                bgDirtyChunks.Remove(coord);
                utilityDirtyChunks.Remove(coord);
                solidDirtyChunks.Remove(coord);
                platformDirtyChunks.Remove(coord);
                liquidDirtyChunks.Remove(coord);
                lightDirtyChunks.Remove(coord);
            }
    
            // Î°úÎìú ?Ä??Î¶¨Ïä§???¨Íµ¨??(?ÑÏßÅ ?ÜÎäî Ï≤?Å¨Îß?
            loadList.Clear();
            foreach (var c in currentNeeded)
            {
                if (!activeChunks.ContainsKey(c))
                    loadList.Add(c);
            }
    
            // ?åÎ†à?¥Ïñ¥?Ä??Í±∞Î¶¨ Í∏∞Ï? ?ïÎ†¨ (Í∞Ä??Í∞ÄÍπåÏö¥ Ï≤?Å¨Î∂Ä??
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
        /// ?îÌã∞ ?åÎûòÍ∑∏Í? ÏºúÏßÑ Ï≤?Å¨???Ä???ºÏù¥?∏Î? ?§Ï†úÎ°??§Ïãú ?∏ÌåÖ.
        /// (WorldManager??FixedUpdate ?±Ïóê???∏Ï∂ú)
        /// </summary>
        public void ProcessDirtyChunks()
        {
            if (solidDirtyChunks.Count == 0 &&
                platformDirtyChunks.Count == 0 &&
                liquidDirtyChunks.Count == 0 &&
                bgDirtyChunks.Count == 0 &&
                utilityDirtyChunks.Count == 0 &&
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
    
            // Utility
            if (utilityDirtyChunks.Count > 0)
            {
                foreach (var coord in utilityDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.utilityDirty) continue;
    
                    RefreshChunkLayer(coord, LayerType.Utility);
                    c.utilityDirty = false;
                }
                utilityDirtyChunks.Clear();
            }
    
            // Solid (+ PlatformCollider Í∞ôÏù¥ Í∞±Ïã†)
            if (solidDirtyChunks.Count > 0)
            {
                foreach (var coord in solidDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.solidDirty && !c.platformDirty) continue;
    
                    RefreshChunkLayer(coord, LayerType.Solid);
                    c.solidDirty = false;
                    c.platformDirty = false;
                }
                solidDirtyChunks.Clear();
    
                // ???¨Í∏∞??platformDirtyChunksÎ•?"?ÑÎ? ?úÍ±∞"?òÎ†§Î©?ClearÍ∞Ä ÎßûÎã§.
                // (Solid Í∞±Ïã†?êÏÑú platform???®Íªò Í∞±Ïã†?àÏúºÎØÄÎ°?
                platformDirtyChunks.Clear();
            }
    
            // (?àÏ†Ñ?•Ïπò) PlatformDirtyÎß??®ÏïÑ?àÏùÑ Í≤ΩÏö∞
            if (platformDirtyChunks.Count > 0)
            {
                foreach (var coord in platformDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.platformDirty) continue;
    
                    RefreshChunkLayer(coord, LayerType.Platform);
                    c.platformDirty = false;
                }
                platformDirtyChunks.Clear();
            }
    
            // Liquid
            if (liquidDirtyChunks.Count > 0)
            {
                foreach (var coord in liquidDirtyChunks)
                {
                    if (!activeChunks.TryGetValue(coord, out var go)) continue;
                    var c = go.GetComponent<Chunk>();
                    if (!c.liquidDirty) continue;
    
                    RefreshChunkLayer(coord, LayerType.Liquid);
                    c.liquidDirty = false;
                }
                liquidDirtyChunks.Clear();
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
        /// ?îÎìú Ï¢åÌëú Í∏∞Ï??ºÎ°ú ?¥Îãπ?òÎäî Ï≤?Å¨Î•?dirty ?úÏãú.
        /// </summary>
        public void MarkChunkDirty(
            int worldX,
            int worldY,
            bool markSolid,
            bool markBG = false,
            bool markLiquid = false,
            bool markUtility = false
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
                c.platformDirty = true;
                solidDirtyChunks.Add(coord);
                platformDirtyChunks.Add(coord);
            }
            if (markLiquid)
            {
                c.liquidDirty = true;
                liquidDirtyChunks.Add(coord);
            }
            if (markBG)
            {
                c.bgDirty = true;
                bgDirtyChunks.Add(coord);
            }
            if (markUtility)
            {
                c.utilityDirty = true;
                utilityDirtyChunks.Add(coord);
            }
        }
    
        /// <summary>
        /// ?îÎìú Ï¢åÌëú 1Ïπ∏Ïù¥ ?çÌïú Ï≤?Å¨??lightDirtyÎ•?Ïº†Îã§.
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
    
        // ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä ?¥Î? Íµ¨ÌòÑ ?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
    
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
            // ???îÎìúÍ∞Ä "?Ä=1?†Îãõ" ?ÑÏ†ú?ºÎ©¥ ??Í≥ÑÏÇ∞??ÎßûÎã§(Í∏∞Ï°¥ ?†Ï?).
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
            var utilBuf = c.utilityBuffer;
            var solidBuf = c.solidBuffer;
            var platBuf = c.platformBuffer;
            var liqBuf = c.liquidBuffer;
    
            int size = chunkSize * chunkSize;
            for (int i = 0; i < size; i++)
            {
                bgBuf[i] = null;
                utilBuf[i] = null;
                solidBuf[i] = null;
                platBuf[i] = null;
                liqBuf[i] = null;
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
    
                // BG
                bgBuf[idx] = cellLibrary.GetBgTile(worldMap.bg[wx, wy]);
    
                // Utility
                var u = worldMap.utility[wx, wy];
                utilBuf[idx] = (u.id != 0) ? cellLibrary.GetUtilityTile(u.id, u.meta) : null;
    
                // Solid(?úÍ∞Å) + PlatformCollider(?ÑÏö© ?Ä?ºÎßµ)
                var s = worldMap.solid[wx, wy];
                if (s.id != 0)
                {
                    solidBuf[idx] = cellLibrary.GetSolidTile(s.id, s.meta);
    
                    platBuf[idx] = cellLibrary.IsPlatform(s.id)
                        ? cellLibrary.GetPlatformColliderTile(s.id, s.meta)
                        : null;
                }
                else
                {
                    solidBuf[idx] = null;
                    platBuf[idx] = null;
                }
    
                // Liquid/Fluid
                var f = worldMap.fluid[wx, wy];
                liqBuf[idx] = (f.id != 0 && f.amount > 0)
                    ? cellLibrary.GetFluidTile(f.id, f.amount)
                    : null;
            }
    
            c.bgTilemap.SetTilesBlock(bounds, bgBuf);
            if (c.utilityTilemap != null) c.utilityTilemap.SetTilesBlock(bounds, utilBuf);
            c.solidTilemap.SetTilesBlock(bounds, solidBuf);
            if (c.platformTilemap != null) c.platformTilemap.SetTilesBlock(bounds, platBuf);
            c.liquidTilemap.SetTilesBlock(bounds, liqBuf);
    
            // ===== Liquid Mask: Ï¥àÍ∏∞ 1??ÍµΩÍ∏∞ + MPB ?ÅÏö© =====
            for (int y = 0; y < chunkSize; y++)
            for (int x = 0; x < chunkSize; x++)
            {
                int wx = coord.x * chunkSize + x;
                int wy = coord.y * chunkSize + y;
                int idx = y * chunkSize + x;
    
                if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight)
                {
                    c.liquidTypePixels[idx] = new Color32(0, 0, 0, 255);
                    c.liquidAmtPixels[idx] = new Color32(0, 0, 0, 255);
                    continue;
                }
    
                var f2 = worldMap.fluid[wx, wy];
                byte type = (byte)((f2.id != 0 && f2.amount > 0) ? Mathf.Min((int)f2.id, 255) : 0);
                byte amt = (byte)((f2.id != 0 && f2.amount > 0) ? f2.amount : 0);
    
                c.liquidTypePixels[idx] = new Color32(type, 0, 0, 255);
                c.liquidAmtPixels[idx] = new Color32(amt, 0, 0, 255);
            }
    
            c.liquidTypeTex.SetPixels32(c.liquidTypePixels);
            c.liquidTypeTex.Apply(false, false);
    
            c.liquidAmountTex.SetPixels32(c.liquidAmtPixels);
            c.liquidAmountTex.Apply(false, false);
    
            Vector3 origin = go.transform.position;
    
            c.liquidRenderer.GetPropertyBlock(c.liquidMpb);
            c.liquidMpb.SetTexture("_TypeTex", c.liquidTypeTex);
            c.liquidMpb.SetTexture("_AmountTex", c.liquidAmountTex);
            c.liquidMpb.SetVector("_ChunkOriginWS", new Vector4(origin.x, origin.y, 0f, 0f));
            c.liquidRenderer.SetPropertyBlock(c.liquidMpb);
    
            // Solid collider Í∞±Ïã†
            var coll = c.solidTilemap.GetComponent<TilemapCollider2D>();
            if (coll != null)
            {
                c.solidTilemap.RefreshAllTiles();
                coll.ProcessTilemapChanges();
            }
    
            // Platform collider Í∞±Ïã†
            if (c.platformTilemap != null)
            {
                var pColl = c.platformTilemap.GetComponent<TilemapCollider2D>();
                if (pColl != null)
                {
                    c.platformTilemap.RefreshAllTiles();
                    pColl.ProcessTilemapChanges();
                }
            }
    
            activeChunks[coord] = go;
    
            // ?ºÏù¥??Ï¥àÍ∏∞???çÏä§Ï≤?
            RefreshLightLayer(coord);
    
            c.bgDirty = false;
            c.utilityDirty = false;
            c.solidDirty = false;
            c.platformDirty = false;
            c.liquidDirty = false;
            c.lightDirty = false;
        }
    
        private enum LayerType { BG, Utility, Solid, Platform, Liquid }
    
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
    
                case LayerType.Utility:
                {
                    if (c.utilityTilemap == null) break;
    
                    var buf = c.utilityBuffer;
                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; continue; }
    
                        var u = worldMap.utility[wx, wy];
                        buf[idx] = (u.id != 0) ? cellLibrary.GetUtilityTile(u.id, u.meta) : null;
                    }
    
                    c.utilityTilemap.SetTilesBlock(bounds, buf);
                    break;
                }
    
                case LayerType.Solid:
                {
                    var buf = c.solidBuffer;
                    var pbuf = c.platformBuffer;
    
                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; pbuf[idx] = null; continue; }
    
                        var s = worldMap.solid[wx, wy];
                        if (s.id == 0)
                        {
                            buf[idx] = null;
                            pbuf[idx] = null;
                            continue;
                        }
    
                        buf[idx] = cellLibrary.GetSolidTile(s.id, s.meta);
                        pbuf[idx] = cellLibrary.IsPlatform(s.id)
                            ? cellLibrary.GetPlatformColliderTile(s.id, s.meta)
                            : null;
                    }
    
                    c.solidTilemap.SetTilesBlock(bounds, buf);
                    if (c.platformTilemap != null) c.platformTilemap.SetTilesBlock(bounds, pbuf);
    
                    var coll2 = c.solidTilemap.GetComponent<TilemapCollider2D>();
                    if (coll2 != null)
                    {
                        c.solidTilemap.RefreshAllTiles();
                        coll2.ProcessTilemapChanges();
                    }
    
                    if (c.platformTilemap != null)
                    {
                        var pColl2 = c.platformTilemap.GetComponent<TilemapCollider2D>();
                        if (pColl2 != null)
                        {
                            c.platformTilemap.RefreshAllTiles();
                            pColl2.ProcessTilemapChanges();
                        }
                    }
    
                    break;
                }
    
                case LayerType.Platform:
                {
                    if (c.platformTilemap == null) break;
    
                    var pbuf = c.platformBuffer;
                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { pbuf[idx] = null; continue; }
    
                        var s = worldMap.solid[wx, wy];
                        pbuf[idx] = (s.id != 0 && cellLibrary.IsPlatform(s.id))
                            ? cellLibrary.GetPlatformColliderTile(s.id, s.meta)
                            : null;
                    }
    
                    c.platformTilemap.SetTilesBlock(bounds, pbuf);
    
                    var pColl = c.platformTilemap.GetComponent<TilemapCollider2D>();
                    if (pColl != null)
                    {
                        c.platformTilemap.RefreshAllTiles();
                        pColl.ProcessTilemapChanges();
                    }
    
                    break;
                }
    
                case LayerType.Liquid:
                {
                    var buf = c.liquidBuffer;
                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight) { buf[idx] = null; continue; }
    
                        var f = worldMap.fluid[wx, wy];
                        buf[idx] = (f.id != 0 && f.amount > 0)
                            ? cellLibrary.GetFluidTile(f.id, f.amount)
                            : null;
                    }
    
                    c.liquidTilemap.SetTilesBlock(bounds, buf);
    
                    // ===== Liquid Mask: Dirty Í∞±Ïã† + MPB ?ÅÏö© =====
                    for (int y = 0; y < chunkSize; y++)
                    for (int x = 0; x < chunkSize; x++)
                    {
                        int wx = coord.x * chunkSize + x;
                        int wy = coord.y * chunkSize + y;
                        int idx = y * chunkSize + x;
    
                        if ((uint)wx >= (uint)worldWidth || (uint)wy >= (uint)worldHeight)
                        {
                            c.liquidTypePixels[idx] = new Color32(0, 0, 0, 255);
                            c.liquidAmtPixels[idx] = new Color32(0, 0, 0, 255);
                            continue;
                        }
    
                        var f2 = worldMap.fluid[wx, wy];
                        byte type2 = (byte)((f2.id != 0 && f2.amount > 0) ? Mathf.Min((int)f2.id, 255) : 0);
                        byte amt2 = (byte)((f2.id != 0 && f2.amount > 0) ? f2.amount : 0);
    
                        c.liquidTypePixels[idx] = new Color32(type2, 0, 0, 255);
                        c.liquidAmtPixels[idx] = new Color32(amt2, 0, 0, 255);
                    }
    
                    c.liquidTypeTex.SetPixels32(c.liquidTypePixels);
                    c.liquidTypeTex.Apply(false, false);
    
                    c.liquidAmountTex.SetPixels32(c.liquidAmtPixels);
                    c.liquidAmountTex.Apply(false, false);
    
                    Vector3 origin2 = go.transform.position;
    
                    c.liquidRenderer.GetPropertyBlock(c.liquidMpb);
                    c.liquidMpb.SetTexture("_TypeTex", c.liquidTypeTex);
                    c.liquidMpb.SetTexture("_AmountTex", c.liquidAmountTex);
                    c.liquidMpb.SetVector("_ChunkOriginWS", new Vector4(origin2.x, origin2.y, 0f, 0f));
                    c.liquidRenderer.SetPropertyBlock(c.liquidMpb);
    
                    break;
                }
            }
        }
    
        private void RefreshLightLayer(Vector2Int coord)
        {
            if (!activeChunks.TryGetValue(coord, out var go)) return;
            var c = go.GetComponent<Chunk>();
    
            if (c.lightOverlayRenderer == null) return;
            if (c.lightTex == null || c.lightPixels == null) return;
    
            int L = chunkSize + 2; // 18
            if (c.lightPixels.Length != L * L) return;
    
            int sx = coord.x * chunkSize;
            int sy = coord.y * chunkSize;
    
            for (int ty = 0; ty < L; ty++)
            {
                for (int tx = 0; tx < L; tx++)
                {
                    int gx = Mathf.Clamp(sx + (tx - 1), 0, worldWidth - 1);
                    int gy = Mathf.Clamp(sy + (ty - 1), 0, worldHeight - 1);
    
                    ushort nat = worldMap.naturalLight[gx, gy];
                    ushort art = worldMap.artificialLight[gx, gy];
    
                    int ns = (int)nat - (int)globalBrightnessOffset;
                    if (ns < 0) ns = 0;
                    if (ns > LIGHT_MAX) ns = LIGHT_MAX;
    
                    int a = (int)art;
                    if (a < 0) a = 0;
                    if (a > LIGHT_MAX) a = LIGHT_MAX;
    
                    float n01 = ns / 15f;
                    float a01 = a / 15f;
    
                    float b01 = Mathf.Max(n01, a01);
                    b01 = Mathf.Clamp01(b01);
    
                    float A01 = 1f - b01;
                    byte Ab = (byte)Mathf.RoundToInt(Mathf.Clamp01(A01) * 255f);
    
                    c.lightPixels[ty * L + tx] = new Color32(0, 0, 0, Ab);
                }
            }
    
            c.lightTex.SetPixels32(c.lightPixels);
            c.lightTex.Apply(false, false);
    
            c.lightOverlayRenderer.GetPropertyBlock(c.lightMpb);
            c.lightMpb.SetTexture("_LightTex", c.lightTex);
            c.lightOverlayRenderer.SetPropertyBlock(c.lightMpb);
        }
    }
}
