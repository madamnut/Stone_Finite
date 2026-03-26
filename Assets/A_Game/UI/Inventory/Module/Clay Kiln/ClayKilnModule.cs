// ClayKilnModule.cs (??ш끽維????????곕츅??
using UnityEngine;
using UnityEngine.UI;

using Game.World;
using Game.Core;

namespace Game.UI
{
    public partial class ClayKilnModule : MonoBehaviour
    {
        [Header("Slots")]
        public ItemSlot fuelIn;
        public ItemSlot fuelOut;
    
        public ItemSlot fireInA;
        public ItemSlot fireOutA;
    
        public ItemSlot fireInB;
        public ItemSlot fireOutB;
    
        [Header("Gauges (Filled Image)")]
        public Image fireGauge;     // ???ㅻ깹???? ?濡ろ뜐???ル쵐??
        public Image progressGaugeA; // A ??繹먮끏????怨룹쐾??癲ル슣???몄춿??
        public Image progressGaugeB; // B ??繹먮끏????怨룹쐾??癲ル슣???몄춿??
    
        ClayKiln _kiln;
    
        // ?????????????????? ???怨좊룴??????곸죷 ?????怨뚮뼚?????좊즴???? ??????????????????
        ItemData _prevFuelIn;
        int _prevFuelInCount;
        int _prevFuelInDur;
    
        ItemData _prevFireInA;
        int _prevFireInACount;
        int _prevFireInADur;
    
        ItemData _prevFireInB;
        int _prevFireInBCount;
        int _prevFireInBDur;
    
        // ?????????????????? ???怨좊룴????⑥レ툓???????怨뚮뼚?????좊즴???? ?????좊읈? ??⑥ル땻濚???) ??????????????????
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
    
            // ?棺??짆?쏆춾?????癲ル슢?꾤땟???
            SetupSlot(fuelIn,   denyPut: false, denyInteraction: false);
            SetupSlot(fuelOut,  denyPut: true,  denyInteraction: false); // ??⑥レ툓?? ?壤굿??몃탿 ??ヂ???, ????곷뎨????源낅츛
    
            SetupSlot(fireInA,  denyPut: false, denyInteraction: false);
            SetupSlot(fireOutA, denyPut: true,  denyInteraction: false); // ??⑥レ툓?? ?壤굿??몃탿 ??ヂ???, ????곷뎨????源낅츛
    
            SetupSlot(fireInB,  denyPut: false, denyInteraction: false);
            SetupSlot(fireOutB, denyPut: true,  denyInteraction: false); // ??⑥レ툓?? ?壤굿??몃탿 ??ヂ???, ????곷뎨????源낅츛
    
            // 癲ル슔?됭짆??UI ?袁⑸즵???
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
    
            // 1) ??⑥レ툓????????怨뚮뼚????源끹걬癲??????좊읈? ??⑥ル땻濚? -> Kiln???袁⑸즵???
            if (OutputsChanged())
            {
                PushOutputsToKiln();
                SnapshotOutputs();
            }
    
            // 2) ????곸죷???怨뚮뼚????源끹걬癲?-> Kiln???袁⑸즵???
            if (InputsChanged())
            {
                PushInputsToKiln();
                SnapshotInputs();
            }
    
            // 3) ??筌?六?????뗫탿??(?????棺??짆?먰맪??????袁⑸즴?????濡ろ뜏????怨멸텭?????袁⑸즵???
            PullFromKiln();
            SnapshotAll(); // Pull ??熬곣뫖?????怨좊룴?????????????ㅼ뒧??嚥싲갭흮獒뺣끇?????袁⑸젻泳?)
    
            // 4) ?濡ろ뜐???ル쵐??
            RefreshGauges();
        }
    
        #if false
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
        #endif
    }
}
