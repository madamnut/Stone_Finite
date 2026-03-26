


using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public partial class Campfire : Multiblock
    {
        public enum SlotKind { FuelIn, FuelOut, IngredientIn, IngredientOut }
    
        

        ItemData _fuelIn;
        ItemData _fuelOut;
        ItemData _ingIn;
        ItemData _ingOut;
    
        
        int _fuelTicksLeft = 0;
        int _fuelTicksMax  = 0;
    
        
        string _fuelResultItemId = null;
        int _fuelResultAmount = 1; 
    
        
        int _cookTicksDone = 0;
        int _cookTicksNeed = 0;
        string _cookResultItemId = null;
        int _cookResultAmount = 1; 
    
        
        string _prevIngItemId = null;
        int _prevIngDur = 0;
    
        
        bool _droppedOnDestroy = false;
    
        
        
        const int FIRE_HOLD_TICKS = 5;
        int _fireHoldTicksLeft = 0;
    
        
        public bool Isburning => _fuelTicksLeft > 0;
    
        
        bool IsFireActiveFx => _fuelTicksLeft > 0 || _fireHoldTicksLeft > 0;
    
        public float FuelProgress01
        {
            get
            {
                if (_fuelTicksMax <= 0) return 0f;
                return Mathf.Clamp01((float)_fuelTicksLeft / _fuelTicksMax);
            }
        }
    
        public float CookProgress01
        {
            get
            {
                if (_cookTicksNeed <= 0) return 0f;
                return Mathf.Clamp01((float)_cookTicksDone / _cookTicksNeed);
            }
        }
    
        
        public ItemData GetSlot(SlotKind kind)
        {
            return kind switch
            {
                SlotKind.FuelIn        => _fuelIn,
                SlotKind.FuelOut       => _fuelOut,
                SlotKind.IngredientIn  => _ingIn,
                SlotKind.IngredientOut => _ingOut,
                _ => null
            };
        }
    
        
        public void SetSlot(SlotKind kind, ItemData item)
        {
            switch (kind)
            {
                case SlotKind.FuelIn:        _fuelIn = item; break;
                case SlotKind.FuelOut:       _fuelOut = item; break;
                case SlotKind.IngredientIn:  _ingIn = item; break;
                case SlotKind.IngredientOut: _ingOut = item; break;
            }
    
            CleanupZeroCountSlots();
        }
    
        
        public override void OnInteract(Vector2Int hitCell)
        {
            Manager.OpenModule("Campfire", this);
        }
    
        #if false
        public override void OnCellBroken(Vector2Int brokenCell)
        {
            if (!_droppedOnDestroy)
            {
                _droppedOnDestroy = true;
                DropAllInternalItems();
            }
    
            base.OnCellBroken(brokenCell);
        }
        #endif
    
        
        
        
        public override void GetVfxRequests(List<VfxRequest> outList)
        {
            if (outList == null) return;
    
            outList.Add(new VfxRequest
            {
                key    = "Fire_01",
                offset = new Vector2(1f, .8f),
                active = IsFireActiveFx
            });
        }
    
        
        public override void Tick()
        {
            CleanupZeroCountSlots();
    
            bool wasBurning   = Isburning;      
            bool wasFireFxOn  = IsFireActiveFx; 
    
            if (IngredientChanged())
            {
                ResetCookProgress();
                SnapshotIngredient();
            }
    
            bool canCookNow = CanCookNow(out int cookNeed, out string cookResult, out int cookAmount);
            bool ingOutBlocked = IsOutputFullOrBlocked(_ingOut, cookResult, cookAmount);
    
            
            if (_fuelTicksLeft <= 0)
            {
                if (!string.IsNullOrEmpty(_fuelResultItemId))
                {
                    TryPushFuelResultToFuelOut();
                }
                else
                {
                    if (canCookNow && !ingOutBlocked)
                    {
                        if (!IsFuelOutBlockedForNewFuel())
                            TryStartBurnFromFuelIn();
                    }
                }
            }
    
            
            if (_fuelTicksLeft > 0)
            {
                _fuelTicksLeft -= 1;
                if (_fuelTicksLeft < 0) _fuelTicksLeft = 0;
    
                
                if (_fuelTicksLeft > 0) _fireHoldTicksLeft = 0;
            }
    
            
            if (wasBurning && _fuelTicksLeft <= 0)
            {
                _fireHoldTicksLeft = FIRE_HOLD_TICKS;
            }
    
            
            if (_fuelTicksLeft <= 0 && _fireHoldTicksLeft > 0)
            {
                _fireHoldTicksLeft -= 1;
                if (_fireHoldTicksLeft < 0) _fireHoldTicksLeft = 0;
            }
    
            
            if (wasBurning && canCookNow && !ingOutBlocked)
            {
                if (_cookTicksNeed <= 0 || _cookResultItemId != cookResult)
                {
                    _cookTicksNeed = cookNeed;
                    _cookResultItemId = cookResult;
                    _cookResultAmount = Mathf.Max(1, cookAmount);
                    _cookTicksDone = Mathf.Clamp(_cookTicksDone, 0, _cookTicksNeed);
                }
    
                _cookTicksDone += 1;
                if (_cookTicksDone > _cookTicksNeed) _cookTicksDone = _cookTicksNeed;
    
                if (_cookTicksNeed > 0 && _cookTicksDone >= _cookTicksNeed)
                {
                    
                    if (!IsOutputFullOrBlocked(_ingOut, _cookResultItemId, _cookResultAmount))
                    {
                        if (ConsumeOne(_ingIn))
                        {
                            if (_ingIn != null && _ingIn.Count <= 0) _ingIn = null;
                            TryProduceCookResult();
                        }
    
                        ResetCookProgress();
                        SnapshotIngredient();
                    }
                }
            }
    
            
            if (wasBurning && _fuelTicksLeft <= 0)
            {
                TryPushFuelResultToFuelOut();
            }
    
            
            bool isFireFxOnNow = IsFireActiveFx;
            if (wasFireFxOn != isFireFxOnNow)
            {
                RequestApplyCampfireMeta(isFireFxOnNow);
            }
    
            CleanupZeroCountSlots();
        }
    
        
        void RequestApplyCampfireMeta(bool burning)
        {
            if (Manager == null) return;
            
            Manager.ApplyMetaToAllOccupiedCells(this, (ushort)(burning ? 6 : 0));
        }
    
        
        void CleanupZeroCountSlots()
        {
            if (_fuelIn != null && _fuelIn.Count <= 0) _fuelIn = null;
            if (_fuelOut != null && _fuelOut.Count <= 0) _fuelOut = null;
            if (_ingIn != null && _ingIn.Count <= 0) _ingIn = null;
            if (_ingOut != null && _ingOut.Count <= 0) _ingOut = null;
        }
    
        
        bool IngredientChanged()
        {
            
            if (_ingIn == null || _ingIn.Count <= 0)
                return !string.IsNullOrEmpty(_prevIngItemId);
    
            string curId = _ingIn.ItemId;
            int curDur = _ingIn.Durability;
    
            if (string.IsNullOrEmpty(_prevIngItemId)) return true;
            if (curId != _prevIngItemId) return true;
            if (curDur != _prevIngDur) return true;
    
            
            return false;
        }
    
        
        void SnapshotIngredient()
        {
            if (_ingIn == null || _ingIn.Count <= 0)
            {
                _prevIngItemId = null;
                _prevIngDur = 0;
                return;
            }
    
            _prevIngItemId = _ingIn.ItemId;
            _prevIngDur = _ingIn.Durability;
        }
    
        
        void ResetCookProgress()
        {
            _cookTicksDone = 0;
            _cookTicksNeed = 0;
            _cookResultItemId = null;
            _cookResultAmount = 1;
        }
    
        
        bool CanCookNow(out int cookNeed, out string cookResult, out int cookAmount)
        {
            cookNeed = 0;
            cookResult = null;
            cookAmount = 1;
    
            if (_ingIn == null) return false;
            if (_ingIn.Count <= 0) return false;
    
            if (_ingIn.ToolActions == null) return false;
            if (!_ingIn.ToolActions.TryGetValue("Cook", out Dictionary<string, object> cfg) || cfg == null)
                return false;
    
            if (cfg.TryGetValue("cookTicks", out var ctObj) && ctObj != null)
            {
                if (ctObj is int i) cookNeed = i;
                
                else if (ctObj is long l) cookNeed = (int)l;
                
                else if (ctObj is float f) cookNeed = Mathf.RoundToInt(f);
                
                else if (ctObj is double d) cookNeed = (int)d;
                else int.TryParse(ctObj.ToString(), out cookNeed);
            }
    
            if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
                cookResult = riObj.ToString();
    
            if (cfg.TryGetValue("amount", out var amObj) && amObj != null)
            {
                if (amObj is int i) cookAmount = i;
                
                else if (amObj is long l) cookAmount = (int)l;
                
                else if (amObj is float f) cookAmount = Mathf.RoundToInt(f);
                
                else if (amObj is double d) cookAmount = (int)d;
                else int.TryParse(amObj.ToString(), out cookAmount);
            }
            cookAmount = Mathf.Max(1, cookAmount);
    
            if (cookNeed <= 0) return false;
            if (string.IsNullOrEmpty(cookResult)) return false;
    
            return true;
        }
    
        
        bool IsFuelOutBlockedForNewFuel()
        {
            if (_fuelIn == null) return true;
    
            if (_fuelIn.ToolActions == null) return true;
            if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
                return true;
    
            string resultItem = null;
            int amount = 1;
    
            if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
                resultItem = riObj.ToString();
    
            if (cfg.TryGetValue("amount", out var amObj) && amObj != null)
            {
                if (amObj is int i) amount = i;
                
                else if (amObj is long l) amount = (int)l;
                
                else if (amObj is float f) amount = Mathf.RoundToInt(f);
                
                else if (amObj is double d) amount = (int)d;
                else int.TryParse(amObj.ToString(), out amount);
            }
            amount = Mathf.Max(1, amount);
    
            if (string.IsNullOrEmpty(resultItem))
                return false;
    
            
            if (_fuelOut == null) return false;
    
            
            if (_fuelOut.ItemId != resultItem) return true;
    
            
            return (_fuelOut.Count + amount) > _fuelOut.MaxStack;
        }
    
        
        void TryStartBurnFromFuelIn()
        {
            if (_fuelIn == null) return;
            if (_fuelIn.Count <= 0) { _fuelIn = null; return; }
    
            if (_fuelIn.ToolActions == null) return;
            if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
                return;
    
            int burnTicks = 0;
            string resultItem = null;
            int amount = 1;
    
            if (cfg.TryGetValue("burnTicks", out var btObj) && btObj != null)
            {
                if (btObj is int i) burnTicks = i;
                
                else if (btObj is long l) burnTicks = (int)l;
                
                else if (btObj is float f) burnTicks = Mathf.RoundToInt(f);
                
                else if (btObj is double d) burnTicks = (int)d;
                else int.TryParse(btObj.ToString(), out burnTicks);
            }
    
            if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
                resultItem = riObj.ToString();
    
            if (cfg.TryGetValue("amount", out var amObj) && amObj != null)
            {
                if (amObj is int i) amount = i;
                
                else if (amObj is long l) amount = (int)l;
                
                else if (amObj is float f) amount = Mathf.RoundToInt(f);
                
                else if (amObj is double d) amount = (int)d;
                else int.TryParse(amObj.ToString(), out amount);
            }
            amount = Mathf.Max(1, amount);
    
            if (burnTicks <= 0) return;
    
            _fuelIn.Count -= 1;
            if (_fuelIn.Count <= 0) _fuelIn = null;
    
            _fuelTicksLeft = burnTicks;
            _fuelTicksMax  = burnTicks;
    
            _fuelResultItemId = resultItem;
            _fuelResultAmount = amount;
    
            
            _fireHoldTicksLeft = 0;
    
            CleanupZeroCountSlots();
    
            
            RequestApplyCampfireMeta(true);
        }
    
        
        void TryPushFuelResultToFuelOut()
        {
            if (string.IsNullOrEmpty(_fuelResultItemId))
            {
                _fuelTicksMax = 0;
                _fuelResultAmount = 1;
                return;
            }
    
            int amount = Mathf.Max(1, _fuelResultAmount);
    
            
            if (IsOutputFullOrBlocked(_fuelOut, _fuelResultItemId, amount))
                return;
    
            if (_fuelOut == null)
            {
                if (Manager != null && Manager.ItemLibrary != null)
                    _fuelOut = Manager.ItemLibrary.Create(_fuelResultItemId, amount);
    
                if (_fuelOut != null)
                {
                    _fuelResultItemId = null;
                    _fuelResultAmount = 1;
                    _fuelTicksMax = 0;
                }
    
                CleanupZeroCountSlots();
                return;
            }
    
            _fuelOut.Count += amount;
    
            _fuelResultItemId = null;
            _fuelResultAmount = 1;
            _fuelTicksMax = 0;
    
            CleanupZeroCountSlots();
        }
    
        
        bool IsOutputFullOrBlocked(ItemData outSlot, string expectedItemId, int requiredAmount)
        {
            if (string.IsNullOrEmpty(expectedItemId))
                return false;
    
            requiredAmount = Mathf.Max(1, requiredAmount);
    
            if (outSlot == null) return false;
            if (outSlot.ItemId != expectedItemId) return true;
    
            return (outSlot.Count + requiredAmount) > outSlot.MaxStack;
        }
    
        
        void TryProduceCookResult()
        {
            if (string.IsNullOrEmpty(_cookResultItemId)) return;
    
            int amount = Mathf.Max(1, _cookResultAmount);
    
            
            if (IsOutputFullOrBlocked(_ingOut, _cookResultItemId, amount))
                return;
    
            if (_ingOut == null)
            {
                if (Manager != null && Manager.ItemLibrary != null)
                    _ingOut = Manager.ItemLibrary.Create(_cookResultItemId, amount);
    
                CleanupZeroCountSlots();
                return;
            }
    
            _ingOut.Count += amount;
            CleanupZeroCountSlots();
        }
    
        
        bool ConsumeOne(ItemData it)
        {
            if (it == null) return false;
            if (it.Count <= 0) return false;
            it.Count -= 1;
            return true;
        }
    
        #if false
        void DropAllInternalItems()
        {
            if (World == null || World.itemDropper == null)
                return;
    
            Vector3 origin = new Vector3(
                Origin.x + (Width * 0.5f),
                Origin.y + (Height * 0.5f),
                0f
            );
    
            DropSlot(ref _fuelIn, origin);
            DropSlot(ref _fuelOut, origin);
            DropSlot(ref _ingIn, origin);
            DropSlot(ref _ingOut, origin);
        }
    
        void DropSlot(ref ItemData slot, Vector3 origin)
        {
            if (slot == null) return;
            if (slot.Count <= 0) { slot = null; return; }
    
            var copy = new ItemData(
                itemId:        slot.ItemId,
                name:          slot.Name,
                spriteName:    slot.SpriteName,
                itemType:      slot.ItemType,
                maxStack:      slot.MaxStack,
                maxDurability: slot.MaxDurability,
                durability:    slot.Durability,
                toolActions:   slot.ToolActions,
                weaponActions: slot.WeaponActions,
                breakActions:  slot.BreakActions,
                tags:          slot.Tags,
                details:       slot.Details,
                icon:          slot.Icon,
                count:         slot.Count
            );
    
            World.itemDropper.SpawnDroppedItem(copy, origin);
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
                    ["id"]    = it.ItemId,
                    ["count"] = it.Count,
                    ["dur"]   = it.Durability
                };
            }
    
            root["fuelIn"]  = PackItem(_fuelIn);
            root["fuelOut"] = PackItem(_fuelOut);
            root["ingIn"]   = PackItem(_ingIn);
            root["ingOut"]  = PackItem(_ingOut);
    
            root["fuelTicksLeft"]    = _fuelTicksLeft;
            root["fuelTicksMax"]     = _fuelTicksMax;
            root["fuelResultItemId"] = _fuelResultItemId;
            root["fuelResultAmount"] = _fuelResultAmount;
    
            root["cookTicksDone"]    = _cookTicksDone;
            root["cookTicksNeed"]    = _cookTicksNeed;
            root["cookResultItemId"] = _cookResultItemId;
            root["cookResultAmount"] = _cookResultAmount;
    
            root["prevIngItemId"]    = _prevIngItemId;
            root["prevIngDur"]       = _prevIngDur;
    
            
            ushort[] orig = new ushort[Width * Height];
            for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
            {
                var cell = new Vector2Int(Origin.x + x, Origin.y + y);
                orig[x + y * Width] = originalSolidIds.TryGetValue(cell, out var id) ? id : (ushort)0;
            }
    
            return new SaveData
            {
                DefId = DefId,
                InstId = InstId,
                Origin = Origin,
                Width = Width,
                Height = Height,
                PayloadJson = root.ToString(),
                OriginalSolidIds = orig
            };
        }
    
        public override void FromSaveData(SaveData data)
        {
            DefId  = data.DefId;
            InstId = data.InstId;
            Origin = data.Origin;
            Width  = data.Width;
            Height = data.Height;
    
            occupiedCells.Clear();
            for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                    occupiedCells.Add(new Vector2Int(Origin.x + x, Origin.y + y));
    
            
            originalSolidIds.Clear();
            if (data.OriginalSolidIds != null && data.OriginalSolidIds.Length == Width * Height)
            {
                for (int y = 0; y < Height; y++)
                for (int x = 0; x < Width; x++)
                {
                    var cell = new Vector2Int(Origin.x + x, Origin.y + y);
                    originalSolidIds[cell] = data.OriginalSolidIds[x + y * Width];
                }
            }
    
            
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
                    ItemData UnpackItem(JToken tok)
                    {
                        if (tok == null || tok.Type == JTokenType.Null) return null;
    
                        string id = tok.Value<string>("id");
                        int count = tok.Value<int?>("count") ?? 0;
                        int dur   = tok.Value<int?>("dur") ?? 0;
    
                        if (string.IsNullOrEmpty(id) || count <= 0) return null;
    
                        ItemData it = null;
                        if (Manager != null && Manager.ItemLibrary != null)
                            it = Manager.ItemLibrary.Create(id, count);
    
                        if (it != null)
                            it.Durability = dur;
    
                        return it;
                    }
    
                    _fuelIn  = UnpackItem(root["fuelIn"]);
                    _fuelOut = UnpackItem(root["fuelOut"]);
                    _ingIn   = UnpackItem(root["ingIn"]);
                    _ingOut  = UnpackItem(root["ingOut"]);
    
                    _fuelTicksLeft    = root.Value<int?>("fuelTicksLeft") ?? 0;
                    _fuelTicksMax     = root.Value<int?>("fuelTicksMax") ?? 0;
                    _fuelResultItemId = root.Value<string>("fuelResultItemId");
                    _fuelResultAmount = root.Value<int?>("fuelResultAmount") ?? 1;
                    if (_fuelResultAmount < 1) _fuelResultAmount = 1;
    
                    _cookTicksDone    = root.Value<int?>("cookTicksDone") ?? 0;
                    _cookTicksNeed    = root.Value<int?>("cookTicksNeed") ?? 0;
                    _cookResultItemId = root.Value<string>("cookResultItemId");
                    _cookResultAmount = root.Value<int?>("cookResultAmount") ?? 1;
                    if (_cookResultAmount < 1) _cookResultAmount = 1;
    
                    _prevIngItemId    = root.Value<string>("prevIngItemId");
                    _prevIngDur       = root.Value<int?>("prevIngDur") ?? 0;
                }
            }
    
            CleanupZeroCountSlots();
    
            
            RequestApplyCampfireMeta(IsFireActiveFx);
        }
        #endif
    }
}
