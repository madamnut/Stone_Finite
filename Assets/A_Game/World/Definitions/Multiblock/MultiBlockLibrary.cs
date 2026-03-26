// MultiblockLibrary.cs (?꾩껜 援먯껜蹂? - JSON row0(留??? -> ?대? y=0(留??꾨옒)濡??뚯떛 ??Y ?ㅼ쭛湲?
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// 硫?곕툝???⑦꽩/寃곌낵 ?뺤쓽 + ?щ즺(? ?대쫫) ??硫?곕툝???꾨낫 ??“???꾨떞.
/// (?뺥솗 留ㅼ묶: pattern??null/鍮덈Ц?먯뿴/?꾨씫 ?덉슜?섏? ?딆쓬)
///
/// JSON ?щ㎎(??:
/// {
///   "Clay Kiln": [
///     {
///       "pattern": [
///         ["Clay", "Clay"],
///         ["Clay", "Clay"]
///       ],
///       "result": [
///         ["Clay Kiln_TL", "Clay Kiln_TR"],
///         ["Clay Kiln_BL", "Clay Kiln_BR"]
///       ]
///     }
///   ]
/// }
/// </summary>
namespace Game.World
{
public class MultiblockLibrary : MonoBehaviour
{
    [Header("Multiblock Json")]
    [Tooltip("硫?곕툝???뺤쓽 JSON (?? Clay Kiln ?⑦꽩/寃곌낵 ??")]
    public TextAsset multiblockJson;

    public class Def
    {
        public string    key;
        public int       width;
        public int       height;
        public string[,] pattern; // pattern[x,y] where y=0 is bottom
        public string[,] result;  // result[x,y] where y=0 is bottom
    }

    static bool _initialized;
    static readonly List<Def> _defs = new List<Def>();
    static readonly Dictionary<string, List<Def>> _byIngredient = new Dictionary<string, List<Def>>();
    static readonly Dictionary<string, List<Def>> _byKey = new Dictionary<string, List<Def>>();

    void Awake()
    {
        if (_initialized) return;

        if (multiblockJson == null || string.IsNullOrEmpty(multiblockJson.text))
        {
            Debug.LogError("[MultiblockLibrary] multiblockJson ??鍮꾩뼱?덉뒿?덈떎.");
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
            string defKey = prop.Name;

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

                // ??JSON? row0=留??? ?대? 諛곗뿴? y=0=留??꾨옒濡??곌린 ?꾪빐 Y瑜??ㅼ쭛?댁꽌 ??ν븳??
                for (int jsonY = 0; jsonY < height; jsonY++)
                {
                    int y = (height - 1 - jsonY);

                    var prow = (JArray)patternArr[jsonY];
                    var rrow = (JArray)resultArr[jsonY];

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

                // ingredient index: pattern ?대???? ?대쫫 ?꾨? ?щ즺濡??깅줉
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

    public static List<Def> GetByKey(string defKey)
    {
        if (string.IsNullOrEmpty(defKey))
            return null;

        if (_byKey.TryGetValue(defKey, out var list))
            return list;

        return null;
    }

    public static IReadOnlyList<Def> AllDefs => _defs;
}
}
