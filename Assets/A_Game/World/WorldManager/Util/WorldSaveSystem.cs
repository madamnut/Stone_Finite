using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

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

                // bg (ushort)
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    bw.Write(worldMap.bg[x, y]);

                // fg (FgCell: id, fluidId, fluidAmount, brightness, flags)
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var cell = worldMap.fg[x, y];
                    bw.Write(cell.id);                       // ushort
                    bw.Write(cell.fluidId);                  // ushort
                    bw.Write(cell.fluidAmount);              // byte
                    bw.Write(cell.brightness);               // byte
                    bw.Write((ushort)cell.flags);            // ushort
                }

                // light (LightCell: natural, artificial)
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bw.Write(worldMap.light[x, y].natural);     // byte
                    bw.Write(worldMap.light[x, y].artificial);  // byte
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
        int  w  = br.ReadInt32();
        int  h  = br.ReadInt32();
        long wt = br.ReadInt64();
        Debug.Log($"[LOAD] start size={w}x{h}, bytes={bytes}");

        var data = new WorldData(w, h);

        // bg
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
            data.bg[x, y] = br.ReadUInt16();

        // fg (FgCell: id, fluidId, fluidAmount, brightness, flags)
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            ref var cell = ref data.fg[x, y];
            cell.id          = br.ReadUInt16();
            cell.fluidId     = br.ReadUInt16();
            cell.fluidAmount = br.ReadByte();
            cell.brightness  = br.ReadByte();
            cell.flags       = (FgFlags)br.ReadUInt16();
        }

        // light
        for (int y = 0; y < h; y++)
        for (int x = 0; x < w; x++)
        {
            data.light[x, y].natural    = br.ReadByte();
            data.light[x, y].artificial = br.ReadByte();
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

                            // 내구도
                            bw.Write(it.MaxDurability);
                            bw.Write(it.Durability);

                            // 수량
                            bw.Write(it.Count);

                            // 태그 / 파라미터 / 액션 4종 직렬화
                            string tagsJson = it.Tags != null
                                ? JsonConvert.SerializeObject(it.Tags)
                                : string.Empty;
                            bw.Write(tagsJson ?? string.Empty);

                            string paramJson = it.Parameters != null
                                ? JsonConvert.SerializeObject(it.Parameters)
                                : string.Empty;
                            bw.Write(paramJson ?? string.Empty);

                            string craftJson = it.CraftingActions != null
                                ? JsonConvert.SerializeObject(it.CraftingActions)
                                : string.Empty;
                            bw.Write(craftJson ?? string.Empty);

                            string interJson = it.InterActions != null
                                ? JsonConvert.SerializeObject(it.InterActions)
                                : string.Empty;
                            bw.Write(interJson ?? string.Empty);

                            string toolJson = it.ToolActions != null
                                ? JsonConvert.SerializeObject(it.ToolActions)
                                : string.Empty;
                            bw.Write(toolJson ?? string.Empty);

                            string weaponJson = it.WeaponActions != null
                                ? JsonConvert.SerializeObject(it.WeaponActions)
                                : string.Empty;
                            bw.Write(weaponJson ?? string.Empty);
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

                        int maxDurability = br.ReadInt32();
                        int durability    = br.ReadInt32();
                        int count         = br.ReadInt32();

                        string tagsJson   = br.ReadString();
                        string paramJson  = br.ReadString();
                        string craftJson  = br.ReadString();
                        string interJson  = br.ReadString();
                        string toolJson   = br.ReadString();
                        string weaponJson = br.ReadString();

                        List<string> tags = null;
                        if (!string.IsNullOrEmpty(tagsJson))
                        {
                            try
                            {
                                tags = JsonConvert.DeserializeObject<List<string>>(tagsJson);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-PLAYER] Tags 파싱 실패: {ex.Message}");
                                tags = null;
                            }
                        }
                        if (tags == null) tags = new List<string>();

                        Dictionary<string, object> paramDict = null;
                        if (!string.IsNullOrEmpty(paramJson))
                        {
                            try
                            {
                                paramDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(paramJson);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-PLAYER] Params 파싱 실패: {ex.Message}");
                                paramDict = null;
                            }
                        }
                        if (paramDict == null) paramDict = new Dictionary<string, object>();

                        List<string> craftingActions = null;
                        if (!string.IsNullOrEmpty(craftJson))
                        {
                            try
                            {
                                craftingActions = JsonConvert.DeserializeObject<List<string>>(craftJson);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-PLAYER] craftingActions 파싱 실패: {ex.Message}");
                                craftingActions = null;
                            }
                        }
                        if (craftingActions == null) craftingActions = new List<string>();

                        List<string> interActions = null;
                        if (!string.IsNullOrEmpty(interJson))
                        {
                            try
                            {
                                interActions = JsonConvert.DeserializeObject<List<string>>(interJson);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-PLAYER] interActions 파싱 실패: {ex.Message}");
                                interActions = null;
                            }
                        }
                        if (interActions == null) interActions = new List<string>();

                        List<string> toolActions = null;
                        if (!string.IsNullOrEmpty(toolJson))
                        {
                            try
                            {
                                toolActions = JsonConvert.DeserializeObject<List<string>>(toolJson);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-PLAYER] toolActions 파싱 실패: {ex.Message}");
                                toolActions = null;
                            }
                        }
                        if (toolActions == null) toolActions = new List<string>();

                        List<string> weaponActions = null;
                        if (!string.IsNullOrEmpty(weaponJson))
                        {
                            try
                            {
                                weaponActions = JsonConvert.DeserializeObject<List<string>>(weaponJson);
                            }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-PLAYER] weaponActions 파싱 실패: {ex.Message}");
                                weaponActions = null;
                            }
                        }
                        if (weaponActions == null) weaponActions = new List<string>();

                        Sprite icon = null;
                        if (itemLibrary != null && !string.IsNullOrEmpty(spriteName))
                        {
                            icon = itemLibrary.GetSprite(spriteName);
                        }

                        var data = new ItemData(
                            itemId:          itemId,
                            name:            name,
                            spriteName:      spriteName,
                            itemType:        itemType,
                            maxStack:        maxStack,
                            maxDurability:   maxDurability,
                            durability:      durability,
                            craftingActions: craftingActions,
                            interActions:    interActions,
                            toolActions:     toolActions,
                            weaponActions:   weaponActions,
                            tags:            tags,
                            parameters:      paramDict,
                            icon:            icon,
                            count:           count
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

                    bw.Write(data.MaxDurability);
                    bw.Write(data.Durability);
                    bw.Write(data.Count);

                    string tagsJson = data.Tags != null
                        ? JsonConvert.SerializeObject(data.Tags)
                        : string.Empty;
                    bw.Write(tagsJson ?? string.Empty);

                    string paramJson = data.Parameters != null
                        ? JsonConvert.SerializeObject(data.Parameters)
                        : string.Empty;
                    bw.Write(paramJson ?? string.Empty);

                    string craftJson = data.CraftingActions != null
                        ? JsonConvert.SerializeObject(data.CraftingActions)
                        : string.Empty;
                    bw.Write(craftJson ?? string.Empty);

                    string interJson = data.InterActions != null
                        ? JsonConvert.SerializeObject(data.InterActions)
                        : string.Empty;
                    bw.Write(interJson ?? string.Empty);

                    string toolJson = data.ToolActions != null
                        ? JsonConvert.SerializeObject(data.ToolActions)
                        : string.Empty;
                    bw.Write(toolJson ?? string.Empty);

                    string weaponJson = data.WeaponActions != null
                        ? JsonConvert.SerializeObject(data.WeaponActions)
                        : string.Empty;
                    bw.Write(weaponJson ?? string.Empty);
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

            int count   = br.ReadInt32();
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
                int    maxDur     = br.ReadInt32();
                int    dur        = br.ReadInt32();
                int    icount     = br.ReadInt32();

                string tagsJson   = br.ReadString();
                string paramJson  = br.ReadString();
                string craftJson  = br.ReadString();
                string interJson  = br.ReadString();
                string toolJson   = br.ReadString();
                string weaponJson = br.ReadString();

                List<string> tags = null;
                if (!string.IsNullOrEmpty(tagsJson))
                {
                    try
                    {
                        tags = JsonConvert.DeserializeObject<List<string>>(tagsJson);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LOAD-ENTITY] Tags 파싱 실패: {ex.Message}");
                        tags = null;
                    }
                }
                if (tags == null) tags = new List<string>();

                Dictionary<string, object> paramDict = null;
                if (!string.IsNullOrEmpty(paramJson))
                {
                    try
                    {
                        paramDict = JsonConvert.DeserializeObject<Dictionary<string, object>>(paramJson);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LOAD-ENTITY] Params 파싱 실패: {ex.Message}");
                        paramDict = null;
                    }
                }
                if (paramDict == null) paramDict = new Dictionary<string, object>();

                List<string> craftingActions = null;
                if (!string.IsNullOrEmpty(craftJson))
                {
                    try
                    {
                        craftingActions = JsonConvert.DeserializeObject<List<string>>(craftJson);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LOAD-ENTITY] craftingActions 파싱 실패: {ex.Message}");
                        craftingActions = null;
                    }
                }
                if (craftingActions == null) craftingActions = new List<string>();

                List<string> interActions = null;
                if (!string.IsNullOrEmpty(interJson))
                {
                    try
                    {
                        interActions = JsonConvert.DeserializeObject<List<string>>(interJson);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LOAD-ENTITY] interActions 파싱 실패: {ex.Message}");
                        interActions = null;
                    }
                }
                if (interActions == null) interActions = new List<string>();

                List<string> toolActions = null;
                if (!string.IsNullOrEmpty(toolJson))
                {
                    try
                    {
                        toolActions = JsonConvert.DeserializeObject<List<string>>(toolJson);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LOAD-ENTITY] toolActions 파싱 실패: {ex.Message}");
                        toolActions = null;
                    }
                }
                if (toolActions == null) toolActions = new List<string>();

                List<string> weaponActions = null;
                if (!string.IsNullOrEmpty(weaponJson))
                {
                    try
                    {
                        weaponActions = JsonConvert.DeserializeObject<List<string>>(weaponJson);
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[LOAD-ENTITY] weaponActions 파싱 실패: {ex.Message}");
                        weaponActions = null;
                    }
                }
                if (weaponActions == null) weaponActions = new List<string>();

                Sprite icon = null;
                if (itemLibrary != null && !string.IsNullOrEmpty(spriteName))
                {
                    icon = itemLibrary.GetSprite(spriteName);
                }

                var data = new ItemData(
                    itemId:          itemId,
                    name:            name,
                    spriteName:      spriteName,
                    itemType:        itemType,
                    maxStack:        maxStack,
                    maxDurability:   maxDur,
                    durability:      dur,
                    craftingActions: craftingActions,
                    interActions:    interActions,
                    toolActions:     toolActions,
                    weaponActions:   weaponActions,
                    tags:            tags,
                    parameters:      paramDict,
                    icon:            icon,
                    count:           icount
                );

                Vector3 pos = new Vector3(px, py, 0f);
                var go   = Object.Instantiate(itemDropper.droppedItemPrefab, pos, Quaternion.identity);
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
