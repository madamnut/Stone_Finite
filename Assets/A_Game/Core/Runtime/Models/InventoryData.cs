using System.Collections.Generic;

namespace Game.Core
{
    
    /// <summary>
    /// ??뽯땾 ?紐껉뭣?醫듼봺 ?怨쀬뵠?????關?? ?④쑴留?
    /// ??MonoBehaviour ?袁⑤뻷 ????堉??⑤끃肉??뺣즲 ?癒??嚥?苡?new 嚥???밴쉐 揶쎛??
    /// ??OnChanged ??源?紐껋쨮 UI ?源녿퓠??揶쏄퉮??Hook 椰?????됱벉
    /// </summary>
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
                items.Add(null);                        // ??????
        }
    
        /// <summary>?紐??癒?퐣 揶쏅벡??揶쏄퉮?????뵝</summary>
        public void NotifyChanged() => OnChanged?.Invoke();
    
        /// <summary>
        /// ItemData.Count 筌띾슦寃??節딇? 筌??節? ??롮쎗??獄쏆꼹???뺣뼄.
        /// </summary>
        public int AddItem(ItemData incoming)
        {
            if (incoming == null || incoming.Count <= 0)
                return 0;
    
            int left = incoming.Count;
    
            /* 1) 揶쏆늿? ID ??쎄문 筌?쑴??묾?*/
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
    
            /* 2) ??????筌?쑴??묾?*/
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
            return left;           // 0 ???袁⑥쎗 ??륁뒠
        }
    }
}
