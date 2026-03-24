using UnityEngine;
using Newtonsoft.Json;

using Game.Data;

namespace Game.World
{
    public class FallingBlock : Entity
    {
        [Header("References")]
        [SerializeField] private WorldManager world;
        [SerializeField] private SpriteRenderer sr;
        [SerializeField] private LayerMask triggerMask;
    
        [Header("Data")]
        [SerializeField] private ushort cellId;
        [SerializeField] private bool placed;
    
        public override EntityKind Kind => EntityKind.FallingBlock;
    
        //?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Save / Load
        //?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        public override EntitySaveData ToSaveData()
        {
            // ?¥Î? ?ÄÎ°?Î∞ïÌûå ?ÅÌÉúÎ©??Ä???òÎ? ?ÜÏùå
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
    
        //?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Init
        //?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
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
    
        //?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Collision ??Cell placement
        //?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        void OnTriggerEnter2D(Collider2D other)
        {
            if (placed) return;
            if (world == null) return;
    
            if (((1 << other.gameObject.layer) & triggerMask.value) == 0)
                return;
    
            int gx = Mathf.FloorToInt(transform.position.x);
            int gy = Mathf.FloorToInt(transform.position.y);
    
            // ???©Ïñ¥ Î≥ÄÍ≤? PlaceFG -> PlaceSolid
            if (world.PlaceSolid(gx, gy, cellId))
            {
                placed = true;
                Destroy(gameObject);
            }
        }
    
        //?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        // Payload
        //?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä?Ä
        [System.Serializable]
        private class FallingBlockPayload
        {
            public ushort cellId;
        }
    }
}
