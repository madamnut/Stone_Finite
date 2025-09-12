using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;
using Newtonsoft.Json.Linq;

public enum CellType : byte { None, Solid, Liquid, Deco }

[DefaultExecutionOrder(-100)]
public class CellLibrary : MonoBehaviour
{
    [Header("스프라이트 아틀라스")]
    [SerializeField] private SpriteAtlas atlas;

    [Header("ATT_Cell.json (딕셔너리 형식)")]
    [SerializeField] private TextAsset cellJson;

    public struct CellDef
    {
        public string   name;
        public Sprite   sprite;
        public ushort   id;
        public CellType type;
        public bool     gravity;
        public DepFlags depend;
    }

    private static readonly Dictionary<ushort, CellDef> byId = new();
    private static readonly Dictionary<ushort, string>  idToKey = new();

    void Awake()
    {
        byId.Clear();
        idToKey.Clear();

        var root = JObject.Parse(cellJson.text);
        foreach (var pair in root)
        {
            string key = pair.Key;
            var obj = (JObject)pair.Value;

            ushort id      = (ushort)(obj["id"]?.Value<int>() ?? 0);
            string typeStr = obj["type"]?.Value<string>() ?? "none";

            CellType type = typeStr.ToLowerInvariant() switch
            {
                "solid"  => CellType.Solid,
                "liquid" => CellType.Liquid,
                "deco"   => CellType.Deco,
                _        => CellType.None
            };

            bool gravity = obj["gravity"]?.Value<bool>() ?? false;

            DepFlags depend = DepFlags.None;
            if (obj["depend"] is JArray deps)
            {
                foreach (var t in deps)
                {
                    switch ((t.Value<string>() ?? "").ToLowerInvariant())
                    {
                        case "background": depend |= DepFlags.Background; break;
                        case "up":   depend |= DepFlags.Up;   break;
                        case "down": depend |= DepFlags.Down; break;
                        case "left": depend |= DepFlags.Left; break;
                        case "right":depend |= DepFlags.Right;break;
                        default:
                            Debug.LogWarning($"CellLibrary: 알 수 없는 depend '{t}' @ {key}({id})");
                            break;
                    }
                }
            }

            var sp = atlas.GetSprite(key);
            if (sp == null)
                Debug.LogWarning($"CellLibrary: 스프라이트 '{key}'(ID {id}) 없음", this);

            if (byId.ContainsKey(id))
            {
                Debug.LogWarning($"CellLibrary: 중복 ID {id} ('{key}') 무시", this);
                continue;
            }

            byId.Add(id, new CellDef {
                name = key, sprite = sp, id = id, type = type, gravity = gravity, depend = depend
            });
            idToKey[id] = key;
        }
    }

    public static CellType TypeOf(ushort id)        => byId.TryGetValue(id, out var d) ? d.type    : CellType.None;
    public static bool     HasGravity(ushort id)     => byId.TryGetValue(id, out var d) && d.gravity;
    public static DepFlags DependFlagsOf(ushort id)  => byId.TryGetValue(id, out var d) ? d.depend : DepFlags.None;
    public static Sprite   GetSprite(ushort id)      => byId.TryGetValue(id, out var d) ? d.sprite  : null;
    public static string   GetName(ushort id)        => byId.TryGetValue(id, out var d) ? d.name    : $"Unknown_{id}";
    public static string   GetKey(ushort id)         => idToKey.TryGetValue(id, out var k) ? k : null;
}