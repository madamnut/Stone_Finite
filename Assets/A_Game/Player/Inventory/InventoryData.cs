using System.Collections.Generic;

namespace Game.Player
{
    
    /// <summary>
    /// ?쒖닔 ?몃깽?좊━ ?곗씠????μ냼) 怨꾩링.
    /// ??MonoBehaviour ?꾨떂 ???대뼡 怨녹뿉?쒕룄 ?먯쑀濡?쾶 new 濡??앹꽦 媛??
    /// ??OnChanged ?대깽?몃줈 UI ?깆뿉??媛깆떊 Hook 嫄????덉쓬
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
                items.Add(null);                        // 鍮??щ’
        }
    
        /// <summary>?몃??먯꽌 媛뺤젣 媛깆떊 ?뚮┝</summary>
        public void NotifyChanged() => OnChanged?.Invoke();
    
        /// <summary>
        /// ItemData.Count 留뚰겮 ?ｊ퀬, 紐??ｌ? ?섎웾??諛섑솚?쒕떎.
        /// </summary>
        public int AddItem(ItemData incoming)
        {
            if (incoming == null || incoming.Count <= 0)
                return 0;
    
            int left = incoming.Count;
    
            /* 1) 媛숈? ID ?ㅽ깮 梨꾩슦湲?*/
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
    
            /* 2) 鍮??щ’ 梨꾩슦湲?*/
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
            return left;           // 0 ???꾨웾 ?섏슜
        }
    }
}
