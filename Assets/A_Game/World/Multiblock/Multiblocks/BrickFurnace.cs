// BrickFurnace.cs (?袁⑷퍥 ?대Ŋ猿쒑퉪?
// - ??ㅻ즲 ??뽯뮞???怨몄뒠
//   - ?怨뺤┷ toolActions["Fuel"].temperature ????뚮선 "?袁⑹삺 ??ㅻ즲"嚥?????
//   - BrickFurnace??筌ㅼ뮆? 筌왖????ㅻ즲 MAX_TEMPERATURE=3 (?怨뺤┷揶쎛 4??猷?3??곗쨮 cap)
//   - ?癒곗넅 ???怨뺤┷ ??곸벉)?癒?즲 "FuelIn????쇰선??덈뮉 ?怨뺤┷"??temperature??沃섎챶????뚮선
//     ??됰튋/hasWork ?④쑴沅??????=?癒곗넅 揶쎛????? ?癒?뼊)
//
// - ??낆젾 9????+ Crucible 1????+ FuelIn/FuelOut
// - 鈺곌퀗援?
//   1) Crucible ??얘탢????곷만??"Crucible") 揶쎛??筌△뫀?? ??利??諭?졿묾?餓λ쵎??/ ?怨뺤┷ ?????癒곗넅) 餓λ쵎??
//   2) ?臾먮짗 餓?Crucible ?袁⑹뵠??筌?野껋럩?? ??利??諭?졿묾?筌욊쑵六???λ뜃由??
//   3) ?諭????利??Crucible.details.layers ??"??쇰퓠?? 筌?쑴?(筌???? top). 揶쏆늿? ?ル굝履???됱몵筌?amount 筌앹빓?
//
// - ???뼎 域뱀뮇????됰튋):
//   "????筌욊쑵六?袁? ???ゅ첎? <=> "域???????袁⑹뵠??1揶?outAmount)揶쎛 ?袁⑥┷ ????쇰선揶??癒?봺 ??됰튋??
//   -> ???????湲?1揶쏆뮆?뉛쭕???됰튋.
//   -> ??됰튋 ??쎈솭 ????? 筌욊쑵六??筌앹빓? ??? ??곸벉(??湲?0).
//   -> ?怨쀪퐨??뽰맄 0~8: ??됰튋/?????? ??湲?0?? ??쀬돳嚥≪뮆彛??類ㅼ젟(?????.

using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.Player;

namespace Game.World
{
    public class BrickFurnace : Multiblock
    {
        public enum SlotKind
        {
            FuelIn, FuelOut,
            Crucible,
            In0, In1, In2, In3, In4, In5, In6, In7, In8
        }
    
        const int MAX_TEMPERATURE = 3;
        const int FIRE_HOLD_TICKS = 5;
    
        // Slots
        ItemData _fuelIn;
        ItemData _fuelOut;
        ItemData _crucible;
        readonly ItemData[] _ins = new ItemData[9];
    
        // Fuel state
        int _fuelTicksLeft = 0;
        int _fuelTicksMax  = 0;
    
        // ?袁⑹삺 ??????덈뮉 "?怨뺤┷ 1揶?????ㅻ즲(?怨뺤┷ ??????筌?Ŋ??
        int _burningFuelTemperature = 0;
    
        string _fuelResultItemId = null;
        int    _fuelResultAmount = 1;
    
        int _fireHoldTicksLeft = 0;
    
        // Smelt per-slot progress + reservation(1揶쏆뮆??
        readonly int[] _smeltTicksDone = new int[9];
        readonly int[] _smeltTicksNeed = new int[9];
    
        readonly bool[]   _reserved = new bool[9];
        readonly int[]    _reservedAmount = new int[9];
        readonly string[] _reservedFluidId = new string[9];
        int _reservedTotal = 0;
    
        bool _droppedOnDestroy = false;
    
        bool IsBurning => _fuelTicksLeft > 0;
        bool IsFireActiveFx => IsBurning || _fireHoldTicksLeft > 0;
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Temperature
        // ??????????????????????????????????????????????????????????????????????????????????????????
        int CurrentTemperatureLevel
        {
            get
            {
                if (!IsBurning) return 0;
                int t = _burningFuelTemperature;
                if (t < 0) t = 0;
                if (t > MAX_TEMPERATURE) t = MAX_TEMPERATURE;
                return t;
            }
        }
    
