using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [Header("Player")]
    public PlayerManager player;   // ← 인스펙터에 PlayerManager 드래그

    [Header("Row Parents (0 ~ 4)")]
    public Transform row0, row1, row2, row3, row4;

    private readonly ItemSlot[] _slots = new ItemSlot[50];
    private InventoryData _inv;

    void Awake()
    {
        int i = 0;
        MapRow(row4, ref i);  // 우선순위: ROW4 → 0 → 1 → 2 → 3
        MapRow(row0, ref i);
        MapRow(row1, ref i);
        MapRow(row2, ref i);
        MapRow(row3, ref i);
    }

    void OnEnable()
    {
        _inv = (player != null) ? player.Inventory : null;
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
            _slots[idx++] = row.Find(c.ToString()).GetComponent<ItemSlot>();
    }

    public ItemSlot GetSlot(int index) => _slots[index];
}
