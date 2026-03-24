using UnityEngine;

namespace Game.Player
{
    
    public class Hotbar : MonoBehaviour
    {
        public Player player;
        public Transform hotbarRoot; // ?먯떇 "0"~"9"
    
        private ItemSlot[] _slots = new ItemSlot[10];
        private InventoryData _inv;
        private int _scope = 0;
    
        void Awake()
        {
            for (int i = 0; i < 10; i++)
                _slots[i] = hotbarRoot.Find(i.ToString()).GetComponent<ItemSlot>();
        }
    
        void Start()
        {
            _inv = player != null ? player.Inventory : null;
            if (_inv != null) _inv.OnChanged += Refresh;
            Refresh();
            SetScope(0); // 珥덇린 ?좏깮 0
        }
    
        void OnDestroy()
        {
            if (_inv != null) _inv.OnChanged -= Refresh;
        }
    
        void Refresh()
        {
            for (int i = 0; i < 10; i++)
            {
                var it = (_inv != null && i < _inv.items.Count) ? _inv.items[i] : null; // Row4 誘몃윭(0~9)
                _slots[i].Set(it);
            }
            // ?꾩씠??媛깆떊 ???뚮몢由??ъ쟻??
            for (int i = 0; i < 10; i++) _slots[i].SetScope(i == _scope);
        }
    
        public void SetScope(int index)
        {
            if (index < 0) index = 0;
            if (index > 9) index = 9;
            _scope = index;
            for (int i = 0; i < 10; i++)
                _slots[i].SetScope(i == _scope);
        }
    
        public int CurrentScope => _scope;
    }
}
