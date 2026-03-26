using UnityEngine;

using Game.Core;
using Game.World;

namespace Game.UI
{
    public partial class BrickFurnaceModule
    {
        void PullFromFurnace()
        {
            if (_furnace == null) return;

            if (fuelIn != null) fuelIn.Set(_furnace.GetSlot(BrickFurnace.SlotKind.FuelIn));
            if (fuelOut != null) fuelOut.Set(_furnace.GetSlot(BrickFurnace.SlotKind.FuelOut));
            if (crucible != null) crucible.Set(_furnace.GetSlot(BrickFurnace.SlotKind.Crucible));

            SetInputSlotUI(0, _furnace.GetSlot(BrickFurnace.SlotKind.In0));
            SetInputSlotUI(1, _furnace.GetSlot(BrickFurnace.SlotKind.In1));
            SetInputSlotUI(2, _furnace.GetSlot(BrickFurnace.SlotKind.In2));
            SetInputSlotUI(3, _furnace.GetSlot(BrickFurnace.SlotKind.In3));
            SetInputSlotUI(4, _furnace.GetSlot(BrickFurnace.SlotKind.In4));
            SetInputSlotUI(5, _furnace.GetSlot(BrickFurnace.SlotKind.In5));
            SetInputSlotUI(6, _furnace.GetSlot(BrickFurnace.SlotKind.In6));
            SetInputSlotUI(7, _furnace.GetSlot(BrickFurnace.SlotKind.In7));
            SetInputSlotUI(8, _furnace.GetSlot(BrickFurnace.SlotKind.In8));
        }

        void SetInputSlotUI(int i, ItemData item)
        {
            var slot = GetInputSlot(i);
            if (slot == null) return;
            slot.Set(item);
        }

        void PushInputsToFurnace()
        {
            if (_furnace == null) return;

            if (fuelIn != null)
            {
                var cur = fuelIn.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, cur))
                    _furnace.SetSlot(BrickFurnace.SlotKind.FuelIn, cur);
            }

            if (crucible != null)
            {
                var cur = crucible.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevCrucible, _prevCrucibleCount, _prevCrucibleDur, cur))
                    _furnace.SetSlot(BrickFurnace.SlotKind.Crucible, cur);
            }

            for (int i = 0; i < 9; i++)
            {
                var s = GetInputSlot(i);
                var cur = (s != null) ? s.Item : null;

                if (!ModuleSlotSyncUtility.HasChanged(_prevIns[i], _prevInsCount[i], _prevInsDur[i], cur))
                    continue;

                switch (i)
                {
                    case 0: _furnace.SetSlot(BrickFurnace.SlotKind.In0, cur); break;
                    case 1: _furnace.SetSlot(BrickFurnace.SlotKind.In1, cur); break;
                    case 2: _furnace.SetSlot(BrickFurnace.SlotKind.In2, cur); break;
                    case 3: _furnace.SetSlot(BrickFurnace.SlotKind.In3, cur); break;
                    case 4: _furnace.SetSlot(BrickFurnace.SlotKind.In4, cur); break;
                    case 5: _furnace.SetSlot(BrickFurnace.SlotKind.In5, cur); break;
                    case 6: _furnace.SetSlot(BrickFurnace.SlotKind.In6, cur); break;
                    case 7: _furnace.SetSlot(BrickFurnace.SlotKind.In7, cur); break;
                    case 8: _furnace.SetSlot(BrickFurnace.SlotKind.In8, cur); break;
                }
            }
        }

        void PushOutputsToFurnace()
        {
            if (_furnace == null) return;

            if (fuelOut != null)
            {
                var cur = fuelOut.Item;
                if (ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, cur))
                    _furnace.SetSlot(BrickFurnace.SlotKind.FuelOut, cur);
            }
        }

        ItemSlot GetInputSlot(int i)
        {
            switch (i)
            {
                case 0: return in0;
                case 1: return in1;
                case 2: return in2;
                case 3: return in3;
                case 4: return in4;
                case 5: return in5;
                case 6: return in6;
                case 7: return in7;
                case 8: return in8;
            }
            return null;
        }

        void RefreshGaugesAndProgress()
        {
            if (_furnace == null) return;

            if (fireGauge != null)
                fireGauge.fillAmount = Mathf.Clamp01(_furnace.FuelProgress01);

            for (int i = 0; i < 9; i++)
            {
                var slot = GetInputSlot(i);
                if (slot == null) continue;

                float p = Mathf.Clamp01(_furnace.GetInputProgress01(i));
                slot.SetProgress(p, p > 0f);
            }
        }

        void RefreshCrucibleView()
        {
            if (crucibleView == null) return;

            ItemData c = (crucible != null) ? crucible.Item : null;

            if (!ReferenceEquals(_boundCrucibleForView, c))
            {
                _boundCrucibleForView = c;
                crucibleView.BindCrucible(c);
                return;
            }

            crucibleView.Refresh();
        }

        void CaptureSnapshots()
        {
            CaptureInputSnapshots();
            CaptureOutputSnapshots();
        }

        void CaptureInputSnapshots()
        {
            ModuleSlotSyncUtility.Capture(fuelIn, ref _prevFuelIn, ref _prevFuelInCount, ref _prevFuelInDur);
            ModuleSlotSyncUtility.Capture(crucible, ref _prevCrucible, ref _prevCrucibleCount, ref _prevCrucibleDur);

            for (int i = 0; i < 9; i++)
            {
                var s = GetInputSlot(i);
                var it = (s != null) ? s.Item : null;

                _prevIns[i] = it;
                _prevInsCount[i] = (it != null) ? it.Count : 0;
                _prevInsDur[i] = (it != null) ? it.Durability : 0;
            }
        }

        void CaptureOutputSnapshots()
        {
            ModuleSlotSyncUtility.Capture(fuelOut, ref _prevFuelOut, ref _prevFuelOutCount, ref _prevFuelOutDur);
        }

        bool InputsChanged()
        {
            var curFuelIn = (fuelIn != null) ? fuelIn.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevFuelIn, _prevFuelInCount, _prevFuelInDur, curFuelIn)) return true;

            var curCrucible = (crucible != null) ? crucible.Item : null;
            if (ModuleSlotSyncUtility.HasChanged(_prevCrucible, _prevCrucibleCount, _prevCrucibleDur, curCrucible)) return true;

            for (int i = 0; i < 9; i++)
            {
                var s = GetInputSlot(i);
                var cur = (s != null) ? s.Item : null;

                if (ModuleSlotSyncUtility.HasChanged(_prevIns[i], _prevInsCount[i], _prevInsDur[i], cur))
                    return true;
            }

            return false;
        }

        bool OutputChanged()
        {
            var curFuelOut = (fuelOut != null) ? fuelOut.Item : null;
            return ModuleSlotSyncUtility.HasChanged(_prevFuelOut, _prevFuelOutCount, _prevFuelOutDur, curFuelOut);
        }
    }
}
