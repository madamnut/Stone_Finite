


using UnityEngine;
using UnityEngine.UI;

using Game.World;
using Game.Core;

namespace Game.UI
{
    public partial class CampfireModule : MonoBehaviour
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
    
        
        ItemData _prevFuelIn;
        int _prevFuelInCount;
        int _prevFuelInDur;
    
        ItemData _prevIngIn;
        int _prevIngInCount;
        int _prevIngInDur;
    
        
        ItemData _prevFuelOut;
        int _prevFuelOutCount;
        int _prevFuelOutDur;
    
        ItemData _prevIngOut;
        int _prevIngOutCount;
        int _prevIngOutDur;
    
        
        public void Bind(Campfire campfire)
        {
            _campfire = campfire;
    
            
            SetupSlot(fuelIn,  denyPut: false, denyInteraction: false);
            SetupSlot(fuelOut, denyPut: true,  denyInteraction: false); 
            SetupSlot(ingIn,   denyPut: false, denyInteraction: false);
            SetupSlot(ingOut,  denyPut: true,  denyInteraction: false); 
    
            
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
    
            
            if (OutputsChanged())
            {
                PushOutputsToCampfire();
                SnapshotOutputs();
            }
    
            
            if (InputsChanged())
            {
                PushInputsToCampfire();
                SnapshotInputs();
            }
    
            
            PullFromCampfire();
            SnapshotAll(); 
    
            
            RefreshGauges();
        }
    
        #if false
        bool InputsChanged()
        {
            
            var f = fuelIn != null ? fuelIn.Item : null;
            int fc = f != null ? f.Count : 0;
            int fd = f != null ? f.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, f))
                return true;
    
            
            var g = ingIn != null ? ingIn.Item : null;
            int gc = g != null ? g.Count : 0;
            int gd = g != null ? g.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevIngIn, _prevIngInCount, _prevIngInDur, g))
                return true;
    
            return false;
        }
    
        bool OutputsChanged()
        {
            
            var f = fuelOut != null ? fuelOut.Item : null;
            int fc = f != null ? f.Count : 0;
            int fd = f != null ? f.Durability : 0;
    
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, f))
                return true;
    
            
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
        #endif
    }
}
