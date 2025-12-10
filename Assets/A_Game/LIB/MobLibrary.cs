using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mob 프리팹 레지스트리 + 스폰 헬퍼.
/// mobId ↔ Mob 프리팹 매핑.
/// 시체 corpseId 는 mobId + "_Corpse" 규칙으로 자동 생성.
/// </summary>
public class MobLibrary : MonoBehaviour
{
    [System.Serializable]
    public class MobEntry
    {
        [Tooltip("슬라임, Cow, Bird_White 같은 고유 ID (세이브/코드에서 사용)")]
        public string mobId;

        [Tooltip("실제 Mob 프리팹 (Mob : Entity 상속)")]
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
                Debug.LogWarning($"[MobLibrary] 중복 mobId='{e.mobId}' 무시됨.");
                continue;
            }

            _byId.Add(e.mobId, e);
        }
    }

    /// <summary>mobId로 프리팹 반환</summary>
    public Mob GetPrefab(string mobId)
    {
        if (string.IsNullOrEmpty(mobId) || _byId == null)
            return null;

        return _byId.TryGetValue(mobId, out var entry) ? entry.prefab : null;
    }

    /// <summary>
    /// mobId 기준 스폰 + corpseId 자동 부여.
    /// corpseId 규칙: mobId + "_Corpse"
    /// </summary>
    public Mob SpawnMob(string mobId, Vector3 position, EntityManager entityManager, Transform parentOverride = null)
    {
        if (_byId == null)
            BuildDictionary();

        if (_byId == null || !_byId.TryGetValue(mobId, out var entry) || entry.prefab == null)
        {
            Debug.LogWarning($"[MobLibrary] mobId='{mobId}' 에 해당하는 프리팹을 찾지 못했습니다.");
            return null;
        }

        Transform parent = parentOverride != null ? parentOverride : transform;

        Mob inst = Instantiate(entry.prefab, position, Quaternion.identity, parent);

        if (inst != null)
        {
            // MobId 보정
            if (string.IsNullOrEmpty(inst.MobId))
                inst.MobId = mobId;

            // 자동 시체 ID 지정
            string corpseId = mobId + "_Corpse";
            inst.SetCorpseId(corpseId);

            // 엔티티 등록
            if (entityManager != null)
                entityManager.Register(inst);
        }

        return inst;
    }
}
