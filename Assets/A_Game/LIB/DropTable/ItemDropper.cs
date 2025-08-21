using System;
using System.Collections.Generic;
using Newtonsoft.Json;            // ← 반드시 추가
using Newtonsoft.Json.Linq;
using UnityEngine;

public class ItemDropper : MonoBehaviour
{
    [Header("References")]
    public ItemLibrary itemLibrary;
    public TextAsset   dropTableJson;
    public GameObject  droppedItemPrefab;
    public float       spawnRadius = 0.5f;

    private Dictionary<string, List<DropEntry>> _dropTable;

    private void Awake()
    {
        LoadDropTable();
    }

    private void LoadDropTable()
    {
        if (dropTableJson == null)
        {
            Debug.LogError("ItemDropper: dropTableJson이 할당되지 않았습니다.");
            _dropTable = new Dictionary<string, List<DropEntry>>();
            return;
        }

        try
        {
            _dropTable = JsonConvert.DeserializeObject<Dictionary<string, List<DropEntry>>>(dropTableJson.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Drop 테이블 파싱 오류: {ex.Message}");
            _dropTable = new Dictionary<string, List<DropEntry>>();
        }
    }

    /// <summary>
    /// 드롭 테이블 키와 위치를 받아 아이템들을 생성합니다.
    /// </summary>
    public void SpawnDroppedItems(string key, Vector3 origin)
    {
        if (_dropTable == null || !_dropTable.TryGetValue(key, out var entries))
            return;

        foreach (var entry in entries)
        {
            if (UnityEngine.Random.value <= entry.probability)
            {
                int count = UnityEngine.Random.Range(entry.min, entry.max + 1);
                for (int i = 0; i < count; i++)
                    SpawnSingle(entry.itemId, origin);
            }
        }
    }

    private void SpawnSingle(string itemId, Vector3 origin)
    {
        // 1) JSON 템플릿 조회
        JObject rawJson = itemLibrary.GetItemJson(itemId);
        if (rawJson == null) return;

        // 2) 스프라이트 조회
        Sprite icon = itemLibrary.GetSprite(rawJson["common"]["spriteName"].ToString());

        // 3) common / unique 분리 후 Dictionary 변환
        var common = rawJson.Value<JObject>("common");
        var unique = rawJson.Value<JObject>("unique");
        var uniqueDict = unique != null
            ? unique.ToObject<Dictionary<string, object>>()
            : new Dictionary<string, object>();

        // 4) ItemData 인스턴스 생성
        var data = new ItemData(
            itemId,
            common.Value<string>("name"),
            common.Value<string>("spriteName"),
            common.Value<string>("itemType"),
            common.Value<int>("maxStack"),
            uniqueDict,
            icon
        );

        // 5) 프리팹 생성 및 초기화
        Vector2 offset = UnityEngine.Random.insideUnitCircle * spawnRadius;
        Vector3 spawnPos = origin + (Vector3)offset;
        var go = Instantiate(droppedItemPrefab, spawnPos, Quaternion.identity);
        var dropped = go.GetComponent<DroppedItem>();
        if (dropped != null)
            dropped.Initialize(data);    // ← 여기 매개변수 하나로
        else
            Debug.LogWarning("DroppedItem 컴포넌트를 찾을 수 없습니다.");
    }
}
