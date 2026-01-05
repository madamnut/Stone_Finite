using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;

public class CrucibleView : MonoBehaviour
{
    [Header("Deps")]
    public ItemLibrary itemLibrary;

    [Header("UI")]
    public RectTransform contentRoot; // 레이어들이 들어갈 부모(VerticalLayoutGroup 있어도/없어도 됨)
    public GameObject layerPrefab;    // Smelt 시각 프리팹
    public float minLayerHeight = 2f; // 너무 얇아져서 안 보이는 것 방지(원하면 0)

    int _prevCapacity;
    int _prevSum;
    string _prevSig;

    /// <summary>
    /// CrucibleView는 "capacity + layers"만 받아서 시각화한다.
    /// layersObj 허용 타입:
    /// - JArray
    /// - List<object> (JObject / Dictionary)
    /// - List<(string itemId, int amount)>
    ///
    /// itemId == spriteName 규칙으로 itemLibrary.GetSprite(itemId) 사용
    /// </summary>
    public void SetData(int capacity, object layersObj)
    {
        if (capacity <= 0)
        {
            Clear();
            return;
        }

        var layers = NormalizeLayers(layersObj); // bottom -> top (last is top)
        int sum = SumAmount(layers);
        string sig = BuildSignature(layers);

        if (capacity == _prevCapacity && sum == _prevSum && sig == _prevSig)
            return;

        ForceRefresh(capacity, layers, sum, sig);
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

    void ForceRefresh(int capacity, List<(string itemId, int amount)> layers, int sum, string sig)
    {
        Clear();

        if (contentRoot == null || layerPrefab == null || itemLibrary == null) return;
        if (layers == null || layers.Count == 0) return;

        // 레이아웃 의존 없이 "부모 Rect" 기준으로 폭/높이를 직접 박기 위해
        // 먼저 캔버스/레이아웃을 1회 갱신한다.
        Canvas.ForceUpdateCanvases();
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRoot);

        float parentW = contentRoot.rect.width;
        float parentH = contentRoot.rect.height;

        if (parentW <= 0.01f) parentW = ((RectTransform)transform).rect.width;
        if (parentH <= 0.01f) parentH = ((RectTransform)transform).rect.height;

        // VerticalLayoutGroup: child index 0이 맨 위.
        // layers는 bottom->top 이므로, top->bottom 순서로 생성해서 위→아래가 되게 함.
        for (int i = layers.Count - 1; i >= 0; i--)
        {
            var (itemId, amount) = layers[i];
            if (string.IsNullOrEmpty(itemId) || amount <= 0) continue;

            float h = parentH * ((float)amount / capacity);
            if (minLayerHeight > 0f && h < minLayerHeight)
                h = minLayerHeight;

            var go = Instantiate(layerPrefab, contentRoot, false);

            var rt = go.GetComponent<RectTransform>();
            if (rt != null)
            {
                // ✅ 앵커/스트레치/레이아웃 영향 최소화: 중앙 고정 + size 직접 지정
                rt.anchorMin = new Vector2(0.5f, 0.5f);
                rt.anchorMax = new Vector2(0.5f, 0.5f);
                rt.pivot = new Vector2(0.5f, 0.5f);

                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, parentW);
                rt.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, h);
            }

            // ✅ VerticalLayoutGroup이 크기를 건드릴 수도 있으니 LayoutElement가 있으면 고정값을 넣어준다.
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

            // ✅ 아이콘은 itemId == spriteName 규칙으로 바로 조회
            var img = go.GetComponentInChildren<Image>(true);
            if (img != null)
            {
                img.sprite = itemLibrary.GetSprite(itemId);
                img.enabled = (img.sprite != null);
                img.preserveAspect = true;
            }
        }

        _prevCapacity = capacity;
        _prevSum = sum;
        _prevSig = sig;
    }

    // ────────── helpers ──────────

    static int SumAmount(List<(string itemId, int amount)> layers)
    {
        if (layers == null) return 0;
        int s = 0;
        for (int i = 0; i < layers.Count; i++)
            s += layers[i].amount;
        return s;
    }

    static string BuildSignature(List<(string itemId, int amount)> layers)
    {
        if (layers == null || layers.Count == 0) return "";

        var sb = new System.Text.StringBuilder(128);
        for (int i = 0; i < layers.Count; i++)
        {
            sb.Append(layers[i].itemId);
            sb.Append(':');
            sb.Append(layers[i].amount);
            sb.Append('|');
        }
        return sb.ToString();
    }

    List<(string itemId, int amount)> NormalizeLayers(object layersObj)
    {
        if (layersObj == null) return null;

        if (layersObj is List<(string itemId, int amount)> typed)
            return typed;

        var result = new List<(string, int)>();

        // JArray 케이스
        if (layersObj is JArray jarr)
        {
            for (int i = 0; i < jarr.Count; i++)
            {
                var jo = jarr[i] as JObject;
                if (jo == null) continue;

                string id = (jo["itemId"] ?? jo["fluidId"])?.ToString();
                int amt = jo["amount"] != null ? jo["amount"].Value<int>() : 0;

                if (!string.IsNullOrEmpty(id) && amt > 0)
                    result.Add((id, amt));
            }
            return result;
        }

        // List<object> 케이스 (JObject or Dictionary)
        if (layersObj is List<object> list)
        {
            for (int i = 0; i < list.Count; i++)
            {
                string id = null;
                int amt = 0;

                if (list[i] is JObject jo)
                {
                    id = (jo["itemId"] ?? jo["fluidId"])?.ToString();
                    amt = jo["amount"] != null ? jo["amount"].Value<int>() : 0;
                }
                else if (list[i] is Dictionary<string, object> d)
                {
                    if (d.TryGetValue("itemId", out var idObj) && idObj != null)
                        id = idObj.ToString();
                    else if (d.TryGetValue("fluidId", out var fidObj) && fidObj != null)
                        id = fidObj.ToString();

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

        return null;
    }
}
