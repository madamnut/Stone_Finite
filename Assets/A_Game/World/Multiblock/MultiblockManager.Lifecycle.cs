using System;
using System.Collections.Generic;
using UnityEngine;


namespace Game.World
{
    public partial class MultiblockManager
    {
        void Awake()
        {
            RegisterFactory("Clay Kiln", () => new ClayKiln());
            RegisterFactory("Primal Workbench", () => new PrimalWorkbench());
            RegisterFactory("Forge Workbench", () => new ForgeWorkbench()); // ???곕떽?
            RegisterFactory("Campfire", () => new Campfire());
            RegisterFactory("Wooden Crate", () => new WoodenCrate());
            RegisterFactory("Brick Furnace", () => new BrickFurnace());
            RegisterFactory("Toolbench", () => new Toolbench()); // ???곕떽?
    
            RegisterFactory("Coke Oven", () => new CokeOven());   // ???곕떽? (Def.key?? ??덉뵬??곷튊 ??
        }
    
        void Start()
        {
            if (vfx != null && playerTransform != null)
                vfx.SetPlayer(playerTransform);
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
