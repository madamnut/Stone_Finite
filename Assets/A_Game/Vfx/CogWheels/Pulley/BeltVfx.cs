using UnityEngine;

[ExecuteAlways]
public sealed class BeltVfx : MonoBehaviour
{
    [Header("Children (assign in Inspector)")]
    [SerializeField] Transform beltStart; // start part (at start point)
    [SerializeField] Transform beltBody;  // middle (stretched) part
    [SerializeField] Transform beltEnd;   // end part (at end point)

    [Header("End (local offset from start, 2D)")]
    [SerializeField] Vector2 endLocal = new Vector2(2f, 0f);

    float _bodyBaseLen = 0.1f;

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

    // Core --------------------------------------------------------------------

    void Apply(Vector2 eLocal2)
    {
        if (beltStart == null || beltBody == null || beltEnd == null) return;

        float len = eLocal2.magnitude;

        beltStart.localPosition = Vector3.zero;
        beltStart.localRotation = Quaternion.identity;

        beltEnd.localPosition = new Vector3(eLocal2.x, eLocal2.y, 0f);
        beltEnd.localRotation = Quaternion.identity;

        if (len <= 0.0001f)
        {
            beltBody.gameObject.SetActive(false);
            return;
        }

        // Body connects start->end (rotate body only)
        Vector2 dir2 = eLocal2 / len;
        float ang = Mathf.Atan2(dir2.y, dir2.x) * Mathf.Rad2Deg;
        Quaternion rot = Quaternion.Euler(0f, 0f, ang);

        beltBody.gameObject.SetActive(true);
        beltBody.localRotation = rot;

        // Caps are zero: body spans full length and is centered.
        beltBody.localPosition = new Vector3(dir2.x, dir2.y, 0f) * (len * 0.5f);

        Vector3 s = beltBody.localScale;
        s.x = (_bodyBaseLen > 0.0001f) ? (len / _bodyBaseLen) : 1f;
        beltBody.localScale = s;
    }

    void OnDrawGizmosSelected()
    {
        if (beltEnd == null) return;
        Gizmos.color = new Color(1f, 1f, 1f, 0.6f);
        Gizmos.DrawLine(transform.position, beltEnd.position);
    }
}
