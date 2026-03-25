using UnityEngine;

namespace Game.Support
{
    
    [ExecuteAlways]
    public sealed class BeltVfx : MonoBehaviour
    {
        [Header("Children (assign in Inspector)")]
        [SerializeField] Transform beltStart; // start part (at start point)
        [SerializeField] Transform beltBody;  // middle (stretched) part
        [SerializeField] Transform beltEnd;   // end part (at end point)
    
        [Header("End (local offset from start, 2D)")]
        [SerializeField] Vector2 endLocal = new Vector2(2f, 0f);
    
        [Header("Rotation (start/end only)")]
        // +1: CCW, -1: CW (Z異?湲곗?) - RotatingVfx? ?숈씪
        [Range(-1, 1)] public int rotationDir = 1;
        [Min(0f)] public float rpm = 0f;
    
        [Header("Color (body only)")]
        public Color bodyColor = Color.white;
    
        float _bodyBaseLen = 0.1f;
        float _spinDeg = 0f;
    
        void OnEnable()
        {
            CacheBodyBaseLen();
            Apply(endLocal);
            ApplyBodyColor();
        }
    
        void OnValidate()
        {
            CacheBodyBaseLen();
            Apply(endLocal);
            ApplyBodyColor();
        }
    
        void LateUpdate()
        {
            if (beltStart == null || beltEnd == null) return;
            if (rpm <= 0f) return;
    
            float dir = rotationDir >= 0 ? 1f : -1f; // +1 CCW, -1 CW
            float degPerSec = rpm * 6f;              // 360deg * rpm / 60
            _spinDeg += degPerSec * dir * Time.deltaTime;
    
            Quaternion spin = Quaternion.Euler(0f, 0f, _spinDeg);
            beltStart.localRotation = spin;
            beltEnd.localRotation   = spin;
        }
    
        void CacheBodyBaseLen()
        {
            if (beltBody == null) return;
    
            var sr = beltBody.GetComponent<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
            {
                float len = sr.sprite.bounds.size.x;
                if (len > 0.0001f) _bodyBaseLen = len;
            }
        }
    
        public void SetEndpointsWorld(Vector2 startWorld, Vector2 endWorld)
        {
            transform.position = new Vector3(startWorld.x, startWorld.y, transform.position.z);
            SetEndLocal(endWorld - startWorld);
        }
    
        public void SetEndLocal(Vector2 newEndLocal)
        {
            endLocal = newEndLocal;
            Apply(endLocal);
        }
    
        public Vector2 GetEndLocal() => endLocal;
    
        public void SetSpin(float newRpm, int newDir)
        {
            rpm = Mathf.Max(0f, newRpm);
            rotationDir = newDir >= 0 ? 1 : -1;
        }
    
        public void SetBodyColor(Color newColor)
        {
            bodyColor = newColor;
            ApplyBodyColor();
        }
    
        void Apply(Vector2 eLocal2)
        {
            if (beltStart == null || beltBody == null || beltEnd == null) return;
    
            float len = eLocal2.magnitude;
    
            beltStart.localPosition = Vector3.zero;
            beltEnd.localPosition = new Vector3(eLocal2.x, eLocal2.y, 0f);
    
            if (len <= 0.0001f)
            {
                beltBody.gameObject.SetActive(false);
                return;
            }
    
            Vector2 dir2 = eLocal2 / len;
            float ang = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
            Quaternion rot = Quaternion.Euler(0f, 0f, ang);
    
            beltBody.gameObject.SetActive(true);
            beltBody.localRotation = rot;
            beltBody.localPosition = new Vector3(dir2.x, dir2.y, 0f) * (len * 0.5f);
    
            Vector3 s = beltBody.localScale;
            s.x = (_bodyBaseLen > 0.0001f) ? (len / _bodyBaseLen) : 1f;
            beltBody.localScale = s;
        }
    
        void ApplyBodyColor()
        {
            if (beltBody == null) return;
            var sr = beltBody.GetComponent<SpriteRenderer>();
            if (sr == null) return;
            sr.color = bodyColor;
        }
    
        void OnDrawGizmosSelected()
        {
            if (beltEnd == null) return;
            Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
            Gizmos.DrawLine(transform.position, beltEnd.position);
        }
    }
}