        int PeekFuelTemperatureLevel()
        {
            if (_fuelIn == null) return 0;
            if (_fuelIn.ToolActions == null) return 0;
            if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
                return 0;
    
            // schema: burnTicks, temperature, resultItem, amount
            int t = 1; // ??됱읈 疫꿸퀡??첎?
            if (cfg.TryGetValue("temperature", out var tempObj) && tempObj != null)
                t = ToInt(tempObj, 1);
    
            if (t < 0) t = 0;
            if (t > MAX_TEMPERATURE) t = MAX_TEMPERATURE;
            return t;
        }
    
        // ??됰튋/hasWork ?癒?뼊???怨뺣뮉 "?④쑵????ㅻ즲"
        // - ??? ????餓? ?袁⑹삺 ??ㅻ즲
        // - ?곗눘議??됱벉: FuelIn????쇰선??덈뮉 ?怨뺤┷嚥??癒곗넅??롢늺 ?袁⑤뼎 揶쎛?館釉???ㅻ즲
        int PlannedTemperatureLevel => IsBurning ? CurrentTemperatureLevel : PeekFuelTemperatureLevel();
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Public read API (Module??
        // ??????????????????????????????????????????????????????????????????????????????????????????
        public float FuelProgress01
        {
            get
            {
                if (_fuelTicksMax <= 0) return 0f;
                return Mathf.Clamp01((float)_fuelTicksLeft / (float)_fuelTicksMax);
            }
        }
    
        public int TemperatureLevel => CurrentTemperatureLevel;
    
        /// <summary>
        /// ??낆젾 ????index(0~8)??smelt 筌욊쑵六??0~1).
        /// ??됰튋????얘탢??筌욊쑵六?餓λ쵐???袁⑤빍筌?0.
        /// </summary>
        public float GetInputProgress01(int index)
        {
            if ((uint)index >= 9u) return 0f;
            if (!_reserved[index]) return 0f;
    
            int need = _smeltTicksNeed[index];
            int done = _smeltTicksDone[index];
    
            if (need <= 0) return 0f;
            return Mathf.Clamp01((float)done / (float)need);
        }
    
        public ItemData GetSlot(SlotKind kind)
        {
            switch (kind)
            {
                case SlotKind.FuelIn:   return _fuelIn;
                case SlotKind.FuelOut:  return _fuelOut;
                case SlotKind.Crucible: return _crucible;
    
                case SlotKind.In0: return _ins[0];
                case SlotKind.In1: return _ins[1];
                case SlotKind.In2: return _ins[2];
                case SlotKind.In3: return _ins[3];
                case SlotKind.In4: return _ins[4];
                case SlotKind.In5: return _ins[5];
                case SlotKind.In6: return _ins[6];
                case SlotKind.In7: return _ins[7];
                case SlotKind.In8: return _ins[8];
            }
            return null;
        }
    
        public void SetSlot(SlotKind kind, ItemData item)
        {
            switch (kind)
            {
                case SlotKind.FuelIn:
                    _fuelIn = item;
                    // ?怨뺤┷ 獄쏅뗀???늺 "?癒곗넅 ???④쑵????ㅻ즲"揶쎛 獄쏅뗀???嚥???됰튋 ?類λ???筌띿쉸??
                    RecomputeReservationsByPriority();
                    break;
    
                case SlotKind.FuelOut:
                    _fuelOut = item;
                    break;
    
                case SlotKind.Crucible:
                {
                    bool wasPresent  = (_crucible != null);
                    bool willPresent = (item != null);
    
                    // 鈺곌퀗援?2) ?臾먮짗 餓?Crucible ?袁⑹뵠??筌?野껋럩??筌욊쑵六???λ뜃由??
                    if (IsBurning && wasPresent && !willPresent)
                        ResetAllSmeltProgressAndReservations();
    
                    _crucible = item;
    
                    // Crucible 癰궰野?????됰튋 ?怨밴묶 ?類λ???筌띿쉸??
                    RecomputeReservationsByPriority();
                    break;
                }
    
                case SlotKind.In0: SetInputSlot(0, item); break;
                case SlotKind.In1: SetInputSlot(1, item); break;
                case SlotKind.In2: SetInputSlot(2, item); break;
                case SlotKind.In3: SetInputSlot(3, item); break;
                case SlotKind.In4: SetInputSlot(4, item); break;
                case SlotKind.In5: SetInputSlot(5, item); break;
                case SlotKind.In6: SetInputSlot(6, item); break;
                case SlotKind.In7: SetInputSlot(7, item); break;
                case SlotKind.In8: SetInputSlot(8, item); break;
            }
    
            CleanupZeroCountSlots();
        }
    
        void SetInputSlot(int i, ItemData item)
        {
            var prev = _ins[i];
    
            bool sameKind =
                prev != null &&
                item != null &&
                prev.Count > 0 &&
                item.Count > 0 &&
                prev.ItemId == item.ItemId &&
                prev.Durability == item.Durability;
    
            _ins[i] = item;
    
            if (!sameKind)
            {
                ClearReservationForSlot(i);
                _smeltTicksDone[i] = 0;
                _smeltTicksNeed[i] = 0;
            }
    
            RecomputeReservationsByPriority();
        }
    
        public override void OnInteract(Game.Player.Player player, Vector2Int hitCell)
        {
            Manager.OpenModule("Brick Furnace", this);
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
                key    = "Fire_02",
                offset = new Vector2(1f, 0.3f),
                active = IsFireActiveFx
            });
    
