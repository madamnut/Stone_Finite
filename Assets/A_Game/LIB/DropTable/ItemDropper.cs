using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

public class ItemDropper : MonoBehaviour
{
    [Header("References")]
    public ItemLibrary itemLibrary;
    public TextAsset   dropTableJson;
    public GameObject  droppedItemPrefab;

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


    //────────────────────────────────────────────
    // 특정 블록 파괴 시 드랍 스폰
    //────────────────────────────────────────────
    public void SpawnDroppedItems(string blockKey, Vector3 origin)
    {
        if (_dropTable == null || !_dropTable.TryGetValue(blockKey, out var list))
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
        // Icon은 읽기 전용 프로퍼티이므로 여기서 수정하지 않음.
        // 아이콘 세팅은 ItemLibrary.Create 쪽에서 책임지도록 유지.

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
