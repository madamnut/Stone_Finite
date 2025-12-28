// WoodenCrateModule.cs
using UnityEngine;

public class WoodenCrateModule : MonoBehaviour
{
    [Header("Rows (ROW0 ~ ROW4)")]
    public Transform ROW0;
    public Transform ROW1;
    public Transform ROW2;
    public Transform ROW3;
    public Transform ROW4;

    readonly ItemSlot[] _slots = new ItemSlot[25];
    WoodenCrate _crate;
    InventoryData _inv;

    void Awake()
    {
        int idx = 0;
        MapRow(ROW0, ref idx);
        MapRow(ROW1, ref idx);
        MapRow(ROW2, ref idx);
        MapRow(ROW3, ref idx);
        MapRow(ROW4, ref idx);
    }

    void MapRow(Transform row, ref int idx)
    {
        if (row == null) { idx += 5; return; }

        for (int c = 0; c < 5; c++)
        {
            var t = row.Find(c.ToString());
            if (t == null) { idx++; continue; }

            var s = t.GetComponent<ItemSlot>();
            if (s == null) { idx++; continue; }

            s.index = idx;          // ✅ 인덱스 고정 (row*5 + col)
            s.useLocalStorage = false;
            s.denyUserPut = false;
            s.denyUserInteraction = false;

            _slots[idx] = s;
            idx++;
        }
    }

    public void Bind(WoodenCrate crate)
    {
        // 기존 바인딩 해제
        if (_inv != null) _inv.OnChanged -= Refresh;

        _crate = crate;
        _inv = (_crate != null) ? _crate.Inventory : null;

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
        _crate = null;
    }

    void Refresh()
    {
        for (int i = 0; i < _slots.Length; i++)
        {
            if (_slots[i] == null) continue;

            var it = (_inv != null && i < _inv.items.Count) ? _inv.items[i] : null;
            _slots[i].Set(it);
        }
    }
}
