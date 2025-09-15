using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// 아이템 정의(JSON)와 스프라이트 아틀라스를 중앙 관리.
/// </summary>
public class ItemLibrary : MonoBehaviour
{
    [Header("Visual Assets")]
    public SpriteAtlas itemAtlas;

    [Header("Item JSON Files")]
    [Tooltip("합칠 JSON 파일 개수 지정 후 할당하세요.")]
    public List<TextAsset> jsonFiles = new List<TextAsset>();

    // key: itemId, value: 정의 원본(JObject)
    private Dictionary<string, JObject> allItemDict;

    void Awake()
    {
        allItemDict = new Dictionary<string, JObject>();
        foreach (var textAsset in jsonFiles) MergeJson(textAsset);
    }

    void MergeJson(TextAsset textAsset)
    {
        if (textAsset == null)
        {
            Debug.LogWarning("할당되지 않은 JSON 파일이 있습니다.");
            return;
        }

        Dictionary<string, JObject> dict;
        try
        {
            dict = Newtonsoft.Json.JsonConvert.DeserializeObject<Dictionary<string, JObject>>(textAsset.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"JSON 파싱 오류 ({textAsset.name}): {ex.Message}");
            return;
        }

        foreach (var kv in dict) allItemDict[kv.Key] = kv.Value;
    }

    /// <summary>아틀라스에서 스프라이트 획득.</summary>
    public Sprite GetSprite(string spriteName)
    {
        if (itemAtlas == null)
        {
            Debug.LogError("SpriteAtlas가 할당되지 않았습니다.");
            return null;
        }
        return itemAtlas.GetSprite(spriteName);
    }

    /// <summary>아이템 정의 JSON 원본.</summary>
    public JObject GetItemJson(string itemId)
    {
        if (allItemDict.TryGetValue(itemId, out var obj)) return obj;
        Debug.LogWarning($"아이템 데이터가 존재하지 않습니다: {itemId}");
        return null;
    }

    /// <summary>
    /// 아이템 인스턴스 생성 팩토리.
    /// 정의(JSON) + 스프라이트를 사용해 ItemData를 구성한다.
    /// </summary>
    public ItemData Create(string itemId, int count = 1)
    {
        var def = GetItemJson(itemId);
        if (def == null) return null;

        string name       = def.Value<string>("name")       ?? itemId;
        string spriteName = def.Value<string>("spriteName") ?? itemId;
        string itemType   = def.Value<string>("itemType")   ?? "Generic";
        int    maxStack   = def.Value<int?>("maxStack")     ?? 1;

        // unique → Dictionary<string, object>
        var props = new Dictionary<string, object>();
        if (def["unique"] is JObject unique)
        {
            foreach (var kv in unique)
                props[kv.Key] = kv.Value.ToObject<object>();
        }

        var icon = GetSprite(spriteName);

        return new ItemData(
            itemId:     itemId,
            name:       name,
            spriteName: spriteName,
            itemType:   itemType,
            maxStack:   maxStack,
            unique:     props,
            icon:       icon,
            count:      count
        );
    }
}
