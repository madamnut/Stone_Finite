// ClayKilnModule.cs (?꾩껜 援먯껜蹂?
using UnityEngine;
using UnityEngine.UI;

using Game.World;

namespace Game.Player
{
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
        public Image fireGauge;     // ?곕즺(遺? 寃뚯씠吏
        public Image progressGaugeA; // A ?쇱씤 援쎄린 吏꾪뻾??
        public Image progressGaugeB; // B ?쇱씤 援쎄린 吏꾪뻾??
    
        ClayKiln _kiln;
    
        // ????????? ?ㅻ깄???낅젰 ?щ’ 蹂寃?媛먯??? ?????????
        ItemData _prevFuelIn;
        int _prevFuelInCount;
        int _prevFuelInDur;
    
        ItemData _prevFireInA;
        int _prevFireInACount;
        int _prevFireInADur;
    
        ItemData _prevFireInB;
        int _prevFireInBCount;
        int _prevFireInBDur;
    
        // ????????? ?ㅻ깄??異쒕젰 ?щ’ 蹂寃?媛먯??? ?좎?媛 爰쇰깉?붿?) ?????????
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
    
            // 濡쒖뺄 ?щ’ 紐⑤뱶
            SetupSlot(fuelIn,   denyPut: false, denyInteraction: false);
            SetupSlot(fuelOut,  denyPut: true,  denyInteraction: false); // 異쒕젰: ?ｊ린 湲덉?, 鍮쇨린 ?덉슜
    
            SetupSlot(fireInA,  denyPut: false, denyInteraction: false);
            SetupSlot(fireOutA, denyPut: true,  denyInteraction: false); // 異쒕젰: ?ｊ린 湲덉?, 鍮쇨린 ?덉슜
    
            SetupSlot(fireInB,  denyPut: false, denyInteraction: false);
            SetupSlot(fireOutB, denyPut: true,  denyInteraction: false); // 異쒕젰: ?ｊ린 湲덉?, 鍮쇨린 ?덉슜
    
            // 理쒖큹 UI 諛섏쁺
            PullFromKiln();
            SnapshotAll();
            RefreshGauges();
        }
    
        void SetupSlot(ItemSlot slot, bool denyPut, bool denyInteraction)
        {
            ModuleSlotSyncUtility.ConfigureLocalSlot(slot, denyPut, denyInteraction);
        }
    
        void Update()
        {
            if (_kiln == null) return;
    
            // 1) 異쒕젰 ?щ’??蹂?덉쑝硫??좎?媛 爰쇰깂) -> Kiln??諛섏쁺
            if (OutputsChanged())
            {
                PushOutputsToKiln();
                SnapshotOutputs();
            }
    
            // 2) ?낅젰??蹂?덉쑝硫?-> Kiln??諛섏쁺
            if (InputsChanged())
            {
                PushInputsToKiln();
                SnapshotInputs();
            }
    
            // 3) ?쒖떆 ?숆린??(?щⅨ 濡쒖쭅?먯꽌 諛붾?寃곌낵/移댁슫??諛섏쁺)
            PullFromKiln();
            SnapshotAll(); // Pull ?댄썑 ?ㅻ깄???ъ젙????뼱?곌린/源쒕묀??諛⑹?)
    
            // 4) 寃뚯씠吏
            RefreshGauges();
        }
    
        bool InputsChanged()
        {
            // FuelIn
            var f = fuelIn != null ? fuelIn.Item : null;
            int fc = f != null ? f.Count : 0;
            int fd = f != null ? f.Durability : 0;
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, f))
                return true;
    
            // FireInA
            var a = fireInA != null ? fireInA.Item : null;
            int ac = a != null ? a.Count : 0;
            int ad = a != null ? a.Durability : 0;
            if (ModuleSlotSyncUtility.HasChanged(_prevFireInA, _prevFireInACount, _prevFireInADur, a))
                return true;
    
            // FireInB
            var b = fireInB != null ? fireInB.Item : null;
            int bc = b != null ? b.Count : 0;
            int bd = b != null ? b.Durability : 0;
            if (ModuleSlotSyncUtility.HasChanged(_prevFireInB, _prevFireInBCount, _prevFireInBDur, b))
                return true;
    
            return false;
        }
    
        bool OutputsChanged()
        {
            // FuelOut
            var f = fuelOut != null ? fuelOut.Item : null;
            int fc = f != null ? f.Count : 0;
            int fd = f != null ? f.Durability : 0;
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, f))
                return true;
    
            // FireOutA
            var a = fireOutA != null ? fireOutA.Item : null;
            int ac = a != null ? a.Count : 0;
            int ad = a != null ? a.Durability : 0;
            if (ModuleSlotSyncUtility.HasChanged(_prevFireOutA, _prevFireOutACount, _prevFireOutADur, a))
                return true;
    
            // FireOutB
            var b = fireOutB != null ? fireOutB.Item : null;
            int bc = b != null ? b.Count : 0;
            int bd = b != null ? b.Durability : 0;
            if (ModuleSlotSyncUtility.HasChanged(_prevFireOutB, _prevFireOutBCount, _prevFireOutBDur, b))
                return true;
    
            return false;
        }
    
        void SnapshotInputs()
        {
            ModuleSlotSyncUtility.Capture(fuelIn, ref _prevFuelIn, ref _prevFuelInCount, ref _prevFuelInDur);
            ModuleSlotSyncUtility.Capture(fireInA, ref _prevFireInA, ref _prevFireInACount, ref _prevFireInADur);
            ModuleSlotSyncUtility.Capture(fireInB, ref _prevFireInB, ref _prevFireInBCount, ref _prevFireInBDur);
        }
    
        void SnapshotOutputs()
        {
            ModuleSlotSyncUtility.Capture(fuelOut, ref _prevFuelOut, ref _prevFuelOutCount, ref _prevFuelOutDur);
            ModuleSlotSyncUtility.Capture(fireOutA, ref _prevFireOutA, ref _prevFireOutACount, ref _prevFireOutADur);
            ModuleSlotSyncUtility.Capture(fireOutB, ref _prevFireOutB, ref _prevFireOutBCount, ref _prevFireOutBDur);
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
}
