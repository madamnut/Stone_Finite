


using UnityEngine;

namespace Game.Player
{
    
    public partial class Player
    {
        
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
