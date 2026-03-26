


using UnityEngine;
using UnityEngine.Serialization;
using Game.Core;

namespace Game.UI
{
    [DisallowMultipleComponent]
    public class PlayerInventory : MonoBehaviour, IInventoryOwnerConsumer
    {
        [Header("Bindings")]
        [FormerlySerializedAs("player")]
        [SerializeField] private MonoBehaviour inventoryOwnerComponent;

        [Header("Inventory Rows (0 ~ 4)")]

        public Transform row0, row1, row2, row3, row4;

        private readonly ItemSlot[] _slots = new ItemSlot[50];
        private IInventoryOwner _inventoryOwner;
        private InventoryData _inv;

        
        void Awake()
        {
            int i = 0;
            MapRow(row4, ref i);
            MapRow(row0, ref i);
            MapRow(row1, ref i);
            MapRow(row2, ref i);
            MapRow(row3, ref i);
        }

        
        void OnEnable()
        {
            ResolveInventoryOwner();
            _inv = _inventoryOwner != null ? _inventoryOwner.Inventory : null;

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
                s.index = idx;
                _slots[idx++] = s;
            }
        }

        
        public ItemSlot GetSlot(int index) => _slots[index];

        
        void ResolveInventoryOwner()
        {
            _inventoryOwner = inventoryOwnerComponent as IInventoryOwner;
            if (inventoryOwnerComponent != null && _inventoryOwner == null)
                Debug.LogWarning($"[PlayerInventory] Assigned component on {name} does not implement IInventoryOwner.", this);
        }

        
        public void SetInventoryOwner(IInventoryOwner inventoryOwner)
        {
            _inventoryOwner = inventoryOwner;
            inventoryOwnerComponent = inventoryOwner as MonoBehaviour;
        }
    }
}
