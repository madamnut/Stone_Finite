using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

[System.Serializable]
public class StructureTemplate
{
    [JsonProperty("anchor")]
    public Anchor anchor;

    [JsonProperty("layers")]
    public Layers layers;

    // key = 배치할 id (예: 2000, 2001)
    // 값 = 해당 id가 덮어쓸 수 있는 기존 타겟 id 목록
    // 예) { "2001": { "targets":[0] }, "2000": { "targets":[0,2001] } }
    [JsonProperty("writeRules")]
    public Dictionary<int, WriteRule> writeRules;
}

[System.Serializable]
public class Anchor
{
    [JsonProperty("x")] public int x;
    [JsonProperty("y")] public int y;
}

[System.Serializable]
public class Layers
{
    // 선택적. 현재 프로젝트는 FG의 deco만 사용.
    [JsonProperty("solid")] public int[][] solid;
    [JsonProperty("deco")]  public int[][] deco;
}

[System.Serializable]
public class WriteRule
{
    [JsonProperty("targets")] public int[] targets; // 덮어쓰기 허용 대상 id 집합
}

public static class StructureLoader
{
    // Resources/Structures/<name>.json 로드
    public static StructureTemplate Load(string name)
    {
        TextAsset ta = Resources.Load<TextAsset>($"Structures/{name}");
        if (ta == null)
        {
            Debug.LogError($"StructureLoader: not found Resources/Structures/{name}.json");
            return null;
        }
        return JsonConvert.DeserializeObject<StructureTemplate>(ta.text);
    }
}
