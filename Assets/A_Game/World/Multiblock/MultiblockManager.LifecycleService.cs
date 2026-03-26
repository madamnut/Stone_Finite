


using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game.World
{
    public partial class MultiblockManager
    {
        private sealed class MultiblockLifecycleService
        {

            readonly MultiblockServiceContext _ctx;

            
            public MultiblockLifecycleService(MultiblockServiceContext context)
            {
                _ctx = context;
            }

            
            public void RegisterBuiltInFactories()
            {
                RegisterFactory("Clay Kiln", () => new ClayKiln());
                RegisterFactory("Primal Workbench", () => new PrimalWorkbench());
                RegisterFactory("Forge Workbench", () => new ForgeWorkbench());
                RegisterFactory("Campfire", () => new Campfire());
                RegisterFactory("Wooden Crate", () => new WoodenCrate());
                RegisterFactory("Brick Furnace", () => new BrickFurnace());
                RegisterFactory("Toolbench", () => new Toolbench());
                RegisterFactory("Coke Oven", () => new CokeOven());
            }

            
            public void BindPlayerToVfx()
            {
                if (_ctx.Vfx != null && _ctx.PlayerTransform != null)
                    _ctx.Vfx.SetPlayer(_ctx.PlayerTransform);
            }

            
            public void TickInstances()
            {
                if (_ctx.Instances.Count == 0) return;

                List<Multiblock> snap = new List<Multiblock>(_ctx.Instances.Count);
                foreach (var kv in _ctx.Instances)
                    snap.Add(kv.Value);

                for (int i = 0; i < snap.Count; i++)
                {
                    var mb = snap[i];
                    if (mb == null) continue;

                    mb.Tick();
                    ApplyVfxRequests(mb);
                }
            }

            
            public void RegisterFactory(string defId, Func<Multiblock> creator)
            {
                _ctx.FactoryByDefId[defId] = creator;
            }

            
            void ApplyVfxRequests(Multiblock mb)
            {
                if (_ctx.Vfx == null) return;

                _ctx.VfxBuffer.Clear();
                mb.GetVfxRequests(_ctx.VfxBuffer);
                if (_ctx.VfxBuffer.Count == 0) return;

                for (int i = 0; i < _ctx.VfxBuffer.Count; i++)
                {
                    var r = _ctx.VfxBuffer[i];
                    Vector3 pos = new Vector3(mb.Origin.x + r.offset.x, mb.Origin.y + r.offset.y, 0f);
                    _ctx.Vfx.SetLoopVfx(mb.InstId, r.key, r.active, pos);
                }
            }
        }
    }
}
