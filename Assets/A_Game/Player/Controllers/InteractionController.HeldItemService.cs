using Game.Core;

namespace Game.Player
{
    public partial class InteractionController
    {
        private sealed class HeldItemService
        {
            readonly InteractionController _owner;

            public HeldItemService(InteractionController owner)
            {
                _owner = owner;
            }

            public bool TryGetHeldItem(out ItemData held)
            {
                held = null;

                if (_owner.player == null || _owner.player.Inventory == null)
                    return false;

                var items = _owner.player.Inventory.items;
                if (_owner._hotbarScope < 0 || _owner._hotbarScope >= items.Count)
                    return false;

                held = items[_owner._hotbarScope];
                return held != null && held.Count > 0;
            }

            public bool Consume(ItemData held, int amount)
            {
                if (held == null || amount <= 0)
                    return false;

                held.Count -= amount;
                if (held.Count <= 0)
                    _owner.player.Inventory.items[_owner._hotbarScope] = null;

                _owner.player.Inventory.NotifyChanged();
                _owner.RefreshHeldHandSprite();
                return true;
            }
        }
    }
}
