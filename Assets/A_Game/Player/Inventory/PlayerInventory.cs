using UnityEngine;

[DisallowMultipleComponent]
public class PlayerInventory : MonoBehaviour
{
    [Header("Player")]
    public Player player;   // ← 인스펙터에 Player 드래그

    [Header("Inventory Rows (0 ~ 4)")]
    public Transform row0, row1, row2, row3, row4;

    private readonly ItemSlot[] _slots = new ItemSlot[50];
    private InventoryData _inv;

    void Awake()
    {
        int i = 0;
        // 우선순위: ROW4 → 0 → 1 → 2 → 3  (Row4 = 인덱스 0~9)
        MapRow(row4, ref i);
        MapRow(row0, ref i);
        MapRow(row1, ref i);
        MapRow(row2, ref i);
        MapRow(row3, ref i);
    }

    void OnEnable()
    {
        _inv = (player != null) ? player.Inventory : null;
        // 슬롯 메타데이터 바인딩(인덱스는 Awake에서, 인벤 참조는 여기서)
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
            s.index = idx;              // 인덱스 고정
            _slots[idx++] = s;
        }
    }

    public ItemSlot GetSlot(int index) => _slots[index];
}
