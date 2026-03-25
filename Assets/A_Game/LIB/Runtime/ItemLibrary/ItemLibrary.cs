using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.U2D;


namespace Game.Data
{
    public partial class ItemLibrary : MonoBehaviour
    {
        [Header("Sprite Atlas (single-sprite lookup)")]
        public SpriteAtlas itemAtlas;
    
        public List<TextAsset> jsonFiles = new List<TextAsset>();
    
        // key: itemId, value: ?筌먦끉踰????沅?JObject)
        private Dictionary<string, JObject> allItemDict;
    
        // ???덈뒆??源녿턄??嶺?흮??
        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(128);
    
        public JObject GetItemJson(string itemId)
        {
            if (allItemDict.TryGetValue(itemId, out var obj))
                return obj;
    
            Debug.LogWarning($"[ItemLibrary] Item definition not found: {itemId}");
            return null;
        }
    
    }
}
