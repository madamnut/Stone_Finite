


using UnityEngine;

namespace Game.Player
{
    
    public class CameraFollow : MonoBehaviour
    {
        [Header("Target")]

        public Transform target;
    
        [Header("Offset")]
        public Vector3 offset = new Vector3(0f, 0f, -10f);
    
        
        void LateUpdate()
        {
            if (target == null)
                return;
    
            transform.position = target.position + offset;
        }
    }
}