            outList.Add(new VfxRequest
            {
                key    = "Smoke",
                offset = new Vector2(1f, 2f),
                active = IsFireActiveFx
            });
        }
    
        public override void Tick()
        {
            CleanupZeroCountSlots();
    
            bool wasBurning = IsBurning;
    
            // ????뽰삂 疫꿸퀣? ??ㅻ즲(筌욊쑵六?域뱀뮇??? "????뽰삂 ???븍뜃??ON"=wasBurning 疫꿸퀣?)
            int tempAtTickStart = wasBurning ? CurrentTemperatureLevel : 0;
    
            bool crucibleOk = TryGetCrucibleCapacity(_crucible, out int cap) && cap > 0;
            int  curAmt     = crucibleOk ? GetCrucibleCurrentAmount(_crucible) : 0;
            bool crucibleFull = crucibleOk && curAmt >= cap;
    
            bool allowWork = crucibleOk && !crucibleFull;
    
            if (!allowWork)
            {
                ClearAllReservationsAndProgressBecauseNoWork();
            }
            else
            {
                // ??ㅻ즲(?袁⑹삺/?④쑵????筌띿쉸????됰튋 ?類λ????醫?
                RecomputeReservationsByPriority();
            }
    
            bool hasWork = allowWork && HasAnyReservedWork();
    
            // 1) ?怨뺤┷揶쎛 ??곸몵筌? hasWork ?????춸 ?癒곗넅 ??뺣즲
            if (_fuelTicksLeft <= 0)
            {
                if (!string.IsNullOrEmpty(_fuelResultItemId))
                    TryPushFuelResultToFuelOut();
    
                if (hasWork)
                {
                    if (!IsFuelOutBlockedForNewFuel())
                    {
                        if (TryConsumeFuelOne(out int gainedTicks, out int fuelTemp, out string fuelResultItem, out int fuelResultAmount))
                        {
                            _fuelTicksLeft = gainedTicks;
                            _fuelTicksMax  = gainedTicks;
    
                            _burningFuelTemperature = fuelTemp;
    
                            _fuelResultItemId = fuelResultItem;
                            _fuelResultAmount = Mathf.Max(1, fuelResultAmount);
    
                            _fireHoldTicksLeft = 0;
                        }
                    }
                }
            }
    
            // 2) ?怨뺤┷ ????餓λ쵐?좑쭖?揶쏅Ŋ??
            if (_fuelTicksLeft > 0)
            {
                _fuelTicksLeft -= 1;
                if (_fuelTicksLeft < 0) _fuelTicksLeft = 0;
            }
    
            // 2.5) ?怨뺤┷ ?ル굝利???hold ??뽰삂
            if (wasBurning && _fuelTicksLeft <= 0)
                _fireHoldTicksLeft = FIRE_HOLD_TICKS;
    
            // 2.6) hold 揶쏅Ŋ??
            if (_fuelTicksLeft <= 0 && _fireHoldTicksLeft > 0)
            {
                _fireHoldTicksLeft -= 1;
                if (_fireHoldTicksLeft < 0) _fireHoldTicksLeft = 0;
            }
    
            // 3) Smelt 筌욊쑵六?(????뽰삂 ???怨뺤┷揶쎛 ??????됰???삠늺 筌욊쑵六?
            if (wasBurning && allowWork)
            {
                // ??ㅻ즲 沃섎챶?????筌욊쑵六??癒?퍥揶쎛 "??됰튋=筌욊쑵六? 域뱀뮇????袁④컳???嚥?
                // - ???源녿퓠??뺣뮉 筌욊쑵六억쭕?筌띾맦??
                // - ??됰튋?? Recompute揶쎛 ??쇱벉???類ｂ봺??뺣뼄(??ㅻ즲 疫꿸퀡而?.
                if (tempAtTickStart > 0)
                {
                    for (int i = 0; i < 9; i++)
                    {
                        if (!_reserved[i]) continue;
    
                        var input = _ins[i];
                        if (input == null || input.Count <= 0)
                        {
                            ClearReservationForSlot(i);
                            _smeltTicksDone[i] = 0;
                            _smeltTicksNeed[i] = 0;
                            continue;
                        }
    
                        if (!TryReadSmeltSpecForOne(input, tempAtTickStart, out int needTicks, out string resId, out int resAmt))
                        {
                            ClearReservationForSlot(i);
                            _smeltTicksDone[i] = 0;
                            _smeltTicksNeed[i] = 0;
                            continue;
                        }
    
                        if (_reservedFluidId[i] != resId || _reservedAmount[i] != resAmt)
                        {
                            ClearReservationForSlot(i);
                            _smeltTicksDone[i] = 0;
                            _smeltTicksNeed[i] = 0;
                            continue;
                        }
    
                        _smeltTicksNeed[i] = needTicks;
    
                        _smeltTicksDone[i] += 1;
                        if (_smeltTicksDone[i] > _smeltTicksNeed[i])
                            _smeltTicksDone[i] = _smeltTicksNeed[i];
    
                        if (_smeltTicksNeed[i] > 0 && _smeltTicksDone[i] >= _smeltTicksNeed[i])
                        {
                            if (ConsumeOneFromInput(i))
                            {
                                AddFluidToCrucibleLayers(_crucible, _reservedFluidId[i], _reservedAmount[i]);
    
                                if (World != null && World.recipeLibrary != null)
                                    World.recipeLibrary.TryApplyAlloysToCrucible(_crucible);
                            }
    
                            ClearReservationForSlot(i);
                            _smeltTicksDone[i] = 0;
                            _smeltTicksNeed[i] = 0;
                        }
                    }
                }
    
                // ?뚣끇而?????? ??낆젾???????筌욊쑵六???醫? 獄쎻뫗??
                RecomputeReservationsByPriority();
            }
    
            // 4) ?怨뺤┷揶쎛 ??멸텢??겹늺 ?봔?怨빿?筌ｌ꼶??
            if (wasBurning && _fuelTicksLeft <= 0)
                TryPushFuelResultToFuelOut();
    
            // meta ??녿┛??(?怨뺤┷ ????餓?疫꿸퀣?)
            bool isBurningNow = IsBurning;
            if (wasBurning != isBurningNow)
                RequestApplyFurnaceMeta(isBurningNow);
    
            CleanupZeroCountSlots();
        }
    
