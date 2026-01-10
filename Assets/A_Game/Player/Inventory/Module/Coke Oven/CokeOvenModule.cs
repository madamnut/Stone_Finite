// CokeOvenModule.cs
using UnityEngine;
using UnityEngine.UI;

public class CokeOvenModule : MonoBehaviour
{
    [Header("Slots")]
    public ItemSlot fuelIn;
    public ItemSlot fuelOut;

    public ItemSlot materialIn;
    public ItemSlot out0;
    public ItemSlot out1;

    [Header("Gauges (Filled Image)")]
    public Image fireGauge;     // 연료(불) 게이지
    public Image progressGauge; // Coke 작업 진행도(선택)

    CokeOven _oven;

    // ───────── 스냅샷(입력 슬롯 변경 감지용) ─────────
    ItemData _prevFuelIn;
    int _prevFuelInCount;
    int _prevFuelInDur;

    ItemData _prevMatIn;
    int _prevMatInCount;
    int _prevMatInDur;

    // ───────── 스냅샷(출력 슬롯 변경 감지용: 유저가 꺼냈는지) ─────────
    ItemData _prevFuelOut;
    int _prevFuelOutCount;
    int _prevFuelOutDur;

    ItemData _prevOut0;
    int _prevOut0Count;
    int _prevOut0Dur;

    ItemData _prevOut1;
    int _prevOut1Count;
    int _prevOut1Dur;

    public void Bind(CokeOven oven)
    {
        _oven = oven;

        SetupSlot(fuelIn,     denyPut: false, denyInteraction: false);
        SetupSlot(fuelOut,    denyPut: true,  denyInteraction: false);

        SetupSlot(materialIn, denyPut: false, denyInteraction: false);
        SetupSlot(out0,       denyPut: true,  denyInteraction: false);
        SetupSlot(out1,       denyPut: true,  denyInteraction: false);

        // 초기 Pull
        PullFromOven();

        CaptureInputSnapshots();
        CaptureOutputSnapshots();

        RefreshGaugesAndProgress();
    }

    void SetupSlot(ItemSlot slot, bool denyPut, bool denyInteraction)
    {
        if (slot == null) return;

        slot.useLocalStorage     = true;
        slot.denyUserPut         = denyPut;
        slot.denyUserInteraction = denyInteraction;

        if (slot.Item == null) slot.Set(null);
        else slot.Refresh();

        slot.SetProgress(0f, false);
    }

    void Update()
    {
        if (_oven == null) return;

        // 1) 출력 슬롯이 변했으면(유저가 꺼냄) -> Oven에 반영
        if (OutputsChanged())
        {
            PushOutputsToOven();
            CaptureOutputSnapshots();
        }

        // 2) 입력 슬롯이 변했으면(유저가 넣음/빼거나 스택 변화) -> Oven에 반영
        if (InputsChanged())
        {
            PushInputsToOven();
            CaptureInputSnapshots();
        }

        // 3) 매 프레임 Oven 상태를 UI로 Pull
        PullFromOven();
        RefreshGaugesAndProgress();
    }

    // ─────────────────────────────────────────────
    // Push (UI -> Oven)
    // ─────────────────────────────────────────────
    void PushInputsToOven()
    {
        if (_oven == null) return;

        if (fuelIn != null)
        {
            var cur = fuelIn.Item;
            if (Changed(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, cur))
                _oven.SetSlot(CokeOven.SlotKind.FuelIn, cur);
        }

        if (materialIn != null)
        {
            var cur = materialIn.Item;
            if (Changed(_prevMatIn, _prevMatInCount, _prevMatInDur, cur))
                _oven.SetSlot(CokeOven.SlotKind.MaterialIn, cur);
        }
    }

