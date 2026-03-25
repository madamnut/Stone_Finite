using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// ????????/ ??????????????濡?씀?濾????/ ???????????????????????傭?끆?????????????????諛몃마嶺뚮?????????????硫λ젒??????????
/// ?????????嫄??????????븐뼐??????????ш끽維????????? ????????????(???????????????????????????
/// WorldData ??????????????????????:
///   bg / utility(id+meta) / solid(id+meta) / fluid(id+amount) / naturalLight / artificialLight
/// + Multiblock ???????嫄???????????????????濡?씀?濾???????????????????:
///   count
///   ?????????썹땟戮녹??諭?????⑸㎦???????????
///     DefId(string)
///     InstId(int)
///     Origin(x,y)
///     Width/Height(int)
///     PayloadJson(string)
///     OriginalSolidIds(ushort[] row-major, length = Width*Height)
/// </summary>
using Game.Data;
using Game.Lobby;
using Game.Core;

namespace Game.World
{
    public static class WorldSaveSystem
    {
        private const string SAVE_FILE        = "world.bin";    // ??????????/??????????????????????+??????椰??????????????????
        private const string PLAYER_SAVE_FILE = "player.bin";   // ??????????????濡?씀?濾????
        private const string ENTITY_SAVE_FILE = "entities.bin"; // ????????
    
        // ????????거????????????????????諛몃마嶺뚮?????????????硫λ젒?????????????????거?????????????????????????????????????????⑤벡瑜??????????????????傭?끆???????
        [System.Serializable]
        private class DroppedItemSavePayload
        {
            public string itemId;
            public int    count;
            public int    durability;
        }
    
        // ???????????????곕춴??????????椰?????????????????????⑤벡瑜??????????????????傭?끆???????(Prefab ???????????????????????
        [System.Serializable]
        private class MobSavePayload
        {
            public string mobId;
        }
    
        // ???????????????????곕춴??????????椰?????????????????????⑤벡瑜??????????????????傭?끆???????(CorpseLibrary.SpawnCorpse ??
        [System.Serializable]
        private class CorpseSavePayload
        {
            public string corpseId;
        }
    
        // ????????????????????????????????????????????????????????????????????????????????????????????????
        //   ????????????
        // ????????????????????????????????????????????????????????????????????????????????????????????????
        public static void SaveWorld(
            int width,
            int height,
            WorldData worldMap,
            long worldTick,
            HashSet<Vector2Int> tickCurr,
            HashSet<Vector2Int> tickNext,
            InventoryData playerInventory,
            Transform player,
            EntityManager entityManager,
            MultiblockManager multiblockManager
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
    
                    // utility (id + meta)  ??????????????됰뼸???
                    for (int y = 0; y < height; y++)
                    for (int x = 0; x < width; x++)
                    {
                        var u = worldMap.utility[x, y];
                        bw.Write(u.id);
                        bw.Write(u.meta);
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
    
                    // tick buffers (??????????諛몃마嶺뚮?????????????硫λ젒?? null ??????????諛몃마嶺뚮?????????????硫λ젒???
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
    
                    // ?????????? ??????椰????????????????????????(??????????????? ??????????
                    int mbCount = 0;
                    if (multiblockManager != null && multiblockManager.Instances != null)
                        mbCount = multiblockManager.Instances.Count;
    
                    bw.Write(mbCount);
    
                    if (mbCount > 0)
                    {
                        foreach (var kv in multiblockManager.Instances)
                        {
                            var mb = kv.Value;
                            if (mb == null)
                            {
                                throw new System.Exception("[SAVE] Multiblock instance is null in Instances.");
                            }
    
                            Multiblock.SaveData sd = mb.ToSaveData();
    
                            bw.Write(sd.DefId ?? "");
                            bw.Write(sd.InstId);
                            bw.Write(sd.Origin.x);
                            bw.Write(sd.Origin.y);
                            bw.Write(sd.Width);
                            bw.Write(sd.Height);
    
                            bw.Write(sd.PayloadJson ?? "");
    
                            ushort[] orig = sd.OriginalSolidIds;
                            int origLen = (orig != null) ? orig.Length : 0;
                            bw.Write(origLen);
                            if (origLen > 0)
                            {
                                for (int i = 0; i < origLen; i++)
                                    bw.Write(orig[i]);
                            }
                        }
                    }
                }
    
                if (File.Exists(path)) File.Delete(path);
                File.Move(tmp, path);
    
                // player, entity ????
                SavePlayerData(playerInventory, player);
                SaveEntities(entityManager);
    
                Debug.Log("[SAVE] world saved.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"SaveWorld failed: {e}");
            }
        }
    
        // ????????????????????????????????????????????????????????????????????????????????????????????????
        //   ???????????????????傭?끆???????
        // ????????????????????????????????????????????????????????????????????????????????????????????????
        public static bool LoadWorldFromDisk(
            out WorldData loaded,
            out int width,
            out int height,
            out long loadedTick,
            HashSet<Vector2Int> tickCurr,
            HashSet<Vector2Int> tickNext,
            out List<Multiblock.SaveData> multiblocks
        )
        {
            loaded = default;
            width = height = 0;
            loadedTick = 0;
            multiblocks = null;
    
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
    
                // utility (id + meta) ??????????????됰뼸???
                for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    ref var u = ref data.utility[x, y];
                    u.id   = br.ReadUInt16();
                    u.meta = br.ReadUInt16();
                }
    
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
    
