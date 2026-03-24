using UnityEngine;

using Game.Data;
using Game.Player;

namespace Game.World
{
    public partial class MultiblockManager
    {
        public void OpenModule(string moduleId, Multiblock owner)
        {
            GameObject prefab = moduleId switch
            {
                "PrimalCraft"     => primalCraftModule,
                "ForgeCraft"  => forgeCraftModule, // ??異붽?
                "Campfire"        => campfireModule,
                "Wooden Crate"    => woodenCrateModule,
                "Clay Kiln"       => clayKilnModule,
                "Brick Furnace"   => brickFurnaceModule,
                "Toolbench"       => toolbenchModule,
                "Coke Oven"       => cokeOvenModule,   // ??異붽?
                _ => null
            };
    
            if (prefab == null) return;
            if (interaction == null) return;
    
            var instGO = interaction.OpenModule(prefab);
            if (instGO == null) return;
    
            if (moduleId == "Campfire" && owner is Campfire campfire)
            {
                var ui = instGO.GetComponentInChildren<CampfireModule>(true);
                if (ui != null)
                    ui.Bind(campfire);
            }
            else if (moduleId == "Wooden Crate" && owner is WoodenCrate crate)
            {
                var ui = instGO.GetComponentInChildren<WoodenCrateModule>(true);
                if (ui != null)
                    ui.Bind(crate);
            }
            else if (moduleId == "Clay Kiln" && owner is ClayKiln kiln)
            {
                var ui = instGO.GetComponentInChildren<ClayKilnModule>(true);
                if (ui != null)
                    ui.Bind(kiln);
            }
            else if (moduleId == "Brick Furnace" && owner is BrickFurnace furnace)
            {
                var ui = instGO.GetComponentInChildren<BrickFurnaceModule>(true);
                if (ui != null)
                    ui.Bind(furnace);
            }
            else if (moduleId == "Toolbench" && owner is Toolbench toolbench)
            {
                var ui = instGO.GetComponentInChildren<ToolbenchModule>(true);
                if (ui != null)
                {
                    // ??ToolbenchModule? CraftModule???꾨땲誘濡??ш린??吏곸젒 二쇱엯
                    ui.recipeLibrary = interaction.recipeLibrary;
                    ui.player = interaction.player;
    
                    ui.Bind(toolbench);
                }
            }
            else if (moduleId == "Coke Oven" && owner is CokeOven cokeOven)
            {
                var ui = instGO.GetComponentInChildren<CokeOvenModule>(true);
                if (ui != null)
                {
                    ui.Bind(cokeOven);
                }
            }
            // ??ForgeWorkbench??PrimalCraft? ?숈씪?섍쾶 "紐⑤뱢 ?대??먯꽌" recipeLibrary/player ?깆쓣 泥섎━?쒕떎怨?媛??
            // (異붽? 諛붿씤???꾩슂 ???ш린??耳?댁뒪 異붽?)
        }
    }
}
