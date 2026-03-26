


using Game.World;
using Game.Core;

namespace Game.UI
{
    public partial class ClayKilnModule
    {
        
        bool InputsChanged()
        {

            var f = fuelIn != null ? fuelIn.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, f))
                return true;

            var a = fireInA != null ? fireInA.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevFireInA, _prevFireInACount, _prevFireInADur, a))
                return true;

            var b = fireInB != null ? fireInB.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevFireInB, _prevFireInBCount, _prevFireInBDur, b))
                return true;

            return false;
        }

        
        bool OutputsChanged()
        {
            var f = fuelOut != null ? fuelOut.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, f))
                return true;

            var a = fireOutA != null ? fireOutA.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevFireOutA, _prevFireOutACount, _prevFireOutADur, a))
                return true;

            var b = fireOutB != null ? fireOutB.Item : null;
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

            if (fuelIn != null) fuelIn.Set(_kiln.GetSlot(ClayKiln.SlotKind.FuelIn));
            if (fuelOut != null) fuelOut.Set(_kiln.GetSlot(ClayKiln.SlotKind.FuelOut));
            if (fireInA != null) fireInA.Set(_kiln.GetSlot(ClayKiln.SlotKind.FireInA));
            if (fireOutA != null) fireOutA.Set(_kiln.GetSlot(ClayKiln.SlotKind.FireOutA));
            if (fireInB != null) fireInB.Set(_kiln.GetSlot(ClayKiln.SlotKind.FireInB));
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