                // tick (??????????諛몃마嶺뚮?????????????硫λ젒?? null ??????????諛몃마嶺뚮?????????????硫λ젒???
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
    
                // ?????????? ??????椰????????????????????????(??????????????? ??????????
                int mbCount = br.ReadInt32();
                if (mbCount < 0) mbCount = 0;
    
                multiblocks = new List<Multiblock.SaveData>(mbCount);
    
                for (int i = 0; i < mbCount; i++)
                {
                    var sd = new Multiblock.SaveData
                    {
                        DefId  = br.ReadString(),
                        InstId = br.ReadInt32(),
                        Origin = new Vector2Int(br.ReadInt32(), br.ReadInt32()),
                        Width  = br.ReadInt32(),
                        Height = br.ReadInt32(),
                        PayloadJson = br.ReadString()
                    };
    
                    int origLen = br.ReadInt32();
                    if (origLen > 0)
                    {
                        sd.OriginalSolidIds = new ushort[origLen];
                        for (int j = 0; j < origLen; j++)
                            sd.OriginalSolidIds[j] = br.ReadUInt16();
                    }
                    else
                    {
                        sd.OriginalSolidIds = null;
                    }
    
                    multiblocks.Add(sd);
                }
    
                loaded = data;
                return true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"LoadWorldFromDisk failed: {e}");
                loaded = null;
                width = height = 0;
                loadedTick = 0;
                multiblocks = null;
                return false;
            }
        }
    
        // ????????????????????????????????????????????????????????????????????????????????????????????????
        //   ??????????????濡?씀?濾???????????????????傭?끆???????
        // ????????????????????????????????????????????????????????????????????????????????????????????????
        public static void SavePlayerData(InventoryData playerInventory, Transform playerTransform)
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
    
                    // inventory (??????????諛몃마嶺뚮?????????????硫λ젒?? playerComp / Inventory / items null ??????????諛몃마嶺뚮?????????????硫λ젒???
                    if (playerInventory == null) throw new System.Exception("Player inventory is missing.");

                    int slotCount = playerInventory.items.Count;
                    bw.Write(slotCount);
    
                    foreach (var it in playerInventory.items)
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
    
                        // JSON ??????椰??????????(?????????
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
                Debug.LogError($"SavePlayerData failed: {e}");
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
                Debug.LogError($"LoadPlayerData failed: {e}");
                return false;
            }
        }
    
        // ????????????????????????????????????????????????????????????????????????????????????????????????
        //   ????????????(EntityManager ???????????????
        // ????????????????????????????????????????????????????????????????????????????????????????????????
        public static void SaveEntities(EntityManager em)
        {
            try
            {
                // ??????????諛몃마嶺뚮?????????????硫λ젒?? em null ??????????諛몃마嶺뚮?????????????硫λ젒???
    
                string dir = WorldLoadContext.GetSavePath();
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);
    
                string path = Path.Combine(dir, ENTITY_SAVE_FILE);
                string tmp  = path + ".tmp";
    
                // null ????????거????????????????????????椰??????????????椰????????꾩룆梨띰쭕?뚢뵾????轅붽틓???????蹂????嶺뚮ㅎ???????????????
                var src  = em.Entities;
                var list = new List<Entity>(src.Count);
                for (int i = 0; i < src.Count; i++)
                {
                    if (src[i] != null)
                        list.Add(src[i]);
                }
    
                // ??????????????????????????ㅻ쑋???+ ????????????산뭐????
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
                Debug.LogError($"SaveEntities failed: {e}");
            }
        }
    
        // ????????????????????????????????????????????????????????????????????????????????????????????????
        //   ???????????????????傭?끆???????
        // ????????????????????????????????????????????????????????????????????????????????????????????????
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
    
                    // DroppedItem?? ItemLibrary ????????????????????????????椰???????????????????⑤벡???????븐뼐?????????
                    if (kind == EntityKind.DroppedItem)
                    {
                        DroppedItemSavePayload p = null;
                        if (!string.IsNullOrEmpty(payload))
                        {
                            try { p = JsonConvert.DeserializeObject<DroppedItemSavePayload>(payload); }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-ENTITY] Failed to parse DroppedItem payload: {ex.Message}");
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
    
                    // Mob ???????????傭?끆???????
                    if (kind == EntityKind.Mob)
                    {
                        MobSavePayload mp = null;
                        if (!string.IsNullOrEmpty(payload))
                        {
                            try { mp = JsonConvert.DeserializeObject<MobSavePayload>(payload); }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-ENTITY] Failed to parse Mob payload: {ex.Message}");
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
    
                    // Corpse ???????????傭?끆???????
                    if (kind == EntityKind.Corpse)
                    {
                        CorpseSavePayload cp = null;
                        if (!string.IsNullOrEmpty(payload))
                        {
                            try { cp = JsonConvert.DeserializeObject<CorpseSavePayload>(payload); }
                            catch (System.Exception ex)
                            {
                                Debug.LogError($"[LOAD-ENTITY] Failed to parse Corpse payload: {ex.Message}");
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
    
                    // ??????????諛몃마嶺뚮?????????????硫λ젒???????怨?????????????????椰??????????????遺얘턁???????猷몄굣???????????????꿔꺂???⑸븶?????????猷몄굣???????Kind ?????????????썹땟戮녹??諭?????⑸㎦???
                }
    
                Debug.Log($"[LOAD-ENTITY] spawned={spawned}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"LoadEntities failed: {e}");
            }
        }
    }
}
