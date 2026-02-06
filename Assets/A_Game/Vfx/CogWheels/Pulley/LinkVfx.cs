using UnityEngine;

[ExecuteAlways]
public sealed class LinkVfx : MonoBehaviour
{
    [Header("Children (assign in Inspector)")]
    [SerializeField] Transform linkStart; // start part (at start point)
    [SerializeField] Transform linkBody;  // middle (stretched) part
    [SerializeField] Transform linkEnd;   // end part (at end point)

    [Header("Caps (world units)")]
    [SerializeField] float startCapLen = 0.25f;
    [SerializeField] float endCapLen   = 0.25f;

    [Header("End (local offset from start, 2D)")]
    [SerializeField] Vector2 endLocal = new Vector2(2f, 0f);

    [Header("Optional rotation (start/end only)")]
    [SerializeField] bool enableRotation = false;
    [SerializeField] float rpm = 0f; // clockwise, both caps rotate same speed

    float _bodyBaseLen = 0.1f;
    float _spinDeg = 0f; // accumulated spin angle (degrees)

    void OnEnable()
    {
        CacheBodyBaseLen();
        Apply(endLocal);
    }

    void OnValidate()
    {
        CacheBodyBaseLen();
        Apply(endLocal);
    }

    void LateUpdate()
    {
        if (!enableRotation) return;
        if (linkStart == null || linkEnd == null) return;
        if (Mathf.Abs(rpm) <= 0.0001f) return;

        float degPerSec = rpm * 360f / 60f;
        _spinDeg += -degPerSec * Time.deltaTime; // clockwise in Unity 2D

        Quaternion spin = Quaternion.Euler(0f, 0f, _spinDeg);
        linkStart.localRotation = spin;
        linkEnd.localRotation   = spin;
    }

    void CacheBodyBaseLen()
    {
        if (linkBody == null) return;

        var sr = linkBody.GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            float len = sr.sprite.bounds.size.x;
            if (len > 0.0001f) _bodyBaseLen = len;
        }
    }

    // Gameplay API ------------------------------------------------------------

    public void SetEndpointsWorld(Vector2 startWorld, Vector2 endWorld)
    {
        transform.position = new Vector3(startWorld.x, startWorld.y, transform.position.z);
        SetEndLocal(endWorld - startWorld);
    }

    public void SetEndLocal(Vector2 newEndLocal)
    {
        endLocal = newEndLocal; // inspector-visible
        Apply(endLocal);
    }

    public Vector2 GetEndLocal() => endLocal;

    public void SetRpm(float newRpm, bool enable)
    {
        rpm = newRpm;
        enableRotation = enable;

        if (!enableRotation)
        {
            _spinDeg = 0f;
            if (linkStart != null) linkStart.localRotation = Quaternion.identity;
            if (linkEnd != null) linkEnd.localRotation = Quaternion.identity;
        }
    }

    // Core --------------------------------------------------------------------

    void Apply(Vector2 eLocal2)
    {
        if (linkStart == null || linkBody == null || linkEnd == null) return;

        float len = eLocal2.magnitude;

        // Start/End positions only (do NOT rotate with link angle)
        linkStart.localPosition = Vector3.zero;
        linkEnd.localPosition = new Vector3(eLocal2.x, eLocal2.y, 0f);

        if (!enableRotation)
        {
            linkStart.localRotation = Quaternion.identity;
            linkEnd.localRotation = Quaternion.identity;
        }
        else
        {
            Quaternion spin = Quaternion.Euler(0f, 0f, _spinDeg);
            linkStart.localRotation = spin;
            linkEnd.localRotation = spin;
        }

        if (len <= 0.0001f)
        {
            linkBody.gameObject.SetActive(false);
            return;
        }

        // Body connects the two points (body rotates to link angle)
        Vector2 dir2 = eLocal2 / len;
        float ang = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, ang);

        float bodyLen = Mathf.Max(0f, len - startCapLen - endCapLen);

        if (bodyLen <= 0.0001f)
        {
            linkBody.gameObject.SetActive(false);
            return;
        }

        linkBody.gameObject.SetActive(true);
        linkBody.localRotation = rot;

        linkBody.localPosition = new Vector3(dir2.x, dir2.y, 0f) * (startCapLen + bodyLen * 0.5f);

        Vector3 s = linkBody.localScale;
        s.x = (_bodyBaseLen > 0.0001f) ? (bodyLen / _bodyBaseLen) : 1f;
        linkBody.localScale = s;
    }

    void OnDrawGizmosSelected()
    {
        if (linkEnd == null) return;
        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        Gizmos.DrawLine(transform.position, linkEnd.position);
    }
}
