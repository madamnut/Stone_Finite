using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json; // Unique 직렬화용

/// <summary>
/// 월드 / 플레이어 / 엔티티 저장/로드 전담 시스템
/// </summary>
public static class WorldSaveSystem
{
    // 저장 포맷
    private const string SAVE_FILE        = "world.bin";   // 월드 셀/라이트/시간 전용
    private const string PLAYER_SAVE_FILE = "player.bin";  // 플레이어 위치/인벤토리 전용
    private const string ENTITY_SAVE_FILE = "entity.bin";  // 드랍 아이템 전용

    // ───────── 월드 저장 ─────────
    public static void SaveWorld(
        int width,
        int height,
        WorldData worldMap,
        long worldTick,
        HashSet<Vector2Int> tickCurr,
        HashSet<Vector2Int> tickNext,
        Player playerComp,
        Transform player,
        ItemDropper itemDropper
    )
    {
        try
        {
            string dir = WorldLoadContext.GetSavePath();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, SAVE_FILE);
            string tmp  = Path.Combine(dir, SAVE_FILE + ".tmp");

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                // 버전 없이 W, H, worldTick부터 저장
                bw.Write(width);
                bw.Write(height);
                bw.Write(worldTick);

                // bg
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    bw.Write(worldMap.bg[x, y]);

                // solid
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bw.Write(worldMap.solid[x, y].id);
                    bw.Write(worldMap.solid[x, y].hasGravity);
                }

