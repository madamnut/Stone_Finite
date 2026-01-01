// ClayKilnModule.cs (전체 교체본)
using UnityEngine;
using UnityEngine.UI;

public class ClayKilnModule : MonoBehaviour
{
    [Header("Slots")]
    public ItemSlot fuelIn;
    public ItemSlot fuelOut;

    public ItemSlot fireInA;
    public ItemSlot fireOutA;

    public ItemSlot fireInB;
    public ItemSlot fireOutB;

    [Header("Gauges (Filled Image)")]
    public Image fireGauge;     // 연료(불) 게이지
    public Image progressGaugeA; // A 라인 굽기 진행도
    public Image progressGaugeB; // B 라인 굽기 진행도

    ClayKiln _kiln;

    // ───────── 스냅샷(입력 슬롯 변경 감지용) ─────────
    ItemData _prevFuelIn;
    int _prevFuelInCount;
    int _prevFuelInDur;

    ItemData _prevFireInA;
    int _prevFireInACount;
    int _prevFireInADur;

    ItemData _prevFireInB;
    int _prevFireInBCount;
    int _prevFireInBDur;

    // ───────── 스냅샷(출력 슬롯 변경 감지용: 유저가 꺼냈는지) ─────────
    ItemData _prevFuelOut;
    int _prevFuelOutCount;
    int _prevFuelOutDur;

    ItemData _prevFireOutA;
    int _prevFireOutACount;
    int _prevFireOutADur;

    ItemData _prevFireOutB;
    int _prevFireOutBCount;
    int _prevFireOutBDur;

    public void Bind(ClayKiln kiln)
    {
        _kiln = kiln;

        // 로컬 슬롯 모드
        SetupSlot(fuelIn,   denyPut: false, denyInteraction: false);
        SetupSlot(fuelOut,  denyPut: true,  denyInteraction: false); // 출력: 넣기 금지, 빼기 허용

        SetupSlot(fireInA,  denyPut: false, denyInteraction: false);
        SetupSlot(fireOutA, denyPut: true,  denyInteraction: false); // 출력: 넣기 금지, 빼기 허용

        SetupSlot(fireInB,  denyPut: false, denyInteraction: false);
        SetupSlot(fireOutB, denyPut: true,  denyInteraction: false); // 출력: 넣기 금지, 빼기 허용

        // 최초 UI 반영
        PullFromKiln();
        SnapshotAll();
        RefreshGauges();
    }

    void SetupSlot(ItemSlot slot, bool denyPut, bool denyInteraction)
    {
        if (slot == null) return;

        slot.useLocalStorage     = true;
        slot.denyUserPut         = denyPut;
        slot.denyUserInteraction = denyInteraction;

        if (slot.Item == null) slot.Set(null);
        else slot.Refresh();
    }

    void Update()
    {
        if (_kiln == null) return;

        // 1) 출력 슬롯이 변했으면(유저가 꺼냄) -> Kiln에 반영
        if (OutputsChanged())
        {
            PushOutputsToKiln();
            SnapshotOutputs();
        }

        // 2) 입력이 변했으면 -> Kiln에 반영
        if (InputsChanged())
        {
            PushInputsToKiln();
            SnapshotInputs();
        }

        // 3) 표시 동기화 (킬른 로직에서 바뀐 결과/카운트 반영)
        PullFromKiln();
        SnapshotAll(); // Pull 이후 스냅샷 재정렬(덮어쓰기/깜빡임 방지)

        // 4) 게이지
        RefreshGauges();
    }

    bool InputsChanged()
    {
        // FuelIn
        var f = fuelIn != null ? fuelIn.Item : null;
        int fc = f != null ? f.Count : 0;
        int fd = f != null ? f.Durability : 0;
        if (f != _prevFuelIn || fc != _prevFuelInCount || fd != _prevFuelInDur)
            return true;

        // FireInA
        var a = fireInA != null ? fireInA.Item : null;
        int ac = a != null ? a.Count : 0;
        int ad = a != null ? a.Durability : 0;
        if (a != _prevFireInA || ac != _prevFireInACount || ad != _prevFireInADur)
            return true;

        // FireInB
        var b = fireInB != null ? fireInB.Item : null;
        int bc = b != null ? b.Count : 0;
        int bd = b != null ? b.Durability : 0;
        if (b != _prevFireInB || bc != _prevFireInBCount || bd != _prevFireInBDur)
            return true;

        return false;
    }

