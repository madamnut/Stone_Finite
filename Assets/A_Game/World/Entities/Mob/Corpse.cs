


using System;
using UnityEngine;
using Newtonsoft.Json;





namespace Game.World
{
public class Corpse : Entity
{
    [Header("Corpse Info")]
    [SerializeField] private string corpseId;

    
    [SerializeField] private Vector2 corpsePosition;

    
    public string CorpseId
    {

        get => corpseId;
        set => corpseId = value;
    }

    
    
    
    
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

    
    
    

    [Header("Hover Highlight")]
    [Tooltip("?類ｌ졊 獄??紐껋쒔??筌롫뗄????쎈늄??깆뵠??(?癒?뻼 SpriteRenderer ????")]
    public SpriteRenderer mainRenderer;

    [Tooltip("??곗춳????紐쇔칰?筌띾슢諭억쭪? (0 = 域밸챶?嚥? 1 = ?袁⑹읈 野꺜??")]
    [Range(0f, 1f)] public float hoverDarkenFactor = 0.6f;

    [Tooltip("??紐쇔칰??꼥獄쏆빓苡????類ｋ궗 雅뚯눊由???")]
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

            
            float u = Mathf.PingPong(t / hoverPeriod, 1f);

            Color darkCol = Color.Lerp(_baseColor, Color.black, hoverDarkenFactor);
            mainRenderer.color = Color.Lerp(_baseColor, darkCol, u);

            yield return null;
        }

        RestoreBaseColor();
        _hoverCo = null;
    }

    
    
    

    [Serializable]
    private class CorpsePayload
    {
        public string corpseId;
    }

    
    public override EntitySaveData ToSaveData()
    {
        
        corpsePosition = transform.position;

        var payload = new CorpsePayload
        {
            corpseId = this.corpseId
        };

        return new EntitySaveData
        {
            Kind        = EntityKind.Corpse,
            Position    = corpsePosition, 
            PayloadJson = JsonConvert.SerializeObject(payload)
        };
    }

    
    public override void FromSaveData(EntitySaveData data)
    {
        
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
                Debug.LogError($"[Corpse] payload ???뼓 ??쎈솭: {ex.Message}");
            }
        }

        
        StopHoverImmediate();
    }
}
}
