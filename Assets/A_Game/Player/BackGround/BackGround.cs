// BackGround.cs
using UnityEngine;

using Game.World;

namespace Game.Player
{
    public class BackGround : MonoBehaviour
    {
        [Header("Follow Target")]
        public Transform player;
    
        [Header("Axes")]
        public bool followX = true;
        public bool followY = true;
    
        [Header("Layers (assign a single centered segment per layer)")]
        public Transform layer0; // far
        public Transform layer1; // mid
        public Transform layer2; // near
    
        [Header("Lag Weights (X only)")]
        [Range(0f, 1f)] public float weight0 = 0.8f;
        [Range(0f, 1f)] public float weight1 = 0.5f;
        [Range(0f, 1f)] public float weight2 = 0.2f;
    
        [Header("Y Follow (Surface Absolute)")]
        public float yBaseline = 810f;
        [Range(0f, 1f)] public float yParallax0 = 0.98f; // far
        [Range(0f, 1f)] public float yParallax1 = 0.99f; // mid
        [Range(0f, 1f)] public float yParallax2 = 1.00f; // near
    
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
    
        [Header("Sun/Moon Angle Offset")]
        public float sunAngleOffsetDeg = 180f;
    
        [Header("Day/Night Brightness")]
        public Color32 dark = new Color32(30, 30, 30, 255);
        public Color32 bright = new Color32(255, 255, 255, 255);
        [Range(0.05f, 5f)] public float brightnessSmoothTime = 0.6f;
    
        // runtime
        Transform l0A, l0B, l0C; float w0; float kx0;
        Transform l1A, l1B, l1C; float w1; float kx1;
        Transform l2A, l2B, l2C; float w2; float kx2;
    
        SpriteRenderer[] l0A_sprs, l0B_sprs, l0C_sprs;
        SpriteRenderer[] l1A_sprs, l1B_sprs, l1C_sprs;
        SpriteRenderer[] l2A_sprs, l2B_sprs, l2C_sprs;
    
        Vector3 _prevP;
        float _L, _Lvel;
        float _baseY0, _baseY1, _baseY2;
    
        void Start()
        {
            if (!player) { enabled = false; return; }
            _prevP = player.position;
    
            kx0 = 1f - weight0;
            kx1 = 1f - weight1;
            kx2 = 1f - weight2;
    
            if (layer0) InitLayer(layer0, out l0A, out l0B, out l0C, out w0);
            if (layer1) InitLayer(layer1, out l1A, out l1B, out l1C, out w1);
            if (layer2) InitLayer(layer2, out l2A, out l2B, out l2C, out w2);
    
            l0A_sprs = l0A ? l0A.GetComponentsInChildren<SpriteRenderer>(true) : null;
            l0B_sprs = l0B ? l0B.GetComponentsInChildren<SpriteRenderer>(true) : null;
            l0C_sprs = l0C ? l0C.GetComponentsInChildren<SpriteRenderer>(true) : null;
    
            l1A_sprs = l1A ? l1A.GetComponentsInChildren<SpriteRenderer>(true) : null;
            l1B_sprs = l1B ? l1B.GetComponentsInChildren<SpriteRenderer>(true) : null;
            l1C_sprs = l1C ? l1C.GetComponentsInChildren<SpriteRenderer>(true) : null;
    
            l2A_sprs = l2A ? l2A.GetComponentsInChildren<SpriteRenderer>(true) : null;
            l2B_sprs = l2B ? l2B.GetComponentsInChildren<SpriteRenderer>(true) : null;
            l2C_sprs = l2C ? l2C.GetComponentsInChildren<SpriteRenderer>(true) : null;
    
            if (l0B) _baseY0 = l0B.position.y;
            if (l1B) _baseY1 = l1B.position.y;
            if (l2B) _baseY2 = l2B.position.y;
        }
    
        void LateUpdate()
        {
            if (!player) return;
    
            Vector3 curP = player.position;
    
            // X : dp 湲곕컲
            Vector3 dp = curP - _prevP;
            if (!followX) dp.x = 0f;
            dp.y = 0f;
            dp.z = 0f;
    
            if (l0A) { Move3(l0A, l0B, l0C, dp, kx0); Wrap3(ref l0A, ref l0B, ref l0C, w0, curP.x); }
            if (l1A) { Move3(l1A, l1B, l1C, dp, kx1); Wrap3(ref l1A, ref l1B, ref l1C, w1, curP.x); }
            if (l2A) { Move3(l2A, l2B, l2C, dp, kx2); Wrap3(ref l2A, ref l2B, ref l2C, w2, curP.x); }
    
            // Y : 湲곗????鍮?李⑥씠 利됱떆 ?곸슜
            if (followY)
            {
                float dy = curP.y - yBaseline;
    
                if (l0B) Set3Y(l0A, l0B, l0C, _baseY0 + dy * yParallax0);
                if (l1B) Set3Y(l1A, l1B, l1C, _baseY1 + dy * yParallax1);
                if (l2B) Set3Y(l2A, l2B, l2C, _baseY2 + dy * yParallax2);
            }
    
            if (center)
            {
                center.position = new Vector3(
                    player.position.x,
                    l0B ? l0B.position.y : center.position.y,
                    center.position.z
                );
            }
    
            UpdateSunMoon();
            UpdateBrightness();
    
            _prevP = curP;
        }
    
