


using System.Collections.Generic;
using UnityEngine;

using Game.Data;

namespace Game.World
{
    public sealed partial class GearNetworkManager
    {
        
        void Awake()
        {
            if (vfx == null && world != null)

                vfx = world.vfx;
    
            CacheUtilityOccupiedId();
        }
    
        
        void CacheUtilityOccupiedId()
        {
            _utilityOccupiedId = 0;
            if (world == null || world.cellLibrary == null) return;
    
            if (world.cellLibrary.TryGetUtilityIdByName("CogwheelOccupied", out var occ))
                _utilityOccupiedId = occ;
        }
    
        
        void EnsureOccupiedCached()
        {
            if (_utilityOccupiedId != 0) return;
            CacheUtilityOccupiedId();
        }
    
        
        bool IsUtilityOccupiedCell(Vector2Int c)
        {
            if (world == null) return false;
            if (!world.InBounds(c.x, c.y)) return false;
    
            EnsureOccupiedCached();
            ushort uid = world.GetUtilityId(c.x, c.y);
            if (uid == 0) return false;
    
            return (_utilityOccupiedId != 0 && uid == _utilityOccupiedId);
        }
    
        
        bool IsGearCenterCell(Vector2Int c)
        {
            if (world == null) return false;
            if (!world.InBounds(c.x, c.y)) return false;
    
            if (IsUtilityOccupiedCell(c)) return false;
            return _gearCenterToNodeId.ContainsKey(c);
        }
    
        
        void EnsureVfxRef()
        {
            if (vfx == null && world != null)
                vfx = world.vfx;
        }
    
        
        static Vector3 CellCenterToWorld(Vector2Int c) => new Vector3(c.x + 0.5f, c.y + 0.5f, 0f);
    
        
        
        
        
        public void RegisterCogwheelSpec(string gearId, GearNode.GearSize size, int maxRpm)
        {
            if (string.IsNullOrEmpty(gearId)) return;
            if (maxRpm < 0) maxRpm = 0;
            _gearSpecById[gearId] = new GearSpec { size = size, maxRpm = maxRpm };
        }
    
        
        public void RegisterSourceSpec(string sourceId, SourceNode.SourceKind kind, int rpm, int stressCapacity)
        {
            if (string.IsNullOrEmpty(sourceId)) return;
            if (rpm < 0) rpm = 0;
            if (stressCapacity < 0) stressCapacity = 0;
            _sourceSpecById[sourceId] = new SourceSpec { kind = kind, rpm = rpm, stressCapacity = stressCapacity };
        }
    
        
        public void RegisterBeltSpec(string beltKind, int maxRpm, string materialItemId, Color color)
        {
            if (string.IsNullOrEmpty(beltKind)) return;
            if (maxRpm < 0) maxRpm = 0;
            if (string.IsNullOrEmpty(materialItemId)) materialItemId = null;
            _beltSpecById[beltKind] = new BeltSpec { maxRpm = maxRpm, materialItemId = materialItemId, color = color };
        }
    
        
        
        
        
        public void TickSources()
        {
            if (world == null) return;
            if (_sourceNodes.Count == 0) return;
    
            foreach (var kv in _sourceNodes)
            {
                int srcNodeId = kv.Key;
                var src = kv.Value;
    
                if (!_sourceIdByNodeId.TryGetValue(srcNodeId, out var sourceId))
                    continue;
    
                if (_sourceSpecById.TryGetValue(sourceId, out var spec))
                {
                    src.Dir = SourceNode.RotationDir.CW;
    
                    if (src.Kind == SourceNode.SourceKind.Windmill)
                    {
                        src.IsActive = true;
                        src.Rpm = spec.rpm;
                    }
                    else
                    {
                        var c = src.AttachedGearCenter;
    
                        bool ok =
                            IsWaterAt(c.x - 1, c.y - 1) &&
                            IsWaterAt(c.x + 0, c.y - 1) &&
                            IsWaterAt(c.x + 1, c.y - 1);
    
                        src.IsActive = ok;
                        src.Rpm = ok ? spec.rpm : 0;
                    }
    
                    src.SetBaseRpm(spec.rpm);
                    src.SetStressCapacity(spec.stressCapacity);
                    src.SetKind(spec.kind);
                }
                else
                {
                    src.Dir = SourceNode.RotationDir.CW;
    
                    if (src.Kind == SourceNode.SourceKind.Windmill)
                    {
                        src.IsActive = true;
                    }
                    else
                    {
                        var c = src.AttachedGearCenter;
                        bool ok =
                            IsWaterAt(c.x - 1, c.y - 1) &&
                            IsWaterAt(c.x + 0, c.y - 1) &&
                            IsWaterAt(c.x + 1, c.y - 1);
                        src.IsActive = ok;
                    }
                }
            }
        }
    
        
        public void TickNetworks()
        {
            if (world == null) return;
    
            _pendingBreakCenters.Clear();
            _pendingBreakSet.Clear();
    
            ClearNetworks();
            BuildAllNetworks();
    
            if (_pendingBreakCenters.Count > 0)
            {
                _suppressRebuild = true;
    
                for (int i = 0; i < _pendingBreakCenters.Count; i++)
                {
                    var c = _pendingBreakCenters[i];
                    world.BreakUtility(c.x, c.y);
                }
    
                _suppressRebuild = false;
    
                ClearNetworks();
                BuildAllNetworks();
            }
        }
    
    }
}
