// RecipeLibrary.cs (?„ì²´ êµì²´ë³?
// - isOrdered ?œê±°, isShapeless(true/false)ë¡??µì¼
// - shapeless: inputs??"ì¡´ì¬?˜ëŠ” ë§Œí¼ë§? ?˜ì—´(ë¹ˆì¹¸ null ë¶ˆí•„??, filledCount == inputs.Count ???Œë§Œ ë§¤ì¹­
// - shaped(isShapeless=false):
//   * inputs??"?ˆì‹œ??ê²©ì ?¬ê¸°(2/4/9/16) ê·¸ë?ë¡? ?˜ì—´ (ë¹ˆì¹¸?€ nullë¡??œì‹œ)
//   * ?Œì „/?€ì¹?ë¯¸ëŸ¬) ??ƒ ?ˆìš©
//   * ???Œì´ë¸?9/16)?ì„œ ?‘ì? ê²©ì(2/4/9) ?ˆì‹œ?¼ëŠ” "?¬ë¼?´ë”©" ê°€??
//   * ?•ì±… A: ?ˆì‹œ??ê²©ì ë°??ˆë„??ë°? ?¬ë¡¯?€ ?„ë? null ?´ì–´??ë§¤ì¹­
//
// - ??2-slot shaped ?ˆì‹œ?¼ëŠ” 2x1(ê°€ë¡?ë¡?ì·¨ê¸‰?˜ë©°, "? í˜• ?œì‘ë²??¬ë£Œ/????• )" ë³´í˜¸ë¥??„í•´ ?Œì „/?€ì¹?ë¯¸ëŸ¬) ë³€?˜ì„ ?ˆìš©?˜ì? ?ŠìŒ.
//
// - ??outputActions ? ê·œ ì§€??
//   * mul: ?€???„ë“œ(?„ì¬ê°? *= (value/fromInput+inputField/fromField)
//   * floorInt: ?€???„ë“œ = floor(?€???„ë“œ)
//   * roundInt: ?€???„ë“œ = round(?€???„ë“œ)
//   * set: fromField ì§€??(dst???„ë“œ/ì¤‘ê°„ ê³„ì‚°ê°’ì„ ë³µì‚¬)

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;


using Game.World;
namespace Game.Data
{
    public partial class RecipeLibrary : MonoBehaviour
    {
        [Header("Deps")]
        public ItemLibrary itemLibrary;
    
        [Header("Recipe Jsons")]
        public TextAsset recipe2Json;  // 2-slot
        public TextAsset recipe4Json;  // 4-slot
        public TextAsset recipe9Json;  // 9-slot (Forge)
        public TextAsset recipe16Json; // 16-slot (Industrial)
    
        [Header("Alloy Jsons")]
        public TextAsset alloyJson;   // ?©ê¸ˆ(?¬ë£¨?œë¸”) ?„ìš©
    
        [Header("Toolbench Jsons")]
        public TextAsset toolbenchJson; // Toolbench ?„ìš© (candidates ?¤í‚¤ë§?
    
        JArray _r2;
        JArray _r4;
        JArray _r9;
        JArray _r16;
    
        JArray _toolbench;
    
        class AlloyEntry
        {
            public readonly List<(string id, int amount)> inputs = new List<(string, int)>();
            public string outId;
            public int outAmount;
        }
        readonly List<AlloyEntry> _alloys = new List<AlloyEntry>();
    }
}
