// BrickFurnaceModule.cs
// - BrickFurnace UI 모듈
// - 입력 9슬롯 진행도는 ItemSlot에 새로 추가한 progressBar 사용
// - 패턴은 CampfireModule / ClayKilnModule과 동일:
//   1) 유저가 출력 슬롯에서 꺼냈으면 먼저 BrickFurnace에 반영
//   2) 입력 변경 시 BrickFurnace에 반영
//   3) BrickFurnace -> UI Pull
//   4) 게이지/진행도 갱신
//
// [전제]
// BrickFurnace가 아래 API를 제공한다고 가정함:
// - ItemData GetSlot(BrickFurnace.SlotKind kind)
// - void    SetSlot(BrickFurnace.SlotKind kind, ItemData item)
// - float   FuelProgress01 { get; }                 // 0~1 (연료 게이지)
// - float   GetInputProgress01(int index0to8)       // 0~1 (입력 슬롯별 진행도, 예약 없으면 0)
//   (진행도 == "예약된 1개분 smelt 진행" 이라는 규칙 그대로)
//
// ItemSlot에 아래 UI 레퍼런스가 추가되어 있다고 가정:
// - public GameObject progressRoot;
// - public Image      progressBar;

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
        SnapshotAll();
        RefreshGaugesAndProgress();
    }

    void SetupSlot(ItemSlot slot, bool denyPut, bool denyInteraction)
    {
        if (slot == null) return;

        slot.useLocalStorage       = true;
        slot.inventory             = null;
        slot.index                 = -1;
        slot.denyUserPut           = denyPut;
        slot.denyUserInteraction   = denyInteraction;

        // progressBar는 시작 비활성(특수 경우에만)
        // (ItemSlot 쪽에서 Awake 시 꺼두는게 베스트지만, 여기서도 한 번 더 안전하게)
        if (slot.progressRoot != null)
            slot.progressRoot.SetActive(false);
        if (slot.progressBar != null)
            slot.progressBar.fillAmount = 0f;
    }

    void Update()
    {
        if (_furnace == null) return;

        // 1) 유저가 출력 슬롯(fuelOut)에서 꺼냈는지 먼저 반영
        if (OutputsChanged())
        {
            PushOutputsToFurnace();
            SnapshotOutputs();
        }

        // 2) 입력 변경 반영
        if (InputsChanged())
        {
            PushInputsToFurnace();
            SnapshotInputs();
        }

        // 3) 표시 동기화
        PullFromFurnace();
        SnapshotAll(); // Pull 이후 스냅샷 재정렬(덮어쓰기/깜빡임 방지)

        // 4) 게이지/진행도
        RefreshGaugesAndProgress();
    }

    // ─────────────────────────────────────────────
    // Push/Pull
    // ─────────────────────────────────────────────
    void PullFromFurnace()
    {
        if (_furnace == null) return;

        if (fuelIn != null)  fuelIn.Set(_furnace.GetSlot(BrickFurnace.SlotKind.FuelIn));
        if (fuelOut != null) fuelOut.Set(_furnace.GetSlot(BrickFurnace.SlotKind.FuelOut));
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
            _furnace.SetSlot(BrickFurnace.SlotKind.FuelIn, fuelIn.Item);

        if (crucible != null)
            _furnace.SetSlot(BrickFurnace.SlotKind.Crucible, crucible.Item);

        var s0 = GetInputSlot(0); if (s0 != null) _furnace.SetSlot(BrickFurnace.SlotKind.In0, s0.Item);
        var s1 = GetInputSlot(1); if (s1 != null) _furnace.SetSlot(BrickFurnace.SlotKind.In1, s1.Item);
        var s2 = GetInputSlot(2); if (s2 != null) _furnace.SetSlot(BrickFurnace.SlotKind.In2, s2.Item);
        var s3 = GetInputSlot(3); if (s3 != null) _furnace.SetSlot(BrickFurnace.SlotKind.In3, s3.Item);
        var s4 = GetInputSlot(4); if (s4 != null) _furnace.SetSlot(BrickFurnace.SlotKind.In4, s4.Item);
        var s5 = GetInputSlot(5); if (s5 != null) _furnace.SetSlot(BrickFurnace.SlotKind.In5, s5.Item);
        var s6 = GetInputSlot(6); if (s6 != null) _furnace.SetSlot(BrickFurnace.SlotKind.In6, s6.Item);
        var s7 = GetInputSlot(7); if (s7 != null) _furnace.SetSlot(BrickFurnace.SlotKind.In7, s7.Item);
        var s8 = GetInputSlot(8); if (s8 != null) _furnace.SetSlot(BrickFurnace.SlotKind.In8, s8.Item);
    }

    void PushOutputsToFurnace()
    {
        if (_furnace == null) return;

        if (fuelOut != null)
            _furnace.SetSlot(BrickFurnace.SlotKind.FuelOut, fuelOut.Item);
    }

    // ─────────────────────────────────────────────
    // Change detection
    // ─────────────────────────────────────────────
    bool OutputsChanged()
    {
        var f = fuelOut != null ? fuelOut.Item : null;
        int fc = f != null ? f.Count : 0;
        int fd = f != null ? f.Durability : 0;

        if (f != _prevFuelOut || fc != _prevFuelOutCount || fd != _prevFuelOutDur)
            return true;

        return false;
    }

    bool InputsChanged()
    {
        // FuelIn
        var fi = fuelIn != null ? fuelIn.Item : null;
        int fic = fi != null ? fi.Count : 0;
        int fid = fi != null ? fi.Durability : 0;
        if (fi != _prevFuelIn || fic != _prevFuelInCount || fid != _prevFuelInDur)
            return true;

        // Crucible
        var c = crucible != null ? crucible.Item : null;
        int cc = c != null ? c.Count : 0;
        int cd = c != null ? c.Durability : 0;
        if (c != _prevCrucible || cc != _prevCrucibleCount || cd != _prevCrucibleDur)
            return true;

        // Inputs 0..8
        for (int i = 0; i < 9; i++)
        {
            var s = GetInputSlot(i);
            var it = s != null ? s.Item : null;
            int cnt = it != null ? it.Count : 0;
            int dur = it != null ? it.Durability : 0;

            if (it != _prevIns[i] || cnt != _prevInsCount[i] || dur != _prevInsDur[i])
                return true;
        }

        return false;
    }

    void SnapshotAll()
    {
        SnapshotInputs();
        SnapshotOutputs();
    }

    void SnapshotOutputs()
    {
        var f = fuelOut != null ? fuelOut.Item : null;
        _prevFuelOut = f;
        _prevFuelOutCount = f != null ? f.Count : 0;
        _prevFuelOutDur   = f != null ? f.Durability : 0;
    }

    void SnapshotInputs()
    {
        var fi = fuelIn != null ? fuelIn.Item : null;
        _prevFuelIn = fi;
        _prevFuelInCount = fi != null ? fi.Count : 0;
        _prevFuelInDur   = fi != null ? fi.Durability : 0;

        var c = crucible != null ? crucible.Item : null;
        _prevCrucible = c;
        _prevCrucibleCount = c != null ? c.Count : 0;
        _prevCrucibleDur   = c != null ? c.Durability : 0;

        for (int i = 0; i < 9; i++)
        {
            var s = GetInputSlot(i);
            var it = s != null ? s.Item : null;

            _prevIns[i] = it;
            _prevInsCount[i] = it != null ? it.Count : 0;
            _prevInsDur[i]   = it != null ? it.Durability : 0;
        }
    }

    // ─────────────────────────────────────────────
    // Gauges / ProgressBar
    // ─────────────────────────────────────────────
    void RefreshGaugesAndProgress()
    {
        if (_furnace == null) return;

        if (fireGauge != null)
            fireGauge.fillAmount = _furnace.FuelProgress01;

        // 입력 9슬롯 progressBar (예약된 슬롯만 0~1)
        for (int i = 0; i < 9; i++)
        {
            var slot = GetInputSlot(i);
            if (slot == null) continue;

            float p01 = _furnace.GetInputProgress01(i);
            p01 = Mathf.Clamp01(p01);

            // 진행도 > 0 일 때만 표시(요구: 시작 시 비활성)
            if (slot.progressRoot != null)
                slot.progressRoot.SetActive(p01 > 0f);

            if (slot.progressBar != null)
                slot.progressBar.fillAmount = p01;
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
}
