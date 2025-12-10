using System;
using UnityEngine;

/// <summary>
/// 시체 라이브러리
/// - corpseId → 시체 프리팹 스폰
/// - (corpseId, toolAction) → 다음 시체 단계로 전환
/// 드랍템은 여기서 처리하지 않음.
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

        return corpse;
    }

    /// <summary>
    /// 시체 위에 툴액션을 사용했을 때 가공 시도.
    /// - corpseId + toolActionName 조합으로 레시피를 찾아 처리.
    /// - 성공 시: 현재 시체 제거 → 필요하면 다음 단계 시체 스폰.
    /// - 드랍 아이템은 여기서 처리하지 않음.
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

        // 1) 기존 시체 제거
        Destroy(corpse.gameObject);

        // 2) 다음 단계 시체 스폰 (있으면)
        if (!string.IsNullOrEmpty(proc.nextCorpseId))
        {
            SpawnCorpse(proc.nextCorpseId, pos);
        }

        return true;
    }
}
