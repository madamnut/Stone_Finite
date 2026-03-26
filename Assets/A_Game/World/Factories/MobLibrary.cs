using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mob ??????덇텣?鰲????????????源낅젾??+ ??????⑤벡彛?????
/// mobId ??Mob ??????덇텣?鰲????饔낅떽??????筌??
/// ??????corpseId ??mobId + "_Corpse" ?????????????????????熬곣뫖利???
/// </summary>
namespace Game.World
{
public class MobLibrary : MonoBehaviour
{
    [System.Serializable]
    public class MobEntry
    {
        [Tooltip("Unique mob ID, for example Cow or Bird_White")]
        public string mobId;

        [Tooltip("Actual mob prefab. Mob must inherit from Entity.")]
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
                Debug.LogWarning($"[MobLibrary] Duplicate mobId='{e.mobId}' ignored.");
                continue;
            }

            _byId.Add(e.mobId, e);
        }
    }

    /// <summary>mobId????????덇텣?鰲???????썹땟戮녹????/summary>
    public Mob GetPrefab(string mobId)
    {
        if (string.IsNullOrEmpty(mobId) || _byId == null)
            return null;

        return _byId.TryGetValue(mobId, out var entry) ? entry.prefab : null;
    }

    /// <summary>
    /// mobId ??????? ??????⑤벡彛?+ corpseId ??????????뼿???
    /// corpseId ?????? mobId + "_Corpse"
    /// </summary>
    public Mob SpawnMob(string mobId, Vector3 position, EntityManager entityManager, Transform parentOverride = null)
    {
        if (_byId == null)
            BuildDictionary();

        if (_byId == null || !_byId.TryGetValue(mobId, out var entry) || entry.prefab == null)
        {
            Debug.LogWarning($"[MobLibrary] Could not find prefab for mobId='{mobId}'.");
            return null;
        }

        Transform parent = parentOverride != null ? parentOverride : transform;

        Mob inst = Instantiate(entry.prefab, position, Quaternion.identity, parent);

        if (inst != null)
        {
            // MobId ????쇰뮛????
            if (string.IsNullOrEmpty(inst.MobId))
                inst.MobId = mobId;

            // ???????????ID ?饔낅떽??????
            string corpseId = mobId + "_Corpse";
            inst.SetCorpseId(corpseId);

            // ??????????關?쒎첎?嫄??怨몃룯??
            if (entityManager != null)
                entityManager.Register(inst);
        }

        return inst;
    }
}
}
