


using UnityEngine;









namespace Game.World
{
    public abstract class Entity : MonoBehaviour
    {
        

        public abstract EntityKind Kind { get; }
    
        
        public bool IsSimActive { get; private set; } = true;
    
        
        
        
        
        
        
        public virtual void SetSimActive(bool active)
        {
            IsSimActive = active;
            gameObject.SetActive(active);
        }
    
        
        
        
        
        public abstract EntitySaveData ToSaveData();
    
        
        
        
        
        public abstract void FromSaveData(EntitySaveData data);
    }
    
    
    
    
    [System.Serializable]
    public class EntitySaveData
    {
        public EntityKind Kind;
        public Vector2 Position;
        public string PayloadJson;
    }
    
    
    
    
    public enum EntityKind : byte
    {
        DroppedItem  = 0,
        FallingBlock = 1,
        Mob          = 2,
        Corpse       = 3,
    }
}