    bool OutputsChanged()
    {
        // FuelOut
        var f = fuelOut != null ? fuelOut.Item : null;
        int fc = f != null ? f.Count : 0;
        int fd = f != null ? f.Durability : 0;
        if (f != _prevFuelOut || fc != _prevFuelOutCount || fd != _prevFuelOutDur)
            return true;

        // FireOutA
        var a = fireOutA != null ? fireOutA.Item : null;
        int ac = a != null ? a.Count : 0;
        int ad = a != null ? a.Durability : 0;
        if (a != _prevFireOutA || ac != _prevFireOutACount || ad != _prevFireOutADur)
            return true;

        // FireOutB
        var b = fireOutB != null ? fireOutB.Item : null;
        int bc = b != null ? b.Count : 0;
        int bd = b != null ? b.Durability : 0;
        if (b != _prevFireOutB || bc != _prevFireOutBCount || bd != _prevFireOutBDur)
            return true;

        return false;
    }

    void SnapshotInputs()
    {
        var f = fuelIn != null ? fuelIn.Item : null;
        _prevFuelIn = f;
        _prevFuelInCount = f != null ? f.Count : 0;
        _prevFuelInDur = f != null ? f.Durability : 0;

        var a = fireInA != null ? fireInA.Item : null;
        _prevFireInA = a;
        _prevFireInACount = a != null ? a.Count : 0;
        _prevFireInADur = a != null ? a.Durability : 0;

        var b = fireInB != null ? fireInB.Item : null;
        _prevFireInB = b;
        _prevFireInBCount = b != null ? b.Count : 0;
        _prevFireInBDur = b != null ? b.Durability : 0;
    }

    void SnapshotOutputs()
    {
        var f = fuelOut != null ? fuelOut.Item : null;
        _prevFuelOut = f;
        _prevFuelOutCount = f != null ? f.Count : 0;
        _prevFuelOutDur = f != null ? f.Durability : 0;

        var a = fireOutA != null ? fireOutA.Item : null;
        _prevFireOutA = a;
        _prevFireOutACount = a != null ? a.Count : 0;
        _prevFireOutADur = a != null ? a.Durability : 0;

        var b = fireOutB != null ? fireOutB.Item : null;
        _prevFireOutB = b;
        _prevFireOutBCount = b != null ? b.Count : 0;
        _prevFireOutBDur = b != null ? b.Durability : 0;
    }

    void SnapshotAll()
    {
        SnapshotInputs();
        SnapshotOutputs();
    }

    void PullFromKiln()
    {
        if (_kiln == null) return;

        if (fuelIn != null)   fuelIn.Set(_kiln.GetSlot(ClayKiln.SlotKind.FuelIn));
        if (fuelOut != null)  fuelOut.Set(_kiln.GetSlot(ClayKiln.SlotKind.FuelOut));

        if (fireInA != null)  fireInA.Set(_kiln.GetSlot(ClayKiln.SlotKind.FireInA));
        if (fireOutA != null) fireOutA.Set(_kiln.GetSlot(ClayKiln.SlotKind.FireOutA));

        if (fireInB != null)  fireInB.Set(_kiln.GetSlot(ClayKiln.SlotKind.FireInB));
        if (fireOutB != null) fireOutB.Set(_kiln.GetSlot(ClayKiln.SlotKind.FireOutB));
    }

    void PushInputsToKiln()
    {
        if (_kiln == null) return;

        if (fuelIn != null)
            _kiln.SetSlot(ClayKiln.SlotKind.FuelIn, fuelIn.Item);

        if (fireInA != null)
            _kiln.SetSlot(ClayKiln.SlotKind.FireInA, fireInA.Item);

        if (fireInB != null)
            _kiln.SetSlot(ClayKiln.SlotKind.FireInB, fireInB.Item);
    }

    void PushOutputsToKiln()
    {
        if (_kiln == null) return;

        if (fuelOut != null)
            _kiln.SetSlot(ClayKiln.SlotKind.FuelOut, fuelOut.Item);

        if (fireOutA != null)
            _kiln.SetSlot(ClayKiln.SlotKind.FireOutA, fireOutA.Item);

        if (fireOutB != null)
            _kiln.SetSlot(ClayKiln.SlotKind.FireOutB, fireOutB.Item);
    }

    void RefreshGauges()
    {
        if (_kiln == null) return;

        if (fireGauge != null)
            fireGauge.fillAmount = _kiln.FuelProgress01;

        if (progressGaugeA != null)
            progressGaugeA.fillAmount = _kiln.FireProgressA01;

        if (progressGaugeB != null)
            progressGaugeB.fillAmount = _kiln.FireProgressB01;
    }
}
