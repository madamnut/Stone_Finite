


using UnityEngine;
using UnityEngine.Tilemaps;


namespace Game.World
{
    public class Chunk : MonoBehaviour
    {

        public const int ChunkSize = 16;
    
        
        public Tilemap bgTilemap;
    
        
        public Tilemap utilityTilemap;
    
        public Tilemap solidTilemap;
    
        
        
        public Tilemap platformTilemap;
    
        public Tilemap liquidTilemap;
    
        
        
        public MeshRenderer lightOverlayRenderer;
    
        
        [HideInInspector] public TileBase[] bgBuffer;
    
        
        [HideInInspector] public TileBase[] utilityBuffer;
    
        [HideInInspector] public TileBase[] solidBuffer;
    
        
        [HideInInspector] public TileBase[] platformBuffer;
    
        [HideInInspector] public TileBase[] liquidBuffer;
    
        
        [HideInInspector] public bool bgDirty       = false;
    
        
        [HideInInspector] public bool utilityDirty  = false;
    
        [HideInInspector] public bool solidDirty    = false;
    
        
        [HideInInspector] public bool platformDirty = false;
    
        [HideInInspector] public bool liquidDirty   = false;
        [HideInInspector] public bool lightDirty    = false;
    
        
        [HideInInspector] public Texture2D liquidTypeTex;     
        [HideInInspector] public Texture2D liquidAmountTex;   
        [HideInInspector] public Color32[] liquidTypePixels;  
        [HideInInspector] public Color32[] liquidAmtPixels;   
        [HideInInspector] public MaterialPropertyBlock liquidMpb;
        [HideInInspector] public TilemapRenderer liquidRenderer;
    
        
        
        [HideInInspector] public Texture2D lightTex;          
        [HideInInspector] public Color32[] lightPixels;       
        [HideInInspector] public MaterialPropertyBlock lightMpb;
    
        
        void Awake()
        {
            
            int ts = ChunkSize * ChunkSize;
            bgBuffer        = new TileBase[ts];
            utilityBuffer   = new TileBase[ts];
            solidBuffer     = new TileBase[ts];
            platformBuffer  = new TileBase[ts];
            liquidBuffer    = new TileBase[ts];
    
            
            
            
            if (platformTilemap != null)
            {
                var r = platformTilemap.GetComponent<TilemapRenderer>();
                if (r != null) r.enabled = false;
            }
    
            
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
    
            
            lightMpb = new MaterialPropertyBlock();
    
            
            if (lightOverlayRenderer != null)
            {
                const int L = ChunkSize + 2; 
                lightPixels = new Color32[L * L];
    
                lightTex = new Texture2D(L, L, TextureFormat.RGBA32, false, true);
                lightTex.filterMode = FilterMode.Bilinear;
                lightTex.wrapMode   = TextureWrapMode.Clamp;
    
                
                for (int i = 0; i < lightPixels.Length; i++)
                    lightPixels[i] = new Color32(0, 0, 0, 0);
    
                lightTex.SetPixels32(lightPixels);
                lightTex.Apply(false, false);
    
                
                lightOverlayRenderer.GetPropertyBlock(lightMpb);
                lightMpb.SetTexture("_LightTex", lightTex);
                lightOverlayRenderer.SetPropertyBlock(lightMpb);
            }
        }
    }
}
