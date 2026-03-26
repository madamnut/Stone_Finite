


using UnityEngine;
using Game.World;

namespace Game.Player
{
    public sealed class CorpseHoverQueryService : MonoBehaviour
    {
        [SerializeField] private LayerMask corpseLayerMask;


        readonly Collider2D[] _hits = new Collider2D[16];

        
        public Corpse Query(Vector2 worldPoint)
        {
            Corpse bestCorpse = null;
            int bestOrder = int.MinValue;

            int count = Physics2D.OverlapPointNonAlloc(worldPoint, _hits, corpseLayerMask);
            for (int i = 0; i < count; i++)
            {
                var col = _hits[i];
                _hits[i] = null;
                if (col == null)
                    continue;

                var corpse = col.GetComponentInParent<Corpse>();
                if (corpse == null)
                    continue;

                int order = corpse.mainRenderer != null ? corpse.mainRenderer.sortingOrder : 0;
                if (bestCorpse == null || order > bestOrder)
                {
                    bestCorpse = corpse;
                    bestOrder = order;
                }
            }

            return bestCorpse;
        }
    }
}