    void PushOutputsToOven()
    {
        if (_oven == null) return;

        if (fuelOut != null)
        {
            var cur = fuelOut.Item;
            if (Changed(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, cur))
                _oven.SetSlot(CokeOven.SlotKind.FuelOut, cur);
        }

        if (out0 != null)
        {
            var cur = out0.Item;
            if (Changed(_prevOut0, _prevOut0Count, _prevOut0Dur, cur))
                _oven.SetSlot(CokeOven.SlotKind.MaterialOut0, cur);
        }

        if (out1 != null)
        {
            var cur = out1.Item;
            if (Changed(_prevOut1, _prevOut1Count, _prevOut1Dur, cur))
                _oven.SetSlot(CokeOven.SlotKind.MaterialOut1, cur);
        }
    }

    // ─────────────────────────────────────────────
    // Pull (Oven -> UI)
    // ─────────────────────────────────────────────
    void PullFromOven()
    {
        if (_oven == null) return;

        SetSlotIfDifferent(fuelIn,     _oven.GetSlot(CokeOven.SlotKind.FuelIn));
        SetSlotIfDifferent(fuelOut,    _oven.GetSlot(CokeOven.SlotKind.FuelOut));

        SetSlotIfDifferent(materialIn, _oven.GetSlot(CokeOven.SlotKind.MaterialIn));
        SetSlotIfDifferent(out0,       _oven.GetSlot(CokeOven.SlotKind.MaterialOut0));
        SetSlotIfDifferent(out1,       _oven.GetSlot(CokeOven.SlotKind.MaterialOut1));
    }

    void SetSlotIfDifferent(ItemSlot ui, ItemData data)
    {
        if (ui == null) return;

        if (!ReferenceEquals(ui.Item, data))
            ui.Set(data);
        else
            ui.Refresh();
    }

    // ─────────────────────────────────────────────
    // Gauges / Progress
    // ─────────────────────────────────────────────
    void RefreshGaugesAndProgress()
    {
        if (_oven == null) return;

        if (fireGauge != null)
            fireGauge.fillAmount = Mathf.Clamp01(_oven.FuelProgress01);

        float cokeP = Mathf.Clamp01(_oven.CokeProgress01);

        // 전체 진행도 게이지(선택)
        if (progressGauge != null)
            progressGauge.fillAmount = cokeP;
    }

    // ─────────────────────────────────────────────
    // Snapshot / Changed
    // ─────────────────────────────────────────────
    bool InputsChanged()
    {
        bool changed = false;

        if (fuelIn != null)
        {
            var cur = fuelIn.Item;
            if (Changed(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, cur)) changed = true;
        }

        if (materialIn != null)
        {
            var cur = materialIn.Item;
            if (Changed(_prevMatIn, _prevMatInCount, _prevMatInDur, cur)) changed = true;
        }

        return changed;
    }

    bool OutputsChanged()
    {
        bool changed = false;

        if (fuelOut != null)
        {
            var cur = fuelOut.Item;
            if (Changed(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, cur)) changed = true;
        }

        if (out0 != null)
        {
            var cur = out0.Item;
            if (Changed(_prevOut0, _prevOut0Count, _prevOut0Dur, cur)) changed = true;
        }

        if (out1 != null)
        {
            var cur = out1.Item;
            if (Changed(_prevOut1, _prevOut1Count, _prevOut1Dur, cur)) changed = true;
        }

        return changed;
    }

    void CaptureInputSnapshots()
    {
        Capture(ref _prevFuelIn, ref _prevFuelInCount, ref _prevFuelInDur, fuelIn);
        Capture(ref _prevMatIn,  ref _prevMatInCount,  ref _prevMatInDur,  materialIn);
    }

    void CaptureOutputSnapshots()
    {
        Capture(ref _prevFuelOut, ref _prevFuelOutCount, ref _prevFuelOutDur, fuelOut);
        Capture(ref _prevOut0,    ref _prevOut0Count,    ref _prevOut0Dur,    out0);
        Capture(ref _prevOut1,    ref _prevOut1Count,    ref _prevOut1Dur,    out1);
    }

    void Capture(ref ItemData prevRef, ref int prevCount, ref int prevDur, ItemSlot slot)
    {
        var cur = (slot != null) ? slot.Item : null;
        prevRef = cur;
        prevCount = (cur != null) ? cur.Count : 0;
        prevDur   = (cur != null) ? cur.Durability : 0;
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
