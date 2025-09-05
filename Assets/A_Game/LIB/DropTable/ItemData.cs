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
    public int Count { get; set; }

    private readonly Dictionary<string, object> _uniqueProps;
    public IReadOnlyDictionary<string, object> UniqueProps => _uniqueProps;
    public Sprite Icon { get; }

    /* 생성자 */
    public ItemData(
        string itemId,
        string name,
        string spriteName,
        string itemType,
        int    maxStack,
        Dictionary<string, object> uniqueProps,
        Sprite icon,
        int    count = 1)
    {
        ItemId     = itemId;
        Name       = name;
        SpriteName = spriteName;
        ItemType   = itemType;
        MaxStack   = maxStack;
        Icon       = icon;
        Count = count;

        _uniqueProps = uniqueProps != null
            ? new Dictionary<string, object>(uniqueProps)
            : new Dictionary<string, object>();
    }

    // 고유 속성 조회
    public T GetUnique<T>(string key)
    {
        if (_uniqueProps.TryGetValue(key, out var v) && v is T t) return t;
        return default;
    }
}