// MultiblockLibrary.cs
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 멀티블럭 패턴/결과 정의 + 재료(셀 이름) → 멀티블럭 후보 역조회 전담.
/// JSON 포맷(예):
/// {
///   "Clay Kiln": [
///     {
///       "name": "Clay Kiln",
///       "pattern": [
///         ["Clay"],
///         ["Clay"]
///       ],
///       "result": [
///         ["Clay Kiln_Top"],
///         ["Clay Kiln_Bottom"]
///       ]
///     }
///   ]
/// }
/// </summary>
public class MultiblockLibrary : MonoBehaviour
{
    [Header("Multiblock Json")]
    [Tooltip("멀티블럭 정의 JSON (예: Clay Kiln 패턴/결과 등)")]
    public TextAsset multiblockJson;

    /// <summary>
    /// 하나의 멀티블럭 정의.
    /// - key: JSON 최상단 키 (예: "Clay Kiln")
    /// - name: 표시용 이름
    /// - width/height: 패턴 크기
    /// - pattern[x,y]: 요구 재료 셀 이름 (null/빈문자열 → 와일드카드)
    /// - result[x,y]: 완성 후 배치될 셀 이름 (null/빈문자열 → 변경 없음)
    /// </summary>
    public class Def
    {
        public string   key;
        public string   name;
        public int      width;
        public int      height;
        public string[,] pattern;
        public string[,] result;
    }

    // 정적 캐시 (한 번만 로드)
    static bool _initialized;
    static readonly List<Def> _defs         = new List<Def>();
    static readonly Dictionary<string, List<Def>> _byIngredient = new Dictionary<string, List<Def>>();
    static readonly Dictionary<string, List<Def>> _byKey        = new Dictionary<string, List<Def>>();

    void Awake()
    {
        // 이미 다른 씬/오브젝트에서 초기화되었으면 스킵
        if (_initialized) return;
        _initialized = true;

        if (multiblockJson == null || string.IsNullOrEmpty(multiblockJson.text))
        {
            Debug.LogError("[MultiblockLibrary] multiblockJson 이 비어있습니다.");
            return;
        }

        LoadFromJson(multiblockJson.text);
    }

    static void LoadFromJson(string json)
    {
        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MultiblockLibrary] JSON parse error: {e}");
            return;
        }

        _defs.Clear();
        _byIngredient.Clear();
        _byKey.Clear();

        foreach (var prop in root.Properties())
        {
            string defKey = prop.Name;      // 예: "Clay Kiln"
            var arr = prop.Value as JArray;
            if (arr == null)
                continue;

            foreach (var entryToken in arr)
            {
                var entryObj = entryToken as JObject;
                if (entryObj == null)
                    continue;

                string name = entryObj.Value<string>("name") ?? defKey;

                var patternArr = entryObj["pattern"] as JArray;
                var resultArr  = entryObj["result"]  as JArray;

                if (patternArr == null || resultArr == null)
                {
                    Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' has no pattern/result.");
                    continue;
                }

                int height = patternArr.Count;
                if (height == 0)
                {
                    Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' pattern has zero height.");
                    continue;
                }

                var firstRow = patternArr[0] as JArray;
                if (firstRow == null)
                {
                    Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' pattern first row invalid.");
                    continue;
                }

                int width = firstRow.Count;
                if (width == 0)
                {
                    Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' pattern has zero width.");
                    continue;
                }

                var pattern = new string[width, height];
                var result  = new string[width, height];

                for (int y = 0; y < height; y++)
                {
                    var prow = patternArr[y] as JArray;
                    var rrow = resultArr[y]  as JArray;

                    if (prow == null || rrow == null)
                    {
                        Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' row {y} invalid.");
                        continue;
                    }

                    for (int x = 0; x < width; x++)
                    {
                        string p = (x < prow.Count && prow[x] != null) ? prow[x].ToString() : null;
                        string r = (x < rrow.Count && rrow[x] != null) ? rrow[x].ToString() : null;

                        if (string.IsNullOrEmpty(p)) p = null;
                        if (string.IsNullOrEmpty(r)) r = null;

                        pattern[x, y] = p;
                        result[x,  y] = r;
                    }
                }

                var def = new Def
                {
                    key     = defKey,
                    name    = name,
                    width   = width,
                    height  = height,
                    pattern = pattern,
                    result  = result
                };

                _defs.Add(def);

                if (!_byKey.TryGetValue(defKey, out var listByKey))
                {
                    listByKey = new List<Def>();
                    _byKey.Add(defKey, listByKey);
                }
                listByKey.Add(def);

                // ingredient index: pattern 내부에서 null/빈칸 아닌 셀 이름 전부 재료로 등록
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        string ingredient = pattern[x, y];
                        if (string.IsNullOrEmpty(ingredient))
                            continue;

                        if (!_byIngredient.TryGetValue(ingredient, out var list))
                        {
                            list = new List<Def>();
                            _byIngredient.Add(ingredient, list);
                        }
                        if (!list.Contains(def))
                            list.Add(def);
                    }
                }
            }
        }

#if UNITY_EDITOR
        Debug.Log($"[MultiblockLibrary] Loaded defs={_defs.Count}, ingredients={_byIngredient.Count}, keys={_byKey.Count}");
#endif
    }

    /// <summary>
    /// 특정 재료 셀 이름을 포함하는 멀티블럭 후보 정의 목록을 가져온다.
    /// InteractionController.HandleBuildMultiblock 에서 사용.
    /// </summary>
    public static bool TryGetByIngredient(string ingredientCellKey, out List<Def> defs)
    {
        if (string.IsNullOrEmpty(ingredientCellKey))
        {
            defs = null;
            return false;
        }

        if (_byIngredient.TryGetValue(ingredientCellKey, out var list) &&
            list != null && list.Count > 0)
        {
            defs = list;
            return true;
        }

        defs = null;
        return false;
    }

    /// <summary>
    /// 멀티블럭 key(JSON 최상단 키)로 정의 목록 조회.
    /// (하나의 key 아래 여러 변형이 있을 수 있음.)
    /// </summary>
    public static List<Def> GetByKey(string defKey)
    {
        if (string.IsNullOrEmpty(defKey))
            return null;

        if (_byKey.TryGetValue(defKey, out var list))
            return list;

        return null;
    }

    /// <summary>전체 정의 열람이 필요할 때 사용.</summary>
    public static IReadOnlyList<Def> AllDefs => _defs;
}
