using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

using Game.Data;
using Game.Core;

namespace Game.UI
{
    public partial class CrucibleView : MonoBehaviour
    {
        [Header("Deps")]
        public ItemLibrary itemLibrary;
    
        [Header("UI")]
        public RectTransform contentRoot;
        public GameObject layerPrefab;
        public float minLayerHeight = 2f;
    
        ItemData _crucibleItem;
        int _capacityCached;
        List<object> _layersListRef;
    
        int _prevCapacity;
        int _prevSum;
        string _prevSig;
    
        public void BindCrucible(ItemData crucibleItem)
        {
            _crucibleItem = crucibleItem;
            Refresh();
        }
    
        public void Refresh()
        {
            if (_crucibleItem == null || _crucibleItem.Count <= 0)
            {
                _capacityCached = 0;
                _layersListRef = null;
                Clear();
                return;
            }
    
            int cap = ReadCrucibleCapacity(_crucibleItem);
            _capacityCached = cap;
    
            if (cap <= 0)
            {
                _layersListRef = null;
                Clear();
                return;
            }
    
            // ??????? Details["layers"]???袁⑸즵?쀫쓧???List<object>?????????⑤똾留??袁⑸즴甕겸넃???釉먮폇??
            _layersListRef = EnsureLayersListRef(_crucibleItem);
    
            var layers = NormalizeLayers(_layersListRef); // bottom->top
            int sum = SumAmount(layers);
            string sig = BuildSignature(layers);
    
            if (cap == _prevCapacity && sum == _prevSum && sig == _prevSig)
                return;
    
            ForceRefresh(cap, layers, sum, sig);
        }
    
        public void Clear()
        {
            if (contentRoot != null)
            {
                for (int i = contentRoot.childCount - 1; i >= 0; i--)
                    Destroy(contentRoot.GetChild(i).gameObject);
            }
    
            _prevCapacity = 0;
            _prevSum = 0;
            _prevSig = null;
        }
    
        public bool BringLayerToTop(int layerIndexInCrucible)
        {
            if (_crucibleItem == null) return false;
            if (_layersListRef == null) return false;
            if (_capacityCached <= 0) return false;
    
            if ((uint)layerIndexInCrucible >= (uint)_layersListRef.Count) return false;
            if (layerIndexInCrucible == _layersListRef.Count - 1) return false;
    
            var moved = _layersListRef[layerIndexInCrucible];
            _layersListRef.RemoveAt(layerIndexInCrucible);
            _layersListRef.Add(moved);
    
            _prevSig = null;
            Refresh();
            return true;
        }
    
        #if false
        void ForceRefresh(int capacity, List<(string itemId, int amount)> layers, int sum, string sig)
        {
            Clear();
    
            if (contentRoot == null || layerPrefab == null || itemLibrary == null) return;
            if (layers == null || layers.Count == 0) return;
    
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);
    
            float parentW = contentRoot.rect.width;
            float parentH = contentRoot.rect.height;
    
            if (parentW <= 0.01f) parentW = ((RectTransform)transform).rect.width;
            if (parentH <= 0.01f) parentH = ((RectTransform)transform).rect.height;
    
            // layers bottom->top, UI??top->bottom ??獄쏅똻??
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var (itemId, amount) = layers[i];
                if (string.IsNullOrEmpty(itemId) || amount <= 0) continue;
    
                float h = parentH * ((float)amount / capacity);
                if (minLayerHeight > 0f && h < minLayerHeight) h = minLayerHeight;
    
                var go = Instantiate(layerPrefab, contentRoot, false);
    
