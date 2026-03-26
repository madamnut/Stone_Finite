using UnityEngine;
using UnityEngine.UI;
using UnityEngine.U2D;

namespace Game.UI
{
    
    public class Heart : MonoBehaviour
    {
        public Image heartImage;
    
        // fill: 0~4
        public void SetHeart(SpriteAtlas atlas, int fill)
        {
            if (atlas == null)
                return;
    
            if (heartImage == null)
                heartImage = GetComponent<Image>();
    
            if (heartImage == null)
                return;
    
            int clamped = Mathf.Clamp(fill, 0, 4);
            var sprite = atlas.GetSprite(clamped.ToString());
            if (sprite == null)
                return;
    
            heartImage.sprite = sprite;
        }
    }
}
