using System;
using System.Collections.Generic;
using Newtonsoft.Json;      // Newtonsoft.JSON 사용
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// ItemDropper
/// • dropTableJson  : <블록키, DropEntry[]> 딕셔너리(JSON)
/// • itemLibrary    : 아이디 → 아이템 JSON·스프라이트 매핑
/// • droppedItemPrefab : DroppedItem 컴포넌트가 붙은 프리팹
/// </summary>
public class ItemDropper : MonoBehaviour
{
    #region Inspector
    [Header("References")]
    public ItemLibrary itemLibrary;
    public TextAsset   dropTableJson;
    public GameObject  droppedItemPrefab;
    [Min(0f)] public float spawnRadius = 0.4f;
    #endregion

    // <블록키, 드랍 리스트>
    private Dictionary<string, List<DropEntry>> _dropTable;

    void Awake() => LoadDropTable();

    /*──────────────────────────────────────────────────────────
     *  DropTable 로드
     *──────────────────────────────────────────────────────────*/
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

    /*──────────────────────────────────────────────────────────
     *  외부 호출 : 블록키 & 월드 좌표 → 아이템 드랍
     *──────────────────────────────────────────────────────────*/
    public void SpawnDroppedItems(string blockKey, Vector3 origin)
    {
        if (_dropTable == null || !_dropTable.TryGetValue(blockKey, out var entries))
            return;

        foreach (var e in entries)
        {
            if (UnityEngine.Random.value > e.probability) continue;

            int n = UnityEngine.Random.Range(e.min, e.max + 1);
            for (int i = 0; i < n; ++i)
                SpawnSingle(e.itemId, origin);
        }
    }

    /*──────────────────────────────────────────────────────────
     *  개별 아이템 스폰
     *──────────────────────────────────────────────────────────*/
    void SpawnSingle(string itemId, Vector3 origin)
    {
        // 1) 평면 구조 JSON 로드
        JObject raw = itemLibrary.GetItemJson(itemId);
        if (raw == null) return;                        // 정의 없으면 무시

        string  name       = raw.Value<string>("name");
        string  spriteName = raw.Value<string>("spriteName");
        string  itemType   = raw.Value<string>("itemType");
        int     maxStack   = raw.Value<int   >("maxStack");

        // unique 섹션은 여전히 객체로 둠
        var uniqueObj   = raw.Value<JObject>("unique");
        var uniqueDict  = uniqueObj != null
            ? uniqueObj.ToObject<Dictionary<string, object>>()
            : new Dictionary<string, object>();

        // 2) 스프라이트
        Sprite icon = itemLibrary.GetSprite(spriteName);

        // 3) ItemData 생성
        var data = new ItemData(
            itemId, name, spriteName, itemType, maxStack, uniqueDict, icon
        );

        // 4) 프리팹 인스턴스 & 초기화
        Vector3 pos = origin + (Vector3)(UnityEngine.Random.insideUnitCircle * spawnRadius);
        var go = Instantiate(droppedItemPrefab, pos, Quaternion.identity);

        go.GetComponent<DroppedItem>()?.Initialize(data);
    }
}

/*──────────────────────────────────────────────────────────────
 *  DropEntry 구조체
 *────────────────────────────────────────────────────────────*/
[Serializable]
public struct DropEntry
{
    public string itemId;     // 아이템 ID
    public float  probability;// 0~1
    public int    min;        // 최소 수량 (inclusive)
    public int    max;        // 최대 수량 (inclusive)
}
