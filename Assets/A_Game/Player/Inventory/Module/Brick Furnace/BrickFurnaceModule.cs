// BrickFurnaceModule.cs
// - BrickFurnace 멀티블럭 UI 모듈
// - 입력 9슬롯은 ItemSlot.SetProgress()로 "슬롯별 smelt 진행도" 표시
// - 연료 게이지는 (선택) fireGauge 로 표시
// - CrucibleView(선택): Crucible 용량 + layers를 전달하여 도가니 내부 층 시각화
//
// BrickFurnace가 아래 API를 제공한다고 가정:
// - ItemData GetSlot(BrickFurnace.SlotKind kind)
// - void    SetSlot(BrickFurnace.SlotKind kind, ItemData item)
// - float   FuelProgress01 { get; }                 // 0~1
// - float   GetInputProgress01(int index0to8)       // 0~1 (예약/진행 없으면 0)
//
// ItemSlot에 아래 API가 추가되어 있다고 가정:
// - public void SetProgress(float fill01, bool show)

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BrickFurnaceModule : MonoBehaviour
{
    [Header("Slots")]
    public ItemSlot fuelIn;
    public ItemSlot fuelOut;

    public ItemSlot crucible;

    public ItemSlot in0;
    public ItemSlot in1;
    public ItemSlot in2;
    public ItemSlot in3;
    public ItemSlot in4;
    public ItemSlot in5;
    public ItemSlot in6;
    public ItemSlot in7;
    public ItemSlot in8;

    [Header("Gauges (Optional)")]
    public Image fireGauge; // 연료 게이지(있으면 표시)

    [Header("Crucible View (Optional)")]
    public CrucibleView crucibleView; // 도가니 레이어 시각화

    BrickFurnace _furnace;

    // ────────── 입력 스냅샷(변경 감지) ──────────
    ItemData _prevFuelIn; int _prevFuelInCount; int _prevFuelInDur;
    ItemData _prevCrucible; int _prevCrucibleCount; int _prevCrucibleDur;

    readonly ItemData[] _prevIns = new ItemData[9];
    readonly int[] _prevInsCount = new int[9];
    readonly int[] _prevInsDur = new int[9];

    // ────────── 출력 스냅샷(유저가 꺼냈는지 감지) ──────────
    ItemData _prevFuelOut; int _prevFuelOutCount; int _prevFuelOutDur;

    public void Bind(BrickFurnace furnace)
    {
        _furnace = furnace;

        // CrucibleView deps 주입
        if (crucibleView != null && furnace != null && furnace.World != null)
            crucibleView.itemLibrary = furnace.World.itemLibrary;

        // 로컬 슬롯 모드
        SetupSlot(fuelIn,   denyPut: false, denyInteraction: false);
        SetupSlot(fuelOut,  denyPut: true,  denyInteraction: false); // 출력: 넣기 금지, 빼기 허용

        SetupSlot(crucible, denyPut: false, denyInteraction: false);

        SetupSlot(in0, denyPut: false, denyInteraction: false);
        SetupSlot(in1, denyPut: false, denyInteraction: false);
        SetupSlot(in2, denyPut: false, denyInteraction: false);
        SetupSlot(in3, denyPut: false, denyInteraction: false);
        SetupSlot(in4, denyPut: false, denyInteraction: false);
        SetupSlot(in5, denyPut: false, denyInteraction: false);
        SetupSlot(in6, denyPut: false, denyInteraction: false);
        SetupSlot(in7, denyPut: false, denyInteraction: false);
        SetupSlot(in8, denyPut: false, denyInteraction: false);

        // 최초 UI 반영
        PullFromFurnace();
        CaptureSnapshots();
        RefreshGaugesAndProgress();
        RefreshCrucibleView();
    }

    void SetupSlot(ItemSlot slot, bool denyPut, bool denyInteraction)
    {
        if (slot == null) return;

        slot.useLocalStorage = true;
        slot.inventory = null;
        slot.index = -1;

        slot.denyUserPut = denyPut;
        slot.denyUserInteraction = denyInteraction;

        // progress UI는 기본 OFF 유지
        slot.SetProgress(0f, false);
    }

    void Update()
    {
        if (_furnace == null) return;

        // 유저 조작(투입/교체/제거) 반영
        bool inputsChanged = InputsChanged();
        bool outputChanged = OutputChanged();

        if (inputsChanged)
        {
            PushInputsToFurnace();      // ✅ 변경된 슬롯만 SetSlot
            CaptureInputSnapshots();
        }

        if (outputChanged)
        {
            PushOutputsToFurnace();     // ✅ 변경된 슬롯만 SetSlot
            CaptureOutputSnapshots();
        }

        // Furnace 틱으로 인해 내부 아이템이 변할 수 있으니 UI는 항상 Pull
        PullFromFurnace();

        // 게이지/진행도 UI 갱신
        RefreshGaugesAndProgress();

        // 도가니 레이어 시각화 갱신
        RefreshCrucibleView();
    }

    void PullFromFurnace()
    {
        if (_furnace == null) return;

        if (fuelIn != null)   fuelIn.Set(_furnace.GetSlot(BrickFurnace.SlotKind.FuelIn));
        if (fuelOut != null)  fuelOut.Set(_furnace.GetSlot(BrickFurnace.SlotKind.FuelOut));
        if (crucible != null) crucible.Set(_furnace.GetSlot(BrickFurnace.SlotKind.Crucible));

        SetInputSlotUI(0, _furnace.GetSlot(BrickFurnace.SlotKind.In0));
        SetInputSlotUI(1, _furnace.GetSlot(BrickFurnace.SlotKind.In1));
        SetInputSlotUI(2, _furnace.GetSlot(BrickFurnace.SlotKind.In2));
        SetInputSlotUI(3, _furnace.GetSlot(BrickFurnace.SlotKind.In3));
        SetInputSlotUI(4, _furnace.GetSlot(BrickFurnace.SlotKind.In4));
        SetInputSlotUI(5, _furnace.GetSlot(BrickFurnace.SlotKind.In5));
        SetInputSlotUI(6, _furnace.GetSlot(BrickFurnace.SlotKind.In6));
        SetInputSlotUI(7, _furnace.GetSlot(BrickFurnace.SlotKind.In7));
        SetInputSlotUI(8, _furnace.GetSlot(BrickFurnace.SlotKind.In8));
    }

    void SetInputSlotUI(int i, ItemData item)
    {
        var slot = GetInputSlot(i);
        if (slot == null) return;
        slot.Set(item);
    }

    // ✅ 구조 수정: 변경된 슬롯만 SetSlot 호출
    void PushInputsToFurnace()
    {
        if (_furnace == null) return;

        // fuelIn
        if (fuelIn != null)
        {
            var cur = fuelIn.Item;
            if (Changed(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, cur))
                _furnace.SetSlot(BrickFurnace.SlotKind.FuelIn, cur);
        }

        // crucible
        if (crucible != null)
        {
            var cur = crucible.Item;
            if (Changed(_prevCrucible, _prevCrucibleCount, _prevCrucibleDur, cur))
                _furnace.SetSlot(BrickFurnace.SlotKind.Crucible, cur);
        }

        // inputs 0~8
        for (int i = 0; i < 9; i++)
        {
            var s = GetInputSlot(i);
            var cur = (s != null) ? s.Item : null;

            if (!Changed(_prevIns[i], _prevInsCount[i], _prevInsDur[i], cur))
                continue;

            switch (i)
            {
                case 0: _furnace.SetSlot(BrickFurnace.SlotKind.In0, cur); break;
                case 1: _furnace.SetSlot(BrickFurnace.SlotKind.In1, cur); break;
                case 2: _furnace.SetSlot(BrickFurnace.SlotKind.In2, cur); break;
                case 3: _furnace.SetSlot(BrickFurnace.SlotKind.In3, cur); break;
                case 4: _furnace.SetSlot(BrickFurnace.SlotKind.In4, cur); break;
                case 5: _furnace.SetSlot(BrickFurnace.SlotKind.In5, cur); break;
                case 6: _furnace.SetSlot(BrickFurnace.SlotKind.In6, cur); break;
                case 7: _furnace.SetSlot(BrickFurnace.SlotKind.In7, cur); break;
                case 8: _furnace.SetSlot(BrickFurnace.SlotKind.In8, cur); break;
            }
        }
    }

    // ✅ 구조 수정: 변경된 경우에만 SetSlot 호출
    void PushOutputsToFurnace()
    {
        if (_furnace == null) return;

        if (fuelOut != null)
        {
            var cur = fuelOut.Item;
            if (Changed(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, cur))
                _furnace.SetSlot(BrickFurnace.SlotKind.FuelOut, cur);
        }
    }

    ItemSlot GetInputSlot(int i)
    {
        switch (i)
        {
            case 0: return in0;
            case 1: return in1;
            case 2: return in2;
            case 3: return in3;
            case 4: return in4;
            case 5: return in5;
            case 6: return in6;
            case 7: return in7;
            case 8: return in8;
        }
        return null;
    }

    void RefreshGaugesAndProgress()
    {
        if (_furnace == null) return;

        // 연료 게이지
        if (fireGauge != null)
            fireGauge.fillAmount = Mathf.Clamp01(_furnace.FuelProgress01);

        // 입력 슬롯별 진행도
        for (int i = 0; i < 9; i++)
        {
            var slot = GetInputSlot(i);
            if (slot == null) continue;

            float p = Mathf.Clamp01(_furnace.GetInputProgress01(i));
            slot.SetProgress(p, p > 0f);
        }
    }

    // ─────────────────────────────────────────────
    // CrucibleView
    // ─────────────────────────────────────────────
    void RefreshCrucibleView()
    {
        if (crucibleView == null) return;

        ItemData c = (crucible != null) ? crucible.Item : null;
        if (c == null || c.Count <= 0)
        {
            crucibleView.Clear();
            return;
        }

        int cap = ReadCrucibleCapacity(c);
        if (cap <= 0)
        {
            crucibleView.Clear();
            return;
        }

        object layersObj = null;
        if (c.Details != null && c.Details.TryGetValue("layers", out var lo) && lo != null)
            layersObj = lo;

        crucibleView.SetData(cap, layersObj);
    }

    int ReadCrucibleCapacity(ItemData c)
    {
        if (c == null) return 0;
        if (c.ToolActions == null) return 0;

        if (!c.ToolActions.TryGetValue("Crucible", out Dictionary<string, object> cfg) || cfg == null)
            return 0;

        if (!cfg.TryGetValue("capacity", out var capObj) || capObj == null)
            return 0;

        if (capObj is int i) return i;
        if (capObj is long l) return (int)l;
        if (capObj is float f) return Mathf.RoundToInt(f);
        if (capObj is double d) return (int)d;

        int r;
        return int.TryParse(capObj.ToString(), out r) ? r : 0;
    }

    // ─────────────────────────────────────────────
    // Change detection (snapshots)
    // ─────────────────────────────────────────────
    void CaptureSnapshots()
    {
        CaptureInputSnapshots();
        CaptureOutputSnapshots();
    }

    void CaptureInputSnapshots()
    {
        // fuelIn
        _prevFuelIn = (fuelIn != null) ? fuelIn.Item : null;
        _prevFuelInCount = (_prevFuelIn != null) ? _prevFuelIn.Count : 0;
        _prevFuelInDur   = (_prevFuelIn != null) ? _prevFuelIn.Durability : 0;

        // crucible
        _prevCrucible = (crucible != null) ? crucible.Item : null;
        _prevCrucibleCount = (_prevCrucible != null) ? _prevCrucible.Count : 0;
        _prevCrucibleDur   = (_prevCrucible != null) ? _prevCrucible.Durability : 0;

        // inputs
        for (int i = 0; i < 9; i++)
        {
            var s = GetInputSlot(i);
            var it = (s != null) ? s.Item : null;

            _prevIns[i] = it;
            _prevInsCount[i] = (it != null) ? it.Count : 0;
            _prevInsDur[i]   = (it != null) ? it.Durability : 0;
        }
    }

    void CaptureOutputSnapshots()
    {
        _prevFuelOut = (fuelOut != null) ? fuelOut.Item : null;
        _prevFuelOutCount = (_prevFuelOut != null) ? _prevFuelOut.Count : 0;
        _prevFuelOutDur   = (_prevFuelOut != null) ? _prevFuelOut.Durability : 0;
    }

    bool InputsChanged()
    {
        // fuelIn
        var curFuelIn = (fuelIn != null) ? fuelIn.Item : null;
        if (Changed(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, curFuelIn)) return true;

        // crucible
        var curCrucible = (crucible != null) ? crucible.Item : null;
        if (Changed(_prevCrucible, _prevCrucibleCount, _prevCrucibleDur, curCrucible)) return true;

        // inputs
        for (int i = 0; i < 9; i++)
        {
            var s = GetInputSlot(i);
            var cur = (s != null) ? s.Item : null;

            if (Changed(_prevIns[i], _prevInsCount[i], _prevInsDur[i], cur))
                return true;
        }

        return false;
    }

    bool OutputChanged()
    {
        var curFuelOut = (fuelOut != null) ? fuelOut.Item : null;
        return Changed(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, curFuelOut);
    }

    bool Changed(ItemData prevRef, int prevCount, int prevDur, ItemData cur)
    {
        if (!ReferenceEquals(prevRef, cur)) return true;

        int curCount = (cur != null) ? cur.Count : 0;
        int curDur   = (cur != null) ? cur.Durability : 0;

        if (prevCount != curCount) return true;
        if (prevDur   != curDur)   return true;

        return false;
    }
}
