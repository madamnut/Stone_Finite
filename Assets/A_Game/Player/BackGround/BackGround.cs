// BackGround.cs
using UnityEngine;

public class BackGround : MonoBehaviour
{
    [Header("Follow Target")]
    public Transform player;

    [Header("Axes")]
    public bool followX = true;
    public bool followY = false;

    [Header("Layers (assign a single centered segment per layer)")]
    public Transform layer0; // far
    public Transform layer1; // mid
    public Transform layer2; // near

    [Header("Lag Weights (0 = tight follow, 1 = no follow)")]
    [Range(0f,1f)] public float weight0 = 0.8f;
    [Range(0f,1f)] public float weight1 = 0.5f;
    [Range(0f,1f)] public float weight2 = 0.2f;

    [Header("Y Tightness (clamped to ≤1)")]
    public float yTightness = 2f;

    [Header("Sun & Moon (children of Center)")]
    public WorldManager world;
    public Transform center;
    public Transform sun;
    public Transform moon;

    [Header("Ellipse")]
    public float a = 100f;
    public float b = 60f;

    [Header("Orbit Smoothing")]
    public float orbitSmooth = 6f;

    [Header("Day/Night Brightness")]
    public Color32 dark = new Color32(30,30,30,255);
    public Color32 bright = new Color32(255,255,255,255);
    [Range(0.05f,5f)] public float brightnessSmoothTime = 0.6f;

    // runtime
    Transform l0A, l0B, l0C; float w0; float kx0, ky0;
    Transform l1A, l1B, l1C; float w1; float kx1, ky1;
    Transform l2A, l2B, l2C; float w2; float kx2, ky2;

    SpriteRenderer[] l0A_sprs, l0B_sprs, l0C_sprs;
    SpriteRenderer[] l1A_sprs, l1B_sprs, l1C_sprs;
    SpriteRenderer[] l2A_sprs, l2B_sprs, l2C_sprs;

    Vector3 _prevP;

    // brightness smoothing state
    float _L;    // current brightness 0..1
    float _Lvel; // SmoothDamp velocity

    void Start()
    {
        if (!player) { enabled = false; return; }
        _prevP = player.position;

        kx0 = 1f - weight0; ky0 = Mathf.Min(1f, kx0 * yTightness);
        kx1 = 1f - weight1; ky1 = Mathf.Min(1f, kx1 * yTightness);
        kx2 = 1f - weight2; ky2 = Mathf.Min(1f, kx2 * yTightness);

        if (layer0) InitLayer(layer0, out l0A, out l0B, out l0C, out w0);
        if (layer1) InitLayer(layer1, out l1A, out l1B, out l1C, out w1);
        if (layer2) InitLayer(layer2, out l2A, out l2B, out l2C, out w2);

        // cache sprite renderers
        l0A_sprs = l0A ? l0A.GetComponentsInChildren<SpriteRenderer>(true) : null;
        l0B_sprs = l0B ? l0B.GetComponentsInChildren<SpriteRenderer>(true) : null;
        l0C_sprs = l0C ? l0C.GetComponentsInChildren<SpriteRenderer>(true) : null;

        l1A_sprs = l1A ? l1A.GetComponentsInChildren<SpriteRenderer>(true) : null;
        l1B_sprs = l1B ? l1B.GetComponentsInChildren<SpriteRenderer>(true) : null;
        l1C_sprs = l1C ? l1C.GetComponentsInChildren<SpriteRenderer>(true) : null;

        l2A_sprs = l2A ? l2A.GetComponentsInChildren<SpriteRenderer>(true) : null;
        l2B_sprs = l2B ? l2B.GetComponentsInChildren<SpriteRenderer>(true) : null;
        l2C_sprs = l2C ? l2C.GetComponentsInChildren<SpriteRenderer>(true) : null;
    }

