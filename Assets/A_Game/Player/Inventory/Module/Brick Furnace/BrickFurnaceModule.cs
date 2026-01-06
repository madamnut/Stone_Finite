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
    public Image fireGauge;

    [Header("Crucible View (Optional)")]
    public CrucibleView crucibleView;

    BrickFurnace _furnace;

    ItemData _prevFuelIn; int _prevFuelInCount; int _prevFuelInDur;
    ItemData _prevCrucible; int _prevCrucibleCount; int _prevCrucibleDur;

    readonly ItemData[] _prevIns = new ItemData[9];
    readonly int[] _prevInsCount = new int[9];
    readonly int[] _prevInsDur = new int[9];

    ItemData _prevFuelOut; int _prevFuelOutCount; int _prevFuelOutDur;

    // ✅ CrucibleView 바인딩 캐시(매 프레임 Bind 호출 방지)
    ItemData _boundCrucibleForView;

    public void Bind(BrickFurnace furnace)
    {
        _furnace = furnace;

        if (crucibleView != null && furnace != null && furnace.World != null)
            crucibleView.itemLibrary = furnace.World.itemLibrary;

        SetupSlot(fuelIn,   denyPut: false, denyInteraction: false);
        SetupSlot(fuelOut,  denyPut: true,  denyInteraction: false);

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

        slot.SetProgress(0f, false);
    }

    void Update()
    {
        if (_furnace == null) return;

        bool inputsChanged = InputsChanged();
        bool outputChanged = OutputChanged();

        if (inputsChanged)
        {
            PushInputsToFurnace();
            CaptureInputSnapshots();
        }

        if (outputChanged)
        {
            PushOutputsToFurnace();
            CaptureOutputSnapshots();
        }

        PullFromFurnace();

        RefreshGaugesAndProgress();
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

    void PushInputsToFurnace()
    {
        if (_furnace == null) return;

        if (fuelIn != null)
        {
            var cur = fuelIn.Item;
            if (Changed(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, cur))
                _furnace.SetSlot(BrickFurnace.SlotKind.FuelIn, cur);
        }

        if (crucible != null)
        {
            var cur = crucible.Item;
            if (Changed(_prevCrucible, _prevCrucibleCount, _prevCrucibleDur, cur))
                _furnace.SetSlot(BrickFurnace.SlotKind.Crucible, cur);
        }

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

        if (fireGauge != null)
            fireGauge.fillAmount = Mathf.Clamp01(_furnace.FuelProgress01);

        for (int i = 0; i < 9; i++)
        {
            var slot = GetInputSlot(i);
            if (slot == null) continue;

            float p = Mathf.Clamp01(_furnace.GetInputProgress01(i));
            slot.SetProgress(p, p > 0f);
        }
    }

    // ─────────────────────────────────────────────
    // CrucibleView (✅ CrucibleView가 ItemData를 직접 수정)
    // ─────────────────────────────────────────────
    void RefreshCrucibleView()
    {
        if (crucibleView == null) return;

        ItemData c = (crucible != null) ? crucible.Item : null;

        if (!ReferenceEquals(_boundCrucibleForView, c))
        {
            _boundCrucibleForView = c;
            crucibleView.BindCrucible(c);
            return;
        }

        // 같은 아이템이면 Refresh만
        crucibleView.Refresh();
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
        _prevFuelIn = (fuelIn != null) ? fuelIn.Item : null;
        _prevFuelInCount = (_prevFuelIn != null) ? _prevFuelIn.Count : 0;
        _prevFuelInDur   = (_prevFuelIn != null) ? _prevFuelIn.Durability : 0;

        _prevCrucible = (crucible != null) ? crucible.Item : null;
        _prevCrucibleCount = (_prevCrucible != null) ? _prevCrucible.Count : 0;
        _prevCrucibleDur   = (_prevCrucible != null) ? _prevCrucible.Durability : 0;

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
        var curFuelIn = (fuelIn != null) ? fuelIn.Item : null;
        if (Changed(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, curFuelIn)) return true;

        var curCrucible = (crucible != null) ? crucible.Item : null;
        if (Changed(_prevCrucible, _prevCrucibleCount, _prevCrucibleDur, curCrucible)) return true;

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
