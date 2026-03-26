// Campfire.cs (?熬곣뫕????흮?우뮂?? - Fuel?? ??믨퀣?????롪틵???壤? Cook?? amount ??ㅻ?????怨몃さ嶺??熬곣뫁?????嫄?????
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
    
        // 4 slots (??븐럥?????
        ItemData _fuelIn;
        ItemData _fuelOut;
        ItemData _ingIn;
        ItemData _ingOut;
    
        // ??⑤벡???뺢퀗????釉띾쐝???롪퍓??醫묒??)
        int _fuelTicksLeft = 0;
        int _fuelTicksMax  = 0;
    
        // ?熬곣뫗????⑤벡?룡뤆?쎛 ????嶺???瑜곴텕???遊붋??⑤뮈??? Ash)
        string _fuelResultItemId = null;
        int _fuelResultAmount = 1; // Fuel.amount
    
        // ??븐뻹遊?嶺뚯쉳?듸쭛??롪퍓??醫묒??)
        int _cookTicksDone = 0;
        int _cookTicksNeed = 0;
        string _cookResultItemId = null;
        int _cookResultAmount = 1; // Cook.amount
    
        // ??筌??곌떠????띠룆흮???(Count ?곌떠???븐뼔裕???쒕샍?? ???꾨Ц ?釉뚯뫓????諭곷뭵?????貫?껆뵳???꾩렮維?)
        string _prevIngItemId = null;
        int _prevIngDur = 0;
    
        // ???????類ㅻ옐 ?띠럾???嶺뚮졋???⑤벤?????뚮봽???????깅턄 ??????濚밸?維곮???1???異???類ㅻ옐)
        bool _droppedOnDestroy = false;
    
        // ?????????????????? ??VFX ??? ?꾩렮維?(???곕츩???遊??戮?츩) ??????????????????
        // ??⑤벡?룡뤆?쎛 0????嶺뚯쉳?????利?N?????덊닱 ?釉띾쐠????? (??⑤벡????흮??1???怨쀫닔壤??꾩렮維?)
        const int FIRE_HOLD_TICKS = 5;
        int _fireHoldTicksLeft = 0;
    
        // ??寃몃뉴??"???????깅쾳"(??븐뻹遊???⑤벡?????嫄??リ옇??)
        public bool Isburning => _fuelTicksLeft > 0;
    
        // ??戮?뻣/嶺뚮∥??/VFX??"?釉띾쐠???곌랜??????⑤객臾?
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
    
        // ?????????????????? VFX ??븐슙????????????????????
        // Campfire??Fire_01 ??濡る룎嶺????? Origin ?リ옇?? (1, 0.5)
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
    
            bool wasBurning   = Isburning;      // ??븐뻹遊???⑤벡?????嫄??リ옇??
            bool wasFireFxOn  = IsFireActiveFx; // ??戮?뻣/嶺뚮∥?? ?リ옇??
    
            if (IngredientChanged())
            {
                ResetCookProgress();
                SnapshotIngredient();
            }
    
            bool canCookNow = CanCookNow(out int cookNeed, out string cookResult, out int cookAmount);
            bool ingOutBlocked = IsOutputFullOrBlocked(_ingOut, cookResult, cookAmount);
    
            // 1) ??⑤벡?룡뤆?쎛 ??怨몃さ嶺? "??븐뻹遊??띠럾??繞③뇡???⑤???????ｇ춯???믨퀣????類ｌ┣
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
    
            // 2) ?釉띾쐝?????밸츋鈺???깅さ嶺? ??筌???ル뱼??????怨몃턄 ??⑤벡?????쒕샍??롮퀪??띠룆흮??
            if (_fuelTicksLeft > 0)
            {
                _fuelTicksLeft -= 1;
                if (_fuelTicksLeft < 0) _fuelTicksLeft = 0;
    
                // ???깆젷 ??⑤벡?룡뤆?쎛 ????繞벿살탳?醫묒춺?hold???熬곣뫗????怨몃쾳
                if (_fuelTicksLeft > 0) _fireHoldTicksLeft = 0;
            }
    
            // 2.5) ??⑤벡?룡뤆?쎛 ?꾩렮維????硫명뀊 ?롪퍔??? hold ??戮곗굚(?????????袁⑸쐩?遊붋??
            if (wasBurning && _fuelTicksLeft <= 0)
            {
                _fireHoldTicksLeft = FIRE_HOLD_TICKS;
            }
    
            // 2.6) hold ???띠룆흮??(??⑤벡?룡뤆?쎛 ???⑸츎 ???덊닱嶺?
            if (_fuelTicksLeft <= 0 && _fireHoldTicksLeft > 0)
            {
                _fireHoldTicksLeft -= 1;
                if (_fireHoldTicksLeft < 0) _fireHoldTicksLeft = 0;
            }
    
            // 3) ??븐뻹遊?? "????戮곗굚 ???釉띾쐝??ON" + ??筌??怨쀫츊???브퀗?쀦뤃?嶺뚮씭?????戮?뱺嶺?嶺뚯쉳?듸쭛?
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
                    // amount嶺뚮씭??칰??롪퍒?????????ㅻ??????怨몃さ嶺?"?熬곣뫁?????嫄? ????ζ뤆?쎛 ??源낆꽑??? ???곷쾳
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
    
            // 4) ??????繹먮끏???釉띾쐠????硫명뀬??寃밸듆 ?遊붋??⑤뮈?嶺뚳퐣瑗??
            if (wasBurning && _fuelTicksLeft <= 0)
            {
                TryPushFuelResultToFuelOut();
            }
    
            // 5) ??⑤객臾??熬곣뫗逾?edge)????ｇ춯?"嶺?伊싮걡???逾???熬곣뫕????怨뺣콦" meta ?곌떠?????븐슙??
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
            // Campfire??嶺뚮ㅄ維獄???怨뺣콦?띠럾? ?띠룇????곌떠??? Default(meta=0), Burning(meta=6)
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
            // ?熬곣뫗????筌???怨몃쾳(???裕?0??: ??怨몄쓧????筌앸럽泥? ??????寃밸듆 ?곌떠??롪퍔????뿉??띠룄?당쳥?
            if (_ingIn == null || _ingIn.Count <= 0)
                return !string.IsNullOrEmpty(_prevIngItemId);
    
            string curId = _ingIn.ItemId;
            int curDur = _ingIn.Durability;
    
            if (string.IsNullOrEmpty(_prevIngItemId)) return true;
            if (curId != _prevIngItemId) return true;
            if (curDur != _prevIngDur) return true;
    
            // Count ?곌떠???븐뼔裕???쒕샍??
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
    
            // fuelOut?????닷젆???깅さ嶺???疫?OK
            if (_fuelOut == null) return false;
    
            // ???섎??熬곣뫗逾??戮곕턄 ???곗꽑???깅さ嶺?嶺뚮씭留??
            if (_fuelOut.ItemId != resultItem) return true;
    
            // amount嶺뚮씭??칰????곗꽑????ㅻ??????怨몃さ嶺?嶺뚮씭留??
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
    
            // ????⑤벡?룡뤆?쎛 ???곗꽑???좊듆 hold?????곷쾳
            _fireHoldTicksLeft = 0;
    
            CleanupZeroCountSlots();
    
            // ??믨퀣??嶺뚯빖留??meta ?곌떠?????븐슙???釉띾쐝????밸츋壤???蹂?뜟?遊붋??
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
    
            // ???깆쓧???????????? (?筌먦끉???"?????롪틵???壤????嶺? ??????꾩룆????????????堉??꾩렮維?)
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
    
            // ?熬곣뫁????戮곗젍???利?amount嶺뚮씭??칰???ㅻ?????怨몃さ嶺???諛댁뎽 ???????熬곣뫖??Tick????????? 嶺뚮씭留⑶뇡???????
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
            // PayloadJson
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
    
            // OriginalSolidIds (row-major)
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
    
            // originalSolidIds restore (row-major)
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
    
            // defaults
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
    
            // payload load
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
    
            // ?β돦裕녻キ???meta???熬곣뫗????⑤객臾??リ옇????怨쀬Ŧ 嶺뚮씮?????戮?뻣/嶺뚮∥?? ?リ옇??)
            RequestApplyCampfireMeta(IsFireActiveFx);
        }
        #endif
    }
}
