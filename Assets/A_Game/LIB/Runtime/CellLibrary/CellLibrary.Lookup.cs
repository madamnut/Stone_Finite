using UnityEngine;
using Game.World;


namespace Game.Data
{
    public partial class CellLibrary
    {
        public SolidCell MakeSolidCell(ushort id, ushort meta = 0) => new SolidCell { id = id, meta = meta };
        public UtilityCell MakeUtilityCell(ushort id, ushort meta = 0) => new UtilityCell { id = id, meta = meta };
        public FluidCell MakeFluidCell(ushort id, byte amount) => new FluidCell { id = id, amount = amount };
    
        // ?€?€?€?€?€?€?€?€?€ Lookups ?€?€?€?€?€?€?€?€?€
        public string GetSolidName(ushort id) => _solidById.TryGetValue(id, out var def) ? def.name : null;
        public string GetSolidType(ushort id) => _solidById.TryGetValue(id, out var def) ? def.type : "Default";
    
        public string GetUtilityName(ushort id) => _utilityById.TryGetValue(id, out var def) ? def.name : null;
        public string GetUtilityType(ushort id) => _utilityById.TryGetValue(id, out var def) ? def.type : "Default";
    
        public string GetFluidName(ushort id) => _fluidById.TryGetValue(id, out var def) ? def.name : null;
    
        public SolidFlags GetSolidFlags(ushort id) => _solidById.TryGetValue(id, out var def) ? def.flags : SolidFlags.None;
        public bool IsPlatform(ushort id) => _solidById.TryGetValue(id, out var def) && def.isPlatform;
    
        public bool HasSolidVariant(ushort id, ushort meta)
            => _solidById.TryGetValue(id, out var def) && def.variants != null && def.variants.ContainsKey(meta);
    
        public bool HasUtilityVariant(ushort id, ushort meta)
            => _utilityById.TryGetValue(id, out var def) && def.variants != null && def.variants.ContainsKey(meta);
    
        public byte GetSolidBrightness(ushort id) => _solidById.TryGetValue(id, out var def) ? def.brightness : (byte)0;
    
        public byte GetSolidBrightness(ushort id, ushort meta)
        {
            if (!_solidById.TryGetValue(id, out var def)) return 0;
    
            if (def.variants != null && def.variants.TryGetValue(meta, out var v))
            {
                if (v.brightnessOverride >= 0) return (byte)v.brightnessOverride;
            }
            return def.brightness;
        }
    
        public byte GetFluidBrightness(ushort id) => _fluidById.TryGetValue(id, out var def) ? def.brightness : (byte)0;
    
        public bool TryGetSolidIdByName(string name, out ushort id) => _solidIdByName.TryGetValue(name, out id);
        public bool TryGetUtilityIdByName(string name, out ushort id) => _utilityIdByName.TryGetValue(name, out id);
        public bool TryGetFluidIdByName(string name, out ushort id) => _fluidIdByName.TryGetValue(name, out id);
    
        public bool GetInteraction(ushort id, out string interaction)
        {
            if (_solidById.TryGetValue(id, out var def) && !string.IsNullOrEmpty(def.interaction))
            {
                interaction = def.interaction;
                return true;
            }
            interaction = null;
            return false;
        }
    
        public bool GetAttachedAt(ushort id, ushort meta, out string attachedAt)
        {
            if (_solidById.TryGetValue(id, out var def) &&
                def.variants != null &&
                def.variants.TryGetValue(meta, out var v) &&
                !string.IsNullOrEmpty(v.attachedAt))
            {
                attachedAt = v.attachedAt;
                return true;
            }
            attachedAt = null;
            return false;
        }
    }
}
