using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Game.Player
{
    public sealed class PlatformDropThroughService : MonoBehaviour
    {
        [SerializeField] private Rigidbody2D body;
        [SerializeField] private Collider2D playerPhysicsCollider;
        [SerializeField] private LayerMask platformLayerMask;
        [SerializeField] private float dropThroughTime = 0.10f;

        readonly List<Collider2D> _dropPlatforms = new List<Collider2D>(16);
        readonly Collider2D[] _contacts = new Collider2D[16];
        ContactFilter2D _platformContactFilter;
        Coroutine _dropCo;

        void Awake()
        {
            if (body == null)
                body = GetComponentInParent<Rigidbody2D>();

            _platformContactFilter = new ContactFilter2D
            {
                useLayerMask = true,
                layerMask = platformLayerMask,
                useTriggers = false
            };
        }

        public bool TryDropThrough()
        {
            if (body == null || playerPhysicsCollider == null || _dropCo != null)
                return false;

            _dropPlatforms.Clear();

            int count = body.GetContacts(_platformContactFilter, _contacts);
            for (int i = 0; i < count; i++)
            {
                var contact = _contacts[i];
                _contacts[i] = null;
                if (contact == null)
                    continue;

                if (!_dropPlatforms.Contains(contact))
                    _dropPlatforms.Add(contact);
            }

            if (_dropPlatforms.Count == 0)
                return false;

            _dropCo = StartCoroutine(CoDropThroughPlatforms());
            return true;
        }

        IEnumerator CoDropThroughPlatforms()
        {
            for (int i = 0; i < _dropPlatforms.Count; i++)
            {
                var platform = _dropPlatforms[i];
                if (platform != null)
                    Physics2D.IgnoreCollision(playerPhysicsCollider, platform, true);
            }

            yield return new WaitForSeconds(dropThroughTime);

            for (int i = 0; i < _dropPlatforms.Count; i++)
            {
                var platform = _dropPlatforms[i];
                if (platform != null)
                    Physics2D.IgnoreCollision(playerPhysicsCollider, platform, false);
            }

            _dropPlatforms.Clear();
            _dropCo = null;
        }
    }
}
