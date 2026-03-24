// Campfire.cs (?袁⑷퍥 ?대Ŋ猿쒑퉪? - Fuel?? ?癒곗넅 ??野꺜??彛? Cook?? amount ?⑤벀而???곸몵筌??袁⑥┷/???걟 ????
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.Player;

namespace Game.World
{
    public class Campfire : Multiblock
    {
        public enum SlotKind { FuelIn, FuelOut, IngredientIn, IngredientOut }
    
        // 4 slots (?遺쎈럡??鍮?
        ItemData _fuelIn;
        ItemData _fuelOut;
        ItemData _ingIn;
        ItemData _ingOut;
    
        // ?怨뺤┷ 甕곌쑵???븍뜃??野껊슣?좑쭪?)
        int _fuelTicksLeft = 0;
        int _fuelTicksMax  = 0;
    
        // ?袁⑹삺 ?怨뺤┷揶쎛 ????筌???륁궎???봔?怨빿??? Ash)
        string _fuelResultItemId = null;
        int _fuelResultAmount = 1; // Fuel.amount
    
        // ?遺얄봺 筌욊쑵六?野껊슣?좑쭪?)
        int _cookTicksDone = 0;
        int _cookTicksNeed = 0;
        string _cookResultItemId = null;
        int _cookResultAmount = 1; // Cook.amount
    
        // ??利?癰궰野?揶쏅Ŋ???(Count 癰궰?遺얜뮉 ?얜똻?? ??쎄문 ?브쑵釉???뱁뒄疫????λ뜃由??獄쎻뫗?)
        string _prevIngItemId = null;
        int _prevIngDur = 0;
    
        // ???댘 ??뺤뿻 揶쎛??筌렺?怨뺥닜???닌딄쉐 ????쇱뵠 ????甕?繹먥뫁議??1???춸 ??뺤뿻)
        bool _droppedOnDestroy = false;
    
        // ?????????????????? ??VFX ??? 獄쎻뫗?(??됰뮞???봺??뽯뮞) ??????????????????
        // ?怨뺤┷揶쎛 0????筌욊낱??癒?즲 N????덈툧 ?븍뜆???醫? (?怨뺤┷ ?대Ŋ猿?1???곗눘彛?獄쎻뫗?)
        const int FIRE_HOLD_TICKS = 5;
        int _fireHoldTicksLeft = 0;
    
        // ??겸봺??"??????됱벉"(?遺얄봺/?怨뺤┷ ???걟 疫꿸퀣?)
        public bool Isburning => _fuelTicksLeft > 0;
    
        // ??뽯뻻/筌롫??/VFX??"?븍뜆??癰귣똻????怨밴묶"
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
    
        public override void OnInteract(Game.Player.Player player, Vector2Int hitCell)
        {
            Manager.OpenModule("Campfire", this);
        }
    
        public override void OnCellBroken(Vector2Int brokenCell)
        {
            if (!_droppedOnDestroy)
            {
                _droppedOnDestroy = true;
                DropAllInternalItems();
            }
    
            base.OnCellBroken(brokenCell);
        }
    
        // ?????????????????? VFX ?遺욧퍕 ??????????????????
        // Campfire??Fire_01 ??롪돌筌????? Origin 疫꿸퀣? (1, 0.5)
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
    
            bool wasBurning   = Isburning;      // ?遺얄봺/?怨뺤┷ ???걟 疫꿸퀣?
            bool wasFireFxOn  = IsFireActiveFx; // ??뽯뻻/筌롫?? 疫꿸퀣?
    
            if (IngredientChanged())
            {
                ResetCookProgress();
                SnapshotIngredient();
            }
    
            bool canCookNow = CanCookNow(out int cookNeed, out string cookResult, out int cookAmount);
            bool ingOutBlocked = IsOutputFullOrBlocked(_ingOut, cookResult, cookAmount);
    
            // 1) ?怨뺤┷揶쎛 ??곸몵筌? "?遺얄봺 揶쎛?館釉??怨뱀넺"?癒?퐣筌??癒곗넅 ??뺣즲
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
    