        void RequestApplyFurnaceMeta(bool burning)
        {
            if (Manager == null) return;
            Manager.ApplyMetaToAllOccupiedCells(this, (ushort)(burning ? 6 : 0));
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Reservation / Progress
        // ??????????????????????????????????????????????????????????????????????????????????????????
        void ResetAllSmeltProgressAndReservations()
        {
            for (int i = 0; i < 9; i++)
            {
                _smeltTicksDone[i] = 0;
                _smeltTicksNeed[i] = 0;
                _reserved[i] = false;
                _reservedAmount[i] = 0;
                _reservedFluidId[i] = null;
            }
            _reservedTotal = 0;
        }
    
        void ClearAllReservationsAndProgressBecauseNoWork()
        {
            ResetAllSmeltProgressAndReservations();
        }
    
        void ClearReservationForSlot(int i)
        {
            if ((uint)i >= 9u) return;
    
            if (_reserved[i])
            {
                _reservedTotal -= _reservedAmount[i];
                if (_reservedTotal < 0) _reservedTotal = 0;
            }
    
            _reserved[i] = false;
            _reservedAmount[i] = 0;
            _reservedFluidId[i] = null;
        }
    
        bool HasAnyReservedWork()
        {
            for (int i = 0; i < 9; i++)
            {
                if (_reserved[i] && _ins[i] != null && _ins[i].Count > 0)
                    return true;
            }
            return false;
        }
    
        void RecomputeReservationsByPriority()
        {
            // Crucible ?醫륁뒞??
            if (!TryGetCrucibleCapacity(_crucible, out int capacity) || capacity <= 0)
            {
                ResetAllSmeltProgressAndReservations();
                return;
            }
    
            int current = GetCrucibleCurrentAmount(_crucible);
            if (current >= capacity)
            {
                ResetAllSmeltProgressAndReservations();
                return;
            }
    
            // ?袁⑹삺/?④쑵????ㅻ즲
            int tempForPlanning = PlannedTemperatureLevel;
            if (tempForPlanning <= 0)
            {
                // ??ㅻ즲 0????smelt ??됰튋 ?븍뜃?(筌욊쑵六??0)
                ResetAllSmeltProgressAndReservations();
                return;
            }
    
            // 疫꿸퀣???怨밴묶 ??산퉬??
            bool[] oldRes = new bool[9];
            int[] oldDone = new int[9];
            int[] oldNeed = new int[9];
            int[] oldAmt  = new int[9];
            string[] oldId = new string[9];
    
            for (int i = 0; i < 9; i++)
            {
                oldRes[i] = _reserved[i];
                oldDone[i] = _smeltTicksDone[i];
                oldNeed[i] = _smeltTicksNeed[i];
                oldAmt[i]  = _reservedAmount[i];
                oldId[i]   = _reservedFluidId[i];
            }
    
            // ??덉쨮 ?類ㅼ젟???怨밴묶
            for (int i = 0; i < 9; i++)
            {
                _reserved[i] = false;
                _reservedAmount[i] = 0;
                _reservedFluidId[i] = null;
                _smeltTicksNeed[i] = 0;
                _smeltTicksDone[i] = 0;
            }
            _reservedTotal = 0;
    
            for (int i = 0; i < 9; i++)
            {
                var input = _ins[i];
                if (input == null || input.Count <= 0) continue;
    
                if (!TryReadSmeltSpecForOne(input, tempForPlanning, out int needTicks, out string resId, out int resAmt))
                    continue;
    
                int remainingForReserve = capacity - current - _reservedTotal;
                if (remainingForReserve < resAmt)
                    continue;
    
                _reserved[i] = true;
                _reservedAmount[i] = resAmt;
                _reservedFluidId[i] = resId;
                _reservedTotal += resAmt;
    
                _smeltTicksNeed[i] = needTicks;
    
                if (oldRes[i] && oldId[i] == resId && oldAmt[i] == resAmt && oldNeed[i] == needTicks)
                {
                    int keep = oldDone[i];
                    if (keep < 0) keep = 0;
                    if (keep > needTicks) keep = needTicks;
                    _smeltTicksDone[i] = keep;
                }
                else
                {
                    _smeltTicksDone[i] = 0;
                }
            }
        }
    
        bool TryReadSmeltSpecForOne(ItemData input, int availableTemp, out int smeltNeed, out string resultItemId, out int amount)
        {
            smeltNeed = 0;
            resultItemId = null;
            amount = 0;
    
            if (input == null) return false;
            if (input.Count <= 0) return false;
    
            if (input.ToolActions == null) return false;
            if (!input.ToolActions.TryGetValue("Smelt", out Dictionary<string, object> cfg) || cfg == null)
                return false;
    
            if (!cfg.TryGetValue("temperature", out var tempObj) || tempObj == null)
                return false;
    
            int reqTemp = ToInt(tempObj, -1);
            if (reqTemp <= 0) return false;
    
            // ???袁⑹삺(?癒?뮉 ?④쑵?? ??ㅻ즲 疫꿸퀡而??곗쨮 ?癒?젟
            if (reqTemp > availableTemp) return false;
    
            if (!cfg.TryGetValue("smeltTicks", out var stObj) || stObj == null)
                return false;
    
            smeltNeed = ToInt(stObj, 0);
            if (smeltNeed <= 0) return false;
    
            if (!cfg.TryGetValue("resultItem", out var riObj) || riObj == null)
                return false;
    
            resultItemId = riObj.ToString();
            if (string.IsNullOrEmpty(resultItemId)) return false;
    
            if (!cfg.TryGetValue("amount", out var amObj) || amObj == null)
                return false;
    
            amount = ToInt(amObj, 0);
            if (amount <= 0) return false;
    
            return true;
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Crucible layers utils
        // ??????????????????????????????????????????????????????????????????????????????????????????
        bool TryGetCrucibleCapacity(ItemData crucible, out int capacity)
        {
            capacity = 0;
            if (crucible == null) return false;
    
            if (crucible.ToolActions == null) return false;
            if (!crucible.ToolActions.TryGetValue("Crucible", out Dictionary<string, object> cfg) || cfg == null)
                return false;
    
            if (!cfg.TryGetValue("capacity", out var capObj) || capObj == null)
                return false;
    
            capacity = ToInt(capObj, 0);
            return capacity > 0;
        }
    
        int GetCrucibleCurrentAmount(ItemData crucible)
        {
            if (crucible == null) return 0;
    
            if (!crucible.Details.TryGetValue("layers", out var layersObj) || layersObj == null)
                return 0;
    
            int sum = 0;
    
            if (layersObj is JArray jarr)
            {
                for (int i = 0; i < jarr.Count; i++)
                {
                    var jo = jarr[i] as JObject;
                    if (jo == null) continue;
                    sum += ToInt(jo["amount"], 0);
                }
                return sum;
            }
    
            if (layersObj is List<object> list)
            {
                for (int i = 0; i < list.Count; i++)
                {
                    if (list[i] is JObject jo)
                        sum += ToInt(jo["amount"], 0);
                    else if (list[i] is Dictionary<string, object> d)
                    {
                        if (d.TryGetValue("amount", out var aObj) && aObj != null)
                            sum += ToInt(aObj, 0);
                    }
                }
                return sum;
            }
    
            return 0;
        }
    
        void AddFluidToCrucibleLayers(ItemData crucible, string fluidId, int addAmount)
        {
            if (crucible == null) return;
    
            if (!crucible.Details.TryGetValue("layers", out var layersObj) || layersObj == null)
            {
                var newList = new List<object>();
                crucible.SetDetail("layers", newList);
                layersObj = newList;
            }
    
            if (layersObj is List<object> list)
            {
                for (int i = list.Count - 1; i >= 0; i--)
                {
                    if (TryGetLayer(list[i], out var itemId, out var amt))
                    {
                        if (itemId == fluidId)
                        {
                            SetLayerAmount(list, i, amt + addAmount);
                            return;
                        }
                    }
                }
    
                var layer = new Dictionary<string, object>
                {
                    { "itemId", fluidId },
                    { "amount", addAmount }
                };
                list.Add(layer);
                return;
            }
    
            if (layersObj is JArray jarr)
            {
                var normalized = new List<object>();
                for (int i = 0; i < jarr.Count; i++)
                    normalized.Add(jarr[i]);
                crucible.SetDetail("layers", normalized);
    
                AddFluidToCrucibleLayers(crucible, fluidId, addAmount);
            }
        }
    
        bool TryGetLayer(object layerObj, out string itemId, out int amount)
        {
            itemId = null;
            amount = 0;
    
            if (layerObj is Dictionary<string, object> d)
            {
                if (d.TryGetValue("itemId", out var idObj) && idObj != null)
                    itemId = idObj.ToString();
                else if (d.TryGetValue("fluidId", out var fidObj) && fidObj != null)
                    itemId = fidObj.ToString();
    
                if (d.TryGetValue("amount", out var aObj) && aObj != null)
                    amount = ToInt(aObj, 0);
    
                return !string.IsNullOrEmpty(itemId);
            }
    
            if (layerObj is JObject jo)
            {
                var idTok = jo["itemId"] ?? jo["fluidId"];
                if (idTok != null) itemId = idTok.ToString();
                amount = ToInt(jo["amount"], 0);
                return !string.IsNullOrEmpty(itemId);
            }
    
            return false;
        }
    
        void SetLayerAmount(List<object> list, int index, int newAmount)
        {
            if ((uint)index >= (uint)list.Count) return;
            newAmount = Mathf.Max(0, newAmount);
    
            var obj = list[index];
    
            if (obj is Dictionary<string, object> d)
            {
                d["amount"] = newAmount;
                list[index] = d;
                return;
            }
    
            if (obj is JObject jo)
            {
                jo["amount"] = newAmount;
                list[index] = jo;
                return;
            }
    
            var repl = new Dictionary<string, object>
            {
                { "itemId", "" },
                { "amount", newAmount }
            };
            list[index] = repl;
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Fuel helpers (Fuel: { burnTicks, temperature, resultItem, amount })
        // ??????????????????????????????????????????????????????????????????????????????????????????
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
    
            if (string.IsNullOrEmpty(resId))
                return false;
    
            return IsOutputFullOrBlocked(_fuelOut, resId, resAmt);
        }
    
        bool TryConsumeFuelOne(out int gainedTicks, out int fuelTemperature, out string resultItemId, out int resultAmount)
        {
            gainedTicks = 0;
            fuelTemperature = 0;
            resultItemId = null;
            resultAmount = 1;
    
            if (_fuelIn == null) return false;
            if (_fuelIn.Count <= 0) return false;
    
            if (_fuelIn.ToolActions == null) return false;
            if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
                return false;
    
            if (!cfg.TryGetValue("burnTicks", out var btObj) || btObj == null)
                return false;
    
            gainedTicks = ToInt(btObj, 0);
            if (gainedTicks <= 0) return false;
    
            // temperature (?袁⑸땾嚥??節뚯뱽 ??됱젟???筌? ??됱읈 疫꿸퀡??첎?1)
            int t = 1;
            if (cfg.TryGetValue("temperature", out var tempObj) && tempObj != null)
                t = ToInt(tempObj, 1);
    
            if (t < 0) t = 0;
            if (t > MAX_TEMPERATURE) t = MAX_TEMPERATURE;
            fuelTemperature = t;
    
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
                if (_fuelOut == null)
                {
                    if (Manager != null && Manager.ItemLibrary != null)
                        _fuelOut = Manager.ItemLibrary.Create(_fuelResultItemId, _fuelResultAmount);
                }
                else if (_fuelOut.ItemId == _fuelResultItemId)
                {
                    _fuelOut.Count += _fuelResultAmount;
                }
    
                _fuelResultItemId = null;
                _fuelResultAmount = 1;
            }
        }
    
        bool IsOutputFullOrBlocked(ItemData outSlot, string wantId, int wantAmount)
        {
            if (string.IsNullOrEmpty(wantId)) return true;
            wantAmount = Mathf.Max(1, wantAmount);
    
            if (outSlot == null) return false;
            if (outSlot.ItemId != wantId) return true;
    
            int max = Mathf.Max(1, outSlot.MaxStack);
            return (outSlot.Count + wantAmount) > max;
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Input consume
        // ??????????????????????????????????????????????????????????????????????????????????????????
        bool ConsumeOneFromInput(int index)
        {
            if ((uint)index >= 9u) return false;
            var it = _ins[index];
            if (it == null) return false;
            if (it.Count <= 0) return false;
    
            it.Count -= 1;
            if (it.Count <= 0) _ins[index] = null;
    
            return true;
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Cleanup / drop
        // ??????????????????????????????????????????????????????????????????????????????????????????
        void CleanupZeroCountSlots()
        {
            if (_fuelIn != null && _fuelIn.Count <= 0) _fuelIn = null;
            if (_fuelOut != null && _fuelOut.Count <= 0) _fuelOut = null;
    
            if (_crucible != null && _crucible.Count <= 0) _crucible = null;
    
            for (int i = 0; i < 9; i++)
            {
                if (_ins[i] != null && _ins[i].Count <= 0)
                    _ins[i] = null;
            }
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
            DropSlot(ref _crucible, origin);
    
            for (int i = 0; i < 9; i++)
                DropSlot(ref _ins[i], origin);
    
            ResetAllSmeltProgressAndReservations();
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
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // Save/Load (PayloadJson + OriginalSolidIds)
        // ??????????????????????????????????????????????????????????????????????????????????????????
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
    
            root["fuelIn"]   = PackItem(_fuelIn);
            root["fuelOut"]  = PackItem(_fuelOut);
            root["crucible"] = PackItem(_crucible);
    
            var ins = new JArray();
            for (int i = 0; i < 9; i++)
                ins.Add(PackItem(_ins[i]));
            root["inputs"] = ins;
    
            root["fuelTicksLeft"] = _fuelTicksLeft;
            root["fuelTicksMax"]  = _fuelTicksMax;
            root["burningFuelTemperature"] = _burningFuelTemperature;
    
            root["fuelResultItemId"] = _fuelResultItemId;
            root["fuelResultAmount"] = _fuelResultAmount;
            root["fireHoldTicksLeft"] = _fireHoldTicksLeft;
    
            var doneArr = new JArray();
            var needArr = new JArray();
            var resArr  = new JArray();
            var resAmtArr = new JArray();
            var resIdArr  = new JArray();
    
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
    
            if (_crucible != null && _crucible.Count > 0)
            {
                if (_crucible.Details.TryGetValue("layers", out var layersObj) && layersObj != null)
                {
                    root["crucible_layers"] = JToken.FromObject(layersObj);
                }
            }
    
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
                PayloadJson = root.ToString(Newtonsoft.Json.Formatting.None),
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
    
                    _fuelIn   = UnpackItem(root["fuelIn"]);
                    _fuelOut  = UnpackItem(root["fuelOut"]);
                    _crucible = UnpackItem(root["crucible"]);
    
                    if (root["inputs"] is JArray ins)
                    {
                        for (int i = 0; i < 9; i++)
                            _ins[i] = (i < ins.Count) ? UnpackItem(ins[i]) : null;
                    }
    
                    _fuelTicksLeft = root.Value<int?>("fuelTicksLeft") ?? 0;
                    _fuelTicksMax  = root.Value<int?>("fuelTicksMax") ?? 0;
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
                        for (int i = 0; i < 9 && i < resIdArr.Count; i++)
                        {
                            var s = resIdArr[i]?.ToString();
                            _reservedFluidId[i] = string.IsNullOrEmpty(s) ? null : s;
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
    
            // 嚥≪뮆諭??袁⑸퓠???怨쀪퐨??뽰맄/??됰튋 域뱀뮇??揶쏅벡??
            RecomputeReservationsByPriority();
    
            RequestApplyFurnaceMeta(IsBurning);
        }
    
        // ??????????????????????????????????????????????????????????????????????????????????????????
        // small utils
        // ??????????????????????????????????????????????????????????????????????????????????????????
        int ToInt(object obj, int fallback)
        {
            if (obj == null) return fallback;
            if (obj is int i) return i;
            if (obj is long l) return (int)l;
            if (obj is float f) return Mathf.RoundToInt(f);
            if (obj is double d) return (int)d;
    
            int r;
            return int.TryParse(obj.ToString(), out r) ? r : fallback;
        }
    
        int ToInt(JToken tok, int fallback)
        {
            if (tok == null) return fallback;
            if (tok.Type == JTokenType.Integer) return tok.Value<int>();
            if (tok.Type == JTokenType.Float) return Mathf.RoundToInt(tok.Value<float>());
            int r;
            return int.TryParse(tok.ToString(), out r) ? r : fallback;
        }
    }
}
