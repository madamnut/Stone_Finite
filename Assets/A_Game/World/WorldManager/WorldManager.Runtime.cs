using System.Collections;
using UnityEngine;


namespace Game.World
{
    public partial class WorldManager
    {
        IEnumerator AutosaveLoop()
        {
            var wait = new WaitForSecondsRealtime(300f);
            while (true)
            {
                yield return wait;
                SaveWorld();
            }
        }
    
        private void ApplyTimeSyncedBrightness(bool forceDirty)
        {
            int m = worldHour * 60 + (worldMinute % 60);
            float off =
                (m >= 300 && m < 540) ? 15f * (1f - (m - 300) / 240f) :
                (m >= 540 && m < 1080) ? 0f :
                (m >= 1080 && m < 1260) ? 15f * ((m - 1080) / 180f) :
                                          15f;
    
            byte newOffset = (byte)Mathf.RoundToInt(Mathf.Clamp(off, 0f, maxDarknessOffset));
    
            if (forceDirty || newOffset != globalBrightnessOffset)
            {
                globalBrightnessOffset = newOffset;
    
                if (newOffset != _lastBrightnessOffset || forceDirty)
                {
                    _lastBrightnessOffset = newOffset;
                    chunkSystem.SetGlobalBrightnessOffset(globalBrightnessOffset);
                    chunkSystem.MarkAllChunksLightDirty();
                }
            }
        }
    
        public TimeBand GetTimeBand()
        {
            int h = worldHour;
            int mm = worldMinute % 60;
            int t = h * 100 + mm;
    
            if (t == 0) return TimeBand.Midnight;
            if (t < 400) return TimeBand.LateNight;
            if (t < 600) return TimeBand.Dawn;
            if (t < 900) return TimeBand.EarlyMorning;
            if (t < 1200) return TimeBand.Morning;
            if (t == 1200) return TimeBand.Noon;
            if (t < 1700) return TimeBand.Afternoon;
            if (t < 1900) return TimeBand.Evening;
            if (t < 2100) return TimeBand.Dusk;
            return TimeBand.Night;
        }
    }
}
