


using System;
using UnityEngine;







namespace Game.World
{
public class CorpseLibrary : MonoBehaviour
{
    [Serializable]
    public class CorpsePrefabDef
    {
        [Tooltip("Corpse ID (for example, Cow_Corpse or Cow_Corpse_Skinned)")]

        public string corpseId;

        [Tooltip("Prefab for this corpse ID. The prefab must include a Corpse component.")]
        public GameObject prefab;
    }

    [Serializable]
    public class CorpseProcessDef
    {
        [Tooltip("Current corpse ID (for example, Cow_Corpse)")]
        public string corpseId;

        [Tooltip("Tool action name (for example, Scraping, Cutting, Chopping)")]
        public string toolAction;

        [Header("Next Stage")]
        [Tooltip("Next stage corpse ID. Leave empty or null for the final stage.")]
        public string nextCorpseId;
    }

    [Header("Corpse Prefabs")]
    public CorpsePrefabDef[] corpsePrefabs;

    [Header("Corpse Process Rules")]
    public CorpseProcessDef[] processDefs;

    [Header("Drops")]
    [Tooltip("ItemDropper that uses corpseId as the drop-table key")]
    public ItemDropper itemDropper;

    [Header("Entity System")]
    [Tooltip("Reference used to register spawned corpses into EntityManager. Auto-detected if empty.")]
    public EntityManager entityManager;

    
    void Awake()
    {
        if (entityManager == null)
            entityManager = FindObjectOfType<EntityManager>();
    }

    
    
    
    
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
            Debug.LogWarning($"[CorpseLibrary] corpseId='{corpseId}' prefab not found.");
            return null;
        }

        var go = Instantiate(def.prefab, new Vector3(position.x, position.y, 0f), Quaternion.identity);
        var corpse = go.GetComponent<Corpse>();
        if (corpse == null)
        {
            Debug.LogError($"[CorpseLibrary] Prefab '{def.prefab.name}' is missing a Corpse component.");
            return null;
        }

        corpse.CorpseId       = corpseId;
        corpse.CorpsePosition = position;

        
        if (entityManager != null)
            entityManager.Register(corpse);

        return corpse;
    }

    
    
    
    
    
    
    
    
    
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

        
        if (itemDropper != null)
        {
            Vector3 dropPos = new Vector3(pos.x, pos.y, 0f);
            itemDropper.SpawnDroppedItems(corpseId, dropPos);
        }

        
        Destroy(corpse.gameObject);

        
        if (!string.IsNullOrEmpty(proc.nextCorpseId))
        {
            WorldEntityFactory.SpawnCorpse(this, proc.nextCorpseId, pos);
        }

        return true;
    }
}
}
