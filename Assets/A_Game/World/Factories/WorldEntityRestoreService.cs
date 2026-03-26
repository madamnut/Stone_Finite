


using System.IO;
using System.Text;
using UnityEngine;
using Newtonsoft.Json;

using Game.Core;
using Game.Data;

namespace Game.World
{
    public static class WorldEntityRestoreService
    {
        [System.Serializable]
        private class DroppedItemSavePayload
        {

            public string itemId;
            public int count;
            public int durability;
        }

        [System.Serializable]
        private class MobSavePayload
        {
            public string mobId;
        }

        [System.Serializable]
        private class CorpseSavePayload
        {
            public string corpseId;
        }

        
        public static void LoadEntities(
            string path,
            EntityManager entityManager,
            ItemLibrary itemLibrary,
            ItemDropper itemDropper,
            GameObject droppedItemPrefab,
            FallingBlock fallingPrefab,
            MobLibrary mobLibrary,
            CorpseLibrary corpseLibrary)
        {
            if (!File.Exists(path))
            {
                Debug.Log("[LOAD-ENTITY] no file");
                return;
            }

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                int count = br.ReadInt32();
                int spawned = 0;

                for (int i = 0; i < count; i++)
                {
                    EntityKind kind = (EntityKind)br.ReadByte();
                    float px = br.ReadSingle();
                    float py = br.ReadSingle();
                    Vector2 pos = new Vector2(px, py);

                    int payloadLen = br.ReadInt32();
                    string payload = payloadLen > 0
                        ? Encoding.UTF8.GetString(br.ReadBytes(payloadLen))
                        : string.Empty;

                    var data = new EntitySaveData
                    {
                        Kind = kind,
                        Position = pos,
                        PayloadJson = payload
                    };

                    if (TryRestoreEntity(data, entityManager, itemLibrary, itemDropper, droppedItemPrefab, fallingPrefab, mobLibrary, corpseLibrary))
                        spawned++;
                }

                Debug.Log($"[LOAD-ENTITY] spawned={spawned}");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"LoadEntities failed: {e}");
            }
        }

        
        static bool TryRestoreEntity(
            EntitySaveData data,
            EntityManager entityManager,
            ItemLibrary itemLibrary,
            ItemDropper itemDropper,
            GameObject droppedItemPrefab,
            FallingBlock fallingPrefab,
            MobLibrary mobLibrary,
            CorpseLibrary corpseLibrary)
        {
            switch (data.Kind)
            {
                case EntityKind.DroppedItem:
                    return TryRestoreDroppedItem(data, entityManager, itemLibrary, itemDropper, droppedItemPrefab);
                case EntityKind.Mob:
                    return TryRestoreMob(data, entityManager, mobLibrary);
                case EntityKind.Corpse:
                    return TryRestoreCorpse(data, corpseLibrary);
                case EntityKind.FallingBlock:
                    return WorldEntityFactory.SpawnFallingBlock(entityManager, fallingPrefab, data) != null;
                default:
                    return false;
            }
        }

        
        static bool TryRestoreDroppedItem(
            EntitySaveData data,
            EntityManager entityManager,
            ItemLibrary itemLibrary,
            ItemDropper itemDropper,
            GameObject droppedItemPrefab)
        {
            DroppedItemSavePayload payload = null;
            if (!string.IsNullOrEmpty(data.PayloadJson))
            {
                try
                {
                    payload = JsonConvert.DeserializeObject<DroppedItemSavePayload>(data.PayloadJson);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LOAD-ENTITY] Failed to parse DroppedItem payload: {ex.Message}");
                }
            }

            if (payload == null || string.IsNullOrEmpty(payload.itemId) || payload.count <= 0 || itemLibrary == null)
                return false;

            var item = itemLibrary.Create(payload.itemId, payload.count);
            if (item == null)
                return false;

            if (payload.durability >= 0 && payload.durability <= item.MaxDurability)
                item.Durability = payload.durability;

            return WorldEntityFactory.SpawnDroppedItem(entityManager, itemDropper, droppedItemPrefab, item, data.Position) != null;
        }

        
        static bool TryRestoreMob(
            EntitySaveData data,
            EntityManager entityManager,
            MobLibrary mobLibrary)
        {
            MobSavePayload payload = null;
            if (!string.IsNullOrEmpty(data.PayloadJson))
            {
                try
                {
                    payload = JsonConvert.DeserializeObject<MobSavePayload>(data.PayloadJson);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LOAD-ENTITY] Failed to parse Mob payload: {ex.Message}");
                }
            }

            if (payload == null || string.IsNullOrEmpty(payload.mobId))
                return false;

            var mob = WorldEntityFactory.SpawnMob(entityManager, mobLibrary, payload.mobId, data.Position);
            if (mob == null)
                return false;

            mob.FromSaveData(data);
            return true;
        }

        
        static bool TryRestoreCorpse(
            EntitySaveData data,
            CorpseLibrary corpseLibrary)
        {
            CorpseSavePayload payload = null;
            if (!string.IsNullOrEmpty(data.PayloadJson))
            {
                try
                {
                    payload = JsonConvert.DeserializeObject<CorpseSavePayload>(data.PayloadJson);
                }
                catch (System.Exception ex)
                {
                    Debug.LogError($"[LOAD-ENTITY] Failed to parse Corpse payload: {ex.Message}");
                }
            }

            if (payload == null || string.IsNullOrEmpty(payload.corpseId))
                return false;

            var corpse = WorldEntityFactory.SpawnCorpse(corpseLibrary, payload.corpseId, data.Position);
            if (corpse == null)
                return false;

            corpse.FromSaveData(data);
            return true;
        }
    }
}
