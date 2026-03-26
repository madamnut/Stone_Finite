


using System.Collections.Generic;

namespace Game.Core
{
    
    
    
    
    
    
    public class InventoryData
    {
        public readonly List<ItemData> items = new();

        public int Capacity { get; }
    
        
        public delegate void Changed();
        public event Changed OnChanged;
    
        
        public InventoryData(int capacity)
        {
            Capacity = capacity;
            for (int i = 0; i < capacity; i++)
                items.Add(null);                        
        }
    
        
        
        public void NotifyChanged() => OnChanged?.Invoke();
    
        
        
        
        
        public int AddItem(ItemData incoming)
        {
            if (incoming == null || incoming.Count <= 0)
                return 0;
    
            int left = incoming.Count;
    
            
            foreach (var it in items)
            {
                if (it == null) continue;
                if (it.ItemId != incoming.ItemId) continue;
                if (it.Count >= it.MaxStack) continue;
    
                int room = it.MaxStack - it.Count;
                int take = left > room ? room : left;
                it.Count += take;
                left -= take;
                if (left == 0) { OnChanged?.Invoke(); return 0; }
            }
    
            
            for (int i = 0; i < Capacity && left > 0; i++)
            {
                if (items[i] != null) continue;
    
                int take = left > incoming.MaxStack ? incoming.MaxStack : left;
                items[i] = new ItemData(
                    itemId:        incoming.ItemId,
                    name:          incoming.Name,
                    spriteName:    incoming.SpriteName,
                    itemType:      incoming.ItemType,
                    maxStack:      incoming.MaxStack,
                    maxDurability: incoming.MaxDurability,
                    durability:    incoming.Durability,
                    toolActions:   incoming.ToolActions,
                    weaponActions: incoming.WeaponActions,
                    breakActions:  incoming.BreakActions,
                    tags:          incoming.Tags,
                    details:       incoming.Details,
                    icon:          incoming.Icon,
                    count:         take
                );
    
                left -= take;
            }
    
            OnChanged?.Invoke();
            return left;           
        }
    }
}
