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
    public TextAsset dropTableJson;        // 셀 드랍 테이블 (DT_Cell.json)
    public TextAsset corpseDropTableJson;  // 시체/몹 드랍 테이블 (DT_Mob.json 또는 시체용 JSON)
    public GameObject droppedItemPrefab;

    [Header("Entity System")]
    public EntityManager entityManager;   // WorldManager 등에서 주입
    public Transform     dropRoot;        // 드랍 아이템 부모(선택)

    [Min(0)] public float spawnRadius = 0.4f;

    Dictionary<string, List<DropEntry>> _dropTable;

    //────────────────────────────────────────────
    // Drop Table 로드
    //────────────────────────────────────────────
    void Awake()
    {
        LoadDropTable();
    }

    void LoadDropTable()
    {
        _dropTable = new Dictionary<string, List<DropEntry>>();

        bool any = false;

        // 셀 드랍 테이블
        if (dropTableJson != null && !string.IsNullOrEmpty(dropTableJson.text))
        {
            MergeDropTable(dropTableJson);
            any = true;
        }
        else
        {
            Debug.LogWarning("[ItemDropper] dropTableJson(셀용)이 비어 있습니다.");
        }

        // 시체/몹 드랍 테이블
        if (corpseDropTableJson != null && !string.IsNullOrEmpty(corpseDropTableJson.text))
        {
            MergeDropTable(corpseDropTableJson);
            any = true;
        }

        if (!any)
        {
            Debug.LogError("[ItemDropper] 어떤 드랍 테이블 JSON도 설정되지 않았습니다.");
        }
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
                {
                    // 같은 키가 이미 있으면 엔트리만 이어붙임
                    list.AddRange(kv.Value);
                }
                else
                {
                    _dropTable[kv.Key] = new List<DropEntry>(kv.Value);
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[ItemDropper] DropTable 파싱 실패 ({json.name}): {ex.Message}");
        }
    }

    //────────────────────────────────────────────
    // 특정 키(셀/시체/몹) 드랍 스폰
    //────────────────────────────────────────────
    public void SpawnDroppedItems(string key, Vector3 origin)
    {
        if (_dropTable == null || !_dropTable.TryGetValue(key, out var list))
            return;

        var totals = new Dictionary<string, int>();

        // 동종 아이템 개수 합산
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

    //────────────────────────────────────────────
    // 실제 드랍 생성
    //────────────────────────────────────────────
    private void SpawnSingle(string itemId, Vector3 origin, int count)
    {
        if (itemLibrary == null || droppedItemPrefab == null)
            return;

        if (entityManager == null)
        {
            Debug.LogWarning("[ItemDropper] EntityManager가 없어 드랍 아이템을 엔티티로 등록할 수 없습니다.");
            return;
        }

        // ItemData 생성
        ItemData data = itemLibrary.Create(itemId, count);
        if (data == null) return;

        // 위치 랜덤 오프셋
        Vector3 pos = origin + (Vector3)(UnityEngine.Random.insideUnitCircle * spawnRadius);

        // 부모 설정
        Transform parent = dropRoot != null ? dropRoot : transform;

        // 프리팹 생성
        GameObject go = Instantiate(droppedItemPrefab, pos, Quaternion.identity, parent);

        // DroppedItem 컴포넌트 확인
        var comp = go.GetComponent<DroppedItem>();
        if (comp == null)
        {
            Debug.LogError("[ItemDropper] droppedItemPrefab에 DroppedItem 컴포넌트가 없습니다.");
            Destroy(go);
            return;
        }

        // 초기화 + 등록
        comp.Initialize(data);
        entityManager.Register(comp);
    }
}

//────────────────────────────────────────────
// Drop Table Entry
//────────────────────────────────────────────
[Serializable]
public struct DropEntry
{
    public string itemId;
    public float  probability;
    public int    min;
    public int    max;
}
