// ItemDropper.cs (?꾩껜 援먯껜蹂?
// 蹂寃쎌젏:
// - "?뺤젙 ?쒕엻(?뺣쪧/?쒕엻?뚯씠釉?臾댁떆)"??API 異붽?: SpawnItemDirect(itemId, origin, count)
//   (湲곗뼱 ?뚭눼 ??遺숈? ?뚯뒪??臾댁“嫄?1媛??쒕엻 ?붽뎄?ы빆 ???

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

using Game.Data;
using Game.World;
using Game.Player;
public class ItemDropper : MonoBehaviour
{
    [Header("References")]
    public ItemLibrary itemLibrary;

    [Header("Drop Table Jsons")]
    public TextAsset dropTableJson;
    public TextAsset corpseDropTableJson;
    public GameObject droppedItemPrefab;

    [Header("Entity System")]
    public EntityManager entityManager;
    public Transform dropRoot;

    [Min(0)] public float spawnRadius = 0.4f;

    Dictionary<string, List<DropEntry>> _dropTable;

    void Awake()
    {
        LoadDropTable();
    }

    void LoadDropTable()
    {
        _dropTable = new Dictionary<string, List<DropEntry>>();
        bool any = false;

        if (dropTableJson != null && !string.IsNullOrEmpty(dropTableJson.text))
        {
            MergeDropTable(dropTableJson);
            any = true;
        }

        if (corpseDropTableJson != null && !string.IsNullOrEmpty(corpseDropTableJson.text))
        {
            MergeDropTable(corpseDropTableJson);
            any = true;
        }

        if (!any)
            Debug.LogError("[ItemDropper] ?대뼡 ?쒕엻 ?뚯씠釉?JSON???ㅼ젙?섏? ?딆븯?듬땲??");
    }

    void MergeDropTable(TextAsset json)
    {
        try
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, List<DropEntry>>>(json.text);
            if (dict == null) return;

            foreach (var kv in dict)
            {
                if (_dropTable.TryGetValue(kv.Key, out var list))
                    list.AddRange(kv.Value);
                else
                    _dropTable[kv.Key] = new List<DropEntry>(kv.Value);
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ItemDropper] DropTable ?뚯떛 ?ㅽ뙣 ({json.name}): {ex.Message}");
        }
    }

    //????????????????????????????????????????????
    // (0) ?뺤젙 ?쒕엻(?쒕엻?뚯씠釉?臾댁떆)  ???좉퇋
    //????????????????????????????????????????????
    public void SpawnItemDirect(string itemId, Vector3 origin, int count = 1)
    {
        if (string.IsNullOrEmpty(itemId)) return;
        if (count <= 0) return;
        SpawnSingle(itemId, origin, count);
    }

    //????????????????????????????????????????????
    // (1) key 湲곕컲 ?쒕엻?뚯씠釉?
    //????????????????????????????????????????????
    public void SpawnDroppedItems(string key, Vector3 origin)
    {
        if (_dropTable == null || !_dropTable.TryGetValue(key, out var list))
            return;

        var totals = new Dictionary<string, int>();

        foreach (var e in list)
        {
            if (UnityEngine.Random.value > e.probability)
                continue;

            int cnt = UnityEngine.Random.Range(e.min, e.max + 1);
            if (cnt <= 0) continue;

            if (totals.TryGetValue(e.itemId, out int cur))
                totals[e.itemId] = cur + cnt;
            else
                totals.Add(e.itemId, cnt);
        }

        foreach (var kv in totals)
            SpawnSingle(kv.Key, origin, kv.Value);
    }

    void SpawnSingle(string itemId, Vector3 origin, int count)
    {
        if (itemLibrary == null) return;

        ItemData data = itemLibrary.Create(itemId, count);
        if (data == null) return;

        SpawnDroppedItem(data, origin);
    }

    //????????????????????????????????????????????
    // (2) ItemData 洹몃?濡??쒕엻
    //????????????????????????????????????????????
    public DroppedItem SpawnDroppedItem(ItemData data, Vector3 origin)
    {
        if (data == null) return null;

        Vector3 pos = origin + (Vector3)(UnityEngine.Random.insideUnitCircle * spawnRadius);
        Transform parent = dropRoot != null ? dropRoot : transform;

        return SpawnDroppedItemInternal(data, pos, parent);
    }

    public DroppedItem SpawnDroppedItemAt(ItemData data, Vector3 position, Transform parent)
    {
        if (data == null) return null;
        return SpawnDroppedItemInternal(data, position, parent);
    }

    DroppedItem SpawnDroppedItemInternal(ItemData data, Vector3 position, Transform parent)
    {
        if (droppedItemPrefab == null || entityManager == null)
            return null;

        GameObject go = Instantiate(droppedItemPrefab, position, Quaternion.identity, parent);

        var comp = go.GetComponent<DroppedItem>();
        if (comp == null)
        {
            Debug.LogError("[ItemDropper] droppedItemPrefab??DroppedItem 而댄룷?뚰듃媛 ?놁뒿?덈떎.");
            Destroy(go);
            return null;
        }

        comp.Initialize(data);
        entityManager.Register(comp);
        return comp;
    }
}

[Serializable]
public struct DropEntry
{
    public string itemId;
    public float probability;
    public int min;
    public int max;
}