            // 2) ?븍뜃????녹뮇議??됱몵筌? ??利??醫듢??怨???곸뵠 ?怨뺤┷???얜똻?쒎쳞?揶쏅Ŋ??
            if (_fuelTicksLeft > 0)
            {
                _fuelTicksLeft -= 1;
                if (_fuelTicksLeft < 0) _fuelTicksLeft = 0;
    
                // ??쇱젫 ?怨뺤┷揶쎛 ????餓λ쵐?좑쭖?hold???袁⑹뒄 ??곸벉
                if (_fuelTicksLeft > 0) _fireHoldTicksLeft = 0;
            }
    
            // 2.5) ?怨뺤┷揶쎛 獄쎻뫕????멸텆 野껋럩?? hold ??뽰삂(??苡?????꾩뜎?봔??
            if (wasBurning && _fuelTicksLeft <= 0)
            {
                _fireHoldTicksLeft = FIRE_HOLD_TICKS;
            }
    
            // 2.6) hold ??揶쏅Ŋ??(?怨뺤┷揶쎛 ??용뮉 ??덈툧筌?
            if (_fuelTicksLeft <= 0 && _fireHoldTicksLeft > 0)
            {
                _fireHoldTicksLeft -= 1;
                if (_fireHoldTicksLeft < 0) _fireHoldTicksLeft = 0;
            }
    
            // 3) ?遺얄봺?? "????뽰삂 ???븍뜃??ON" + ??利??곗뮆??鈺곌퀗援?筌띾슣????뽯퓠筌?筌욊쑵六?
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
                    // amount筌띾슦寃?野껉퀗???????⑤벀而????곸몵筌?"?袁⑥┷/???걟" ?癒?퍥揶쎛 ??깅선??? ??놁벉
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
    
            // 4) ??苡??源녿퓠 ?븍뜆????멸텢??겹늺 ?봔?怨빿?筌ｌ꼶??
            if (wasBurning && _fuelTicksLeft <= 0)
            {
                TryPushFuelResultToFuelOut();
            }
    
            // 5) ?怨밴묶 ?袁⑹뵠(edge)?癒?퐣筌?"筌?쥚遊???뵠???袁⑷퍥 ??곕뱜" meta 癰궰野??遺욧퍕
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
            // Campfire??筌뤴뫀諭???곕뱜揶쎛 揶쏆늿??癰궰?? Default(meta=0), Burning(meta=6)
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
            // ?袁⑹삺 ??利???곸벉(?癒?뮉 0揶?: ??곸읈????利뷴첎? ??됰???겹늺 癰궰野껋럩?앮에?揶쏄쑴竊?
            if (_ingIn == null || _ingIn.Count <= 0)
                return !string.IsNullOrEmpty(_prevIngItemId);
    
            string curId = _ingIn.ItemId;
            int curDur = _ingIn.Durability;
    
            if (string.IsNullOrEmpty(_prevIngItemId)) return true;
            if (curId != _prevIngItemId) return true;
            if (curDur != _prevIngDur) return true;
    
            // Count 癰궰?遺얜뮉 ?얜똻??
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
    
            // fuelOut????쑴堉??됱몵筌???湲?OK
            if (_fuelOut == null) return false;
    
            // ??삘뀲 ?袁⑹뵠??뽰뵠 ??쇰선??됱몵筌?筌띾맪??
            if (_fuelOut.ItemId != resultItem) return true;
    
            // amount筌띾슦寃???쇰선揶??⑤벀而????곸몵筌?筌띾맪??
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
    
            // ???怨뺤┷揶쎛 ??쇰선??삠늺 hold????놁벉
            _fireHoldTicksLeft = 0;
    
            CleanupZeroCountSlots();
    
            // ?癒곗넅 筌앸맩??meta 癰궰野??遺욧퍕(?븍뜃???녹뮇彛???볦퍢?봔??
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
    
            // ??됱읈??????????醫? (?類ㅼ퐠??"????野꺜??彛????筌? ?????獄쏅뗀?????????癒?뼄 獄쎻뫗?)
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
    
            // ?袁⑥┷ ??뽰젎?癒?즲 amount筌띾슦寃??⑤벀而???곸몵筌???밴쉐 ????域??袁⑸퓠 Tick?癒?퐣 ??? 筌띾맩釉?癒?┸ ??
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
    
            // 嚥≪뮆諭???meta???袁⑹삺 ?怨밴묶 疫꿸퀣???곗쨮 筌띿쉸????뽯뻻/筌롫?? 疫꿸퀣?)
            RequestApplyCampfireMeta(IsFireActiveFx);
        }
    }
}
