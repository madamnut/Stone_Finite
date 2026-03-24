using System.Collections;
using UnityEngine;

namespace Game.Player
{
    
    public partial class Player
    {
        private void TryDropThroughPlatform()
        {
            if (playerPhysicsCollider == null) return;
    
            if (_dropCo != null) return;
    
            _dropPlatforms.Clear();
    
            var contacts = new Collider2D[16];
            int n = rb.GetContacts(_platformContactFilter, contacts);
            for (int i = 0; i < n; i++)
            {
                var c = contacts[i];
                if (c == null) continue;
                if (!_dropPlatforms.Contains(c))
                    _dropPlatforms.Add(c);
            }
    
            if (_dropPlatforms.Count == 0) return;
    
            _dropCo = StartCoroutine(CoDropThroughPlatforms());
        }
    
        private IEnumerator CoDropThroughPlatforms()
        {
            for (int i = 0; i < _dropPlatforms.Count; i++)
            {
                var p = _dropPlatforms[i];
                if (p != null)
                    Physics2D.IgnoreCollision(playerPhysicsCollider, p, true);
            }
    
            yield return new WaitForSeconds(dropThroughTime);
    
            for (int i = 0; i < _dropPlatforms.Count; i++)
            {
                var p = _dropPlatforms[i];
                if (p != null)
                    Physics2D.IgnoreCollision(playerPhysicsCollider, p, false);
            }
    
            _dropPlatforms.Clear();
            _dropCo = null;
        }
    
        void SetFacing(int dir)
        {
            if (dir != -1 && dir != 1) return;
            if (_facing == dir) return;
    
            _facing = dir;
            ApplyFacingAndSorting();
        }
    
        void ApplyFacingAndSorting()
        {
            float sign = (_facing == -1) ? 1f : -1f;
            skinRoot.localScale = new Vector3(_baseSkinScaleX * sign, _baseSkinScaleY, _baseSkinScaleZ);
    
            if (_facing == -1)
            {
                leftArmRenderer.sortingOrder = _leftArmOrder;
                rightArmRenderer.sortingOrder = _rightArmOrder;
                leftLegRenderer.sortingOrder = _leftLegOrder;
                rightLegRenderer.sortingOrder = _rightLegOrder;
    
                rightHandItemRenderer.sortingOrder = _rightHandItemOrder;
            }
            else
            {
                leftArmRenderer.sortingOrder = _rightArmOrder;
                rightArmRenderer.sortingOrder = _leftArmOrder;
                leftLegRenderer.sortingOrder = _rightLegOrder;
                rightLegRenderer.sortingOrder = _leftLegOrder;
    
                rightHandItemRenderer.sortingOrder = -_rightHandItemOrder;
            }
        }
    
        void UpdateWalkAnimation()
        {
            bool isMovingHoriz = Mathf.Abs(_moveInput) > 0.01f;
    
            if (isMovingHoriz && _isGrounded)
            {
                _walkAnimPhase += Time.deltaTime * walkSwingSpeed * Mathf.Abs(_moveInput);
                float sin = Mathf.Sin(_walkAnimPhase);
    
                float armAngle = sin * walkArmAmplitude;
                float legAngle = sin * walkLegAmplitude;
    
                leftLegRenderer.transform.localRotation = _leftLegBaseRot * Quaternion.Euler(0f, 0f, +legAngle);
                rightLegRenderer.transform.localRotation = _rightLegBaseRot * Quaternion.Euler(0f, 0f, -legAngle);
    
                rightArmRenderer.transform.localRotation = _rightArmBaseRot * Quaternion.Euler(0f, 0f, +armAngle);
                leftArmRenderer.transform.localRotation = _leftArmBaseRot * Quaternion.Euler(0f, 0f, -armAngle);
            }
            else
            {
                float t = Time.deltaTime * walkReturnSpeed;
    
                leftLegRenderer.transform.localRotation = Quaternion.Lerp(leftLegRenderer.transform.localRotation, _leftLegBaseRot, t);
                rightLegRenderer.transform.localRotation = Quaternion.Lerp(rightLegRenderer.transform.localRotation, _rightLegBaseRot, t);
                rightArmRenderer.transform.localRotation = Quaternion.Lerp(rightArmRenderer.transform.localRotation, _rightArmBaseRot, t);
                leftArmRenderer.transform.localRotation = Quaternion.Lerp(leftArmRenderer.transform.localRotation, _leftArmBaseRot, t);
            }
        }
    }
}
