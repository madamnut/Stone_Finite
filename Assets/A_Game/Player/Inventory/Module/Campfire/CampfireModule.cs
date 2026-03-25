// CampfireModule.cs
using UnityEngine;
using UnityEngine.UI;

using Game.World;
using Game.Player;
using Game.Core;

namespace Game.UI
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
    
        // ???怨좊룴??????곸죷 ?????怨뚮뼚?????좊즴????
        ItemData _prevFuelIn;
        int _prevFuelInCount;
        int _prevFuelInDur;
    
        ItemData _prevIngIn;
        int _prevIngInCount;
        int _prevIngInDur;
    
        // ???怨좊룴????⑥レ툓???????怨뚮뼚?????좊즴???? ?????좊읈? ??⑥ル땻濚???)
        ItemData _prevFuelOut;
        int _prevFuelOutCount;
        int _prevFuelOutDur;
    
        ItemData _prevIngOut;
        int _prevIngOutCount;
        int _prevIngOutDur;
    
        public void Bind(Campfire campfire)
        {
            _campfire = campfire;
    
            // ?棺??짆?쏆춾?????癲ル슢?꾤땟???
            SetupSlot(fuelIn,  denyPut: false, denyInteraction: false);
            SetupSlot(fuelOut, denyPut: true,  denyInteraction: false); // ??⑥レ툓?? ?壤굿??몃탿癲???ヂ???, ????곷뎨?????源낅츛
            SetupSlot(ingIn,   denyPut: false, denyInteraction: false);
            SetupSlot(ingOut,  denyPut: true,  denyInteraction: false); // ??⑥レ툓?? ?壤굿??몃탿癲???ヂ???, ????곷뎨?????源낅츛
    
            // 癲ル슔?됭짆??UI ?袁⑸즵???
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
    
            // 1) ?????좊읈? ??⑥レ툓????????????⑥ル땻濚??? ?沃섅굥?? ??좊즴?????⑤똾留?Campfire???袁⑸즵???
            if (OutputsChanged())
            {
                PushOutputsToCampfire();
                SnapshotOutputs();
            }
    
            // 2) ????곸죷 ?怨뚮뼚??濡ろ뜑??????源낃도 ?????Campfire???袁⑸즵???
            if (InputsChanged())
            {
                PushInputsToCampfire();
                SnapshotInputs();
            }
    
            // 3) ??筌?六?????뗫탿??
            PullFromCampfire();
            SnapshotAll(); // Pull ??熬곣뫖?????怨좊룴??猷?獄????怨뺣빰 癲ル슢???????????ㅼ뒧??嚥싲갭흮獒뺣끇?????袁⑸젻泳?)
    
            // 4) ?濡ろ뜐???ル쵐??
            RefreshGauges();
        }
    
        bool InputsChanged()
        {
            // ???ㅻ깹??????곸죷
            var f = fuelIn != null ? fuelIn.Item : null;
            int fc = f != null ? f.Count : 0;
            int fd = f != null ? f.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, f))
                return true;
    
            // ??嶺?????곸죷
            var g = ingIn != null ? ingIn.Item : null;
            int gc = g != null ? g.Count : 0;
            int gd = g != null ? g.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevIngIn, _prevIngInCount, _prevIngInDur, g))
                return true;
    
            return false;
        }
    
        bool OutputsChanged()
        {
            // ???ㅻ깹????⑥レ툓??
            var f = fuelOut != null ? fuelOut.Item : null;
            int fc = f != null ? f.Count : 0;
            int fd = f != null ? f.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, f))
                return true;
    
            // ??嶺???⑥レ툓??
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
            // ??⑥レ툓??? Campfire??좊읈? ??獄쏅똻?????굿?域밸Ŧ肉ョ뵳?異?堉온癲?
            // ?????좊읈? "??⑥ル땻雅??UI ????????????? ?濡ろ뜏????Campfire???筌??袁⑸즵????筌뚯슦苑????
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