                var rt = go.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    rt.pivot = new Vector2(0.5f, 0.5f);
    
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parentW);
                    rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
                }
    
                var le = go.GetComponent<LayoutElement>();
                if (le != null)
                {
                    le.minWidth = parentW;
                    le.preferredWidth = parentW;
                    le.flexibleWidth = 0f;
    
                    le.minHeight = h;
                    le.preferredHeight = h;
                    le.flexibleHeight = 0f;
                }
    
                var img = go.GetComponentInChildren<Image>(true);
                if (img != null)
                {
                    img.sprite = itemLibrary.GetSprite(itemId);
                    img.enabled = (img.sprite != null);
                    img.preserveAspect = true;
                }
    
                var smelts = go.GetComponent<CrucibleSmelts>();
                if (smelts != null)
                    smelts.Init(this, i, itemId, amount);
            }
    
            _prevCapacity = capacity;
            _prevSum = sum;
            _prevSig = sig;
        }
    
        // ????????關履????딅텑??? IList ??? ?袁⑸즵?룸돁???List<object>????????SetDetail?????怨뺣빰 ?袁⑸즴甕곗떓逾?
        List<object> EnsureLayersListRef(ItemData c)
        {
            object lo = null;
            if (c.Details != null)
                c.Details.TryGetValue("layers", out lo);
    
            // ???⑤챶?뺧┼???獄쏅똻??
            if (lo == null)
            {
                var created = new List<object>();
                c.SetDetail("layers", created);
                return created;
            }
    
            // ???? List<object>
            if (lo is List<object> listObj)
                return listObj;
    
            // JArray -> List<object>
            if (lo is JArray ja)
            {
                var converted = new List<object>(ja.Count);
                for (int i = 0; i < ja.Count; i++) converted.Add(ja[i]); // JObject ???
                c.SetDetail("layers", converted);
                return converted;
            }
    
            // ??????딅텑???????癲꾧퀗?э㎖?? List<Dictionary<...>> / List<JObject> / ??れ삀?? IList
            if (lo is IList ilist && lo is not string)
            {
                var converted = new List<object>(ilist.Count);
                for (int i = 0; i < ilist.Count; i++)
                    converted.Add(ilist[i]);
    
                c.SetDetail("layers", converted);
                return converted;
            }
    
            // ???⑤?彛??????怨룻꼧癲???????
            var fallback = new List<object>();
            c.SetDetail("layers", fallback);
            return fallback;
        }
    
        int ReadCrucibleCapacity(ItemData c)
        {
            if (c == null) return 0;
            if (c.ToolActions == null) return 0;
            if (!c.ToolActions.TryGetValue("Crucible", out Dictionary<string, object> cfg) || cfg == null) return 0;
            if (!cfg.TryGetValue("capacity", out var capObj) || capObj == null) return 0;
    
            if (capObj is int i) return i;
            if (capObj is long l) return (int)l;
            if (capObj is float f) return Mathf.RoundToInt(f);
            if (capObj is double d) return (int)d;
            return int.TryParse(capObj.ToString(), out int r) ? r : 0;
        }
    
        static int SumAmount(List<(string itemId, int amount)> layers)
        {
            if (layers == null) return 0;
            int s = 0;
            for (int i = 0; i < layers.Count; i++) s += layers[i].amount;
            return s;
        }
    
        static string BuildSignature(List<(string itemId, int amount)> layers)
        {
            if (layers == null || layers.Count == 0) return "";
            var sb = new System.Text.StringBuilder(128);
            for (int i = 0; i < layers.Count; i++)
                sb.Append(layers[i].itemId).Append(':').Append(layers[i].amount).Append('|');
            return sb.ToString();
        }
    
        List<(string itemId, int amount)> NormalizeLayers(List<object> layersList)
        {
            if (layersList == null) return null;

            var result = new List<(string, int)>();

            for (int i = 0; i < layersList.Count; i++)
            {
                string id = null;
                int amt = 0;

                var obj = layersList[i];

                if (obj is JObject jo)
                {
                    id = (jo["itemId"] ?? jo["fluidId"])?.ToString();
                    amt = jo["amount"] != null ? jo["amount"].Value<int>() : 0;
                }
                else if (obj is Dictionary<string, object> d)
                {
                    if (d.TryGetValue("itemId", out var idObj) && idObj != null) id = idObj.ToString();
                    else if (d.TryGetValue("fluidId", out var fidObj) && fidObj != null) id = fidObj.ToString();

                    if (d.TryGetValue("amount", out var aObj) && aObj != null)
                    {
                        if (aObj is int ii) amt = ii;
                        else if (aObj is long ll) amt = (int)ll;
                        else if (aObj is float ff) amt = Mathf.RoundToInt(ff);
                        else if (aObj is double dd) amt = (int)dd;
                        else int.TryParse(aObj.ToString(), out amt);
                    }
                }

                if (!string.IsNullOrEmpty(id) && amt > 0)
                    result.Add((id, amt));
            }

            return result;
        }
        #endif
    }
}
