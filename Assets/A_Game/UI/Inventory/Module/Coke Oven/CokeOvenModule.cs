


using UnityEngine;
using UnityEngine.UI;

using Game.World;
using Game.Core;

namespace Game.UI
{
    public partial class CokeOvenModule : MonoBehaviour
    {
        [Header("Slots")]

        public ItemSlot fuelIn;
        public ItemSlot fuelOut;
    
        public ItemSlot materialIn;
        public ItemSlot out0;
        public ItemSlot out1;
    
        [Header("Gauges (Filled Image)")]
        public Image fireGauge;     
        public Image progressGauge; 
    
        CokeOven _oven;
    
        
        ItemData _prevFuelIn;
        int _prevFuelInCount;
        int _prevFuelInDur;
    
        ItemData _prevMatIn;
        int _prevMatInCount;
        int _prevMatInDur;
    
        
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
    
            
            PullFromOven();
    
            CaptureInputSnapshots();
            CaptureOutputSnapshots();
    
            RefreshGaugesAndProgress();
        }
    
        
        void SetupSlot(ItemSlot slot, bool denyPut, bool denyInteraction)
        {
            ModuleSlotSyncUtility.ConfigureLocalSlot(slot, denyPut, denyInteraction, resetProgress: true);
        }
    
        
        void Update()
        {
            if (_oven == null) return;
    
            
            if (OutputsChanged())
            {
                PushOutputsToOven();
                CaptureOutputSnapshots();
            }
    
            
            if (InputsChanged())
            {
                PushInputsToOven();
                CaptureInputSnapshots();
            }
    
            
            PullFromOven();
            RefreshGaugesAndProgress();
        }
    
        
        
        
        #if false
        void PushInputsToOven()
        {
            if (_oven == null) return;
    
            if (fuelIn != null)
            {
                var cur = fuelIn.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, cur))
                    _oven.SetSlot(CokeOven.SlotKind.FuelIn, cur);
            }
    
            if (materialIn != null)
            {
                var cur = materialIn.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevMatIn, _prevMatInCount, _prevMatInDur, cur))
                    _oven.SetSlot(CokeOven.SlotKind.MaterialIn, cur);
            }
        }
    
        void PushOutputsToOven()
        {
            if (_oven == null) return;
    
            if (fuelOut != null)
            {
                var cur = fuelOut.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, cur))
                    _oven.SetSlot(CokeOven.SlotKind.FuelOut, cur);
            }
    
            if (out0 != null)
            {
                var cur = out0.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevOut0, _prevOut0Count, _prevOut0Dur, cur))
                    _oven.SetSlot(CokeOven.SlotKind.MaterialOut0, cur);
            }
    
            if (out1 != null)
            {
                var cur = out1.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevOut1, _prevOut1Count, _prevOut1Dur, cur))
                    _oven.SetSlot(CokeOven.SlotKind.MaterialOut1, cur);
            }
        }
    
        
        
        
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
            ModuleSlotSyncUtility.SetSlotIfDifferent(ui, data);
        }
    
        
        
        
        void RefreshGaugesAndProgress()
        {
            if (_oven == null) return;
    
            if (fireGauge != null)
                fireGauge.fillAmount = Mathf.Clamp01(_oven.FuelProgress01);
    
            float cokeP = Mathf.Clamp01(_oven.CokeProgress01);
    
            
            if (progressGauge != null)
                progressGauge.fillAmount = cokeP;
        }
    
        
        
        
        bool InputsChanged()
        {
            bool changed = false;
    
            if (fuelIn != null)
            {
                var cur = fuelIn.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, cur)) changed = true;
            }
    
            if (materialIn != null)
            {
                var cur = materialIn.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevMatIn, _prevMatInCount, _prevMatInDur, cur)) changed = true;
            }
    
            return changed;
        }
    
        bool OutputsChanged()
        {
            bool changed = false;
    
            if (fuelOut != null)
            {
                var cur = fuelOut.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, cur)) changed = true;
            }
    
            if (out0 != null)
            {
                var cur = out0.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevOut0, _prevOut0Count, _prevOut0Dur, cur)) changed = true;
            }
    
            if (out1 != null)
            {
                var cur = out1.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevOut1, _prevOut1Count, _prevOut1Dur, cur)) changed = true;
            }
    
            return changed;
        }
    
        void CaptureInputSnapshots()
        {
            ModuleSlotSyncUtility.Capture(fuelIn, ref _prevFuelIn, ref _prevFuelInCount, ref _prevFuelInDur);
            ModuleSlotSyncUtility.Capture(materialIn, ref _prevMatIn, ref _prevMatInCount, ref _prevMatInDur);
        }
    
        void CaptureOutputSnapshots()
        {
            ModuleSlotSyncUtility.Capture(fuelOut, ref _prevFuelOut, ref _prevFuelOutCount, ref _prevFuelOutDur);
            ModuleSlotSyncUtility.Capture(out0, ref _prevOut0, ref _prevOut0Count, ref _prevOut0Dur);
            ModuleSlotSyncUtility.Capture(out1, ref _prevOut1, ref _prevOut1Count, ref _prevOut1Dur);
        }
        #endif
    }
}
