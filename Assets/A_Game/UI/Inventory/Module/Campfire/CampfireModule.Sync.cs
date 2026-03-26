


using Game.World;
using Game.Core;

namespace Game.UI
{
    public partial class CampfireModule
    {
        
        bool InputsChanged()
        {

            var f = fuelIn != null ? fuelIn.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, f))
                return true;

            var g = ingIn != null ? ingIn.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevIngIn, _prevIngInCount, _prevIngInDur, g))
                return true;

            return false;
        }

        
        bool OutputsChanged()
        {
            var f = fuelOut != null ? fuelOut.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, f))
                return true;

            var g = ingOut != null ? ingOut.Item : null;
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
    }
}
