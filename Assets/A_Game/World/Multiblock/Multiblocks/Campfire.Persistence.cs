


using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Core;

namespace Game.World
{
    public partial class Campfire
    {
        
        public override void OnCellBroken(Vector2Int brokenCell)
        {
            if (!_droppedOnDestroy)
            {

                _droppedOnDestroy = true;
                DropAllInternalItems();
            }

            base.OnCellBroken(brokenCell);
        }

        
        void DropAllInternalItems()
        {
            if (World == null || World.entityManager == null)
                return;

            Vector2 origin = new Vector2(
                Origin.x + (Width * 0.5f),
                Origin.y + (Height * 0.5f)
            );

            DropSlot(ref _fuelIn, origin);
            DropSlot(ref _fuelOut, origin);
            DropSlot(ref _ingIn, origin);
            DropSlot(ref _ingOut, origin);
        }

        
        void DropSlot(ref ItemData slot, Vector2 origin)
        {
            if (slot == null) return;
            if (slot.Count <= 0) { slot = null; return; }

            var copy = new ItemData(
                itemId: slot.ItemId,
                name: slot.Name,
                spriteName: slot.SpriteName,
                itemType: slot.ItemType,
                maxStack: slot.MaxStack,
                maxDurability: slot.MaxDurability,
                durability: slot.Durability,
                toolActions: slot.ToolActions,
                weaponActions: slot.WeaponActions,
                breakActions: slot.BreakActions,
                tags: slot.Tags,
                details: slot.Details,
                icon: slot.Icon,
                count: slot.Count
            );

            WorldEntityFactory.SpawnDroppedItem(World.entityManager, World.itemDropper, null, copy, origin);
            slot = null;
        }

        
        public override SaveData ToSaveData()
        {
            JObject root = new JObject();

            
            JObject PackItem(ItemData it)
            {
                if (it == null || it.Count <= 0) return null;
                return new JObject
                {
                    ["id"] = it.ItemId,
                    ["count"] = it.Count,
                    ["dur"] = it.Durability
                };
            }

            root["fuelIn"] = PackItem(_fuelIn);
            root["fuelOut"] = PackItem(_fuelOut);
            root["ingIn"] = PackItem(_ingIn);
            root["ingOut"] = PackItem(_ingOut);
            root["fuelTicksLeft"] = _fuelTicksLeft;
            root["fuelTicksMax"] = _fuelTicksMax;
            root["fuelResultItemId"] = _fuelResultItemId;
            root["fuelResultAmount"] = _fuelResultAmount;
            root["cookTicksDone"] = _cookTicksDone;
            root["cookTicksNeed"] = _cookTicksNeed;
            root["cookResultItemId"] = _cookResultItemId;
            root["cookResultAmount"] = _cookResultAmount;
            root["prevIngItemId"] = _prevIngItemId;
            root["prevIngDur"] = _prevIngDur;

            return new SaveData
            {
                DefId = DefId,
                InstId = InstId,
                Origin = Origin,
                Width = Width,
                Height = Height,
                PayloadJson = root.ToString(),
                OriginalSolidIds = SnapshotOriginalSolidIds()
            };
        }

        
        public override void FromSaveData(SaveData data)
        {
            RestoreBaseSaveData(data);

            _fuelIn = _fuelOut = _ingIn = _ingOut = null;
            _fuelTicksLeft = _fuelTicksMax = 0;
            _fuelResultItemId = null;
            _fuelResultAmount = 1;
            _cookTicksDone = 0;
            _cookTicksNeed = 0;
            _cookResultItemId = null;
            _cookResultAmount = 1;
            _prevIngItemId = null;
            _prevIngDur = 0;
            _droppedOnDestroy = false;
            _fireHoldTicksLeft = 0;

            if (!string.IsNullOrEmpty(data.PayloadJson))
            {
                JObject root = null;
                try { root = JObject.Parse(data.PayloadJson); }
                catch { root = null; }

                if (root != null)
                {
                    _fuelIn = UnpackSavedItem(root["fuelIn"]);
                    _fuelOut = UnpackSavedItem(root["fuelOut"]);
                    _ingIn = UnpackSavedItem(root["ingIn"]);
                    _ingOut = UnpackSavedItem(root["ingOut"]);

                    _fuelTicksLeft = root.Value<int?>("fuelTicksLeft") ?? 0;
                    _fuelTicksMax = root.Value<int?>("fuelTicksMax") ?? 0;
                    _fuelResultItemId = root.Value<string>("fuelResultItemId");
                    _fuelResultAmount = root.Value<int?>("fuelResultAmount") ?? 1;
                    if (_fuelResultAmount < 1) _fuelResultAmount = 1;

                    _cookTicksDone = root.Value<int?>("cookTicksDone") ?? 0;
                    _cookTicksNeed = root.Value<int?>("cookTicksNeed") ?? 0;
                    _cookResultItemId = root.Value<string>("cookResultItemId");
                    _cookResultAmount = root.Value<int?>("cookResultAmount") ?? 1;
                    if (_cookResultAmount < 1) _cookResultAmount = 1;

                    _prevIngItemId = root.Value<string>("prevIngItemId");
                    _prevIngDur = root.Value<int?>("prevIngDur") ?? 0;
                }
            }

            CleanupZeroCountSlots();
            RequestApplyCampfireMeta(IsFireActiveFx);
        }
    }
}
