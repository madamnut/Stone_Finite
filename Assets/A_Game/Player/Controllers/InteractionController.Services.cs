using UnityEngine;

namespace Game.Player
{
    public partial class InteractionController
    {
        HeldItemService _heldItemService;
        BlockInteractionService _blockInteractionService;
        UtilityInteractionService _utilityInteractionService;
        GearInteractionService _gearInteractionService;
        MultiblockInteractionService _multiblockInteractionService;
        CorpseInteractionService _corpseInteractionService;

        void InitializeBuildServices()
        {
            _heldItemService ??= new HeldItemService(this);
            _blockInteractionService ??= new BlockInteractionService(this);
            _utilityInteractionService ??= new UtilityInteractionService(this);
            _gearInteractionService ??= new GearInteractionService(this);
            _multiblockInteractionService ??= new MultiblockInteractionService(this);
            _corpseInteractionService ??= new CorpseInteractionService(this);
        }
    }
}
