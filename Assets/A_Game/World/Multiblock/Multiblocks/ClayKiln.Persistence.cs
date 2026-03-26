


using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Core;

namespace Game.World
{
    public partial class ClayKiln
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
            DropSlot(ref _fireInA, origin);
            DropSlot(ref _fireOutA, origin);
            DropSlot(ref _fireInB, origin);
            DropSlot(ref _fireOutB, origin);
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
            root["fireInA"] = PackItem(_fireInA);
            root["fireOutA"] = PackItem(_fireOutA);
            root["fireInB"] = PackItem(_fireInB);
            root["fireOutB"] = PackItem(_fireOutB);
            root["fuelTicksLeft"] = _fuelTicksLeft;
            root["fuelTicksMax"] = _fuelTicksMax;
            root["fuelResultItemId"] = _fuelResultItemId;
            root["fuelResultAmount"] = _fuelResultAmount;
            root["laneA_ticksDone"] = _laneA.ticksDone;
            root["laneA_ticksNeed"] = _laneA.ticksNeed;
            root["laneA_resultItem"] = _laneA.resultItemId;
            root["laneA_resultAmt"] = _laneA.resultAmount;
            root["laneA_prevId"] = _laneA.prevInItemId;
            root["laneA_prevDur"] = _laneA.prevInDur;
            root["laneB_ticksDone"] = _laneB.ticksDone;
            root["laneB_ticksNeed"] = _laneB.ticksNeed;
            root["laneB_resultItem"] = _laneB.resultItemId;
            root["laneB_resultAmt"] = _laneB.resultAmount;
            root["laneB_prevId"] = _laneB.prevInItemId;
            root["laneB_prevDur"] = _laneB.prevInDur;

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
            _fireInA = _fireOutA = null;
            _fireInB = _fireOutB = null;
            _fuelTicksLeft = _fuelTicksMax = 0;
            _fuelResultItemId = null;
            _fuelResultAmount = 1;
            _laneA.ResetProgress();
            _laneB.ResetProgress();
            _laneA.prevInItemId = null; _laneA.prevInDur = 0;
            _laneB.prevInItemId = null; _laneB.prevInDur = 0;
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
                    _fireInA = UnpackSavedItem(root["fireInA"]);
                    _fireOutA = UnpackSavedItem(root["fireOutA"]);
                    _fireInB = UnpackSavedItem(root["fireInB"]);
                    _fireOutB = UnpackSavedItem(root["fireOutB"]);

                    _fuelTicksLeft = root.Value<int?>("fuelTicksLeft") ?? 0;
                    _fuelTicksMax = root.Value<int?>("fuelTicksMax") ?? 0;
                    _fuelResultItemId = root.Value<string>("fuelResultItemId");
                    _fuelResultAmount = root.Value<int?>("fuelResultAmount") ?? 1;
                    if (_fuelResultAmount < 1) _fuelResultAmount = 1;

                    _laneA.ticksDone = root.Value<int?>("laneA_ticksDone") ?? 0;
                    _laneA.ticksNeed = root.Value<int?>("laneA_ticksNeed") ?? 0;
                    _laneA.resultItemId = root.Value<string>("laneA_resultItem");
                    _laneA.resultAmount = root.Value<int?>("laneA_resultAmt") ?? 1;
                    if (_laneA.resultAmount < 1) _laneA.resultAmount = 1;
                    _laneA.prevInItemId = root.Value<string>("laneA_prevId");
                    _laneA.prevInDur = root.Value<int?>("laneA_prevDur") ?? 0;

                    _laneB.ticksDone = root.Value<int?>("laneB_ticksDone") ?? 0;
                    _laneB.ticksNeed = root.Value<int?>("laneB_ticksNeed") ?? 0;
                    _laneB.resultItemId = root.Value<string>("laneB_resultItem");
                    _laneB.resultAmount = root.Value<int?>("laneB_resultAmt") ?? 1;
                    if (_laneB.resultAmount < 1) _laneB.resultAmount = 1;
                    _laneB.prevInItemId = root.Value<string>("laneB_prevId");
                    _laneB.prevInDur = root.Value<int?>("laneB_prevDur") ?? 0;
                }
            }

            CleanupZeroCountSlots();
            RequestApplyKilnMeta(Isburning);
        }
    }
}
