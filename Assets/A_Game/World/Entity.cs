using UnityEngine;

//
// ?”í‹°??ê³µí†µ ë² ì´???´ë˜??
// - DroppedItem, FallingBlock, Mob, Corpse ?±ì´ ?´ê²ƒ???ì†
// - ëª¨ë“  ?”í‹°?°ëŠ” ?™ì¼??ë°©ì‹?¼ë¡œ ?œì„±/ë¹„í™œ??SetSimActive) ì²˜ë¦¬
// - ?¸ì´ë¸?ë¡œë“œ???Œìƒ ?€?…ì´ êµ¬í˜„
//


namespace Game.World
{
    public abstract class Entity : MonoBehaviour
    {
        /// <summary>?”í‹°??ì¢…ë¥˜ ?ë³„??/summary>
        public abstract EntityKind Kind { get; }
    
        /// <summary>?„ì¬ ?œë??ˆì´???œì„± ?¬ë?</summary>
        public bool IsSimActive { get; private set; } = true;
    
        /// <summary>
        /// ?”í‹°?°ë? ?µì§¸ë¡??œì„±/ë¹„í™œ???„í™˜.
        /// ê°œë³„ ì»´í¬?ŒíŠ¸ ?œì–´ ?†ì´ GameObject.SetActive ë§??¬ìš©.
        /// ëª¨ë“  ?”í‹°??ê³µí†µ ì²˜ë¦¬.
        /// </summary>
        public virtual void SetSimActive(bool active)
        {
            IsSimActive = active;
            gameObject.SetActive(active);
        }
    
        /// <summary>
        /// ?„ì¬ ?”í‹°???íƒœë¥??€???°ì´?°ë¡œ ë³€??
        /// </summary>
        public abstract EntitySaveData ToSaveData();
    
        /// <summary>
        /// ?€?¥ëœ ?°ì´?°ë? ê¸°ë°˜?¼ë¡œ ?”í‹°???íƒœ ë³µì›
        /// </summary>
        public abstract void FromSaveData(EntitySaveData data);
    }
    
    /// <summary>
    /// ?¸ì´ë¸Œìš© ?„ìš© ?°ì´??êµ¬ì¡°
    /// </summary>
    [System.Serializable]
    public class EntitySaveData
    {
        public EntityKind Kind;
        public Vector2 Position;
        public string PayloadJson;
    }
    
    /// <summary>
    /// ?”í‹°??ì¢…ë¥˜
    /// </summary>
    public enum EntityKind : byte
    {
        DroppedItem  = 0,
        FallingBlock = 1,
        Mob          = 2,
        Corpse       = 3,
    }
}
