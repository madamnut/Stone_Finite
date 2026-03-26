


using UnityEngine;
using UnityEngine.Serialization;
using Game.Core;

namespace Game.UI
{
    public class Hotbar : MonoBehaviour, IInventoryOwnerConsumer
    {
        [Header("Bindings")]
        [FormerlySerializedAs("player")]
        [SerializeField] private MonoBehaviour inventoryOwnerComponent;

        public Transform hotbarRoot;

        private readonly ItemSlot[] _slots = new ItemSlot[10];
        private IInventoryOwner _inventoryOwner;
        private InventoryData _inv;
        private int _scope;

        
        void Awake()
        {
            for (int i = 0; i < 10; i++)
                _slots[i] = hotbarRoot.Find(i.ToString()).GetComponent<ItemSlot>();
        }

        
        void Start()
        {
            ResolveInventoryOwner();
            _inv = _inventoryOwner != null ? _inventoryOwner.Inventory : null;
            if (_inv != null) _inv.OnChanged += Refresh;
            Refresh();
            SetScope(0);
        }

        
        void OnDestroy()
        {
            if (_inv != null) _inv.OnChanged -= Refresh;
        }

        
        void Refresh()
        {
            for (int i = 0; i < 10; i++)
            {
                var item = (_inv != null && i < _inv.items.Count) ? _inv.items[i] : null;
                _slots[i].Set(item);
            }

            for (int i = 0; i < 10; i++)
                _slots[i].SetScope(i == _scope);
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

        
        void ResolveInventoryOwner()
        {
            _inventoryOwner = inventoryOwnerComponent as IInventoryOwner;
            if (inventoryOwnerComponent != null && _inventoryOwner == null)
                Debug.LogWarning($"[Hotbar] Assigned component on {name} does not implement IInventoryOwner.", this);
        }

        
        public void SetInventoryOwner(IInventoryOwner inventoryOwner)
        {
            _inventoryOwner = inventoryOwner;
            inventoryOwnerComponent = inventoryOwner as MonoBehaviour;
        }
    }
}
