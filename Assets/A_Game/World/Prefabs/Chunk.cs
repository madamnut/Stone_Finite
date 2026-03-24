using UnityEngine;
using UnityEngine.Tilemaps;


namespace Game.World
{
    public class Chunk : MonoBehaviour
    {
        public const int ChunkSize = 16;
    
        // ?€?€ Tilemap ?ˆì´?? ?„ê²½ + ? í‹¸ë¦¬í‹° + ?”ë¦¬???œê°) + ?Œë«??ì½œë¼?´ë” ?„ìš©) + ? ì²´ ?€?€
        public Tilemap bgTilemap;
    
        // ??ì¶”ê?: ? í‹¸ë¦¬í‹°(ë²½ë©´ ?¤ë¹„) ?ˆì´??
        public Tilemap utilityTilemap;
    
        public Tilemap solidTilemap;
    
        // ??ì¶”ê?: ?Œë«??ì½œë¼?´ë” ?„ìš© ?€?¼ë§µ (TilemapCollider2D/Composite/PlatformEffector2D)
        // - ?Œë”???„ëŠ” ?„ì œ (TilemapRenderer disabled)
        public Tilemap platformTilemap;
    
        public Tilemap liquidTilemap;
    
        // ?€?€ Light Overlay (Quad) ?€?€
        // ?„ë¦¬?¹ì—??LightOverlay ?¤ë¸Œ?íŠ¸ë¥??°ê²° (MeshRenderer ë³´ìœ )
        public MeshRenderer lightOverlayRenderer;
    
        // ?€?€ ?€??ë²„í¼(?¬ì‚¬?? ?€?€
        [HideInInspector] public TileBase[] bgBuffer;
    
        // ??ì¶”ê?: ? í‹¸ë¦¬í‹° ë²„í¼
        [HideInInspector] public TileBase[] utilityBuffer;
    
        [HideInInspector] public TileBase[] solidBuffer;
    
        // ??ì¶”ê?: ?Œë«??ì½œë¼?´ë” ?„ìš© ë²„í¼
        [HideInInspector] public TileBase[] platformBuffer;
    
        [HideInInspector] public TileBase[] liquidBuffer;
    
        // ?€?€ Dirty ?Œë˜ê·??€?€
        [HideInInspector] public bool bgDirty       = false;
    
        // ??ì¶”ê?: ? í‹¸ë¦¬í‹° ?”í‹°
        [HideInInspector] public bool utilityDirty  = false;
    
        [HideInInspector] public bool solidDirty    = false;
    
        // ??ì¶”ê?: ?Œë«??ì½œë¼?´ë” ?”í‹°
        [HideInInspector] public bool platformDirty = false;
    
        [HideInInspector] public bool liquidDirty   = false;
        [HideInInspector] public bool lightDirty    = false;
    
        // ?€?€ Liquid Mask (ì²?¬ë³??Œë” ë¶„ê¸°?? ?€?€
        [HideInInspector] public Texture2D liquidTypeTex;     // 16x16, R=liquidId(0..255)
        [HideInInspector] public Texture2D liquidAmountTex;   // 16x16, R=amount(0..128)
        [HideInInspector] public Color32[] liquidTypePixels;  // 256
        [HideInInspector] public Color32[] liquidAmtPixels;   // 256
        [HideInInspector] public MaterialPropertyBlock liquidMpb;
        [HideInInspector] public TilemapRenderer liquidRenderer;
    
        // ?€?€ Light Texture (ì²?¬ë³?1???ì„±, ?´í›„ ?¬ì‚¬?? ?€?€
        // 18x18: ê°€?´ë° 16x16 + ?Œë‘ë¦?1?½ì? ?¨ë”©(?¸ì ‘ ì²?¬ ë³´ê°„??
        [HideInInspector] public Texture2D lightTex;          // 18x18, RGBA (A???´ë‘  ?ŒíŒŒ)
        [HideInInspector] public Color32[] lightPixels;       // 18*18
        [HideInInspector] public MaterialPropertyBlock lightMpb;
    
