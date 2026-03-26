using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;

using Game.Core;
using Game.Data;

namespace Game.World
{
    internal static class PlayerPersistence
    {
        const string PlayerSaveFile = "player.bin";

        public static void SavePlayerData(InventoryData playerInventory, Transform playerTransform)
        {
            try
            {
                string dir = WorldSavePathResolver.EnsureDirectory();
                string path = Path.Combine(dir, PlayerSaveFile);
                string tmp = path + ".tmp";

                using (var fs = new FileStream(tmp, FileMode.Create, FileAccess.Write, FileShare.None))
                using (var bw = new BinaryWriter(fs))
                {
                    bw.Write(playerTransform.position.x);
                    bw.Write(playerTransform.position.y);

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
            out List<ItemData> inventory)
        {
            playerPosition = Vector2.zero;
            inventory = null;

            string path = WorldSavePathResolver.GetPath(PlayerSaveFile);
            if (!File.Exists(path))
                return false;

            try
            {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
                using var br = new BinaryReader(fs);

                playerPosition = new Vector2(br.ReadSingle(), br.ReadSingle());

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

                    string itemId = br.ReadString();
                    string name = br.ReadString();
                    string spriteName = br.ReadString();
                    string itemType = br.ReadString();
                    int maxStack = br.ReadInt32();
                    int maxDur = br.ReadInt32();
                    int dur = br.ReadInt32();
                    int count = br.ReadInt32();

                    var tags = JsonConvert.DeserializeObject<List<string>>(br.ReadString()) ?? new List<string>();
                    var details = JsonConvert.DeserializeObject<Dictionary<string, object>>(br.ReadString()) ?? new Dictionary<string, object>();
                    var breakA = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(br.ReadString()) ?? new Dictionary<string, Dictionary<string, object>>();
                    var toolA = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(br.ReadString()) ?? new Dictionary<string, Dictionary<string, object>>();
                    var weaponA = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, object>>>(br.ReadString()) ?? new Dictionary<string, Dictionary<string, object>>();

                    Sprite icon = itemLibrary.GetSprite(spriteName);
                    var data = new ItemData(itemId, name, spriteName, itemType, maxStack, maxDur, dur, toolA, weaponA, breakA, tags, details, icon, count);
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
    }
}
