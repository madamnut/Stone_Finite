// CokeOven.cs (?熬곣뫕????흮?우뮂??
// - FuelIn/FuelOut + MaterialIn + MaterialOut0/1
// - MaterialIn ?熬곣뫗逾??戮곕꺄 toolActions["Coke"] ??臾먮뺄 (cokeTicks + results[])
// - ??⑤벡???toolActions["Fuel"] ????(burnTicks + temperature + resultItem + amount)
// - ???? ???살┣ ??戮?츩?? "?熬곣뫗????⑥リ틬 繞벿살탳????⑤벡????temperature??嶺?흮???怨댄맋 ????(CokeOven cap=3)
//   - Coke ??얜???? 嶺뚣끉裕?????살┣ 2 ??怨대쭜?????異?嶺뚯쉳?듸쭛?(Wood temp=1 ?띠룇?? ??⑤벡?룟슖?る츊??嶺뚯쉳?듸쭛??釉띾쐝?)
// - VFX: Fire_02 (root ?リ옇?? local offset 1.5, 0.67) / ??⑤벡??????繞?+ hold ???덊닱 ??戮?뻣
// - Save/Load: Multiblock.SaveData.PayloadJson ????(CustomJson ??怨몃쾳)
// - Drop: World.itemDropper.SpawnDroppedItem ????(WorldManager.DropItemToGround ??怨몃쾳)
// - FIX: toolActions["Coke"].results ???堉??List<object>/JArray + ???爰?Dictionary/JObject 嶺뚮ㅄ維筌?嶺뚯솘???

using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.Core;

namespace Game.World
{
    public class CokeOven : Multiblock
    {
        public enum SlotKind
        {
            FuelIn, FuelOut,
            MaterialIn,
            MaterialOut0, MaterialOut1
        }
    
        const int FIRE_HOLD_TICKS = 5;
    
        // ???살┣ ??
        const int MAX_TEMP = 3;        // CokeOven cap
        const int MIN_COKE_TEMP = 2;   // Coke ??ㅻ쾴??嶺뚣끉裕?????살┣
    
        // Slots
        ItemData _fuelIn;
        ItemData _fuelOut;
    
        ItemData _matIn;
        ItemData _matOut0;
        ItemData _matOut1;
    
        // Fuel state
        int _fuelTicksLeft = 0;
        int _fuelTicksMax = 0;
    
        // ?熬곣뫗????⑥リ틬 繞벿살탳????⑤벡?룟슖?る츊???嶺?흮??????살┣
        int _currentTemp = 0;
    
        // ??⑤벡???遊붋??⑤뮈?? "??⑤벡??1?띠룇裕? ????????蹂?뜟" FuelOut??怨쀬Ŧ ?筌뤾쑬六?(嶺뚮씭留??귥춺?pending ???)
        string _fuelResultItemId = null;
        int _fuelResultAmount = 1;
    
        // VFX hold
        int _fireHoldTicksLeft = 0;
    
        // Coke progress
        int _cokeTicksDone = 0;
        int _cokeTicksNeed = 0;
    
        // Cached coke results (up to 2)
        string _res0Id = null; int _res0Amt = 1;
        string _res1Id = null; int _res1Amt = 1;
    
        // Input snapshot (change detection)
        string _prevInItemId = null;
        int _prevInDur = 0;
    
        bool IsBurning => _fuelTicksLeft > 0;
        bool IsFireActiveFx => _fuelTicksLeft > 0 || _fireHoldTicksLeft > 0;
    