        void Awake()
        {
            // ?€??ë²„í¼
            int ts = ChunkSize * ChunkSize;
            bgBuffer        = new TileBase[ts];
            utilityBuffer   = new TileBase[ts];
            solidBuffer     = new TileBase[ts];
            platformBuffer  = new TileBase[ts];
            liquidBuffer    = new TileBase[ts];
    
            // ?€?€ Platform TilemapRenderer ë¹„í™œ???œê° ?Œë” ì¤‘ë³µ ë°©ì?) ?€?€
            // ?Œë«?¼ì? Solid ?€?¼ë§µ??"ì½œë¼?´ë” ?†ëŠ” ?”ë¦¬?œì²˜?? ê·¸ë¦¬ë¯€ë¡?
            // platformTilemap?€ ì½œë¼?´ë”ë§??ì„±?˜ê³  ?Œë”???ˆë‹¤.
            if (platformTilemap != null)
            {
                var r = platformTilemap.GetComponent<TilemapRenderer>();
                if (r != null) r.enabled = false;
            }
    
            // ?€?€ Liquid Mask ê¸°ë³¸ ë¦¬ì†Œ??(ì²?¬??1???ì„±, ?´í›„ ?¬ì‚¬?? ?€?€
            liquidRenderer = liquidTilemap.GetComponent<TilemapRenderer>();
            liquidMpb = new MaterialPropertyBlock();
    
            liquidTypePixels = new Color32[ts];
            liquidAmtPixels  = new Color32[ts];
    
            liquidTypeTex = new Texture2D(ChunkSize, ChunkSize, TextureFormat.RGBA32, false, true);
            liquidTypeTex.filterMode = FilterMode.Point;
            liquidTypeTex.wrapMode   = TextureWrapMode.Clamp;
    
            liquidAmountTex = new Texture2D(ChunkSize, ChunkSize, TextureFormat.RGBA32, false, true);
            liquidAmountTex.filterMode = FilterMode.Point;
            liquidAmountTex.wrapMode   = TextureWrapMode.Clamp;
    
            // ?€?€ Light Overlay ê¸°ë³¸ ë¦¬ì†Œ??(ì²?¬??1???ì„±, ?´í›„ ?¬ì‚¬?? ?€?€
            lightMpb = new MaterialPropertyBlock();
    
            // LightOverlayRendererê°€ ?†ìœ¼ë©?Light ?ˆì´?´ëŠ” ë¹„í™œ???Œë”?????˜ì?ë§?ê²Œì„ ì§„í–‰???í–¥ ?†ìŒ)
            if (lightOverlayRenderer != null)
            {
                const int L = ChunkSize + 2; // 18
                lightPixels = new Color32[L * L];
    
                lightTex = new Texture2D(L, L, TextureFormat.RGBA32, false, true);
                lightTex.filterMode = FilterMode.Bilinear;
                lightTex.wrapMode   = TextureWrapMode.Clamp;
    
                // ì´ˆê¸°ê°? ?„ì „ ?¬ëª…(?´ë‘  ?†ìŒ)
                for (int i = 0; i < lightPixels.Length; i++)
                    lightPixels[i] = new Color32(0, 0, 0, 0);
    
                lightTex.SetPixels32(lightPixels);
                lightTex.Apply(false, false);
    
                // MPB???ìŠ¤ì²?ë°”ì¸??(?„ë¡œ?¼í‹° ?´ë¦„?€ ?°ì´?”ì— ë§ì¶° ?µì¼)
                lightOverlayRenderer.GetPropertyBlock(lightMpb);
                lightMpb.SetTexture("_LightTex", lightTex);
                lightOverlayRenderer.SetPropertyBlock(lightMpb);
            }
        }
    }
}
