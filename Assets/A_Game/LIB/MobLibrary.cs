using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Mob 프리팹 레지스트리 + 스폰 헬퍼.
/// - mobId ↔ Mob 프리팹 매핑
/// - 코드/세이브 시스템은 mobId 문자열만 들고 있다가,
///   필요할 때 여기서 프리팹 찾아서 스폰.
/// </summary>
public class MobLibrary : MonoBehaviour
{
    [System.Serializable]
    public class MobEntry
    {
        [Tooltip("슬라임, Boar, Bird_White 같은 고유 ID (세이브/코드에서 사용)")]
        public string mobId;

        [Tooltip("실제 Mob 프리팹 (Mob : Entity 상속)")]
        public Mob prefab;
    }

    [Header("Mob List")]
    [Tooltip("mobId ↔ 프리팹 매핑 목록")]
    public MobEntry[] mobs;

    // 런타임용 빠른 조회용 딕셔너리
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


    /// <summary>
    /// mobId로 프리팹 반환 (읽기 전용).
    /// </summary>
    public Mob GetPrefab(string mobId)
    {
        if (string.IsNullOrEmpty(mobId) || _byId == null)
            return null;

        return _byId.TryGetValue(mobId, out var entry) ? entry.prefab : null;
    }


    /// <summary>
    /// mobId 기준 스폰 헬퍼.
    /// - prefab은 MobLibrary에서 찾고
    /// - EntityManager에 등록까지 한 번에 처리.
    /// </summary>
    /// <param name="mobId">Mob 고유 ID (세이브/AI/스폰에서 사용)</param>
    /// <param name="position">스폰 위치</param>
    /// <param name="entityManager">엔티티 매니저(등록용). null이면 등록 스킵</param>
    /// <param name="parentOverride">부모 Transform. null이면 MobLibrary 아래에 생성</param>
    public Mob SpawnMob(string mobId, Vector3 position, EntityManager entityManager, Transform parentOverride = null)
    {
        if (_byId == null || !_byId.TryGetValue(mobId, out var entry) || entry.prefab == null)
        {
            Debug.LogWarning($"[MobLibrary] mobId='{mobId}' 에 해당하는 프리팹을 찾지 못했습니다.");
            return null;
        }

        Transform parent = parentOverride != null ? parentOverride : transform;

        // 프리팹 인스턴스 생성
        Mob inst = Instantiate(entry.prefab, position, Quaternion.identity, parent);

        // MobId 보정 (프리팹에서 이미 세팅해뒀으면 그대로 쓰고, 비어있으면 라이브러리 기준으로 채움)
        if (inst != null && string.IsNullOrEmpty(inst.MobId))
            inst.MobId = mobId;

        // 엔티티 시스템에 등록
        if (inst != null && entityManager != null)
            entityManager.Register(inst);

        return inst;
    }
}
