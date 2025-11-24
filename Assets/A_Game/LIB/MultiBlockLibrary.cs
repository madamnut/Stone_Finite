using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

[DefaultExecutionOrder(-90)]
public class MultiblockLibrary : MonoBehaviour
{
    [Header("멀티블럭 레시피 JSON (예: ATT_Multiblock.json)")]
    [SerializeField] private TextAsset multiblockJson;

    public sealed class Definition
    {
        public string key;
        public string name;
        public int width;
        public int height;

        // 내부 좌표계: x = 0..width-1, y = 0..height-1 (y=0이 "아래")
        // 값은 셀 키 문자열 (예: "Mud", "MudFurnace_0")
        public string[,] pattern; 
        public string[,] result;
    }

    // 재료(셀 키) → 이 재료를 사용하는 모든 멀티블럭 정의
    static readonly Dictionary<string, List<Definition>> _byIngredient = new();

    static bool _initialized;

    void Awake()
    {
        if (_initialized) return;
        _initialized = true;

        _byIngredient.Clear();

        if (multiblockJson == null || string.IsNullOrWhiteSpace(multiblockJson.text))
        {
            Debug.LogWarning("[MultiblockLibrary] multiblockJson 이 비어있음.");
            return;
        }

        JObject root;
        try
        {
            root = JObject.Parse(multiblockJson.text);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[MultiblockLibrary] JSON 파싱 실패: {ex.Message}");
            return;
        }

        foreach (var prop in root.Properties())
        {
            string key = prop.Name; // 예: "MudFurnace"

            if (prop.Value is not JArray arr)
            {
                Debug.LogWarning($"[MultiblockLibrary] '{key}' 값이 배열이 아님. 스킵.");
                continue;
            }

            for (int i = 0; i < arr.Count; i++)
            {
                if (arr[i] is not JObject obj)
                {
                    Debug.LogWarning($"[MultiblockLibrary] '{key}'[{i}] 가 객체가 아님. 스킵.");
                    continue;
                }

                var def = ParseDefinition(key, obj);
                if (def == null) continue;

                RegisterIngredients(def);
            }
        }

#if UNITY_EDITOR
        Debug.Log($"[MultiblockLibrary] 로드 완료: 재료 인덱스 개수 = {_byIngredient.Count}");
#endif
    }

    static Definition ParseDefinition(string key, JObject obj)
    {
        string name = obj.Value<string>("name") ?? key;

        var patternToken = obj["pattern"] as JArray;
        var resultToken  = obj["result"]  as JArray;

        if (patternToken == null || resultToken == null)
        {
            Debug.LogWarning($"[MultiblockLibrary] '{key}' 에 pattern/result 가 없음. 스킵.");
            return null;
        }

        int hPat = patternToken.Count;
        if (hPat == 0)
        {
            Debug.LogWarning($"[MultiblockLibrary] '{key}' pattern 높이가 0. 스킵.");
            return null;
        }

        int wPat = (patternToken[0] as JArray)?.Count ?? 0;
        if (wPat == 0)
        {
            Debug.LogWarning($"[MultiblockLibrary] '{key}' pattern 폭이 0. 스킵.");
            return null;
        }

        // pattern 가로 길이 검증
        for (int ry = 0; ry < hPat; ry++)
        {
            if (patternToken[ry] is not JArray row || row.Count != wPat)
            {
                Debug.LogWarning($"[MultiblockLibrary] '{key}' pattern row {ry} 폭이 일치하지 않음. 스킵.");
                return null;
            }
        }

        int hRes = resultToken.Count;
        if (hRes != hPat)
        {
            Debug.LogWarning($"[MultiblockLibrary] '{key}' pattern/result 높이 불일치. 스킵.");
            return null;
        }

        // result 가로 길이 검증
        for (int ry = 0; ry < hRes; ry++)
        {
            if (resultToken[ry] is not JArray row || row.Count != wPat)
            {
                Debug.LogWarning($"[MultiblockLibrary] '{key}' result row {ry} 폭이 pattern 과 다름. 스킵.");
                return null;
            }
        }

        int width  = wPat;
        int height = hPat;

        var pattern = new string[width, height];
        var result  = new string[width, height];

        // JSON: 위→아래
        // 내부: 아래(y=0)→위 구조로 뒤집어서 저장
        for (int ry = 0; ry < height; ry++)
        {
            int y = (height - 1) - ry;

            var prow = (JArray)patternToken[ry];
            var rrow = (JArray)resultToken[ry];

            for (int x = 0; x < width; x++)
            {
                pattern[x, y] = prow[x]?.ToString();
                result[x, y]  = rrow[x]?.ToString();
            }
        }

        return new Definition
        {
            key     = key,
            name    = name,
            width   = width,
            height  = height,
            pattern = pattern,
            result  = result
        };
    }

    static void RegisterIngredients(Definition def)
    {
        // 같은 Definition에서 같은 재료를 중복 등록하지 않기 위한 집합
        HashSet<string> seen = new HashSet<string>();

        for (int y = 0; y < def.height; y++)
        {
            for (int x = 0; x < def.width; x++)
            {
                string cell = def.pattern[x, y];
                if (string.IsNullOrEmpty(cell)) continue;
                if (!seen.Add(cell)) continue; // 이미 이 def에서 등록한 재료면 스킵

                if (!_byIngredient.TryGetValue(cell, out var list))
                {
                    list = new List<Definition>();
                    _byIngredient[cell] = list;
                }
                list.Add(def);
            }
        }
    }

    // ────────────────────── 외부 API ──────────────────────
    // 이 라이브러리의 역할은 "이 셀 키가 들어가는 모든 멀티블럭 레시피 반환" 딱 하나.

    public static bool TryGetByIngredient(string cellKey, out IReadOnlyList<Definition> defs)
    {
        if (_byIngredient.TryGetValue(cellKey, out var list) && list != null && list.Count > 0)
        {
            defs = list;
            return true;
        }

        defs = null;
        return false;
    }
}