                // deco
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bw.Write(worldMap.deco[x, y].id);
                    bw.Write((byte)worldMap.deco[x, y].depend);
                }

                // liquid
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bw.Write(worldMap.liquid[x, y].id);
                    bw.Write(worldMap.liquid[x, y].amount);
                }

                // light
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bw.Write(worldMap.light[x, y].natural);
                    bw.Write(worldMap.light[x, y].artificial);
                }

                // tick 큐 (curr / next 분리 저장)
                int currCountToWrite = (tickCurr != null) ? tickCurr.Count : 0;
                int nextCountToWrite = (tickNext != null) ? tickNext.Count : 0;

                bw.Write(currCountToWrite);
                if (currCountToWrite > 0 && tickCurr != null)
                {
                    foreach (var p in tickCurr)
                    {
                        bw.Write(p.x);
                        bw.Write(p.y);
                    }
                }

                bw.Write(nextCountToWrite);
                if (nextCountToWrite > 0 && tickNext != null)
                {
                    foreach (var p in tickNext)
                    {
                        bw.Write(p.x);
                        bw.Write(p.y);
                    }
                }
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            // 플레이어 데이터/엔티티는 별도 파일로 저장
            SavePlayerData(playerComp, player);
            SaveEntities(itemDropper);

            // 요약 로그
            long bytes = new FileInfo(path).Length;
            int slotCountLog = 0;

            Player pCompLog = playerComp;
            if (pCompLog != null && pCompLog.Inventory != null)
                slotCountLog = pCompLog.Inventory.items.Count;

            int currCountLog = (tickCurr != null) ? tickCurr.Count : 0;
            int nextCountLog = (tickNext != null) ? tickNext.Count : 0;

            Debug.Log($"[SAVE] worldBytes={bytes}, slotCount={slotCountLog}, hasPlayer={(pCompLog!=null)}, tickCurr={currCountLog}, tickNext={nextCountLog}");
            Debug.Log($"[SAVE-TICK] curr={currCountLog}, next={nextCountLog}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveWorld 실패: {e}");
        }
    }

    // ───────── 월드 로드 ─────────
    public static bool LoadWorldFromDisk(
        out WorldData loaded,
        out int width,
        out int height,
        out long loadedTick,
        HashSet<Vector2Int> tickCurr,
        HashSet<Vector2Int> tickNext
    )
    {
        loaded = default;
        width = 0;
        height = 0;
        loadedTick = 0;

        string path = Path.Combine(WorldLoadContext.GetSavePath(), SAVE_FILE);
        if (!File.Exists(path))
        {
            Debug.Log("[LOAD] file not found");
            return false;
        }

        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        long bytes = fs.Length;
        using var br = new BinaryReader(fs);

        // 버전 없이 W, H, worldTick부터 읽기
        int  w = br.ReadInt32();
        int  h = br.ReadInt32();
        long wt = br.ReadInt64();
        Debug.Log($"[LOAD] start size={w}x{h}, bytes={bytes}");

        var data = new WorldData(w, h);

        // bg
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            data.bg[x, y] = br.ReadUInt16();

        // solid
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            data.solid[x, y].id         = br.ReadUInt16();
            data.solid[x, y].hasGravity = br.ReadBoolean();
        }

        // deco
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            data.deco[x, y].id     = br.ReadUInt16();
            data.deco[x, y].depend = (DepFlags)br.ReadByte();
        }

        // liquid
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            data.liquid[x, y].id     = br.ReadUInt16();
            data.liquid[x, y].amount = br.ReadByte();
        }

        // light
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            data.light[x, y].natural     = br.ReadByte();
            data.light[x, y].artificial  = br.ReadByte();
        }

        // tick 큐 로드
        if (tickCurr != null) tickCurr.Clear();
        if (tickNext != null) tickNext.Clear();
        int loadedCurrCount = 0;
        int loadedNextCount = 0;

        if (br.BaseStream.Position < br.BaseStream.Length)
        {
            // curr
            loadedCurrCount = br.ReadInt32();
            for (int i = 0; i < loadedCurrCount; i++)
            {
                int x = br.ReadInt32();
                int y = br.ReadInt32();
                if ((uint)x >= (uint)w || (uint)y >= (uint)h) continue;
                tickCurr?.Add(new Vector2Int(x, y));
            }

            // next
            if (br.BaseStream.Position < br.BaseStream.Length)
            {
                loadedNextCount = br.ReadInt32();
                for (int i = 0; i < loadedNextCount; i++)
                {
                    int x = br.ReadInt32();
                    int y = br.ReadInt32();
                    if ((uint)x >= (uint)w || (uint)y >= (uint)h) continue;
                    tickNext?.Add(new Vector2Int(x, y));
                }
            }
        }

        width      = w;
        height     = h;
        loadedTick = wt;
        loaded     = data;

        Debug.Log($"[LOAD] success (tickCurr={loadedCurrCount}, tickNext={loadedNextCount})");
        return true;
    }

    // ───────── 플레이어 저장 ─────────
    public static void SavePlayerData(Player playerComp, Transform playerTransform)
    {
        try
        {
            string dir = WorldLoadContext.GetSavePath();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, PLAYER_SAVE_FILE);
            string tmp  = path + ".tmp";

            float px = 0f, py = 0f;
            if (playerTransform != null)
            {
                px = playerTransform.position.x;
                py = playerTransform.position.y;
            }

            // 매 세이브마다 플레이어 위치 로그 출력
            Debug.Log($"[SAVE] PlayerPos=({px:F2}, {py:F2})");

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                // 위치
                bw.Write(px);
                bw.Write(py);

                // 인벤토리
                int slotCount = 0;
                List<ItemData> slots = null;

                if (playerComp != null && playerComp.Inventory != null)
                {
                    slots = playerComp.Inventory.items;
                    slotCount = slots.Count;
                }

                bw.Write(slotCount);
                if (slotCount > 0 && slots != null)
                {
                    for (int i = 0; i < slotCount; i++)
                    {
                        var it = slots[i];
                        bool has = it != null && it.Count > 0 && !string.IsNullOrEmpty(it.ItemId);
                        bw.Write(has);
                        if (has)
                        {
                            bw.Write(it.ItemId     ?? string.Empty);
                            bw.Write(it.Name       ?? string.Empty);
                            bw.Write(it.SpriteName ?? string.Empty);
                            bw.Write(it.ItemType   ?? string.Empty);
                            bw.Write(it.MaxStack);
                            bw.Write(it.Count);

                            string uniqueJson = it.Unique != null
                                ? JsonConvert.SerializeObject(it.Unique)
                                : string.Empty;
                            bw.Write(uniqueJson ?? string.Empty);
                        }
                    }
                }
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SavePlayerData 실패: {e}");
        }
    }

    // ───────── 플레이어 로드 ─────────
    public static bool LoadPlayerData(
        ItemLibrary itemLibrary,
        out Vector2 playerPosition,
        out List<ItemData> inventory
    )
    {
        playerPosition = Vector2.zero;
        inventory = null;

        try
        {
            string path = Path.Combine(WorldLoadContext.GetSavePath(), PLAYER_SAVE_FILE);
            if (!File.Exists(path))
            {
                Debug.Log("[LOAD-PLAYER] player.bin not found");
                return false;
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            float px = br.ReadSingle();
            float py = br.ReadSingle();
            playerPosition = new Vector2(px, py);

            if (br.BaseStream.Position < br.BaseStream.Length)
            {
                int slotCount = br.ReadInt32();
                inventory = new List<ItemData>(slotCount);
                for (int i = 0; i < slotCount; i++)
                {
                    bool has = br.ReadBoolean();
                    if (has)
                    {
                        string itemId     = br.ReadString();
                        string name       = br.ReadString();
                        string spriteName = br.ReadString();
                        string itemType   = br.ReadString();
                        int    maxStack   = br.ReadInt32();
                        int    count      = br.ReadInt32();
                        string uniqueJson = br.ReadString();

                        Dictionary<string, object> uniqueDict = null;
                        if (!string.IsNullOrEmpty(uniqueJson))
                        {
                            try
                            {
                                uniqueDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(uniqueJson);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-PLAYER] Unique 파싱 실패: {ex.Message}");
                                uniqueDict = null;
                            }
                        }
                        if (uniqueDict == null)
                            uniqueDict = new Dictionary<string, object>();

                        Sprite icon = null;
                        if (itemLibrary != null && !string.IsNullOrEmpty(spriteName))
                        {
                            icon = itemLibrary.GetSprite(spriteName);
                        }

                        var data = new ItemData(
                            itemId:     itemId,
                            name:       name,
                            spriteName: spriteName,
                            itemType:   itemType,
                            maxStack:   maxStack,
                            unique:     uniqueDict,
                            icon:       icon,
                            count:      count
                        );
                        inventory.Add(data);
                    }
                    else
                    {
                        inventory.Add(null);
                    }
                }
            }

            Debug.Log("[LOAD-PLAYER] success");
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LoadPlayerData 실패: {e}");
            playerPosition = Vector2.zero;
            inventory = null;
            return false;
        }
    }

    // ───────── 엔티티 저장 ─────────
    public static void SaveEntities(ItemDropper itemDropper)
    {
        try
        {
            string dir = WorldLoadContext.GetSavePath();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, ENTITY_SAVE_FILE);
            string tmp  = path + ".tmp";

            var allDrops = Object.FindObjectsOfType<DroppedItem>();
            var list = new List<DroppedItem>();
            foreach (var d in allDrops)
            {
                if (d != null && d.ItemData != null && d.ItemData.Count > 0)
                    list.Add(d);
            }

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(list.Count);

                for (int i = 0; i < list.Count; i++)
                {
                    var drop = list[i];
                    var data = drop.ItemData;
                    Vector3 pos = drop.transform.position;

                    bw.Write(pos.x);
                    bw.Write(pos.y);

                    bw.Write(data.ItemId     ?? string.Empty);
                    bw.Write(data.Name       ?? string.Empty);
                    bw.Write(data.SpriteName ?? string.Empty);
                    bw.Write(data.ItemType   ?? string.Empty);
                    bw.Write(data.MaxStack);
                    bw.Write(data.Count);

                    string uniqueJson = data.Unique != null
                        ? JsonConvert.SerializeObject(data.Unique)
                        : string.Empty;
                    bw.Write(uniqueJson ?? string.Empty);
                }
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            Debug.Log($"[SAVE-ENTITY] count={list.Count}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveEntities 실패: {e}");
        }
    }

    // ───────── 엔티티 로드 ─────────
    public static void LoadEntities(ItemDropper itemDropper, ItemLibrary itemLibrary)
    {
        try
        {
            string path = Path.Combine(WorldLoadContext.GetSavePath(), ENTITY_SAVE_FILE);
            if (!File.Exists(path))
            {
                Debug.Log("[LOAD-ENTITY] entity.bin not found");
                return;
            }

            if (itemDropper == null || itemDropper.droppedItemPrefab == null)
            {
                Debug.LogWarning("[LOAD-ENTITY] ItemDropper 또는 droppedItemPrefab 이 없어 드랍 아이템을 복원할 수 없습니다.");
                return;
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            int count = br.ReadInt32();
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                float px = br.ReadSingle();
                float py = br.ReadSingle();

                string itemId     = br.ReadString();
                string name       = br.ReadString();
                string spriteName = br.ReadString();
                string itemType   = br.ReadString();
                int    maxStack   = br.ReadInt32();
                int    icount     = br.ReadInt32();
                string uniqueJson = br.ReadString();

                Dictionary<string, object> uniqueDict = null;
                if (!string.IsNullOrEmpty(uniqueJson))
                {
                    try
                    {
                        uniqueDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(uniqueJson);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LOAD-ENTITY] Unique 파싱 실패: {ex.Message}");
                        uniqueDict = null;
                    }
                }
                if (uniqueDict == null)
                    uniqueDict = new Dictionary<string, object>();

                Sprite icon = null;
                if (itemLibrary != null && !string.IsNullOrEmpty(spriteName))
                {
                    icon = itemLibrary.GetSprite(spriteName);
                }

                var data = new ItemData(
                    itemId:     itemId,
                    name:       name,
                    spriteName: spriteName,
                    itemType:   itemType,
                    maxStack:   maxStack,
                    unique:     uniqueDict,
                    icon:       icon,
                    count:      icount
                );

                Vector3 pos = new Vector3(px, py, 0f);
                var go = Object.Instantiate(itemDropper.droppedItemPrefab, pos, Quaternion.identity);
                var comp = go.GetComponent<DroppedItem>();
                if (comp != null) comp.Initialize(data);
                spawned++;
            }

            Debug.Log($"[LOAD-ENTITY] count={spawned}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LoadEntities 실패: {e}");
        }
    }
}
