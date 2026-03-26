


using UnityEngine;

namespace Game.Support
{
    
    public class RotatingVfx : MonoBehaviour
    {
        
        [Range(-1, 1)] public int rotationDir = 1;
    
        [Min(0f)] public float rpm = 0f;
    
        
        void Update()
        {
            if (rpm <= 0f) return;
    

            float dir = rotationDir >= 0 ? 1f : -1f;
            float degPerSec = rpm * 6f; 
            float dz = degPerSec * dir * Time.deltaTime;
    
            transform.Rotate(0f, 0f, dz);
        }
    
        
        public void Set(float newRpm, int newDir)
        {
            rpm = Mathf.Max(0f, newRpm);
            rotationDir = newDir >= 0 ? 1 : -1;
        }
    }
}
