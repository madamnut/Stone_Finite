using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Core;

namespace Game.World
{
    public partial class BrickFurnace
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
            DropSlot(ref _crucible, origin);

            for (int i = 0; i < 9; i++)
                DropSlot(ref _ins[i], origin);

            ResetAllSmeltProgressAndReservations();
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
            root["crucible"] = PackItem(_crucible);

            var ins = new JArray();
            for (int i = 0; i < 9; i++)
                ins.Add(PackItem(_ins[i]));
            root["inputs"] = ins;

            root["fuelTicksLeft"] = _fuelTicksLeft;
            root["fuelTicksMax"] = _fuelTicksMax;
            root["burningFuelTemperature"] = _burningFuelTemperature;
            root["fuelResultItemId"] = _fuelResultItemId;
            root["fuelResultAmount"] = _fuelResultAmount;
            root["fireHoldTicksLeft"] = _fireHoldTicksLeft;

            var doneArr = new JArray();
            var needArr = new JArray();
            var resArr = new JArray();
            var resAmtArr = new JArray();
            var resIdArr = new JArray();

            for (int i = 0; i < 9; i++)
            {
                doneArr.Add(_smeltTicksDone[i]);
                needArr.Add(_smeltTicksNeed[i]);
                resArr.Add(_reserved[i] ? 1 : 0);
                resAmtArr.Add(_reservedAmount[i]);
                resIdArr.Add(_reservedFluidId[i] ?? "");
            }

            root["smeltDone"] = doneArr;
            root["smeltNeed"] = needArr;
            root["reserved"] = resArr;
            root["reservedAmount"] = resAmtArr;
            root["reservedFluidId"] = resIdArr;
            root["reservedTotal"] = _reservedTotal;

            if (_crucible != null && _crucible.Count > 0 &&
                _crucible.Details.TryGetValue("layers", out var layersObj) &&
                layersObj != null)
            {
                root["crucible_layers"] = JToken.FromObject(layersObj);
            }

            return new SaveData
            {
                DefId = DefId,
                InstId = InstId,
                Origin = Origin,
                Width = Width,
                Height = Height,
                PayloadJson = root.ToString(Newtonsoft.Json.Formatting.None),
                OriginalSolidIds = SnapshotOriginalSolidIds()
            };
        }

        public override void FromSaveData(SaveData data)
        {
            RestoreBaseSaveData(data);

            _fuelIn = _fuelOut = null;
            _crucible = null;
            for (int i = 0; i < 9; i++) _ins[i] = null;

            _fuelTicksLeft = _fuelTicksMax = 0;
            _burningFuelTemperature = 0;
            _fuelResultItemId = null;
            _fuelResultAmount = 1;
            _fireHoldTicksLeft = 0;

            ResetAllSmeltProgressAndReservations();
            _droppedOnDestroy = false;

            if (!string.IsNullOrEmpty(data.PayloadJson))
            {
                JObject root = null;
                try { root = JObject.Parse(data.PayloadJson); }
                catch { root = null; }

                if (root != null)
                {
                    _fuelIn = UnpackSavedItem(root["fuelIn"]);
                    _fuelOut = UnpackSavedItem(root["fuelOut"]);
                    _crucible = UnpackSavedItem(root["crucible"]);

                    if (root["inputs"] is JArray ins)
                    {
                        for (int i = 0; i < 9; i++)
                            _ins[i] = (i < ins.Count) ? UnpackSavedItem(ins[i]) : null;
                    }

                    _fuelTicksLeft = root.Value<int?>("fuelTicksLeft") ?? 0;
                    _fuelTicksMax = root.Value<int?>("fuelTicksMax") ?? 0;
                    _burningFuelTemperature = root.Value<int?>("burningFuelTemperature") ?? 0;
                    _fuelResultItemId = root.Value<string>("fuelResultItemId");
                    _fuelResultAmount = root.Value<int?>("fuelResultAmount") ?? 1;
                    if (_fuelResultAmount < 1) _fuelResultAmount = 1;
                    _fireHoldTicksLeft = root.Value<int?>("fireHoldTicksLeft") ?? 0;

                    if (root["smeltDone"] is JArray doneArr)
                        for (int i = 0; i < 9 && i < doneArr.Count; i++)
                            _smeltTicksDone[i] = ToInt(doneArr[i], 0);

                    if (root["smeltNeed"] is JArray needArr)
                        for (int i = 0; i < 9 && i < needArr.Count; i++)
                            _smeltTicksNeed[i] = ToInt(needArr[i], 0);

                    if (root["reserved"] is JArray resArr)
                        for (int i = 0; i < 9 && i < resArr.Count; i++)
                            _reserved[i] = ToInt(resArr[i], 0) != 0;

                    if (root["reservedAmount"] is JArray resAmtArr)
                        for (int i = 0; i < 9 && i < resAmtArr.Count; i++)
                            _reservedAmount[i] = ToInt(resAmtArr[i], 0);

                    if (root["reservedFluidId"] is JArray resIdArr)
                    {
                        for (int i = 0; i < 9 && i < resIdArr.Count; i++)
                        {
                            var s = resIdArr[i]?.ToString();
                            _reservedFluidId[i] = string.IsNullOrEmpty(s) ? null : s;
                        }
                    }

                    _reservedTotal = root.Value<int?>("reservedTotal") ?? 0;
                    if (_reservedTotal < 0) _reservedTotal = 0;

                    if (_crucible != null && root["crucible_layers"] != null && root["crucible_layers"].Type != JTokenType.Null)
                    {
                        var list = root["crucible_layers"].ToObject<List<object>>();
                        if (list != null)
                            _crucible.SetDetail("layers", list);
                    }
                }
            }

            CleanupZeroCountSlots();
            RecomputeReservationsByPriority();
            RequestApplyFurnaceMeta(IsBurning);
        }
    }
}
