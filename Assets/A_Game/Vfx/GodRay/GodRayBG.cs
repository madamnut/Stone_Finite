


using UnityEngine;

namespace Game.Support
{

    [ExecuteAlways]
    public class GodRay : MonoBehaviour
    {

        public Material material;
        public Transform sun;
        public Camera mainCamera;

        [Header("Strength")]
        [Min(0.001f)] public float maxStrength = 1.7f; 
        [Min(0.001f)] public float yRange = 12f;       

        
        void Update()
        {
            if (!material || !sun || !mainCamera) return;
            if (!material.HasProperty("_Center") || !material.HasProperty("_Intensity")) return;

            
            float y = sun.localPosition.y;
            float strength;

            if (y >= 0f)
            {
                strength = maxStrength;
            }
            else
            {
                float cy = Mathf.Clamp(y, -yRange, 0f);
                float t = 1f - (cy * cy) / (yRange * yRange); 
                strength = Mathf.Clamp01(t) * maxStrength;
            }

            material.SetFloat("_Intensity", strength);

            
            Vector3 sp = mainCamera.WorldToScreenPoint(sun.position);

            float w = Mathf.Max(1, mainCamera.pixelWidth);
            float h = Mathf.Max(1, mainCamera.pixelHeight);

            Vector2 uv = new Vector2(sp.x / w, sp.y / h);
            material.SetVector("_Center", new Vector4(uv.x, uv.y, 0f, 0f));
        }
    }
}
