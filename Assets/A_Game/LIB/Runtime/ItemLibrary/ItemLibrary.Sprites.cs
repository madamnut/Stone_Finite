using System;
using System.Collections.Generic;
using UnityEngine;


namespace Game.Data
{
    public partial class ItemLibrary
    {
        public Sprite GetSprite(string spriteName)
        {
            if (string.IsNullOrEmpty(spriteName)) return null;
    
            if (_spriteCache.TryGetValue(spriteName, out var cached))
                return cached;
    
            // ?©ÏÑ± ?§ÌîÑ?ºÏù¥??("A + B + C")
            if (spriteName.Contains(" + "))
            {
                var parts = spriteName.Split(new[] { " + " }, StringSplitOptions.RemoveEmptyEntries);
                var sprites = new List<Sprite>(parts.Length);
    
                for (int i = 0; i < parts.Length; i++)
                {
                    string key = parts[i].Trim();
    
                    if (_spriteCache.TryGetValue(key, out var partCached))
                    {
                        sprites.Add(partCached);
                        continue;
                    }
    
                    Sprite s = null;
    
                    // Resources (Í∏∞Î≥∏)
                    s = Resources.Load<Sprite>(key);
                    // ItemParts ?¥Îçî
                    if (s == null)
                        s = Resources.Load<Sprite>("Textures/ItemParts/" + key);
                    // Atlas
                    if (s == null && itemAtlas != null)
                        s = itemAtlas.GetSprite(key);
    
                    if (s == null)
                    {
                        Debug.LogWarning($"[ItemLibrary] ?©ÏÑ± ?§ÌîÑ?ºÏù¥???åÏä§ ?ÜÏùå: {key}");
                        continue;
                    }
    
                    _spriteCache[key] = s;
                    sprites.Add(s);
                }
    
                if (sprites.Count == 0) return null;
    
                // ?©ÏÑ± ?ëÏóÖ
                var baseS = sprites[0];
                int w = (int)baseS.rect.width;
                int h = (int)baseS.rect.height;
    
                var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
                tex.filterMode = FilterMode.Point;
                tex.SetPixels32(new Color32[w * h]);
    
                var dst = tex.GetPixels32();
    
                void BlendOver(Color32[] dstBuf, Color32[] srcBuf)
                {
                    for (int i = 0; i < dstBuf.Length; i++)
                    {
                        float aS = srcBuf[i].a / 255f;
                        if (aS <= 0f) continue;
    
                        float aD = dstBuf[i].a / 255f;
                        float outA = aS + aD * (1 - aS);
    
                        float r = (srcBuf[i].r / 255f) * aS + (dstBuf[i].r / 255f) * aD * (1 - aS);
                        float g = (srcBuf[i].g / 255f) * aS + (dstBuf[i].g / 255f) * aD * (1 - aS);
                        float b = (srcBuf[i].b / 255f) * aS + (dstBuf[i].b / 255f) * aD * (1 - aS);
    
                        if (outA > 0f)
                        {
                            r /= outA;
                            g /= outA;
                            b /= outA;
                        }
    
                        dstBuf[i] = new Color(r, g, b, outA);
                    }
                }
    
                for (int i = sprites.Count - 1; i >= 0; i--)
                {
                    var s = sprites[i];
                    var srcTex = s.texture;
                    var r = s.rect;
    
                    int sx = (int)r.x;
                    int sy = (int)r.y;
                    int sw = (int)r.width;
                    int sh = (int)r.height;
    
                    var srcColors = srcTex.GetPixels(sx, sy, sw, sh);
                    Color32[] srcBuf = Array.ConvertAll(srcColors, c => (Color32)c);
    
                    var lay = new Color32[w * h];
                    int offX = (w - sw) / 2;
                    int offY = (h - sh) / 2;
    
                    for (int row = 0; row < sh; row++)
                    {
                        Array.Copy(srcBuf, row * sw, lay, (offY + row) * w + offX, sw);
                    }
    
                    BlendOver(dst, lay);
                }
    
                tex.SetPixels32(dst);
                tex.Apply();
    
                var finalSprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
                _spriteCache[spriteName] = finalSprite;
    
                return finalSprite;
            }
    
            // ?®Ïùº ?§ÌîÑ?ºÏù¥??
            Sprite single = Resources.Load<Sprite>(spriteName);
            if (single == null)
                single = Resources.Load<Sprite>("Textures/ItemParts/" + spriteName);
            if (single == null && itemAtlas != null)
                single = itemAtlas.GetSprite(spriteName);
    
            if (single != null)
                _spriteCache[spriteName] = single;
    
            return single;
        }
    }
}
