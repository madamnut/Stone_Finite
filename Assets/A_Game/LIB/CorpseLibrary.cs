using System;
using UnityEngine;

/// <summary>
/// ?œì²´ ?¼ì´ë¸ŒëŸ¬ë¦?
/// - corpseId ???œì²´ ?„ë¦¬???¤í°
/// - (corpseId, toolAction) ???¤ìŒ ?œì²´ ?¨ê³„ë¡??„í™˜
/// - ?œì²´ ?¨ê³„ë³?corpseIdë¥??¤ë¡œ ?´ì„œ ?œë ?„ì´???¤í°
/// </summary>
using Game.World;
public class CorpseLibrary : MonoBehaviour
{
    [Serializable]
    public class CorpsePrefabDef
    {
        [Tooltip("?œì²´ ID (?? \"Cow_Corpse\", \"Cow_Corpse_Skinned\" ??")]
        public string corpseId;

        [Tooltip("?´ë‹¹ ?œì²´ ID???€?‘í•˜???„ë¦¬??(Corpse ì»´í¬?ŒíŠ¸ê°€ ë¶™ì–´?ˆì–´????")]
        public GameObject prefab;
    }

    [Serializable]
    public class CorpseProcessDef
    {
        [Tooltip("?„ì¬ ?œì²´ ID (?? \"Cow_Corpse\")")]
        public string corpseId;

        [Tooltip("???¡ì…˜ ?´ë¦„ (?? \"Scraping\", \"Cutting\", \"Chopping\")")]
        public string toolAction;

        [Header("?¤ìŒ ?œì²´ ?¨ê³„")]
        [Tooltip("?¤ìŒ ?¨ê³„ ?œì²´ ID (ë§ˆì?ë§??¨ê³„ë©?ë¹„ì›Œ?ê±°??null)")]
        public string nextCorpseId;
    }

    [Header("Corpse Prefabs")]
    public CorpsePrefabDef[] corpsePrefabs;

    [Header("Corpse Process Rules")]
    public CorpseProcessDef[] processDefs;

    [Header("Drops")]
    [Tooltip("corpseIdë¥??¤ë¡œ ?¬ìš©?˜ëŠ” ?œë ?Œì´ë¸”ì„ ê°€ì§?ItemDropper")]
    public ItemDropper itemDropper;

    [Header("Entity System")]
    [Tooltip("?œì²´ë¥?EntityManager???±ë¡?˜ê¸° ?„í•œ ì°¸ì¡° (ë¹„ì›Œ?ë©´ ?ë™ ê²€??")]
    public EntityManager entityManager;

    void Awake()
    {
        if (entityManager == null)
            entityManager = FindObjectOfType<EntityManager>();
    }

    /// <summary>
    /// corpseId ???€?‘í•˜???œì²´ ?„ë¦¬?¹ì„ ?´ë‹¹ ?„ì¹˜???¤í°?˜ê³  Corpse ì»´í¬?ŒíŠ¸ë¥?ë°˜í™˜.
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
            Debug.LogWarning($"[CorpseLibrary] corpseId='{corpseId}' ???´ë‹¹?˜ëŠ” ?„ë¦¬?¹ì´ ?†ìŒ.");
            return null;
        }

        var go = Instantiate(def.prefab, new Vector3(position.x, position.y, 0f), Quaternion.identity);
        var corpse = go.GetComponent<Corpse>();
        if (corpse == null)
        {
            Debug.LogError($"[CorpseLibrary] ?„ë¦¬??'{def.prefab.name}' ??Corpse ì»´í¬?ŒíŠ¸ê°€ ?†ìŒ.");
            return null;
        }

        corpse.CorpseId       = corpseId;
        corpse.CorpsePosition = position;

        // EntityManager???±ë¡ (?¸ì´ë¸?ë¡œë“œ ?€?ì´ ?˜ë„ë¡?
        if (entityManager != null)
            entityManager.Register(corpse);

        return corpse;
    }

    /// <summary>
    /// ?œì²´ ?„ì— ?´ì•¡?˜ì„ ?¬ìš©?ˆì„ ??ê°€ê³??œë„.
    /// - corpseId + toolActionName ì¡°í•©?¼ë¡œ ?ˆì‹œ?¼ë? ì°¾ì•„ ì²˜ë¦¬.
    /// - ?±ê³µ ?? ?„ì¬ ?œì²´ ?œë ???„ì¬ ?œì²´ ?œê±° ???„ìš”?˜ë©´ ?¤ìŒ ?¨ê³„ ?œì²´ ?¤í°.
    /// </summary>
    /// <param name="corpse">?€???œì²´</param>
    /// <param name="toolActionName">???¡ì…˜ ?´ë¦„ (?? "Scraping")</param>
    /// <returns>ê°€ê³µì´ ?¤ì œë¡??¼ì–´?¬ìœ¼ë©?true, ?„ë‹ˆ?¼ë©´ false</returns>
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

        // 1) ?œë: ???œì²´ ?¨ê³„??corpseIdë¥??¤ë¡œ ?¬ìš©
        if (itemDropper != null)
        {
            Vector3 dropPos = new Vector3(pos.x, pos.y, 0f);
            itemDropper.SpawnDroppedItems(corpseId, dropPos);
        }

        // 2) ê¸°ì¡´ ?œì²´ ?œê±°
        Destroy(corpse.gameObject);

        // 3) ?¤ìŒ ?¨ê³„ ?œì²´ ?¤í° (?ˆìœ¼ë©?
        if (!string.IsNullOrEmpty(proc.nextCorpseId))
        {
            SpawnCorpse(proc.nextCorpseId, pos);
        }

        return true;
    }
}
