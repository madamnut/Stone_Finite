using UnityEngine;

namespace Game.Player
{
    
    public static class ModuleSlotSyncUtility
    {
        public static void ConfigureLocalSlot(ItemSlot slot, bool denyPut, bool denyInteraction, bool resetProgress = false)
        {
            if (slot == null) return;
    
            slot.useLocalStorage = true;
            slot.denyUserPut = denyPut;
            slot.denyUserInteraction = denyInteraction;
    
            if (slot.Item == null) slot.Set(null);
            else slot.Refresh();
    
            if (resetProgress)
                slot.SetProgress(0f, false);
        }
    
        public static void Capture(ItemSlot slot, ref ItemData prevRef, ref int prevCount, ref int prevDur)
        {
            var cur = slot != null ? slot.Item : null;
            prevRef = cur;
            prevCount = cur != null ? cur.Count : 0;
            prevDur = cur != null ? cur.Durability : 0;
        }
    
        public static bool HasChanged(ItemData prevRef, int prevCount, int prevDur, ItemData cur)
        {
            if (!ReferenceEquals(prevRef, cur)) return true;
    
            int curCount = cur != null ? cur.Count : 0;
            int curDur = cur != null ? cur.Durability : 0;
    
            return prevCount != curCount || prevDur != curDur;
        }
    
        public static void SetSlotIfDifferent(ItemSlot slot, ItemData data, bool refreshWhenSame = true)
        {
            if (slot == null) return;
    
            if (!ReferenceEquals(slot.Item, data))
                slot.Set(data);
            else if (refreshWhenSame)
                slot.Refresh();
        }
    }
}
