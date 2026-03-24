// SourceNode.cs (?„ì²´ êµì²´ë³?
// ë³€ê²½ì :
// - ?ŒìŠ¤ ?œì„±/ë¹„í™œ??IsActive) ì¶”ê? (Windmill=true ê³ ì •, Waterwheel?€ ì¡°ê±´ê²€??ê²°ê³¼ë¡?? ê?)
// - BaseRpm ì¶”ê?(ATT?ì„œ ?½ì? rpm ?ë³¸)
// - "?„ìž¬ ì¶œë ¥"?€ IsActive???˜í•´ ?ë™?¼ë¡œ 0?¼ë¡œ ?¨ì–´ì§€?„ë¡ CurrentRpm / CurrentStressCapacity ?œê³µ
// - ?Œì „ë°©í–¥?€ ê¸°ë³¸ CWë¡?ê³ ì •(?”êµ¬?¬í•­), ?„ìš”?˜ë©´ Dirë§?? ì?

using UnityEngine;


namespace Game.World
{
    public sealed class SourceNode
    {
        public enum SourceKind { Waterwheel, Windmill }
        public enum RotationDir { CW, CCW }
    
        // Identity (assigned by GearNetworkManager)
        public int NodeId { get; private set; }
    
        // Attachment: which gear this source is attached to (gear center coord)
        public Vector2Int AttachedGearCenter { get; private set; }
    
        // Kind/spec (ATT ê¸°ë°˜)
        public SourceKind Kind { get; private set; }
        public int StressCapacity { get; private set; }
        public int BaseRpm { get; private set; }
    
        // Runtime state (Waterwheel ì¡°ê±´???˜í•´ ? ê?)
        public bool IsActive { get; set; }
    
        // Output (?½ê¸° ?„ìš© ?•íƒœë¡??°ëŠ” ê±?ê¶Œìž¥)
        public RotationDir Dir { get; set; } // ?”êµ¬?¬í•­: ê¸°ë³¸ CW
        public int Rpm { get; set; }         // ?„ìš” ???¸ë??ì„œ ??–´?????ˆê²Œ ? ì? (ê¸°ë³¸?€ BaseRpm ?¬ìš©)
    
        // ??Solver/?¸ë??ì„œ "?¤ì œ ê¸°ì—¬ê°??¼ë¡œ ?°ë¼ê³??œê³µ
        public int CurrentRpm => IsActive ? Mathf.Max(0, (Rpm > 0 ? Rpm : BaseRpm)) : 0;
        public int CurrentStressCapacity => IsActive ? Mathf.Max(0, StressCapacity) : 0;
    
        public SourceNode(
            int nodeId,
            Vector2Int attachedGearCenter,
            SourceKind kind,
            int stressCapacity,
            int baseRpm
        )
        {
            NodeId = nodeId;
            AttachedGearCenter = attachedGearCenter;
    
            Kind = kind;
            StressCapacity = Mathf.Max(0, stressCapacity);
            BaseRpm = Mathf.Max(0, baseRpm);
    
            Dir = RotationDir.CW;
            Rpm = 0;
    
            // Windmill?€ ??ƒ ?œì„±, Waterwheel?€ ì¡°ê±´???°ë¼(ì´ˆê¸° false ê¶Œìž¥)
            IsActive = (kind == SourceKind.Windmill);
        }
    
        public void SetAttachment(Vector2Int newGearCenter) => AttachedGearCenter = newGearCenter;
        public void SetStressCapacity(int newCapacity) => StressCapacity = Mathf.Max(0, newCapacity);
        public void SetBaseRpm(int newBaseRpm) => BaseRpm = Mathf.Max(0, newBaseRpm);
        public void SetKind(SourceKind newKind)
        {
            Kind = newKind;
            if (Kind == SourceKind.Windmill) IsActive = true;
        }
    }
}
