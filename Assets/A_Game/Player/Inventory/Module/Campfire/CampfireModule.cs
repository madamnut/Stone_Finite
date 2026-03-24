// CampfireModule.cs
using UnityEngine;
using UnityEngine.UI;

using Game.World;

namespace Game.Player
{
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
    
        // ?ㅻ깄???낅젰 ?щ’ 蹂寃?媛먯???
        ItemData _prevFuelIn;
        int _prevFuelInCount;
        int _prevFuelInDur;
    
        ItemData _prevIngIn;
        int _prevIngInCount;
        int _prevIngInDur;
    
        // ?ㅻ깄??異쒕젰 ?щ’ 蹂寃?媛먯??? ?좎?媛 爰쇰깉?붿?)
        ItemData _prevFuelOut;
        int _prevFuelOutCount;
        int _prevFuelOutDur;
    
        ItemData _prevIngOut;
        int _prevIngOutCount;
        int _prevIngOutDur;
    
        public void Bind(Campfire campfire)
        {
            _campfire = campfire;
    
            // 濡쒖뺄 ?щ’ 紐⑤뱶
            SetupSlot(fuelIn,  denyPut: false, denyInteraction: false);
            SetupSlot(fuelOut, denyPut: true,  denyInteraction: false); // 異쒕젰: ?ｊ린留?湲덉?, 鍮쇨린???덉슜
            SetupSlot(ingIn,   denyPut: false, denyInteraction: false);
            SetupSlot(ingOut,  denyPut: true,  denyInteraction: false); // 異쒕젰: ?ｊ린留?湲덉?, 鍮쇨린???덉슜
    
            // 理쒖큹 UI 諛섏쁺
            PullFromCampfire();
            SnapshotAll();
            RefreshGauges();
        }
    
        void SetupSlot(ItemSlot slot, bool denyPut, bool denyInteraction)
        {
            ModuleSlotSyncUtility.ConfigureLocalSlot(slot, denyPut, denyInteraction);
        }
    
        void Update()
        {
            if (_campfire == null) return;
    
            // 1) ?좎?媛 異쒕젰 ?щ’?먯꽌 爰쇰깉?붿? 癒쇱? 媛먯??댁꽌 Campfire??諛섏쁺
            if (OutputsChanged())
            {
                PushOutputsToCampfire();
                SnapshotOutputs();
            }
    
            // 2) ?낅젰 蹂寃쎌씠 ?덉쓣 ?뚮쭔 Campfire??諛섏쁺
            if (InputsChanged())
            {
                PushInputsToCampfire();
                SnapshotInputs();
            }
    
            // 3) ?쒖떆 ?숆린??
            PullFromCampfire();
            SnapshotAll(); // Pull ?댄썑 ?ㅻ깄?룹쓣 ?ㅼ떆 留욎떠????뼱?곌린/源쒕묀??諛⑹?)
    
            // 4) 寃뚯씠吏
            RefreshGauges();
        }
    
        bool InputsChanged()
        {
            // ?곕즺 ?낅젰
            var f = fuelIn != null ? fuelIn.Item : null;
            int fc = f != null ? f.Count : 0;
            int fd = f != null ? f.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, f))
                return true;
    
            // ?щ즺 ?낅젰
            var g = ingIn != null ? ingIn.Item : null;
            int gc = g != null ? g.Count : 0;
            int gd = g != null ? g.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevIngIn, _prevIngInCount, _prevIngInDur, g))
                return true;
    
            return false;
        }
    
        bool OutputsChanged()
        {
            // ?곕즺 異쒕젰
            var f = fuelOut != null ? fuelOut.Item : null;
            int fc = f != null ? f.Count : 0;
            int fd = f != null ? f.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, f))
                return true;
    
            // ?щ즺 異쒕젰
            var g = ingOut != null ? ingOut.Item : null;
            int gc = g != null ? g.Count : 0;
            int gd = g != null ? g.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevIngOut, _prevIngOutCount, _prevIngOutDur, g))
                return true;
    
            return false;
        }
    
        void SnapshotInputs()
        {
            ModuleSlotSyncUtility.Capture(fuelIn, ref _prevFuelIn, ref _prevFuelInCount, ref _prevFuelInDur);
            ModuleSlotSyncUtility.Capture(ingIn, ref _prevIngIn, ref _prevIngInCount, ref _prevIngInDur);
        }
    
        void SnapshotOutputs()
        {
            ModuleSlotSyncUtility.Capture(fuelOut, ref _prevFuelOut, ref _prevFuelOutCount, ref _prevFuelOutDur);
            ModuleSlotSyncUtility.Capture(ingOut, ref _prevIngOut, ref _prevIngOutCount, ref _prevIngOutDur);
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
            // 異쒕젰? Campfire媛 ?앹꽦/愿由ы븯吏留?
            // ?좎?媛 "爰쇰궡??UI ?щ’??鍮꾩썙吏? 寃곌낵??Campfire?먮룄 諛섏쁺?섏뼱????
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
}
