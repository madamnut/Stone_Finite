using System.Collections.Generic;
using UnityEngine;

public class ItemData
{
    /* 기본 메타 */
    public string ItemId     { get; }
    public string Name       { get; }
    public string SpriteName { get; }
    public string ItemType   { get; }
    public int    MaxStack   { get; }
    public int    Count      { get; set; }
    public Sprite Icon       { get; }

    /* 수정 가능 고유 속성 */
    public Dictionary<string, object> Unique { get; private set; }

    /* 생성자 */
    public ItemData(
        string itemId,
        string name,
        string spriteName,
        string itemType,
        int    maxStack,
        Dictionary<string, object> unique,
        Sprite icon,
        int    count = 1)
    {
        ItemId     = itemId;
        Name       = name;
        SpriteName = spriteName;
        ItemType   = itemType;
        MaxStack   = maxStack;
        Icon       = icon;
        Count      = count;

        // 방어 복사로 내부 소유
        Unique = unique != null
            ? new Dictionary<string, object>(unique)
            : new Dictionary<string, object>();
    }

    // 고유 속성 조회
    public T GetUnique<T>(string key)
    {
        if (Unique.TryGetValue(key, out var v) && v is T t) return t;
        return default;
    }
}
