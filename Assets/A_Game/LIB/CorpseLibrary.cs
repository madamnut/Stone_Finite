using System;
using UnityEngine;

/// <summary>
/// 시체 라이브러리
/// - corpseId → 시체 프리팹 스폰
/// - (corpseId, toolAction) → 다음 시체 단계로 전환
/// - 시체 단계별 corpseId를 키로 해서 드랍 아이템 스폰
/// </summary>
public class CorpseLibrary : MonoBehaviour
{
    [Serializable]
    public class CorpsePrefabDef
    {
        [Tooltip("시체 ID (예: \"Cow_Corpse\", \"Cow_Corpse_Skinned\" 등)")]
        public string corpseId;

        [Tooltip("해당 시체 ID에 대응하는 프리팹 (Corpse 컴포넌트가 붙어있어야 함)")]
        public GameObject prefab;
    }

    [Serializable]
    public class CorpseProcessDef
    {
        [Tooltip("현재 시체 ID (예: \"Cow_Corpse\")")]
        public string corpseId;

        [Tooltip("툴 액션 이름 (예: \"Scraping\", \"Cutting\", \"Chopping\")")]
        public string toolAction;

        [Header("다음 시체 단계")]
        [Tooltip("다음 단계 시체 ID (마지막 단계면 비워두거나 null)")]
        public string nextCorpseId;
    }

    [Header("Corpse Prefabs")]
    public CorpsePrefabDef[] corpsePrefabs;

    [Header("Corpse Process Rules")]
    public CorpseProcessDef[] processDefs;

    [Header("Drops")]
    [Tooltip("corpseId를 키로 사용하는 드랍 테이블을 가진 ItemDropper")]
    public ItemDropper itemDropper;

    [Header("Entity System")]
    [Tooltip("시체를 EntityManager에 등록하기 위한 참조 (비워두면 자동 검색)")]
    public EntityManager entityManager;

    void Awake()
    {
        if (entityManager == null)
            entityManager = FindObjectOfType<EntityManager>();
    }

    /// <summary>
    /// corpseId 에 대응하는 시체 프리팹을 해당 위치에 스폰하고 Corpse 컴포넌트를 반환.
    /// </summary>
    public Corpse SpawnCorpse(string corpseId, Vector2 position)
    {
        if (string.IsNullOrEmpty(corpseId))
            return null;

        CorpsePrefabDef def = null;
        if (corpsePrefabs != null)
        {
            for (int i = 0; i < corpsePrefabs.Length; i++)
            {
                var d = corpsePrefabs[i];
                if (d != null && d.corpseId == corpseId)
                {
                    def = d;
                    break;
                }
            }
        }

        if (def == null || def.prefab == null)
        {
            Debug.LogWarning($"[CorpseLibrary] corpseId='{corpseId}' 에 해당하는 프리팹이 없음.");
            return null;
        }

        var go = Instantiate(def.prefab, new Vector3(position.x, position.y, 0f), Quaternion.identity);
        var corpse = go.GetComponent<Corpse>();
        if (corpse == null)
        {
            Debug.LogError($"[CorpseLibrary] 프리팹 '{def.prefab.name}' 에 Corpse 컴포넌트가 없음.");
            return null;
        }

        corpse.CorpseId       = corpseId;
        corpse.CorpsePosition = position;

        // EntityManager에 등록 (세이브/로드 대상이 되도록)
        if (entityManager != null)
            entityManager.Register(corpse);

        return corpse;
    }

    /// <summary>
    /// 시체 위에 툴액션을 사용했을 때 가공 시도.
    /// - corpseId + toolActionName 조합으로 레시피를 찾아 처리.
    /// - 성공 시: 현재 시체 드랍 → 현재 시체 제거 → 필요하면 다음 단계 시체 스폰.
    /// </summary>
    /// <param name="corpse">대상 시체</param>
    /// <param name="toolActionName">툴 액션 이름 (예: "Scraping")</param>
    /// <returns>가공이 실제로 일어났으면 true, 아니라면 false</returns>
    public bool TryProcessCorpse(Corpse corpse, string toolActionName)
    {
        if (corpse == null)
            return false;
        if (string.IsNullOrEmpty(toolActionName))
            return false;

        string corpseId = corpse.CorpseId;
        if (string.IsNullOrEmpty(corpseId))
            return false;

        CorpseProcessDef proc = null;
        if (processDefs != null)
        {
            for (int i = 0; i < processDefs.Length; i++)
            {
                var d = processDefs[i];
                if (d == null) continue;
                if (d.corpseId == corpseId && d.toolAction == toolActionName)
                {
                    proc = d;
                    break;
                }
            }
        }

        if (proc == null)
            return false;

        Vector2 pos = corpse.CorpsePosition;

        // 1) 드랍: 이 시체 단계의 corpseId를 키로 사용
        if (itemDropper != null)
        {
            Vector3 dropPos = new Vector3(pos.x, pos.y, 0f);
            itemDropper.SpawnDroppedItems(corpseId, dropPos);
        }

        // 2) 기존 시체 제거
        Destroy(corpse.gameObject);

        // 3) 다음 단계 시체 스폰 (있으면)
        if (!string.IsNullOrEmpty(proc.nextCorpseId))
        {
            SpawnCorpse(proc.nextCorpseId, pos);
        }

        return true;
    }
}
