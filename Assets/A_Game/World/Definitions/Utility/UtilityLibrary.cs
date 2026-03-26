// UtilityLibrary.cs
// - ATT_Cogwheel / ATT_Belt / ATT_Source 瑜??뚯떛?댁꽌 罹먯떛?섍퀬,
// - cellName ?ㅻ줈 TryGet 議고쉶留??쒓났?쒕떎.

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace Game.World
{
    public sealed class UtilityLibrary : MonoBehaviour
{
    [Header("Utility Definition Json Files")]
    public TextAsset attCogwheel; // ATT_Cogwheel.json
    public TextAsset attBelt;     // ATT_Belt.json
    public TextAsset attSource;   // ATT_Source.json

    [Header("Future Utility Definition Json Files")]
    public TextAsset attWire;     // ATT_Wire.json
    public TextAsset attPipe;     // ATT_Pipe.json
    public TextAsset attTube;     // ATT_Tube.json

    Dictionary<string, CogwheelDef> _cogwheelByCell;
    Dictionary<string, BeltDef> _beltByCell;
    Dictionary<string, SourceDef> _sourceByCell;

    [Serializable]
    public struct CogwheelDef
    {
        public string size; // "Small" / "Big"
        public int maxRpm;
    }

    [Serializable]
    public struct BeltDef
    {
        public int maxRpm;
        public string materialItemId;
        public float[] color; // [r,g,b,a]
    }

    [Serializable]
    public struct SourceDef
    {
        public int rpm;
        public int stressCapacity;
    }

    void Awake()
    {
        Reload();
    }

    public void Reload()
    {
        _cogwheelByCell = ParseDict<CogwheelDef>(attCogwheel, "ATT_Cogwheel");
        _beltByCell = ParseDict<BeltDef>(attBelt, "ATT_Belt");
        _sourceByCell = ParseDict<SourceDef>(attSource, "ATT_Source");
    }

    public bool TryGetCogwheel(string cellName, out CogwheelDef def)
    {
        def = default;
        return _cogwheelByCell != null && _cogwheelByCell.TryGetValue(cellName, out def);
    }

    public bool TryGetBelt(string cellName, out BeltDef def)
    {
        def = default;
        return _beltByCell != null && _beltByCell.TryGetValue(cellName, out def);
    }

    public bool TryGetSource(string cellName, out SourceDef def)
    {
        def = default;
        return _sourceByCell != null && _sourceByCell.TryGetValue(cellName, out def);
    }

    Dictionary<string, T> ParseDict<T>(TextAsset ta, string label)
    {
        if (ta == null || string.IsNullOrEmpty(ta.text))
            return new Dictionary<string, T>(0);

        try
        {
            var dict = JsonConvert.DeserializeObject<Dictionary<string, T>>(ta.text);
            return dict ?? new Dictionary<string, T>(0);
        }
        catch (Exception ex)
        {
            Debug.LogError($"[UtilityLibrary] Failed to parse {label}: {ex.Message}");
            return new Dictionary<string, T>(0);
        }
    }
}
}
