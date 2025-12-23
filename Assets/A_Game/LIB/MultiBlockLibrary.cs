// MultiblockLibrary.cs
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 멀티블럭 패턴/결과 정의 + 재료(셀 이름) → 멀티블럭 후보 역조회 전담.
/// (정확 매칭: pattern에 null/빈문자열/누락 허용하지 않음)
///
/// JSON 포맷(예):
/// {
///   "Clay Kiln": [
///     {
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
    /// - width/height: 패턴 크기
    /// - pattern[x,y]: 요구 재료 셀 이름 (정확 매칭: null/빈문자열 불가)
    /// - result[x,y]: 완성 후 배치될 셀 이름 (null/빈문자열 → 변경 없음 허용)
    /// </summary>
    public class Def
    {
        public string    key;
        public int       width;
        public int       height;
        public string[,] pattern;
        public string[,] result;
    }

    // 정적 캐시 (게임 공통 1세트, 한 번만 로드)
    static bool _initialized;
    static readonly List<Def> _defs = new List<Def>();
    static readonly Dictionary<string, List<Def>> _byIngredient = new Dictionary<string, List<Def>>();
    static readonly Dictionary<string, List<Def>> _byKey = new Dictionary<string, List<Def>>();

    void Awake()
    {
        if (_initialized) return;

        if (multiblockJson == null || string.IsNullOrEmpty(multiblockJson.text))
        {
            Debug.LogError("[MultiblockLibrary] multiblockJson 이 비어있습니다.");
            return;
        }

        if (LoadFromJson(multiblockJson.text))
            _initialized = true;
    }

    static bool LoadFromJson(string json)
    {
        JObject root;
        try
        {
            root = JObject.Parse(json);
        }
        catch (System.Exception e)
        {
            Debug.LogError($"[MultiblockLibrary] JSON parse error: {e}");
            return false;
        }

        _defs.Clear();
        _byIngredient.Clear();
        _byKey.Clear();

        foreach (var prop in root.Properties())
        {
            string defKey = prop.Name; // 예: "Clay Kiln"

            var arr = prop.Value as JArray;
            if (arr == null || arr.Count == 0)
                continue;

            for (int i = 0; i < arr.Count; i++)
            {
                var entryObj = arr[i] as JObject;
                if (entryObj == null)
                    continue;

                var patternArr = entryObj["pattern"] as JArray;
                var resultArr  = entryObj["result"]  as JArray;

                if (patternArr == null || resultArr == null)
                {
                    Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' has no pattern/result.");
                    continue;
                }

                int height = patternArr.Count;
                if (height <= 0)
                {
                    Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' pattern has zero height.");
                    continue;
                }

                if (resultArr.Count != height)
                {
                    Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' result height mismatch. patternH={height}, resultH={resultArr.Count}");
                    continue;
                }

                var firstRow = patternArr[0] as JArray;
                if (firstRow == null)
                {
                    Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' pattern first row invalid.");
                    continue;
                }

                int width = firstRow.Count;
                if (width <= 0)
                {
                    Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' pattern has zero width.");
                    continue;
                }

                // Rect 검증 + pattern 필수값 검증
                bool invalid = false;

                for (int y = 0; y < height; y++)
                {
                    var prow = patternArr[y] as JArray;
                    var rrow = resultArr[y] as JArray;

                    if (prow == null || rrow == null)
                    {
                        Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' row {y} invalid.");
                        invalid = true;
                        break;
                    }

                    if (prow.Count != width)
                    {
                        Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' pattern row {y} width mismatch. expected={width}, got={prow.Count}");
                        invalid = true;
                        break;
                    }

                    if (rrow.Count != width)
                    {
                        Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' result row {y} width mismatch. expected={width}, got={rrow.Count}");
                        invalid = true;
                        break;
                    }

                    for (int x = 0; x < width; x++)
                    {
                        if (prow[x] == null)
                        {
                            Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' pattern[{x},{y}] is null (not allowed).");
                            invalid = true;
                            break;
                        }

                        string p = prow[x].ToString();
                        if (string.IsNullOrEmpty(p))
                        {
                            Debug.LogWarning($"[MultiblockLibrary] def '{defKey}' pattern[{x},{y}] is empty (not allowed).");
                            invalid = true;
                            break;
                        }
                    }

                    if (invalid) break;
                }

                if (invalid)
                    continue;

                var pattern = new string[width, height];
                var result  = new string[width, height];

                for (int y = 0; y < height; y++)
                {
                    var prow = (JArray)patternArr[y];
                    var rrow = (JArray)resultArr[y];

                    for (int x = 0; x < width; x++)
                    {
                        pattern[x, y] = prow[x].ToString();

                        string r = (rrow[x] != null) ? rrow[x].ToString() : null;
                        if (string.IsNullOrEmpty(r)) r = null;
                        result[x, y] = r;
                    }
                }

                var def = new Def
                {
                    key     = defKey,
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

                // ingredient index: pattern 내부의 셀 이름 전부 재료로 등록 (정확 매칭이므로 전부 유효)
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        string ingredient = pattern[x, y];

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
        return true;
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
