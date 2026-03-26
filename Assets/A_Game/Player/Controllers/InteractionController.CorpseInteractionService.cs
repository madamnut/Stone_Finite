


namespace Game.Player
{
    public partial class InteractionController
    {
        private sealed class CorpseInteractionService
        {

            readonly InteractionController _owner;

            
            public CorpseInteractionService(InteractionController owner)
            {
                _owner = owner;
            }

            
            public bool TryCorpseInteraction()
            {
                if (_owner._state != GameState.Ingame) return false;
                if (_owner._hoverCorpse == null) return false;

                if (!_owner._heldItemService.TryGetHeldItem(out var held))
                    return false;

                if (held.ToolActions == null || held.ToolActions.Count == 0)
                    return false;

                foreach (var kv in held.ToolActions)
                {
                    string actionName = kv.Key;
                    if (string.IsNullOrEmpty(actionName))
                        continue;

                    if (_owner.corpseLibrary.TryProcessCorpse(_owner._hoverCorpse, actionName))
                    {
                        _owner._hoverCorpse.SetHovered(false);
                        _owner._hoverCorpse = null;
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
