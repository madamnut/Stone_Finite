


using UnityEngine;
using Newtonsoft.Json;

using Game.Data;

namespace Game.World
{
    public partial class FallingBlock : Entity
    {
        [Header("References")]
        [SerializeField] private WorldManager world;
        [SerializeField] private SpriteRenderer sr;
        [SerializeField] private LayerMask triggerMask;
    
        [Header("Data")]
        [SerializeField] private ushort cellId;
        [SerializeField] private bool placed;
    

        public override EntityKind Kind => EntityKind.FallingBlock;
    
        
        
        
#if false
        public override EntitySaveData ToSaveData()
        {
            
            if (placed)
                return null;
    
            var payload = new FallingBlockPayload
            {
                cellId = cellId
            };
    
            return new EntitySaveData
            {
                Kind        = EntityKind.FallingBlock,
                Position    = transform.position,
                PayloadJson = JsonConvert.SerializeObject(payload)
            };
        }
    
        public override void FromSaveData(EntitySaveData data)
        {
            transform.position = data.Position;
    
            if (!string.IsNullOrEmpty(data.PayloadJson))
            {
                var payload = JsonConvert.DeserializeObject<FallingBlockPayload>(data.PayloadJson);
                if (payload != null)
                    cellId = payload.cellId;
            }
    
            ApplySprite();
        }
#endif
    
        
        
        
        
        public void Init(ushort id, WorldManager wm, Sprite overrideSprite = null)
        {
            cellId = id;
            world  = wm;
    
            if (!sr)
                sr = GetComponent<SpriteRenderer>();
    
            if (overrideSprite != null) sr.sprite = overrideSprite;
            
            else ApplySprite();
        }
    
        
        private void ApplySprite()
        {
            if (!sr)
                sr = GetComponent<SpriteRenderer>();
    
            if (sr == null || world == null || world.cellLibrary == null)
                return;
    
            sr.sprite = world.cellLibrary.GetSolidSprite(cellId);
        }
    
        
        
        
#if false
        void OnTriggerEnter2D(Collider2D other)
        {
            if (placed) return;
            if (world == null) return;
    
            if (((1 << other.gameObject.layer) & triggerMask.value) == 0)
                return;
    
            int gx = Mathf.FloorToInt(transform.position.x);
            int gy = Mathf.FloorToInt(transform.position.y);
    
            
            if (world.PlaceSolid(gx, gy, cellId))
            {
                placed = true;
                Destroy(gameObject);
            }
        }
#endif
    
        
        
        
#if false
        [System.Serializable]
        private class FallingBlockPayload
        {
            public ushort cellId;
        }
#endif
    }
}
