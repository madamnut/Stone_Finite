// RecipeLibrary.cs (?꾩껜 援먯껜蹂?
// - isOrdered ?쒓굅, isShapeless(true/false)濡??듭씪
// - shapeless: inputs??"議댁옱?섎뒗 留뚰겮留? ?섏뿴(鍮덉뭏 null 遺덊븘??, filledCount == inputs.Count ???뚮쭔 留ㅼ묶
// - shaped(isShapeless=false):
//   * inputs??"?덉떆??寃⑹옄 ?ш린(2/4/9/16) 洹몃?濡? ?섏뿴 (鍮덉뭏? null濡??쒖떆)
//   * ?뚯쟾/?移?誘몃윭) ??긽 ?덉슜
//   * ???뚯씠釉?9/16)?먯꽌 ?묒? 寃⑹옄(2/4/9) ?덉떆?쇰뒗 "?щ씪?대뵫" 媛??
//   * ?뺤콉 A: ?덉떆??寃⑹옄 諛??덈룄??諛? ?щ’? ?꾨? null ?댁뼱??留ㅼ묶
//
// - ??2-slot shaped ?덉떆?쇰뒗 2x1(媛濡?濡?痍④툒?섎ŉ, "?좏삎 ?쒖옉踰??щ즺/????븷)" 蹂댄샇瑜??꾪빐 ?뚯쟾/?移?誘몃윭) 蹂?섏쓣 ?덉슜?섏? ?딆쓬.
//
// - ??outputActions ?좉퇋 吏??
//   * mul: ????꾨뱶(?꾩옱媛? *= (value/fromInput+inputField/fromField)
//   * floorInt: ????꾨뱶 = floor(????꾨뱶)
//   * roundInt: ????꾨뱶 = round(????꾨뱶)
//   * set: fromField 吏??(dst???꾨뱶/以묎컙 怨꾩궛媛믪쓣 蹂듭궗)

using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;


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
        public TextAsset alloyJson;   // ?⑷툑(?щ（?쒕툝) ?꾩슜
    
        [Header("Toolbench Jsons")]
        public TextAsset toolbenchJson; // Toolbench ?꾩슜 (candidates ?ㅽ궎留?
    
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
