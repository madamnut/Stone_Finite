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

    /* 내구도 (ATT의 maxDurability + 현재 내구도) */
    public int MaxDurability { get; }
    public int Durability    { get; set; }

    /* 개수 및 아이콘 */
    public int    Count { get; set; }
    public Sprite Icon  { get; }

    /* 태그 (ATT의 tags) */
    public List<string> Tags { get; }

    /* 액션 4종 (ATT의 crafting/inter/tool/weaponActions 계승) */
    public List<string> CraftingActions { get; }
    public List<string> InterActions    { get; }
    public List<string> ToolActions     { get; }
    public List<string> WeaponActions   { get; }

    /* 수정 가능 파라미터 (ATT의 params 등 확장 필드) */
    public Dictionary<string, object> Parameters { get; private set; }

    /* 생성자 */
    public ItemData(
        string itemId,
        string name,
        string spriteName,
        string itemType,
        int    maxStack,
        int    maxDurability,
        int    durability,
        List<string> craftingActions,
        List<string> interActions,
        List<string> toolActions,
        List<string> weaponActions,
        List<string> tags,
        Dictionary<string, object> parameters,
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

        CraftingActions = craftingActions != null
            ? new List<string>(craftingActions)
            : new List<string>();

        InterActions = interActions != null
            ? new List<string>(interActions)
            : new List<string>();

        ToolActions = toolActions != null
            ? new List<string>(toolActions)
            : new List<string>();

        WeaponActions = weaponActions != null
            ? new List<string>(weaponActions)
            : new List<string>();

        Tags = tags != null
            ? new List<string>(tags)
            : new List<string>();

        // 방어 복사하여 내부에서 독립적으로 소유
        Parameters = parameters != null
            ? new Dictionary<string, object>(parameters)
            : new Dictionary<string, object>();
    }

    /* ───────────────── 유틸 메서드 ───────────────── */

    // 파라미터 조회
    public T GetParameter<T>(string key)
    {
        if (Parameters.TryGetValue(key, out var v) && v is T t)
            return t;
        return default;
    }

    // 파라미터 설정 (1단계 키)
    public void SetParameter(string key, object value)
    {
        Parameters[key] = value;
    }

    // 중첩 경로 기반 파라미터 설정 (예: "Parts.head")
    public void SetParamPath(string path, object value)
    {
        if (string.IsNullOrEmpty(path))
            return;

        var parts = path.Split('.');
        var dict  = Parameters;

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

    // 내구도 변경 (레시피/사용 시 공통 로직)
    public void ModifyDurability(int amount)
    {
        Durability += amount;
        if (Durability > MaxDurability) Durability = MaxDurability;
        if (Durability < 0)            Durability = 0;
    }

    // 태그 보유 여부 (Tags 우선, 없으면 Parameters["tags"] fallback)
    public bool HasTag(string tag)
    {
        if (Tags != null && Tags.Contains(tag))
            return true;

        if (Parameters.TryGetValue("tags", out var v) && v is List<string> tags)
            return tags.Contains(tag);

        return false;
    }

    // 액션 4종 검사
    public bool HasCraftingAction(string action)
        => CraftingActions.Contains(action);

    public bool HasInterAction(string action)
        => InterActions.Contains(action);

    public bool HasToolAction(string action)
        => ToolActions.Contains(action);

    public bool HasWeaponAction(string action)
        => WeaponActions.Contains(action);
}
