


using UnityEngine;


namespace Game.World
{
    public sealed class SourceNode
    {
        public enum SourceKind { Waterwheel, Windmill }
        public enum RotationDir { CW, CCW }
    
        

        public int NodeId { get; private set; }
    
        
        public Vector2Int AttachedGearCenter { get; private set; }
    
        
        public SourceKind Kind { get; private set; }
        public int StressCapacity { get; private set; }
        public int BaseRpm { get; private set; }
    
        
        public bool IsActive { get; set; }
    
        
        public RotationDir Dir { get; set; } 
        public int Rpm { get; set; }         
    
        
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