        void UpdateSunMoon()
        {
            if (!center || world == null) return;
    
            long tickInDay = world.worldTick % world.ticksPerDay;
            float angleDeg = (tickInDay / (float)world.ticksPerDay) * 360f;
            angleDeg += sunAngleOffsetDeg;
    
            float rad = -(angleDeg + 90f) * Mathf.Deg2Rad;
    
            Vector3 sunTarget  = new Vector3(Mathf.Cos(rad) * a, Mathf.Sin(rad) * b, sun ? sun.localPosition.z : 0f);
            Vector3 moonTarget = -sunTarget;
    
            float k = 1f - Mathf.Exp(-orbitSmooth * (Application.isPlaying ? Time.deltaTime : 1f / 60f));
    
            if (sun)  sun.localPosition  = Vector3.Lerp(sun.localPosition,  sunTarget,  k);
            if (moon) moon.localPosition = Vector3.Lerp(moon.localPosition, moonTarget, k);
    
            // ?꾩튂 湲곕컲 媛볥젅?대줈 蹂듦?: BackGround?먯꽌 GodRay???섍만 寃??놁쓬
        }
    
        void UpdateBrightness()
        {
            if (!world) return;
    
            int m = world.worldHour * 60 + (world.worldMinute % 60);
            float target =
                (m >= 300 && m < 540) ? (m - 300) / 240f :
                (m >= 540 && m < 1080) ? 1f :
                (m >= 1080 && m < 1260) ? 1f - (m - 1080) / 180f :
                0f;
    
            float dt = Application.isPlaying ? Time.deltaTime : 1f / 60f;
            _L = Mathf.SmoothDamp(_L, target, ref _Lvel, brightnessSmoothTime, Mathf.Infinity, dt);
    
            Color c = Color.Lerp((Color)dark, (Color)bright, _L);
    
            ApplyColor(l0A_sprs, c); ApplyColor(l0B_sprs, c); ApplyColor(l0C_sprs, c);
            ApplyColor(l1A_sprs, c); ApplyColor(l1B_sprs, c); ApplyColor(l1C_sprs, c);
            ApplyColor(l2A_sprs, c); ApplyColor(l2B_sprs, c); ApplyColor(l2C_sprs, c);
        }
    
        void ApplyColor(SpriteRenderer[] arr, Color c)
        {
            if (arr == null) return;
            for (int i = 0; i < arr.Length; i++)
            {
                var sr = arr[i];
                if (!sr) continue;
                float a = sr.color.a;
                sr.color = new Color(c.r, c.g, c.b, a);
            }
        }
    
        void Set3Y(Transform A, Transform B, Transform C, float y)
        {
            if (A) A.position = new Vector3(A.position.x, y, A.position.z);
            if (B) B.position = new Vector3(B.position.x, y, B.position.z);
            if (C) C.position = new Vector3(C.position.x, y, C.position.z);
        }
    
        void InitLayer(Transform center, out Transform A, out Transform B, out Transform C, out float width)
        {
            width = ComputeWorldWidth(center);
            if (width <= 0f) width = 10f;
    
            var parent = center.parent;
            A = Instantiate(center.gameObject, parent).transform;
            B = center;
            C = Instantiate(center.gameObject, parent).transform;
    
            Vector3 p = center.position;
            A.position = new Vector3(p.x - width, p.y, p.z);
            C.position = new Vector3(p.x + width, p.y, p.z);
    
            A.name = center.name + "_L";
            C.name = center.name + "_R";
    
            SortByX(ref A, ref B, ref C);
        }
    
        float ComputeWorldWidth(Transform t)
        {
            var rends = t.GetComponentsInChildren<Renderer>();
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            return b.size.x;
        }
    
        void Move3(Transform A, Transform B, Transform C, Vector3 dp, float kx)
        {
            Vector3 mv = new Vector3(dp.x * kx, 0f, 0f);
            A.position += mv; B.position += mv; C.position += mv;
        }
    
        void Wrap3(ref Transform A, ref Transform B, ref Transform C, float width, float px)
        {
            SortByX(ref A, ref B, ref C);
    
            while (px > C.position.x)
            {
                A.position = new Vector3(C.position.x + width, A.position.y, A.position.z);
                SortByX(ref A, ref B, ref C);
            }
            while (px < A.position.x)
            {
                C.position = new Vector3(A.position.x - width, C.position.y, C.position.z);
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
}
