using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// 아이템 관련 데이터와 스프라이트를 중앙에서 관리하는 라이브러리입니다.
/// SpriteAtlas 하나와 여러 개의 JSON 데이터를 받아 파싱하여 런타임에 제공합니다.
/// 인스펙터에서 JSON 파일 개수를 지정하고 할당할 수 있습니다.
/// </summary>
public class ItemLibrary : MonoBehaviour
{
    [Header("Visual Assets")]
    public SpriteAtlas itemAtlas;

    [Header("Item JSON Files")]
    [Tooltip("합칠 JSON 파일 개수 지정 후 할당하세요.")]
    public List<TextAsset> jsonFiles = new List<TextAsset>();

    // 합쳐진 아이템 데이터 (key: itemId, value: 전체 JObject)
    private Dictionary<string, JObject> allItemDict;

    private void Awake()
    {
        allItemDict = new Dictionary<string, JObject>();

        foreach (var textAsset in jsonFiles)
        {
            MergeJson(textAsset);
        }
    }

    /// <summary>
    /// TextAsset의 JSON을 파싱하여 사전에 병합합니다.
    /// </summary>
    private void MergeJson(TextAsset textAsset)
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

        foreach (var kv in dict)
        {
            allItemDict[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// 아이템 스프라이트를 Atlas에서 가져옵니다.
    /// </summary>
    public Sprite GetSprite(string spriteName)
    {
        if (itemAtlas == null)
        {
            Debug.LogError("SpriteAtlas가 할당되지 않았습니다.");
            return null;
        }
        return itemAtlas.GetSprite(spriteName);
    }

    /// <summary>
    /// 통합된 아이템 JSON을 반환합니다.
    /// </summary>
    public JObject GetItemJson(string itemId)
    {
        if (allItemDict.TryGetValue(itemId, out var obj))
            return obj;
        Debug.LogWarning($"아이템 데이터가 존재하지 않습니다: {itemId}");
        return null;
    }
}
