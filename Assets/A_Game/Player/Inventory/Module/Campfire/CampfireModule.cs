// CampfireModule.cs
using UnityEngine;
using UnityEngine.UI;

public class CampfireModule : MonoBehaviour
{
    [Header("Slots")]
    public ItemSlot fuelIn;
    public ItemSlot fuelOut;
    public ItemSlot ingIn;
    public ItemSlot ingOut;

    [Header("Gauges (Filled Image)")]
    public Image fireGauge;
    public Image cookGauge;

    Campfire _campfire;

    // 스냅샷(입력 슬롯 변경 감지용)
    ItemData _prevFuelIn;
    int _prevFuelInCount;
    int _prevFuelInDur;

    ItemData _prevIngIn;
    int _prevIngInCount;
    int _prevIngInDur;

    // 스냅샷(출력 슬롯 변경 감지용: 유저가 꺼냈는지)
    ItemData _prevFuelOut;
    int _prevFuelOutCount;
    int _prevFuelOutDur;

    ItemData _prevIngOut;
    int _prevIngOutCount;
    int _prevIngOutDur;

    public void Bind(Campfire campfire)
    {
        _campfire = campfire;

        // 로컬 슬롯 모드
        SetupSlot(fuelIn,  denyPut: false, denyInteraction: false);
        SetupSlot(fuelOut, denyPut: true,  denyInteraction: false); // 출력: 넣기만 금지, 빼기는 허용
        SetupSlot(ingIn,   denyPut: false, denyInteraction: false);
        SetupSlot(ingOut,  denyPut: true,  denyInteraction: false); // 출력: 넣기만 금지, 빼기는 허용

        // 최초 UI 반영
        PullFromCampfire();
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
        if (_campfire == null) return;

        // 1) 유저가 출력 슬롯에서 꺼냈는지 먼저 감지해서 Campfire에 반영
        if (OutputsChanged())
        {
            PushOutputsToCampfire();
            SnapshotOutputs();
        }

        // 2) 입력 변경이 있을 때만 Campfire에 반영
        if (InputsChanged())
        {
            PushInputsToCampfire();
            SnapshotInputs();
        }

        // 3) 표시 동기화
        PullFromCampfire();
        SnapshotAll(); // Pull 이후 스냅샷을 다시 맞춰둠(덮어쓰기/깜빡임 방지)

        // 4) 게이지
        RefreshGauges();
    }

    bool InputsChanged()
    {
        // 연료 입력
        var f = fuelIn != null ? fuelIn.Item : null;
        int fc = f != null ? f.Count : 0;
        int fd = f != null ? f.Durability : 0;

        if (f != _prevFuelIn || fc != _prevFuelInCount || fd != _prevFuelInDur)
            return true;

        // 재료 입력
        var g = ingIn != null ? ingIn.Item : null;
        int gc = g != null ? g.Count : 0;
        int gd = g != null ? g.Durability : 0;

        if (g != _prevIngIn || gc != _prevIngInCount || gd != _prevIngInDur)
            return true;

        return false;
    }

    bool OutputsChanged()
    {
        // 연료 출력
        var f = fuelOut != null ? fuelOut.Item : null;
        int fc = f != null ? f.Count : 0;
        int fd = f != null ? f.Durability : 0;

        if (f != _prevFuelOut || fc != _prevFuelOutCount || fd != _prevFuelOutDur)
            return true;

        // 재료 출력
        var g = ingOut != null ? ingOut.Item : null;
        int gc = g != null ? g.Count : 0;
        int gd = g != null ? g.Durability : 0;

        if (g != _prevIngOut || gc != _prevIngOutCount || gd != _prevIngOutDur)
            return true;

        return false;
    }

    void SnapshotInputs()
    {
        var f = fuelIn != null ? fuelIn.Item : null;
        _prevFuelIn = f;
        _prevFuelInCount = f != null ? f.Count : 0;
        _prevFuelInDur = f != null ? f.Durability : 0;

        var g = ingIn != null ? ingIn.Item : null;
        _prevIngIn = g;
        _prevIngInCount = g != null ? g.Count : 0;
        _prevIngInDur = g != null ? g.Durability : 0;
    }

    void SnapshotOutputs()
    {
        var f = fuelOut != null ? fuelOut.Item : null;
        _prevFuelOut = f;
        _prevFuelOutCount = f != null ? f.Count : 0;
        _prevFuelOutDur = f != null ? f.Durability : 0;

        var g = ingOut != null ? ingOut.Item : null;
        _prevIngOut = g;
        _prevIngOutCount = g != null ? g.Count : 0;
        _prevIngOutDur = g != null ? g.Durability : 0;
    }

    void SnapshotAll()
    {
        SnapshotInputs();
        SnapshotOutputs();
    }

    void PushInputsToCampfire()
    {
        if (_campfire == null) return;

        if (fuelIn != null)
            _campfire.SetSlot(Campfire.SlotKind.FuelIn, fuelIn.Item);

        if (ingIn != null)
            _campfire.SetSlot(Campfire.SlotKind.IngredientIn, ingIn.Item);
    }

    void PushOutputsToCampfire()
    {
        // 출력은 Campfire가 생성/관리하지만,
        // 유저가 "꺼내서 UI 슬롯이 비워진" 결과는 Campfire에도 반영되어야 함.
        if (_campfire == null) return;

        if (fuelOut != null)
            _campfire.SetSlot(Campfire.SlotKind.FuelOut, fuelOut.Item);

        if (ingOut != null)
            _campfire.SetSlot(Campfire.SlotKind.IngredientOut, ingOut.Item);
    }

    void PullFromCampfire()
    {
        if (_campfire == null) return;

        if (fuelIn != null)
            fuelIn.Set(_campfire.GetSlot(Campfire.SlotKind.FuelIn));

        if (fuelOut != null)
            fuelOut.Set(_campfire.GetSlot(Campfire.SlotKind.FuelOut));

        if (ingIn != null)
            ingIn.Set(_campfire.GetSlot(Campfire.SlotKind.IngredientIn));

        if (ingOut != null)
            ingOut.Set(_campfire.GetSlot(Campfire.SlotKind.IngredientOut));
    }

    void RefreshGauges()
    {
        if (_campfire == null) return;

        if (fireGauge != null)
            fireGauge.fillAmount = _campfire.FuelProgress01;

        if (cookGauge != null)
            cookGauge.fillAmount = _campfire.CookProgress01;
    }
}
