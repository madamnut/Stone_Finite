using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

public class ItemDropper : MonoBehaviour
{
    [Header("References")]
    public ItemLibrary itemLibrary;
    public TextAsset   dropTableJson;
    public GameObject  droppedItemPrefab;
    [Min(0)] public float spawnRadius = 0.4f;

    Dictionary<string, List<DropEntry>> _dropTable;

    void Awake() => LoadDropTable();

    void LoadDropTable()
    {
        if (dropTableJson == null)
        {
            Debug.LogError("[ItemDropper] dropTableJson이 비어 있습니다.");
            _dropTable = new Dictionary<string, List<DropEntry>>();
            return;
        }

        try
        {
            _dropTable = JsonConvert.DeserializeObject<Dictionary<string, List<DropEntry>>>(dropTableJson.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ItemDropper] DropTable 파싱 실패: {ex.Message}");
            _dropTable = new Dictionary<string, List<DropEntry>>();
        }
    }

    public void SpawnDroppedItems(string blockKey, Vector3 origin)
    {
        if (_dropTable == null || !_dropTable.TryGetValue(blockKey, out var list))
            return;

        // 같은 itemId끼리 개수 합산 → 종류당 1개만 스폰
        var totals = new Dictionary<string, int>();
        foreach (var e in list)
        {
            if (UnityEngine.Random.value > e.probability) continue;

            int cnt = UnityEngine.Random.Range(e.min, e.max + 1);
            if (cnt <= 0) continue;

            if (totals.TryGetValue(e.itemId, out int cur)) totals[e.itemId] = cur + cnt;
            else totals.Add(e.itemId, cnt);
        }

        foreach (var kv in totals)
            SpawnSingle(kv.Key, origin, kv.Value);
    }

    void SpawnSingle(string itemId, Vector3 origin, int count)
    {
        if (itemLibrary == null || droppedItemPrefab == null) return;

        // ATT 스키마에 맞게 ItemLibrary.Create로 일관된 ItemData 생성
        ItemData data = itemLibrary.Create(itemId, count);
        if (data == null) return;

        Vector3 pos = origin + (Vector3)(UnityEngine.Random.insideUnitCircle * spawnRadius);
        var go = Instantiate(droppedItemPrefab, pos, Quaternion.identity);
        var comp = go.GetComponent<DroppedItem>();
        if (comp != null)
            comp.Initialize(data);
    }
}

[Serializable]
public struct DropEntry
{
    public string itemId;
    public float  probability;
    public int    min;
    public int    max;
}
