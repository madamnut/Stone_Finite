using UnityEngine;

namespace Game.Player
{
    
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour
    {
        [Header("Player")]
        public Player player;   // ???몄뒪?숉꽣??Player ?쒕옒洹?
    
        [Header("Inventory Rows (0 ~ 4)")]
        public Transform row0, row1, row2, row3, row4;
    
        private readonly ItemSlot[] _slots = new ItemSlot[50];
        private InventoryData _inv;
    
        void Awake()
        {
            int i = 0;
            // ?곗꽑?쒖쐞: ROW4 ??0 ??1 ??2 ??3  (Row4 = ?몃뜳??0~9)
            MapRow(row4, ref i);
            MapRow(row0, ref i);
            MapRow(row1, ref i);
            MapRow(row2, ref i);
            MapRow(row3, ref i);
        }
    
        void OnEnable()
        {
            _inv = (player != null) ? player.Inventory : null;
            // ?щ’ 硫뷀??곗씠??諛붿씤???몃뜳?ㅻ뒗 Awake?먯꽌, ?몃깽 李몄“???ш린??
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
                s.index = idx;              // ?몃뜳??怨좎젙
                _slots[idx++] = s;
            }
        }
    
        public ItemSlot GetSlot(int index) => _slots[index];
    }
}
