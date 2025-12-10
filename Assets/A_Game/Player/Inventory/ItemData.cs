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

    /* 내구도 */
    public int MaxDurability { get; }
    public int Durability    { get; set; }

    /* 개수 및 아이콘 */
    public int    Count { get; set; }
    public Sprite Icon  { get; }

    /* 태그 (ATT의 tags) */
    public List<string> Tags { get; }

    /* 액션 3종 (각 액션 이름 → 해당 액션의 세부 파라미터 딕셔너리) */
    public Dictionary<string, Dictionary<string, object>> ToolActions   { get; }
    public Dictionary<string, Dictionary<string, object>> WeaponActions { get; }
    public Dictionary<string, Dictionary<string, object>> BreakActions  { get; }

    /* 디테일스 (ATT details + 조합 결과 등 기타 확장 구조) */
    public Dictionary<string, object> Details { get; private set; }

    /* 생성자 */
    public ItemData(
        string itemId,
        string name,
        string spriteName,
        string itemType,
        int    maxStack,
        int    maxDurability,
        int    durability,
        Dictionary<string, Dictionary<string, object>> toolActions,
        Dictionary<string, Dictionary<string, object>> weaponActions,
        Dictionary<string, Dictionary<string, object>> breakActions,
        List<string> tags,
        Dictionary<string, object> details,
        Sprite icon,
        int    count = 1)
    {
        ItemId       = itemId;
        Name         = name;
        SpriteName   = spriteName;
        ItemType     = itemType;
        MaxStack     = maxStack;

        MaxDurability = maxDurability;
        Durability    = (durability > 0) ? durability : maxDurability;

        Icon  = icon;
        Count = count;

        Tags = tags != null
            ? new List<string>(tags)
            : new List<string>();

        // 액션 3종: 이름 → 파라미터 딕셔너리
        ToolActions = toolActions != null
            ? new Dictionary<string, Dictionary<string, object>>(toolActions)
            : new Dictionary<string, Dictionary<string, object>>();

        WeaponActions = weaponActions != null
            ? new Dictionary<string, Dictionary<string, object>>(weaponActions)
            : new Dictionary<string, Dictionary<string, object>>();

        BreakActions = breakActions != null
            ? new Dictionary<string, Dictionary<string, object>>(breakActions)
            : new Dictionary<string, Dictionary<string, object>>();

        Details = details != null
            ? new Dictionary<string, object>(details)
            : new Dictionary<string, object>();
    }

    /* ───────────────── 유틸 메서드 ───────────────── */

    // 단일 키 조회
    public T GetDetail<T>(string key)
    {
        if (Details.TryGetValue(key, out var v) && v is T t)
            return t;
        return default;
    }

    // 단일 키 설정
    public void SetDetail(string key, object value)
    {
        Details[key] = value;
    }

    // 중첩 경로 기반 detail 설정 (예: "head.itemId", "weapon.head.damage")
    public void SetDetailPath(string path, object value)
    {
        if (string.IsNullOrEmpty(path))
            return;

        var parts = path.Split('.');
        var dict  = Details;

        for (int i = 0; i < parts.Length - 1; i++)
        {
            string key = parts[i];

            if (!dict.TryGetValue(key, out var next) || next is not Dictionary<string, object> nextDict)
            {
                nextDict = new Dictionary<string, object>();
                dict[key] = nextDict;
            }

            dict = nextDict;
        }

        string last = parts[parts.Length - 1];
        dict[last] = value;
    }

    // 내구도 조절
    public void ModifyDurability(int amount)
    {
        Durability += amount;
        if (Durability > MaxDurability) Durability = MaxDurability;
        if (Durability < 0)            Durability = 0;
    }

    // 태그 검사
    public bool HasTag(string tag)
    {
        if (Tags != null && Tags.Contains(tag))
            return true;

        // fallback
        if (Details.TryGetValue("tags", out var v) && v is List<string> tags)
            return tags.Contains(tag);

        return false;
    }

    // 액션 보유 여부
    public bool HasToolAction(string action)
        => ToolActions != null && ToolActions.ContainsKey(action);

    public bool HasWeaponAction(string action)
        => WeaponActions != null && WeaponActions.ContainsKey(action);

    public bool HasBreakAction(string action)
        => BreakActions != null && BreakActions.ContainsKey(action);

    // 액션 파라미터 딕셔너리 가져오기 (없으면 null)
    public Dictionary<string, object> GetToolActionParams(string action)
    {
        if (ToolActions != null && ToolActions.TryGetValue(action, out var cfg))
            return cfg;
        return null;
    }

    public Dictionary<string, object> GetWeaponActionParams(string action)
    {
        if (WeaponActions != null && WeaponActions.TryGetValue(action, out var cfg))
            return cfg;
        return null;
    }

    public Dictionary<string, object> GetBreakActionParams(string action)
    {
        if (BreakActions != null && BreakActions.TryGetValue(action, out var cfg))
            return cfg;
        return null;
    }
}
