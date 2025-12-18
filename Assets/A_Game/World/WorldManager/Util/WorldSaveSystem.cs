using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// 월드 / 플레이어 / 엔티티 저장/로드 전담 시스템
/// ⚠ 호환성 전혀 고려 안 함 (신규 포맷 고정)
/// WorldData 구조(신규):
///   bg / solid(id+meta) / fluid(id+amount) / naturalLight / artificialLight
/// </summary>
public static class WorldSaveSystem
{
    private const string SAVE_FILE        = "world.bin";    // 월드 셀/라이트/시간
    private const string PLAYER_SAVE_FILE = "player.bin";   // 플레이어
    private const string ENTITY_SAVE_FILE = "entities.bin"; // 엔티티

    // 드랍 아이템용 가벼운 페이로드
    [System.Serializable]
    private class DroppedItemSavePayload
    {
        public string itemId;
        public int    count;
        public int    durability;
    }

    // 몹 스폰용 최소 페이로드 (Prefab 선택에만 사용)
    [System.Serializable]
    private class MobSavePayload
    {
        public string mobId;
    }

    // 시체 스폰용 최소 페이로드 (CorpseLibrary.SpawnCorpse 용)
    [System.Serializable]
    private class CorpseSavePayload
    {
        public string corpseId;
    }

    // ────────────────────────────────────────────────
    //   월드 저장
    // ────────────────────────────────────────────────
    public static void SaveWorld(
        int width,
        int height,
        WorldData worldMap,
        long worldTick,
        HashSet<Vector2Int> tickCurr,
        HashSet<Vector2Int> tickNext,
        Player playerComp,
        Transform player,
        EntityManager entityManager
    )
    {
        try
        {
            string dir = WorldLoadContext.GetSavePath();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, SAVE_FILE);
            string tmp  = path + ".tmp";

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                // world meta
                bw.Write(width);
                bw.Write(height);
                bw.Write(worldTick);

                // bg
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bw.Write(worldMap.bg[x, y]);
                }

