using System.Collections.Generic;

/// <summary>
/// 순수 인벤토리 데이터(저장소) 계층.
/// • MonoBehaviour 아님 ― 어떤 곳에서도 자유롭게 new 로 생성 가능
/// • OnChanged 이벤트로 UI 등에서 갱신 Hook 걸 수 있음
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
            items.Add(null);                        // 빈 슬롯
    }

    /// <summary>외부에서 강제 갱신 알림</summary>
    public void NotifyChanged() => OnChanged?.Invoke();

    /// <summary>
    /// ItemData.Count 만큼 넣고, 못 넣은 수량을 반환한다.
    /// </summary>
    public int AddItem(ItemData incoming)
    {
        int left = incoming.Count;

        /* 1) 같은 ID 스택 채우기 */
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

        /* 2) 빈 슬롯 채우기 */
        for (int i = 0; i < Capacity && left > 0; i++)
        {
            if (items[i] != null) continue;

            int take = left > incoming.MaxStack ? incoming.MaxStack : left;
            items[i] = new ItemData(
                incoming.ItemId,  incoming.Name,       incoming.SpriteName,
                incoming.ItemType, incoming.MaxStack,  new Dictionary<string, object>(incoming.UniqueProps),
                incoming.Icon,     take);

            left -= take;
        }

        OnChanged?.Invoke();
        return left;           // 0 → 전량 수용
    }
}
