using UnityEngine;
using Game.Player;
using Game.UI;
using Game.Core;

namespace Game.UI
{
    
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour
    {
        [Header("Player")]
        public Game.Player.Player player;   // ????꿔꺂????筌???????Player ??癲ル슢캉??る쨨??
    
        [Header("Inventory Rows (0 ~ 4)")]
        public Transform row0, row1, row2, row3, row4;
    
        private readonly ItemSlot[] _slots = new ItemSlot[50];
        private InventoryData _inv;
    
        void Awake()
        {
            int i = 0;
            // ???????????癲ル슢???쇳맪? ROW4 ??0 ??1 ??2 ??3  (Row4 = ??꿔꺂??????0~9)
            MapRow(row4, ref i);
            MapRow(row0, ref i);
            MapRow(row1, ref i);
            MapRow(row2, ref i);
            MapRow(row3, ref i);
        }
    
        void OnEnable()
        {
            _inv = (player != null) ? player.Inventory : null;
            // ?????轅붽틓???????????????ш끽維뽳쭩??????꿔꺂????????筌뤾쑵??Awake????? ??꿔꺂???沃????轅붽틓?????????????
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i] != null) _slots[i].inventory = _inv;
    
            if (_inv != null) _inv.OnChanged += Refresh;
            Refresh();
        }
    
        void OnDisable()
        {
            if (_inv != null) _inv.OnChanged -= Refresh;
            _inv = null;
        }
    
        void Refresh()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                var it = (_inv != null && i < _inv.items.Count) ? _inv.items[i] : null;
                _slots[i].Set(it);
            }
        }
    
        void MapRow(Transform row, ref int idx)
        {
            for (int c = 0; c < 10; c++)
            {
                var s = row.Find(c.ToString()).GetComponent<ItemSlot>();
                s.index = idx;              // ??꿔꺂??????????숈???
                _slots[idx++] = s;
            }
        }
    
        public ItemSlot GetSlot(int index) => _slots[index];
    }
}
