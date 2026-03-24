using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.U2D;


namespace Game.Data
{
    public partial class ItemLibrary : MonoBehaviour
    {
        [Header("Sprite Atlas (?¨ì¼ ?¤í”„?¼ì´?¸ìš©)")]
        public SpriteAtlas itemAtlas;
    
        public List<TextAsset> jsonFiles = new List<TextAsset>();
    
        // key: itemId, value: ?•ì˜ ?ë³¸(JObject)
        private Dictionary<string, JObject> allItemDict;
    
        // ?¤í”„?¼ì´??ìºì‹œ
        private readonly Dictionary<string, Sprite> _spriteCache = new Dictionary<string, Sprite>(128);
    
        public JObject GetItemJson(string itemId)
        {
            if (allItemDict.TryGetValue(itemId, out var obj))
                return obj;
    
            Debug.LogWarning($"?„ì´???°ì´???†ìŒ: {itemId}");
            return null;
        }
    
    }
}