    void LateUpdate()
    {
        if (!player) return;

        Vector3 curP = player.position;
        Vector3 dp = curP - _prevP;
        if (!followX) dp.x = 0f;
        if (!followY) dp.y = 0f;
        dp.z = 0f;

        if (l0A) { Move3(l0A, l0B, l0C, dp, kx0, ky0); Wrap3(ref l0A, ref l0B, ref l0C, w0, curP.x); }
        if (l1A) { Move3(l1A, l1B, l1C, dp, kx1, ky1); Wrap3(ref l1A, ref l1B, ref l1C, w1, curP.x); }
        if (l2A) { Move3(l2A, l2B, l2C, dp, kx2, ky2); Wrap3(ref l2A, ref l2B, ref l2C, w2, curP.x); }

        if (center)
        {
            float cx = player.position.x;
            float cy = l0B ? l0B.position.y : center.position.y;
            center.position = new Vector3(cx, cy, center.position.z);
        }

        UpdateSunMoon();

        // brightness: compute target L from world time and smooth it
        if (world)
        {
            int m = world.worldHour * 60 + (world.worldMinute % 60); // 0..1439
            float target =
                (m >= 300  && m < 540 ) ? (m - 300) / 240f :        // 05:00~09:00 up
                (m >= 540  && m < 1080) ? 1f :                      // 09:00~18:00 max
                (m >= 1080 && m < 1260) ? 1f - (m - 1080) / 180f :  // 18:00~21:00 down
                0f;                                                  // 21:00~05:00 min

            float dt = Application.isPlaying ? Time.deltaTime : 1f/60f;
            _L = Mathf.SmoothDamp(_L, target, ref _Lvel, brightnessSmoothTime, Mathf.Infinity, dt);

            Color c = Color.Lerp((Color)dark, (Color)bright, _L);

            // apply to all cached sprite renderers, keep alpha
            if (l0A_sprs != null) for (int i=0;i<l0A_sprs.Length;i++){ var sr=l0A_sprs[i]; if(!sr) continue; var col=sr.color; float a=col.a; col=c; col.a=a; sr.color=col; }
            if (l0B_sprs != null) for (int i=0;i<l0B_sprs.Length;i++){ var sr=l0B_sprs[i]; if(!sr) continue; var col=sr.color; float a=col.a; col=c; col.a=a; sr.color=col; }
            if (l0C_sprs != null) for (int i=0;i<l0C_sprs.Length;i++){ var sr=l0C_sprs[i]; if(!sr) continue; var col=sr.color; float a=col.a; col=c; col.a=a; sr.color=col; }

            if (l1A_sprs != null) for (int i=0;i<l1A_sprs.Length;i++){ var sr=l1A_sprs[i]; if(!sr) continue; var col=sr.color; float a=col.a; col=c; col.a=a; sr.color=col; }
            if (l1B_sprs != null) for (int i=0;i<l1B_sprs.Length;i++){ var sr=l1B_sprs[i]; if(!sr) continue; var col=sr.color; float a=col.a; col=c; col.a=a; sr.color=col; }
            if (l1C_sprs != null) for (int i=0;i<l1C_sprs.Length;i++){ var sr=l1C_sprs[i]; if(!sr) continue; var col=sr.color; float a=col.a; col=c; col.a=a; sr.color=col; }

            if (l2A_sprs != null) for (int i=0;i<l2A_sprs.Length;i++){ var sr=l2A_sprs[i]; if(!sr) continue; var col=sr.color; float a=col.a; col=c; col.a=a; sr.color=col; }
            if (l2B_sprs != null) for (int i=0;i<l2B_sprs.Length;i++){ var sr=l2B_sprs[i]; if(!sr) continue; var col=sr.color; float a=col.a; col=c; col.a=a; sr.color=col; }
            if (l2C_sprs != null) for (int i=0;i<l2C_sprs.Length;i++){ var sr=l2C_sprs[i]; if(!sr) continue; var col=sr.color; float a=col.a; col=c; col.a=a; sr.color=col; }
        }

        _prevP = curP;
    }

    void UpdateSunMoon()
    {
        if (!center || world == null) return;

        long tickInDay = world.worldTick % world.ticksPerDay; // 0~28799
        float angleDeg = (tickInDay / (float)world.ticksPerDay) * 360f;

        // clockwise + 90°
        float rad = -(angleDeg + 90f) * Mathf.Deg2Rad;

        Vector3 targetSunLocal  = new Vector3(Mathf.Cos(rad) * a, Mathf.Sin(rad) * b, sun ? sun.localPosition.z : 0f);
        Vector3 targetMoonLocal = -targetSunLocal;

        float k = 1f - Mathf.Exp(-orbitSmooth * (Application.isPlaying ? Time.deltaTime : 1f/60f));

        if (sun)
            sun.localPosition = Vector3.Lerp(sun.localPosition, targetSunLocal, k);
        if (moon)
            moon.localPosition = Vector3.Lerp(moon.localPosition, targetMoonLocal, k);
    }

    void InitLayer(Transform center, out Transform A, out Transform B, out Transform C, out float width)
    {
        width = ComputeWorldWidth(center);
        if (width <= 0f) width = 10f;

        var parent = center.parent;
        var leftGO  = Instantiate(center.gameObject, parent);
        var rightGO = Instantiate(center.gameObject, parent);
        A = leftGO.transform;
        B = center;
        C = rightGO.transform;

        Vector3 cpos = center.position;
        A.position = new Vector3(cpos.x - width, cpos.y, cpos.z);
        C.position = new Vector3(cpos.x + width, cpos.y, cpos.z);

        A.name = center.name + "_L";
        C.name = center.name + "_R";

        SortByX(ref A, ref B, ref C);
    }

    float ComputeWorldWidth(Transform t)
    {
        var rends = t.GetComponentsInChildren<Renderer>();
        if (rends == null || rends.Length == 0) return 0f;
        Bounds b = rends[0].bounds;
        for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
        return b.size.x;
    }

    void Move3(Transform A, Transform B, Transform C, Vector3 dp, float kx, float ky)
    {
        Vector3 mv = new Vector3(dp.x * kx, dp.y * ky, 0f);
        A.position += mv; B.position += mv; C.position += mv;
    }

    void Wrap3(ref Transform A, ref Transform B, ref Transform C, float width, float px)
    {
        SortByX(ref A, ref B, ref C);

        while (px > C.position.x)
        {
            Vector3 p = A.position; p.x = C.position.x + width; A.position = p;
            SortByX(ref A, ref B, ref C);
        }
        while (px < A.position.x)
        {
            Vector3 p = C.position; p.x = A.position.x - width; C.position = p;
            SortByX(ref A, ref B, ref C);
        }
    }

    void SortByX(ref Transform A, ref Transform B, ref Transform C)
    {
        if (A.position.x > B.position.x) Swap(ref A, ref B);
        if (B.position.x > C.position.x) Swap(ref B, ref C);
        if (A.position.x > B.position.x) Swap(ref A, ref B);
    }

    void Swap(ref Transform a, ref Transform b)
    {
        var t = a; a = b; b = t;
    }
}
