using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Core;

namespace Game.World
{
    public partial class CokeOven
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
            DropSlot(ref _matIn, origin);
            DropSlot(ref _matOut0, origin);
            DropSlot(ref _matOut1, origin);
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
            root["matIn"] = PackItem(_matIn);
            root["matOut0"] = PackItem(_matOut0);
            root["matOut1"] = PackItem(_matOut1);
            root["fuelLeft"] = _fuelTicksLeft;
            root["fuelMax"] = _fuelTicksMax;
            root["temp"] = _currentTemp;
            root["fuelResId"] = _fuelResultItemId;
            root["fuelResAmt"] = _fuelResultAmount;
            root["hold"] = _fireHoldTicksLeft;
            root["cokeDone"] = _cokeTicksDone;
            root["cokeNeed"] = _cokeTicksNeed;
            root["prevId"] = _prevInItemId;
            root["prevDur"] = _prevInDur;

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

            _fuelIn = _fuelOut = null;
            _matIn = _matOut0 = _matOut1 = null;
            _fuelTicksLeft = 0;
            _fuelTicksMax = 0;
            _currentTemp = 0;
            _fuelResultItemId = null;
            _fuelResultAmount = 1;
            _fireHoldTicksLeft = 0;
            ResetCokeProgress();
            _prevInItemId = null;
            _prevInDur = 0;

            if (string.IsNullOrEmpty(data.PayloadJson))
                return;

            JObject root = JObject.Parse(data.PayloadJson);

            _fuelIn = UnpackSavedItem(root["fuelIn"]);
            _fuelOut = UnpackSavedItem(root["fuelOut"]);
            _matIn = UnpackSavedItem(root["matIn"]);
            _matOut0 = UnpackSavedItem(root["matOut0"]);
            _matOut1 = UnpackSavedItem(root["matOut1"]);

            _fuelTicksLeft = root.Value<int?>("fuelLeft") ?? 0;
            _fuelTicksMax = root.Value<int?>("fuelMax") ?? 0;
            _currentTemp = Mathf.Clamp(root.Value<int?>("temp") ?? 0, 0, MAX_TEMP);
            _fuelResultItemId = root.Value<string>("fuelResId");
            _fuelResultAmount = root.Value<int?>("fuelResAmt") ?? 1;
            _fireHoldTicksLeft = root.Value<int?>("hold") ?? 0;
            _cokeTicksDone = root.Value<int?>("cokeDone") ?? 0;
            _cokeTicksNeed = root.Value<int?>("cokeNeed") ?? 0;
            _prevInItemId = root.Value<string>("prevId");
            _prevInDur = root.Value<int?>("prevDur") ?? 0;

            CleanupZeroCountSlots();
        }
    }
}
