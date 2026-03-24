using System;
using UnityEngine;
using Newtonsoft.Json;

/// <summary>
/// ?쒖껜 ?뷀떚??
/// - corpseId + ?꾩튂留????濡쒕뱶
/// </summary>
using Game.World;
using Game.Player;
public class Corpse : Entity
{
    [Header("Corpse Info")]
    [SerializeField] private string corpseId;

    // ?몄씠釉?濡쒕뱶???쇰━ ?꾩튂(?붾뱶 醫뚰몴)
    [SerializeField] private Vector2 corpsePosition;

    /// <summary>?쒖껜 醫낅쪟 ?앸퀎??ID (?? "Cow_Corpse")</summary>
    public string CorpseId
    {
        get => corpseId;
        set => corpseId = value;
    }

    /// <summary>
    /// ?쒖껜???쇰━ ?꾩튂.
    /// ?ㅼ젙 ??transform.position ???④퍡 媛깆떊.
    /// </summary>
    public Vector2 CorpsePosition
    {
        get => corpsePosition;
        set
        {
            corpsePosition = value;
            transform.position = new Vector3(value.x, value.y, transform.position.z);
        }
    }

    public override EntityKind Kind => EntityKind.Corpse;

    // ?????????????????????????????????????????????
    //   ?몃쾭 ?섏씠?쇱씠??(?쒖껜 ?꾩뿉 留덉슦???щ졇????
    // ?????????????????????????????????????????????

    [Header("Hover Highlight")]
    [Tooltip("?뺣젹 諛??몃쾭??硫붿씤 ?ㅽ봽?쇱씠??(?먯떇 SpriteRenderer ????")]
    public SpriteRenderer mainRenderer;

    [Tooltip("?쇰쭏???대몼寃?留뚮뱾吏 (0 = 洹몃?濡? 1 = ?꾩쟾 寃??")]
    [Range(0f, 1f)] public float hoverDarkenFactor = 0.6f;

    [Tooltip("?대몼寃뚢넂諛앷쾶 ???뺣났 二쇨린(珥?")]
    [Range(0.1f, 5f)] public float hoverPeriod = 1.0f;

    Color _baseColor = Color.white;
    bool _isHovered;
    Coroutine _hoverCo;

    void OnEnable()
    {
        CacheBaseColor();
        StopHoverImmediate();
    }

    void OnDisable()
    {
        StopHoverImmediate();
    }

    void CacheBaseColor()
    {
        if (mainRenderer != null)
            _baseColor = mainRenderer.color;
        else
            _baseColor = Color.white;
    }

    void RestoreBaseColor()
    {
        if (mainRenderer == null) return;
        mainRenderer.color = _baseColor;
    }

    /// <summary>
    /// ?몃?(InteractionController ???먯꽌 ?몃쾭 ?곹깭瑜?吏?뺥븳??
    /// true: ?쒖꽌???대몢?뚯죱??諛앹븘吏???꾩뒪 ?쒖옉
    /// false: 肄붾（???뺤? + 利됱떆 ?먮옒 ??蹂듭썝
    /// </summary>
    public void SetHovered(bool on)
    {
        if (on)
        {
            if (_isHovered) return;
            _isHovered = true;

            CacheBaseColor();

            if (_hoverCo != null)
            {
                StopCoroutine(_hoverCo);
                _hoverCo = null;
            }

            if (mainRenderer != null)
                _hoverCo = StartCoroutine(CoHoverPulse());
        }
        else
        {
            if (!_isHovered && _hoverCo == null) return;

            _isHovered = false;
            StopHoverImmediate();
        }
    }

    void StopHoverImmediate()
    {
        if (_hoverCo != null)
        {
            StopCoroutine(_hoverCo);
            _hoverCo = null;
        }
        RestoreBaseColor();
        _isHovered = false;
    }

    System.Collections.IEnumerator CoHoverPulse()
    {
        if (mainRenderer == null)
        {
            _hoverCo = null;
            yield break;
        }

        float t = 0f;

        while (_isHovered)
        {
            t += Time.deltaTime;
            if (hoverPeriod <= 0.0001f) hoverPeriod = 0.1f;

            // 0~1 ?ъ씠瑜?遺?쒕읇寃??뺣났
            float u = Mathf.PingPong(t / hoverPeriod, 1f);

            Color darkCol = Color.Lerp(_baseColor, Color.black, hoverDarkenFactor);
            mainRenderer.color = Color.Lerp(_baseColor, darkCol, u);

            yield return null;
        }

        RestoreBaseColor();
        _hoverCo = null;
    }

    // ?????????????????????????????????????????????
    //   ?몄씠釉?/ 濡쒕뱶
    // ?????????????????????????????????????????????

    [Serializable]
    private class CorpsePayload
    {
        public string corpseId;
    }

    public override EntitySaveData ToSaveData()
    {
        // ?꾩옱 ?꾩튂瑜?corpsePosition???숆린??
        corpsePosition = transform.position;

        var payload = new CorpsePayload
        {
            corpseId = this.corpseId
        };

        return new EntitySaveData
        {
            Kind        = EntityKind.Corpse,
            Position    = corpsePosition, // ?꾩튂??怨듯넻 ?꾨뱶濡????
            PayloadJson = JsonConvert.SerializeObject(payload)
        };
    }

    public override void FromSaveData(EntitySaveData data)
    {
        // ?꾩튂 蹂듭썝 (CorpsePosition ?듯빐 transform??媛숈씠 媛깆떊)
        CorpsePosition = data.Position;

        if (!string.IsNullOrEmpty(data.PayloadJson))
        {
            try
            {
                var payload = JsonConvert.DeserializeObject<CorpsePayload>(data.PayloadJson);
                if (payload != null)
                    corpseId = payload.corpseId;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Corpse] payload ?뚯떛 ?ㅽ뙣: {ex.Message}");
            }
        }

        // 濡쒕뱶 吏곹썑?먮뒗 ?몃쾭 爰쇱쭊 ?곹깭濡?蹂댁옣
        StopHoverImmediate();
    }
}
