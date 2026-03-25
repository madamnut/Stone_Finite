// RotatingVfx.cs
// - 吏?뺣맂 Transform(=遺숈씤 ?ㅻ툕?앺듃)??rpm/?뚯쟾諛⑺뼢?濡?怨꾩냽 ?뚯쟾?쒗궡
using UnityEngine;

namespace Game.Support
{
    
    public class RotatingVfx : MonoBehaviour
    {
        // +1: CCW, -1: CW (Z異?湲곗?)
        [Range(-1, 1)] public int rotationDir = 1;
    
        [Min(0f)] public float rpm = 0f;
    
        void Update()
        {
            if (rpm <= 0f) return;
    
            float dir = rotationDir >= 0 ? 1f : -1f;
            float degPerSec = rpm * 6f; // 360deg * rpm / 60
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