        bool _droppedOnDestroy = false;
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Public read API (Module??
        // ??????????????????????????????????????????????????????????????????????????????????????????
        public ItemData GetSlot(SlotKind kind)
        {
            return kind switch
            {
                SlotKind.FuelIn => _fuelIn,
                SlotKind.FuelOut => _fuelOut,
                SlotKind.MaterialIn => _matIn,
                SlotKind.MaterialOut0 => _matOut0,
                SlotKind.MaterialOut1 => _matOut1,
                _ => null
            };
        }
    
        public void SetSlot(SlotKind kind, ItemData item)
        {
            switch (kind)
            {
                case SlotKind.FuelIn: _fuelIn = item; break;
                case SlotKind.FuelOut: _fuelOut = item; break;
                case SlotKind.MaterialIn: _matIn = item; break;
                case SlotKind.MaterialOut0: _matOut0 = item; break;
                case SlotKind.MaterialOut1: _matOut1 = item; break;
            }
    
            CleanupZeroCountSlots();
        }
    
        public float FuelProgress01
        {
            get
            {
                if (_fuelTicksMax <= 0) return 0f;
                return Mathf.Clamp01((float)_fuelTicksLeft / _fuelTicksMax);
            }
        }
    
        public float CokeProgress01
        {
            get
            {
                if (_cokeTicksNeed <= 0) return 0f;
                return Mathf.Clamp01((float)_cokeTicksDone / _cokeTicksNeed);
            }
        }
    
        public int CurrentTemperature => _currentTemp;
    
        public float Temperature01
        {
            get
            {
                if (MAX_TEMP <= 0) return 0f;
                return Mathf.Clamp01((float)_currentTemp / MAX_TEMP);
            }
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Interaction / VFX
        // ??????????????????????????????????????????????????????????????????????????????????????????
        public override void OnInteract(Vector2Int hitCell)
        {
            Manager.OpenModule("Coke Oven", this);
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
    
        public override void GetVfxRequests(List<VfxRequest> outList)
        {
            if (outList == null) return;
    
            outList.Add(new VfxRequest
            {
                key = "Fire_02",
                offset = new Vector2(1.5f, 0.67f),
                active = IsFireActiveFx
            });
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Tick
        // ??????????????????????????????????????????????????????????????????????????????????????????
        public override void Tick()
        {
            CleanupZeroCountSlots();
    
            // ???놁졑 ?곌떠????띠룆흮? ??嶺뚯쉳?듸쭛???洹먮봾??
            if (IngredientChanged())
            {
                ResetCokeProgress();
                SnapshotIngredient();
            }
    
            bool wasBurning = IsBurning;
    
            // ??얜???繞벿뮻??
            bool canCoke = TryPrepareCoke(
                out int needTicks,
                out string r0Id, out int r0Amt,
                out string r1Id, out int r1Amt,
                out bool outBlocked
            );
    
            // "嶺뚯쉳?듸쭛?????고뒎" ?잙?裕?? ?怨쀫츊??嶺뚮씭留????????얜???????????戮곗굚??? ???곷쾳
            bool hasWork = canCoke && !outBlocked;
    
            // (1) ??⑤벡?룡뤆?쎛 ?怨쀫닔鈺???깅さ嶺? pending ?遊붋??⑤뮈??筌뤾쑬六???類ｌ┣ ??hasWork?????異???믨퀣????類ｌ┣
            if (_fuelTicksLeft <= 0)
            {
                // pending fuel result push
                TryPushFuelResultToFuelOut();
    
                if (hasWork)
                {
                    // FuelOut 嶺뚮씭留??????????⑤벡????믨퀣???ル??
                    if (!IsFuelOutBlockedForNewFuel())
                    {
                        // ??믨퀣???熬곣뫖??"???????⑤벡?룡뤆?쎛 ??蹂κ텢?????살┣"???筌먦끉逾??怨댄맋,
                        // MIN_COKE_TEMP 亦껋꼶梨뜹퐲???????⑤벡????????? ???곷쾳
                        int peekTemp = PeekFuelTemperatureClamped();
                        if (peekTemp >= MIN_COKE_TEMP)
                        {
                            if (TryConsumeFuelOne(out int gainedTicks, out string fuelResId, out int fuelResAmt, out int fuelTemp))
                            {
                                _fuelTicksLeft = gainedTicks;
                                _fuelTicksMax = gainedTicks;
    
                                _currentTemp = Mathf.Clamp(fuelTemp, 0, MAX_TEMP);
    
                                _fuelResultItemId = fuelResId;
                                _fuelResultAmount = Mathf.Max(1, fuelResAmt);
    
                                // ??믨퀣???濡?듆 hold???怨쀫닔壤?
                                _fireHoldTicksLeft = 0;
                            }
                        }
                    }
                }
            }
    
            // (2) ??⑤벡???띠룆흮??(??밸츋鈺???깅さ嶺???얜?????ル뱼?? ??쒕뼬????우벟 ?띠룆흮??
            if (_fuelTicksLeft > 0)
            {
                _fuelTicksLeft -= 1;
                if (_fuelTicksLeft < 0) _fuelTicksLeft = 0;
            }
    
            // (3) ??⑤벡?룡뤆?쎛 ?꾩렮維???怨쀫닔雅??寃밸듆 hold ??戮곗굚 + ???살┣ ?洹먮봾??
            if (wasBurning && _fuelTicksLeft <= 0)
            {
                _fireHoldTicksLeft = FIRE_HOLD_TICKS;
                _currentTemp = 0;
            }
    
            // (4) hold ?띠룆흮??
            if (_fuelTicksLeft <= 0 && _fireHoldTicksLeft > 0)
            {
                _fireHoldTicksLeft -= 1;
                if (_fireHoldTicksLeft < 0) _fireHoldTicksLeft = 0;
            }
    
            // (5) Coke 嶺뚯쉳?듸쭛? "????戮곗굚 ???釉띾쐝??ON" ?リ옇??(= wasBurning) + 嶺뚣끉裕?????살┣ ?寃몃쳴??
            if (wasBurning && hasWork && _currentTemp >= MIN_COKE_TEMP)
            {
                // 嶺?흮???띠룄???
                _cokeTicksNeed = needTicks;
                _res0Id = r0Id; _res0Amt = Mathf.Max(1, r0Amt);
                _res1Id = r1Id; _res1Amt = Mathf.Max(1, r1Amt);
    
                _cokeTicksDone += 1;
                if (_cokeTicksNeed > 0 && _cokeTicksDone > _cokeTicksNeed)
                    _cokeTicksDone = _cokeTicksNeed;
    
                // ?熬곣뫁????ｋ걞?? ?怨쀫츊??2??嶺뚮ㅄ維筌??띠럾??繞③뇡????異?+ ?잙갭梨띌뇡節륁춹????놁졑 ???嫄?
                if (_cokeTicksNeed > 0 && _cokeTicksDone >= _cokeTicksNeed)
                {
                    bool blocked0 = IsOutputFullOrBlocked(_matOut0, _res0Id, _res0Amt);
                    bool blocked1 = string.IsNullOrEmpty(_res1Id) ? false : IsOutputFullOrBlocked(_matOut1, _res1Id, _res1Amt);
    
                    if (!blocked0 && !blocked1)
                    {
                        if (ConsumeOne(_matIn))
                        {
                            if (_matIn != null && _matIn.Count <= 0) _matIn = null;
    
                            TryProduceToOut(ref _matOut0, _res0Id, _res0Amt);
                            if (!string.IsNullOrEmpty(_res1Id))
                                TryProduceToOut(ref _matOut1, _res1Id, _res1Amt);
                        }
    
                        ResetCokeProgress();
                        SnapshotIngredient();
                    }
                }
            }
            else
            {
                // ?怨쀫츊??嶺뚮씭留?????놁졑 ??怨몃쾳/??얜????釉띾쐝?/???살┣ ?遊붋?브퀗??醫묒춺?嶺뚯쉳?듸쭛?熬곣뫀裕?0 ???
                if (!hasWork || _currentTemp < MIN_COKE_TEMP)
                    ResetCokeProgress();
            }
    
            // (6) meta: ??⑤벡?룡뤆?쎛 ???????깅さ嶺?1, ?熬곣뫀鍮띸춯?0
            RequestApplyCokeOvenMeta(IsBurning);
    
            CleanupZeroCountSlots();
        }
    
        void RequestApplyCokeOvenMeta(bool burning)
        {
            if (Manager == null) return;
            Manager.ApplyMetaToAllOccupiedCells(this, (ushort)(burning ? 6 : 0));
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Coke config
        // ??????????????????????????????????????????????????????????????????????????????????????????
        bool TryPrepareCoke(
            out int needTicks,
            out string res0Id, out int res0Amt,
            out string res1Id, out int res1Amt,
            out bool outBlocked
        )
        {
            needTicks = 0;
    
            res0Id = null; res0Amt = 1;
            res1Id = null; res1Amt = 1;
    
            outBlocked = true;
    
            if (_matIn == null) return false;
            if (_matIn.Count <= 0) return false;
    
            if (_matIn.ToolActions == null) return false;
            if (!_matIn.ToolActions.TryGetValue("Coke", out Dictionary<string, object> cfg) || cfg == null)
                return false;
    
            if (!cfg.TryGetValue("cokeTicks", out var ctObj) || ctObj == null)
                return false;
    
            needTicks = ToInt(ctObj, 0);
            if (needTicks <= 0) return false;
    
            if (!cfg.TryGetValue("results", out var rsObj) || rsObj == null)
                return false;
    
            // ??results??List<object> ?????逾?(List<object> / JArray 嶺뚮ㅄ維筌?嶺뚯솘???
            List<object> list = null;
    
            if (rsObj is List<object> lo)
            {
                list = lo;
            }
            else if (rsObj is JArray jarr)
            {
                list = new List<object>(jarr.Count);
                for (int i = 0; i < jarr.Count; i++)
                    list.Add(jarr[i]); // JToken ?잙갭梨???(????JObject)
            }
            else
            {
                return false;
            }
    
            if (list.Count <= 0) return false;
    
            // 0?뺢퀡????롪퍒???
            {
                var e0 = list[0];
    
                if (e0 is JObject jo0)
                {
                    res0Id = jo0.Value<string>("item");
                    res0Amt = jo0.Value<int?>("amount") ?? 1;
                }
                else if (e0 is Dictionary<string, object> d0)
                {
                    if (d0.TryGetValue("item", out var i0) && i0 != null) res0Id = i0.ToString();
                    if (d0.TryGetValue("amount", out var a0) && a0 != null) res0Amt = ToInt(a0, 1);
                }
            }
    
            // 1?뺢퀡????롪퍒?????ルㅎ臾?
            if (list.Count >= 2)
            {
                var e1 = list[1];
    
                if (e1 is JObject jo1)
                {
                    res1Id = jo1.Value<string>("item");
                    res1Amt = jo1.Value<int?>("amount") ?? 1;
                }
                else if (e1 is Dictionary<string, object> d1)
                {
                    if (d1.TryGetValue("item", out var i1) && i1 != null) res1Id = i1.ToString();
                    if (d1.TryGetValue("amount", out var a1) && a1 != null) res1Amt = ToInt(a1, 1);
                }
            }
            else
            {
                res1Id = null;
                res1Amt = 0;
            }
    
            // ???롪틵?嶺?(id ??怨몃さ嶺???얜????釉띾쐝???嶺뚳퐣瑗??
            if (string.IsNullOrEmpty(res0Id))
                return false;
    
            res0Amt = Mathf.Max(1, res0Amt);
            if (!string.IsNullOrEmpty(res1Id))
                res1Amt = Mathf.Max(1, res1Amt);
    
            bool blocked0 = IsOutputFullOrBlocked(_matOut0, res0Id, res0Amt);
            bool blocked1 = string.IsNullOrEmpty(res1Id) ? false : IsOutputFullOrBlocked(_matOut1, res1Id, res1Amt);
            outBlocked = blocked0 || blocked1;
    
            return true;
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Fuel
        // ??????????????????????????????????????????????????????????????????????????????????????????
        int PeekFuelTemperatureClamped()
        {
            if (_fuelIn == null) return 0;
            if (_fuelIn.ToolActions == null) return 0;
            if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
                return 0;
    
            int t = 0;
            if (cfg.TryGetValue("temperature", out var tObj) && tObj != null)
                t = ToInt(tObj, 0);
    
            return Mathf.Clamp(t, 0, MAX_TEMP);
        }
    
        bool IsFuelOutBlockedForNewFuel()
        {
            if (_fuelIn == null) return true;
    
            if (_fuelIn.ToolActions == null) return true;
            if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
                return true;
    
            string resId = null;
            int resAmt = 1;
    
            if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
                resId = riObj.ToString();
    
            if (cfg.TryGetValue("amount", out var amObj) && amObj != null)
                resAmt = ToInt(amObj, 1);
    
            resAmt = Mathf.Max(1, resAmt);
    
            // resultItem ??怨몃さ嶺?FuelOut ????
            if (string.IsNullOrEmpty(resId)) return false;
    
            return IsOutputFullOrBlocked(_fuelOut, resId, resAmt);
        }
    
        bool TryConsumeFuelOne(out int gainedTicks, out string resultItemId, out int resultAmount, out int temperature)
        {
            gainedTicks = 0;
            resultItemId = null;
            resultAmount = 1;
            temperature = 0;
    
            if (_fuelIn == null) return false;
            if (_fuelIn.Count <= 0) return false;
    
            if (_fuelIn.ToolActions == null) return false;
            if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
                return false;
    
            if (!cfg.TryGetValue("burnTicks", out var btObj) || btObj == null)
                return false;
    
            gainedTicks = ToInt(btObj, 0);
            if (gainedTicks <= 0) return false;
    
            if (cfg.TryGetValue("temperature", out var tObj) && tObj != null)
                temperature = ToInt(tObj, 0);
    
            if (cfg.TryGetValue("resultItem", out var riObj) && riObj != null)
                resultItemId = riObj.ToString();
    
            if (cfg.TryGetValue("amount", out var amObj) && amObj != null)
                resultAmount = ToInt(amObj, 1);
    
            resultAmount = Mathf.Max(1, resultAmount);
    
            _fuelIn.Count -= 1;
            if (_fuelIn.Count <= 0) _fuelIn = null;
    
            return true;
        }
    
        void TryPushFuelResultToFuelOut()
        {
            if (string.IsNullOrEmpty(_fuelResultItemId)) return;
    
            if (!IsOutputFullOrBlocked(_fuelOut, _fuelResultItemId, _fuelResultAmount))
            {
                TryProduceToOut(ref _fuelOut, _fuelResultItemId, _fuelResultAmount);
                _fuelResultItemId = null;
                _fuelResultAmount = 1;
            }
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Output / consume helpers
        // ??????????????????????????????????????????????????????????????????????????????????????????
        bool IsOutputFullOrBlocked(ItemData outSlot, string wantId, int wantAmount)
        {
            if (string.IsNullOrEmpty(wantId)) return true;
            wantAmount = Mathf.Max(1, wantAmount);
    
            if (outSlot == null) return false;
            if (outSlot.ItemId != wantId) return true;
    
            int max = Mathf.Max(1, outSlot.MaxStack);
            return (outSlot.Count + wantAmount) > max;
        }
    
        void TryProduceToOut(ref ItemData outSlot, string itemId, int amount)
        {
            if (string.IsNullOrEmpty(itemId)) return;
            amount = Mathf.Max(1, amount);
    
            if (outSlot == null)
            {
                if (Manager == null || Manager.ItemLibrary == null) return;
                outSlot = Manager.ItemLibrary.Create(itemId, amount);
                CleanupZeroCountSlots();
                return;
            }
    
            if (outSlot.ItemId != itemId) return;
    
            int max = Mathf.Max(1, outSlot.MaxStack);
            if (outSlot.Count + amount > max) return;
    
            outSlot.Count += amount;
            CleanupZeroCountSlots();
        }
    
        bool ConsumeOne(ItemData it)
        {
            if (it == null) return false;
            if (it.Count <= 0) return false;
            it.Count -= 1;
            return true;
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Progress / snapshots
        // ??????????????????????????????????????????????????????????????????????????????????????????
        bool IngredientChanged()
        {
            string curId = _matIn != null ? _matIn.ItemId : null;
            int curDur = _matIn != null ? _matIn.Durability : 0;
    
            if (_prevInItemId != curId) return true;
            if (_prevInDur != curDur) return true;
    
            return false;
        }
    
        void SnapshotIngredient()
        {
            _prevInItemId = _matIn != null ? _matIn.ItemId : null;
            _prevInDur = _matIn != null ? _matIn.Durability : 0;
        }
    
        void ResetCokeProgress()
        {
            _cokeTicksDone = 0;
            _cokeTicksNeed = 0;
            _res0Id = null; _res0Amt = 1;
            _res1Id = null; _res1Amt = 1;
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Cleanup / Drop
        // ??????????????????????????????????????????????????????????????????????????????????????????
        void CleanupZeroCountSlots()
        {
            if (_fuelIn != null && _fuelIn.Count <= 0) _fuelIn = null;
            if (_fuelOut != null && _fuelOut.Count <= 0) _fuelOut = null;
    
            if (_matIn != null && _matIn.Count <= 0) _matIn = null;
            if (_matOut0 != null && _matOut0.Count <= 0) _matOut0 = null;
            if (_matOut1 != null && _matOut1.Count <= 0) _matOut1 = null;
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
    
            DropSlot(ref _matIn, origin);
            DropSlot(ref _matOut0, origin);
            DropSlot(ref _matOut1, origin);
        }
    
        void DropSlot(ref ItemData slot, Vector3 origin)
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
    
            World.itemDropper.SpawnDroppedItem(copy, origin);
            slot = null;
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Save/Load (PayloadJson)
        // ??????????????????????????????????????????????????????????????????????????????????????????
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
    
            // ??OriginalSolidIds (row-major)
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
    
            ItemData UnpackItem(JToken tok)
            {
                if (tok == null || tok.Type == JTokenType.Null) return null;
    
                string id = tok.Value<string>("id");
                int count = tok.Value<int?>("count") ?? 0;
                int dur = tok.Value<int?>("dur") ?? 0;
    
                if (string.IsNullOrEmpty(id) || count <= 0) return null;
    
                ItemData it = null;
                if (Manager != null && Manager.ItemLibrary != null)
                    it = Manager.ItemLibrary.Create(id, count);
    
                if (it != null)
                    it.Durability = dur;
    
                return it;
            }
    
            _fuelIn = UnpackItem(root["fuelIn"]);
            _fuelOut = UnpackItem(root["fuelOut"]);
    
            _matIn = UnpackItem(root["matIn"]);
            _matOut0 = UnpackItem(root["matOut0"]);
            _matOut1 = UnpackItem(root["matOut1"]);
    
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
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Int parse helper
        // ??????????????????????????????????????????????????????????????????????????????????????????
        int ToInt(object obj, int fallback)
        {
            if (obj == null) return fallback;
            if (obj is int i) return i;
            if (obj is long l) return (int)l;
            if (obj is float f) return Mathf.RoundToInt(f);
            if (obj is double d) return (int)d;
            if (obj is string s && int.TryParse(s, out int si)) return si;
            return fallback;
        }
    }
}
