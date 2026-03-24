using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mob ?„ë¦¬???ˆì??¤íŠ¸ë¦?+ ?¤í° ?¬í¼.
/// mobId ??Mob ?„ë¦¬??ë§¤í•‘.
/// ?œì²´ corpseId ??mobId + "_Corpse" ê·œì¹™?¼ë¡œ ?ë™ ?ì„±.
/// </summary>
using Game.World;
public class MobLibrary : MonoBehaviour
{
    [System.Serializable]
    public class MobEntry
    {
        [Tooltip("?¬ë¼?? Cow, Bird_White ê°™ì? ê³ ìœ  ID (?¸ì´ë¸?ì½”ë“œ?ì„œ ?¬ìš©)")]
        public string mobId;

        [Tooltip("?¤ì œ Mob ?„ë¦¬??(Mob : Entity ?ì†)")]
        public Mob prefab;
    }

    [Header("Mob List")]
    public MobEntry[] mobs;

    Dictionary<string, MobEntry> _byId;

    void Awake()
    {
        BuildDictionary();
    }

    void BuildDictionary()
    {
        _byId = new Dictionary<string, MobEntry>();
        if (mobs == null) return;

        for (int i = 0; i < mobs.Length; i++)
        {
            var e = mobs[i];
            if (e == null || string.IsNullOrEmpty(e.mobId) || e.prefab == null)
                continue;

            if (_byId.ContainsKey(e.mobId))
            {
                Debug.LogWarning($"[MobLibrary] ì¤‘ë³µ mobId='{e.mobId}' ë¬´ì‹œ??");
                continue;
            }

            _byId.Add(e.mobId, e);
        }
    }

    /// <summary>mobIdë¡??„ë¦¬??ë°˜í™˜</summary>
    public Mob GetPrefab(string mobId)
    {
        if (string.IsNullOrEmpty(mobId) || _byId == null)
            return null;

        return _byId.TryGetValue(mobId, out var entry) ? entry.prefab : null;
    }

    /// <summary>
    /// mobId ê¸°ì? ?¤í° + corpseId ?ë™ ë¶€??
    /// corpseId ê·œì¹™: mobId + "_Corpse"
    /// </summary>
    public Mob SpawnMob(string mobId, Vector3 position, EntityManager entityManager, Transform parentOverride = null)
    {
        if (_byId == null)
            BuildDictionary();

        if (_byId == null || !_byId.TryGetValue(mobId, out var entry) || entry.prefab == null)
        {
            Debug.LogWarning($"[MobLibrary] mobId='{mobId}' ???´ë‹¹?˜ëŠ” ?„ë¦¬?¹ì„ ì°¾ì? ëª»í–ˆ?µë‹ˆ??");
            return null;
        }

        Transform parent = parentOverride != null ? parentOverride : transform;

        Mob inst = Instantiate(entry.prefab, position, Quaternion.identity, parent);

        if (inst != null)
        {
            // MobId ë³´ì •
            if (string.IsNullOrEmpty(inst.MobId))
                inst.MobId = mobId;

            // ?ë™ ?œì²´ ID ì§€??
            string corpseId = mobId + "_Corpse";
            inst.SetCorpseId(corpseId);

            // ?”í‹°???±ë¡
            if (entityManager != null)
                entityManager.Register(inst);
        }

        return inst;
    }
}
