


using UnityEngine;

using Game.UI;
using Game.World;

namespace Game.Player
{
    public partial class InteractionController
    {
        
        void InitializeMultiblockUiBridge()
        {
            if (multiblockManager == null)

                return;

            multiblockManager.moduleOpenHandler = HandleOpenMultiblockModule;
            multiblockManager.playerTransform = player != null ? player.transform : null;
        }

        
        void OnDestroy()
        {
            if (multiblockManager == null)
                return;

            if (multiblockManager.moduleOpenHandler == HandleOpenMultiblockModule)
                multiblockManager.moduleOpenHandler = null;
        }

        
        void HandleOpenMultiblockModule(string moduleId, Multiblock owner)
        {
            GameObject prefab = moduleId switch
            {
                "PrimalCraft" => multiblockManager.primalCraftModule,
                "ForgeCraft" => multiblockManager.forgeCraftModule,
                "Campfire" => multiblockManager.campfireModule,
                "Wooden Crate" => multiblockManager.woodenCrateModule,
                "Clay Kiln" => multiblockManager.clayKilnModule,
                "Brick Furnace" => multiblockManager.brickFurnaceModule,
                "Toolbench" => multiblockManager.toolbenchModule,
                "Coke Oven" => multiblockManager.cokeOvenModule,
                _ => null
            };

            if (prefab == null)
                return;

            var instGO = OpenModule(prefab);
            if (instGO == null)
                return;

            if (moduleId == "Campfire" && owner is Campfire campfire)
            {
                var ui = instGO.GetComponentInChildren<CampfireModule>(true);
                if (ui != null)
                    ui.Bind(campfire);
                return;
            }

            if (moduleId == "Wooden Crate" && owner is WoodenCrate crate)
            {
                var ui = instGO.GetComponentInChildren<WoodenCrateModule>(true);
                if (ui != null)
                    ui.Bind(crate);
                return;
            }

            if (moduleId == "Clay Kiln" && owner is ClayKiln kiln)
            {
                var ui = instGO.GetComponentInChildren<ClayKilnModule>(true);
                if (ui != null)
                    ui.Bind(kiln);
                return;
            }

            if (moduleId == "Brick Furnace" && owner is BrickFurnace furnace)
            {
                var ui = instGO.GetComponentInChildren<BrickFurnaceModule>(true);
                if (ui != null)
                    ui.Bind(furnace);
                return;
            }

            if (moduleId == "Toolbench" && owner is Toolbench toolbench)
            {
                var ui = instGO.GetComponentInChildren<ToolbenchModule>(true);
                if (ui != null)
                {
                    ui.recipeLibrary = recipeLibrary;
                    ui.Bind(toolbench);
                }
                return;
            }

            if (moduleId == "Coke Oven" && owner is CokeOven cokeOven)
            {
                var ui = instGO.GetComponentInChildren<CokeOvenModule>(true);
                if (ui != null)
                    ui.Bind(cokeOven);
            }
        }
    }
}
