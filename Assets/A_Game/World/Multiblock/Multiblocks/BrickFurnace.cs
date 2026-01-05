// BrickFurnace.cs (전체 교체본)
// - 온도(레벨) 1로 Smelt
// - 입력 9슬롯 + Crucible 1슬롯 + FuelIn/FuelOut
// - 조건:
//   1) Crucible 없거나(툴액션 "Crucible") 가득 차면: 재료 녹이기 중단 / 연료 투입(점화) 중단
//   2) 작동 중 Crucible 아이템 뺄 경우, 재료 녹이기 진행도 초기화
//   3) 녹인 재료는 Crucible.details.layers 에 "뒤에서" 채움(맨 뒤가 top). 같은 종류 있으면 amount 증가
//
// - 핵심 규칙(예약):
//   "슬롯 진행도가 올라감" <=> "그 슬롯의 아이템 1개(outAmount)가 완료 시 들어갈 자리 예약됨"
//   -> 슬롯당 항상 1개분만 예약.
//   -> 예약 실패 슬롯은 진행도 증가 절대 없음(항상 0).
//   -> 우선순위 0~8: 예약/재예약은 항상 0→8 순회로만 확정(재계산).

using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public class BrickFurnace : Multiblock
{
    public enum SlotKind
    {
        FuelIn, FuelOut,
        Crucible,
        In0, In1, In2, In3, In4, In5, In6, In7, In8
    }

    const int SMELT_LEVEL = 1;
    const int FIRE_HOLD_TICKS = 5;

    // Slots
    ItemData _fuelIn;
    ItemData _fuelOut;
    ItemData _crucible;
    readonly ItemData[] _ins = new ItemData[9];

    // Fuel state
    int _fuelTicksLeft = 0;
    int _fuelTicksMax  = 0;

    string _fuelResultItemId = null;
    int    _fuelResultAmount = 1;

    int _fireHoldTicksLeft = 0;

    // Smelt per-slot progress + reservation(1개분)
    readonly int[] _smeltTicksDone = new int[9];
    readonly int[] _smeltTicksNeed = new int[9];

    readonly bool[]   _reserved = new bool[9];
    readonly int[]    _reservedAmount = new int[9];
    readonly string[] _reservedFluidId = new string[9];
    int _reservedTotal = 0;

    bool _droppedOnDestroy = false;

    bool IsBurning => _fuelTicksLeft > 0;
    bool IsFireActiveFx => IsBurning || _fireHoldTicksLeft > 0;

    // ─────────────────────────────────────────────
    // Public read API (Module용)
    // ─────────────────────────────────────────────
    public float FuelProgress01
    {
        get
        {
            if (_fuelTicksMax <= 0) return 0f;
            return Mathf.Clamp01((float)_fuelTicksLeft / (float)_fuelTicksMax);
        }
    }

    /// <summary>
    /// 입력 슬롯 index(0~8)의 smelt 진행도(0~1).
    /// 예약이 없거나 진행 중이 아니면 0.
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
                break;

            case SlotKind.FuelOut:
                _fuelOut = item;
                break;

            case SlotKind.Crucible:
            {
                bool wasPresent  = (_crucible != null);
                bool willPresent = (item != null);

                // 조건 2) 작동 중 Crucible 아이템 뺄 경우 진행도 초기화
                if (IsBurning && wasPresent && !willPresent)
                    ResetAllSmeltProgressAndReservations();

                _crucible = item;

                // Crucible 변경 시 예약 상태 정합성 맞춤
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
        // 기존 입력 스냅샷
        var prev = _ins[i];

        // "종류/내구도 동일"이면 count 변화만으로는 진행도/예약을 깨지 않음
        bool sameKind =
            prev != null &&
            item != null &&
            prev.Count > 0 &&
            item.Count > 0 &&
            prev.ItemId == item.ItemId &&
            prev.Durability == item.Durability;

        _ins[i] = item;

        // 종류가 바뀌었거나, null/0이 되었으면 그 슬롯 진행도/예약 무효
        if (!sameKind)
        {
            ClearReservationForSlot(i);
            _smeltTicksDone[i] = 0;
            _smeltTicksNeed[i] = 0;
        }

        // 우선순위(0→8) 보장: 전체 재예약
        // sameKind면 Recompute 내부의 "같은 예약(spec 동일)" 로직이 진행도를 유지해줌
        RecomputeReservationsByPriority();
    }

    public override void OnInteract(Player player, Vector2Int hitCell)
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
        base.OnCellBroken(brokenCell); // 기본 구현: Despawn + 원복:contentReference[oaicite:2]{index=2}
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

        bool crucibleOk = TryGetCrucibleCapacity(_crucible, out int cap) && cap > 0;
        int  curAmt     = crucibleOk ? GetCrucibleCurrentAmount(_crucible) : 0;
        bool crucibleFull = crucibleOk && curAmt >= cap;

        // 조건 1) Crucible 없거나 가득 차면: 녹이기 중단 / 연료 투입(점화) 중단
        bool allowWork = crucibleOk && !crucibleFull;

        if (!allowWork)
        {
            // 진행도는 예약과 동치 규칙이므로 전부 0
            ClearAllReservationsAndProgressBecauseNoWork();
        }
        else
        {
            // allowWork일 때만 "진행도 유지" 방식으로 재계산
            RecomputeReservationsByPriority();
        }

        bool hasWork = allowWork && HasAnyReservedWork();

        // 1) 연료가 없으면: hasWork 일 때만 점화 시도
        if (_fuelTicksLeft <= 0)
        {
            if (!string.IsNullOrEmpty(_fuelResultItemId))
                TryPushFuelResultToFuelOut();

            if (hasWork)
            {
                if (!IsFuelOutBlockedForNewFuel())
                {
                    if (TryConsumeFuelOne(out int gainedTicks, out string fuelResultItem, out int fuelResultAmount))
                    {
                        _fuelTicksLeft = gainedTicks;
                        _fuelTicksMax  = gainedTicks;

                        _fuelResultItemId = fuelResultItem;
                        _fuelResultAmount = Mathf.Max(1, fuelResultAmount);

                        _fireHoldTicksLeft = 0;
                    }
                }
            }
        }

        // 2) 연료 타는 중이면 감소
        if (_fuelTicksLeft > 0)
        {
            _fuelTicksLeft -= 1;
            if (_fuelTicksLeft < 0) _fuelTicksLeft = 0;
        }

        // 2.5) 연료 종료 시 hold 시작
        if (wasBurning && _fuelTicksLeft <= 0)
            _fireHoldTicksLeft = FIRE_HOLD_TICKS;

        // 2.6) hold 감소
        if (_fuelTicksLeft <= 0 && _fireHoldTicksLeft > 0)
        {
            _fireHoldTicksLeft -= 1;
            if (_fireHoldTicksLeft < 0) _fireHoldTicksLeft = 0;
        }

        // 3) Smelt 진행 (틱 시작 시 연료가 타고 있었다면 진행)
        if (wasBurning && allowWork)
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

                // 예약과 동일한 spec인지 확인
                if (!TryReadSmeltSpecForOne(input, out int needTicks, out string resId, out int resAmt))
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

                // 진행도 증가
                _smeltTicksDone[i] += 1;
                if (_smeltTicksDone[i] > _smeltTicksNeed[i])
                    _smeltTicksDone[i] = _smeltTicksNeed[i];

                // 완료 커밋
                if (_smeltTicksNeed[i] > 0 && _smeltTicksDone[i] >= _smeltTicksNeed[i])
                {
                    if (ConsumeOneFromInput(i))
                        AddFluidToCrucibleLayers(_crucible, _reservedFluidId[i], _reservedAmount[i]);

                    // 1개 처리 완료 -> 예약 해제 + 진행 리셋
                    ClearReservationForSlot(i);
                    _smeltTicksDone[i] = 0;
                    _smeltTicksNeed[i] = 0;
                }
            }

            // 커밋 후 남은 입력들 재예약(진행도 유지 방식)
            RecomputeReservationsByPriority();
        }

        // 4) 연료가 끝났으면 부산물 처리
        if (wasBurning && _fuelTicksLeft <= 0)
            TryPushFuelResultToFuelOut();

        // meta 동기화 (연료 타는 중 기준)
        bool isBurningNow = IsBurning;
        if (wasBurning != isBurningNow)
            RequestApplyFurnaceMeta(isBurningNow);

        CleanupZeroCountSlots();
    }

    void RequestApplyFurnaceMeta(bool burning)
    {
        if (Manager == null) return;
        Manager.ApplyMetaToAllOccupiedCells(this, (ushort)(burning ? 1 : 0));
    }

    // ─────────────────────────────────────────────
    // Reservation / Progress
    // ─────────────────────────────────────────────
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
        // Crucible 유효성
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

        // 기존 상태 스냅샷
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

        // 새로 확정할 상태(우선순위 0→8 단방향 확정)
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

            if (!TryReadSmeltSpecForOne(input, out int needTicks, out string resId, out int resAmt))
                continue;

            int remainingForReserve = capacity - current - _reservedTotal;
            if (remainingForReserve < resAmt)
            {
                // 예약 실패 -> 시작 자체 안 함(진행도 0)
                continue;
            }

            _reserved[i] = true;
            _reservedAmount[i] = resAmt;
            _reservedFluidId[i] = resId;
            _reservedTotal += resAmt;

            _smeltTicksNeed[i] = needTicks;

            // "같은 예약(spec 동일)"이면 기존 진행도 유지
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

    bool TryReadSmeltSpecForOne(ItemData input, out int smeltNeed, out string resultItemId, out int amount)
    {
        smeltNeed = 0;
        resultItemId = null;
        amount = 0;

        if (input == null) return false;
        if (input.Count <= 0) return false;

        if (input.ToolActions == null) return false;
        if (!input.ToolActions.TryGetValue("Smelt", out Dictionary<string, object> cfg) || cfg == null)
            return false;

        // 네 "정식 양식" 강제:
        // temperature, smeltTicks, resultItem, amount 전부 필수

        if (!cfg.TryGetValue("temperature", out var tempObj) || tempObj == null)
            return false;

        int reqTemp = ToInt(tempObj, -1);
        if (reqTemp <= 0) return false;
        if (reqTemp > SMELT_LEVEL) return false;

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

    // ─────────────────────────────────────────────
    // Crucible layers utils
    // ─────────────────────────────────────────────
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

    // ─────────────────────────────────────────────
    // Fuel helpers (네 Fuel 양식)
    // Fuel: { burnTicks, resultItem, amount }
    // ─────────────────────────────────────────────
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

    bool TryConsumeFuelOne(out int gainedTicks, out string resultItemId, out int resultAmount)
    {
        gainedTicks = 0;
        resultItemId = null;
        resultAmount = 1;

        if (_fuelIn == null) return false;
        if (_fuelIn.Count <= 0) return false;

        if (_fuelIn.ToolActions == null) return false;
        if (!_fuelIn.ToolActions.TryGetValue("Fuel", out Dictionary<string, object> cfg) || cfg == null)
            return false;

        // 네 양식: burnTicks
        if (!cfg.TryGetValue("burnTicks", out var btObj) || btObj == null)
            return false;

        gainedTicks = ToInt(btObj, 0);
        if (gainedTicks <= 0) return false;

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

    // ─────────────────────────────────────────────
    // Input consume
    // ─────────────────────────────────────────────
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

    // ─────────────────────────────────────────────
    // Cleanup / drop
    // ─────────────────────────────────────────────
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
            details:       slot.Details,   // Crucible layers 포함
            icon:          slot.Icon,
            count:         slot.Count
        );

        World.itemDropper.SpawnDroppedItem(copy, origin);
        slot = null;
    }

    // ─────────────────────────────────────────────
    // Save/Load (ClayKiln 스타일 + Crucible layers 별도 저장)
    // + OriginalSolidIds 저장 (로드된 멀티블럭 분해 원복용)
    // ─────────────────────────────────────────────
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

        // fuel state
        root["fuelTicksLeft"] = _fuelTicksLeft;
        root["fuelTicksMax"]  = _fuelTicksMax;
        root["fuelResultItemId"] = _fuelResultItemId;
        root["fuelResultAmount"] = _fuelResultAmount;
        root["fireHoldTicksLeft"] = _fireHoldTicksLeft;

        // smelt state
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

        // Crucible layers 저장 (ItemData.Details 자체를 저장하지 않는 프로젝트 패턴이므로 별도)
        if (_crucible != null && _crucible.Count > 0)
        {
            if (_crucible.Details.TryGetValue("layers", out var layersObj) && layersObj != null)
            {
                root["crucible_layers"] = JToken.FromObject(layersObj);
            }
        }

        // ✅ OriginalSolidIds (row-major)
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

                // Crucible layers 복원
                if (_crucible != null && root["crucible_layers"] != null && root["crucible_layers"].Type != JTokenType.Null)
                {
                    var list = root["crucible_layers"].ToObject<List<object>>();
                    if (list != null)
                        _crucible.SetDetail("layers", list);
                }
            }
        }

        CleanupZeroCountSlots();

        // 로드 후에도 우선순위/예약 규칙을 강제해서 모순 제거(진행도 유지 방식)
        RecomputeReservationsByPriority();

        // meta는 "연료가 타는중" 기준으로 맞춤
        RequestApplyFurnaceMeta(IsBurning);
    }

    // ─────────────────────────────────────────────
    // small utils
    // ─────────────────────────────────────────────
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
