// ItemDropper.cs
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

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

    //────────────────────────────────────────────
    // Drop Table 로드
    //────────────────────────────────────────────
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
            Debug.LogError("[ItemDropper] 어떤 드랍 테이블 JSON도 설정되지 않았습니다.");
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
            Debug.LogError($"[ItemDropper] DropTable 파싱 실패 ({json.name}): {ex.Message}");
        }
    }

    //────────────────────────────────────────────
    // (1) itemId 기반 드랍
    //────────────────────────────────────────────
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

    //────────────────────────────────────────────
    // (2) ItemData 그대로 드랍
    //────────────────────────────────────────────
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

    //────────────────────────────────────────────
    // 내부 실제 생성 함수 (단일)
    //────────────────────────────────────────────
    DroppedItem SpawnDroppedItemInternal(ItemData data, Vector3 position, Transform parent)
    {
        if (droppedItemPrefab == null || entityManager == null)
            return null;

        GameObject go = Instantiate(droppedItemPrefab, position, Quaternion.identity, parent);

        var comp = go.GetComponent<DroppedItem>();
        if (comp == null)
        {
            Debug.LogError("[ItemDropper] droppedItemPrefab에 DroppedItem 컴포넌트가 없습니다.");
            Destroy(go);
            return null;
        }

        comp.Initialize(data);
        entityManager.Register(comp);
        return comp;
    }
}

//────────────────────────────────────────────
[Serializable]
public struct DropEntry
{
    public string itemId;
    public float probability;
    public int min;
    public int max;
}
