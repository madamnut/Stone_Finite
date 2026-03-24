using System;
using System.Collections.Generic;
using UnityEngine;
using Game.Player;


namespace Game.World
{
    public partial class MultiblockManager
    {
        void Awake()
        {
            RegisterFactory("Clay Kiln", () => new ClayKiln());
            RegisterFactory("Primal Workbench", () => new PrimalWorkbench());
            RegisterFactory("Forge Workbench", () => new ForgeWorkbench()); // ??異붽?
            RegisterFactory("Campfire", () => new Campfire());
            RegisterFactory("Wooden Crate", () => new WoodenCrate());
            RegisterFactory("Brick Furnace", () => new BrickFurnace());
            RegisterFactory("Toolbench", () => new Toolbench()); // ??異붽?
    
            RegisterFactory("Coke Oven", () => new CokeOven());   // ??異붽? (Def.key? ?숈씪?댁빞 ??
        }
    
        void Start()
        {
            if (vfx != null && interaction != null && interaction.player != null)
                vfx.SetPlayer(interaction.player.transform);
        }
    
        void FixedUpdate()
        {
            if (_instances.Count == 0) return;
    
            List<Multiblock> snap = new List<Multiblock>(_instances.Count);
            foreach (var kv in _instances)
                snap.Add(kv.Value);
    
            for (int i = 0; i < snap.Count; i++)
            {
                var mb = snap[i];
                if (mb == null) continue;
    
                mb.Tick();
                ApplyVfxRequests(mb);
            }
        }
    
        void ApplyVfxRequests(Multiblock mb)
        {
            if (vfx == null) return;
    
            _vfxBuf.Clear();
            mb.GetVfxRequests(_vfxBuf);
            if (_vfxBuf.Count == 0) return;
    
            for (int i = 0; i < _vfxBuf.Count; i++)
            {
                var r = _vfxBuf[i];
    
                Vector3 pos = new Vector3(
                    mb.Origin.x + r.offset.x,
                    mb.Origin.y + r.offset.y,
                    0f
                );
    
                vfx.SetLoopVfx(mb.InstId, r.key, r.active, pos);
            }
        }
    
        public void RegisterFactory(string defId, Func<Multiblock> creator)
        {
            _factoryByDefId[defId] = creator;
        }
    }
}