                // solid (id + meta)
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var s = worldMap.solid[x, y];
                    bw.Write(s.id);
                    bw.Write(s.meta);
                }

                // fluid (id + amount)
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    var f = worldMap.fluid[x, y];
                    bw.Write(f.id);
                    bw.Write(f.amount);
                }

                // light (natural / artificial) : ushort
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    bw.Write(worldMap.naturalLight[x, y]);
                    bw.Write(worldMap.artificialLight[x, y]);
                }

                // tick buffers (전제: null 아님)
                bw.Write(tickCurr.Count);
                foreach (var p in tickCurr)
                {
                    bw.Write(p.x);
                    bw.Write(p.y);
                }

                bw.Write(tickNext.Count);
                foreach (var p in tickNext)
                {
                    bw.Write(p.x);
                    bw.Write(p.y);
                }
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            // player, entity 저장
            SavePlayerData(playerComp, player);
            SaveEntities(entityManager);

            Debug.Log("[SAVE] world saved.");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveWorld 실패: {e}");
        }
    }

    // ────────────────────────────────────────────────
    //   월드 로드
    // ────────────────────────────────────────────────
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
        width = height = 0;
        loadedTick = 0;

        string path = Path.Combine(WorldLoadContext.GetSavePath(), SAVE_FILE);
        if (!File.Exists(path))
        {
            Debug.Log("[LOAD] world.bin not found");
            return false;
        }

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            width      = br.ReadInt32();
            height     = br.ReadInt32();
            loadedTick = br.ReadInt64();

            var data = new WorldData(width, height);

            // bg
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                data.bg[x, y] = br.ReadUInt16();

            // solid (id + meta)
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                ref var s = ref data.solid[x, y];
                s.id   = br.ReadUInt16();
                s.meta = br.ReadUInt16();
            }

            // fluid (id + amount)
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                ref var f = ref data.fluid[x, y];
                f.id     = br.ReadUInt16();
                f.amount = br.ReadByte();
            }

            // light (natural / artificial) : ushort
            for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
            {
                data.naturalLight[x, y]    = br.ReadUInt16();
                data.artificialLight[x, y] = br.ReadUInt16();
            }

            // tick (전제: null 아님)
            tickCurr.Clear();
            tickNext.Clear();

            int cCount = br.ReadInt32();
            for (int i = 0; i < cCount; i++)
            {
                int x = br.ReadInt32();
                int y = br.ReadInt32();
                if ((uint)x < (uint)width && (uint)y < (uint)height)
                    tickCurr.Add(new Vector2Int(x, y));
            }

            int nCount = br.ReadInt32();
            for (int i = 0; i < nCount; i++)
            {
                int x = br.ReadInt32();
                int y = br.ReadInt32();
                if ((uint)x < (uint)width && (uint)y < (uint)height)
                    tickNext.Add(new Vector2Int(x, y));
            }

            loaded = data;
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LoadWorldFromDisk 실패: {e}");
            loaded = null;
            width = height = 0;
            loadedTick = 0;
            return false;
        }
    }

    // ────────────────────────────────────────────────
    //   플레이어 저장/로드
    // ────────────────────────────────────────────────
    public static void SavePlayerData(Player playerComp, Transform playerTransform)
    {
        try
        {
            string dir = WorldLoadContext.GetSavePath();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, PLAYER_SAVE_FILE);
            string tmp  = path + ".tmp";

            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                float px = playerTransform.position.x;
                float py = playerTransform.position.y;

                bw.Write(px);
                bw.Write(py);

                // inventory (전제: playerComp / Inventory / items null 아님)
                int slotCount = playerComp.Inventory.items.Count;
                bw.Write(slotCount);

                foreach (var it in playerComp.Inventory.items)
                {
                    bool has = it != null && it.Count > 0;
                    bw.Write(has);

                    if (!has) continue;

                    bw.Write(it.ItemId);
                    bw.Write(it.Name);
                    bw.Write(it.SpriteName);
                    bw.Write(it.ItemType);
                    bw.Write(it.MaxStack);

                    bw.Write(it.MaxDurability);
                    bw.Write(it.Durability);
                    bw.Write(it.Count);

                    // JSON 직렬화 (새 구조)
                    bw.Write(JsonConvert.SerializeObject(it.Tags));
                    bw.Write(JsonConvert.SerializeObject(it.Details));
                    bw.Write(JsonConvert.SerializeObject(it.BreakActions));
                    bw.Write(JsonConvert.SerializeObject(it.ToolActions));
                    bw.Write(JsonConvert.SerializeObject(it.WeaponActions));
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

    public static bool LoadPlayerData(
        ItemLibrary itemLibrary,
        out Vector2 playerPosition,
        out List<ItemData> inventory
    )
    {
        playerPosition = Vector2.zero;
        inventory = null;

        string path = Path.Combine(WorldLoadContext.GetSavePath(), PLAYER_SAVE_FILE);
        if (!File.Exists(path))
            return false;

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            float px = br.ReadSingle();
            float py = br.ReadSingle();
            playerPosition = new Vector2(px, py);

            int slotCount = br.ReadInt32();
            inventory = new List<ItemData>(slotCount);

            for (int i = 0; i < slotCount; i++)
            {
                bool has = br.ReadBoolean();
                if (!has)
                {
                    inventory.Add(null);
                    continue;
                }

                string itemId     = br.ReadString();
                string name       = br.ReadString();
                string spriteName = br.ReadString();
                string itemType   = br.ReadString();
                int    maxStack   = br.ReadInt32();

                int maxDur = br.ReadInt32();
                int dur    = br.ReadInt32();
                int count  = br.ReadInt32();

                var tags    = JsonConvert.DeserializeObject<List<string>>(br.ReadString()) ?? new List<string>();
                var details = JsonConvert.DeserializeObject<Dictionary<string, object>>(br.ReadString()) ?? new Dictionary<string, object>();

                var breakA  = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(br.ReadString())
                              ?? new Dictionary<string, Dictionary<string, object>>();
                var toolA   = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(br.ReadString())
                              ?? new Dictionary<string, Dictionary<string, object>>();
                var weaponA = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(br.ReadString())
                              ?? new Dictionary<string, Dictionary<string, object>>();

                Sprite icon = itemLibrary.GetSprite(spriteName);

                var data = new ItemData(
                    itemId,
                    name,
                    spriteName,
                    itemType,
                    maxStack,
                    maxDur,
                    dur,
                    toolA,      // ToolActions
                    weaponA,    // WeaponActions
                    breakA,     // BreakActions
                    tags,
                    details,
                    icon,
                    count
                );

                inventory.Add(data);
            }

            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LoadPlayerData 실패: {e}");
            return false;
        }
    }

    // ────────────────────────────────────────────────
    //   엔티티 저장 (EntityManager 기반)
    // ────────────────────────────────────────────────
    public static void SaveEntities(EntityManager em)
    {
        try
        {
            // 전제: em null 아님

            string dir = WorldLoadContext.GetSavePath();
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, ENTITY_SAVE_FILE);
            string tmp  = path + ".tmp";

            // null 제외한 엔티티 목록 구성
            var src  = em.Entities;
            var list = new List<Entity>(src.Count);
            for (int i = 0; i < src.Count; i++)
            {
                if (src[i] != null)
                    list.Add(src[i]);
            }

            // 파일 열기 + 쓰기
            using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
            using (var bw = new BinaryWriter(fs))
            {
                bw.Write(list.Count);

                foreach (var e in list)
                {
                    EntitySaveData data = e.ToSaveData();
                    if (data == null) continue;

                    // kind
                    bw.Write((byte)data.Kind);

                    // pos
                    bw.Write(data.Position.x);
                    bw.Write(data.Position.y);

                    // payload
                    string payload = data.PayloadJson ?? "";
                    byte[] bytes   = System.Text.Encoding.UTF8.GetBytes(payload);
                    bw.Write(bytes.Length);
                    bw.Write(bytes);
                }
            }

            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);

            Debug.Log($"[SAVE-ENTITY] saved count={list.Count}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"SaveEntities 실패: {e}");
        }
    }

    // ────────────────────────────────────────────────
    //   엔티티 로드
    // ────────────────────────────────────────────────
    public static void LoadEntities(
        EntityManager em,
        ItemLibrary itemLibrary,
        FallingBlock fallingPrefab,
        GameObject droppedItemPrefab,
        MobLibrary mobLibrary,
        CorpseLibrary corpseLibrary
    )
    {
        string path = Path.Combine(WorldLoadContext.GetSavePath(), ENTITY_SAVE_FILE);
        if (!File.Exists(path))
        {
            Debug.Log("[LOAD-ENTITY] no file");
            return;
        }

        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            using var br = new BinaryReader(fs);

            int count   = br.ReadInt32();
            int spawned = 0;

            for (int i = 0; i < count; i++)
            {
                EntityKind kind = (EntityKind)br.ReadByte();
                float px = br.ReadSingle();
                float py = br.ReadSingle();
                Vector2 pos = new Vector2(px, py);

                int payloadLen = br.ReadInt32();
                string payload = payloadLen > 0
                    ? System.Text.Encoding.UTF8.GetString(br.ReadBytes(payloadLen))
                    : "";

                // DroppedItem은 ItemLibrary 기반으로 직접 복원
                if (kind == EntityKind.DroppedItem)
                {
                    DroppedItemSavePayload p = null;
                    if (!string.IsNullOrEmpty(payload))
                    {
                        try { p = JsonConvert.DeserializeObject<DroppedItemSavePayload>(payload); }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[LOAD-ENTITY] DroppedItem payload 파싱 실패: {ex.Message}");
                        }
                    }

                    if (p == null || string.IsNullOrEmpty(p.itemId) || p.count <= 0)
                        continue;

                    var item = itemLibrary.Create(p.itemId, p.count);
                    if (item == null)
                        continue;

                    if (p.durability >= 0 && p.durability <= item.MaxDurability)
                        item.Durability = p.durability;

                    var go = Object.Instantiate(droppedItemPrefab, pos, Quaternion.identity);
                    var di = go.GetComponent<DroppedItem>();
                    if (di == null)
                    {
                        Object.Destroy(go);
                        continue;
                    }

                    di.Initialize(item);
                    em.Register(di);
                    spawned++;
                    continue;
                }

                // Mob 로드
                if (kind == EntityKind.Mob)
                {
                    MobSavePayload mp = null;
                    if (!string.IsNullOrEmpty(payload))
                    {
                        try { mp = JsonConvert.DeserializeObject<MobSavePayload>(payload); }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[LOAD-ENTITY] Mob payload 파싱 실패: {ex.Message}");
                        }
                    }

                    if (mp == null || string.IsNullOrEmpty(mp.mobId))
                        continue;

                    var mob = mobLibrary.SpawnMob(mp.mobId, pos, em);
                    if (mob == null)
                        continue;

                    var data = new EntitySaveData
                    {
                        Kind        = kind,
                        Position    = pos,
                        PayloadJson = payload
                    };
                    mob.FromSaveData(data);

                    spawned++;
                    continue;
                }

                // Corpse 로드
                if (kind == EntityKind.Corpse)
                {
                    CorpseSavePayload cp = null;
                    if (!string.IsNullOrEmpty(payload))
                    {
                        try { cp = JsonConvert.DeserializeObject<CorpseSavePayload>(payload); }
                        catch (System.Exception ex)
                        {
                            Debug.LogError($"[LOAD-ENTITY] Corpse payload 파싱 실패: {ex.Message}");
                        }
                    }

                    if (cp == null || string.IsNullOrEmpty(cp.corpseId))
                        continue;

                    var corpse = corpseLibrary.SpawnCorpse(cp.corpseId, pos);
                    if (corpse == null)
                        continue;

                    var data = new EntitySaveData
                    {
                        Kind        = kind,
                        Position    = pos,
                        PayloadJson = payload
                    };

                    corpse.FromSaveData(data);
                    spawned++;
                    continue;
                }

                // FallingBlock
                if (kind == EntityKind.FallingBlock)
                {
                    var go = Object.Instantiate(fallingPrefab, pos, Quaternion.identity);
                    var fb = go.GetComponent<FallingBlock>();
                    if (fb == null)
                    {
                        Object.Destroy(go);
                        continue;
                    }

                    var data = new EntitySaveData
                    {
                        Kind        = kind,
                        Position    = pos,
                        PayloadJson = payload
                    };

                    fb.FromSaveData(data);
                    em.Register(fb);
                    spawned++;
                    continue;
                }

                // 아직 처리 안 하는 Kind 는 스킵
            }

            Debug.Log($"[LOAD-ENTITY] spawned={spawned}");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"LoadEntities 실패: {e}");
        }
    }
}
