using System.Collections;
using UnityEngine;

namespace Game.World
{
    public partial class WorldManager
    {
        private sealed class RuntimeStateService
        {
            readonly WorldServiceContext _ctx;

            public RuntimeStateService(WorldServiceContext context)
            {
                _ctx = context;
            }

            public IEnumerator CreateAutosaveLoop()
            {
                var wait = new WaitForSecondsRealtime(300f);
                while (true)
                {
                    yield return wait;
                    _ctx.SaveWorld();
                }
            }

            public void ApplyTimeSyncedBrightness(bool forceDirty)
            {
                int m = _ctx.WorldHour * 60 + (_ctx.WorldMinute % 60);
                float off =
                    (m >= 300 && m < 540) ? 15f * (1f - (m - 300) / 240f) :
                    (m >= 540 && m < 1080) ? 0f :
                    (m >= 1080 && m < 1260) ? 15f * ((m - 1080) / 180f) :
                                              15f;

                byte newOffset = (byte)Mathf.RoundToInt(Mathf.Clamp(off, 0f, _ctx.MaxDarknessOffset));

                if (forceDirty || newOffset != _ctx.GlobalBrightnessOffset)
                {
                    _ctx.GlobalBrightnessOffset = newOffset;

                    if (newOffset != _ctx.LastBrightnessOffset || forceDirty)
                    {
                        _ctx.LastBrightnessOffset = newOffset;
                        _ctx.ChunkSystem.SetGlobalBrightnessOffset(_ctx.GlobalBrightnessOffset);
                        _ctx.ChunkSystem.MarkAllChunksLightDirty();
                    }
                }
            }

            public TimeBand GetTimeBand()
            {
                int h = _ctx.WorldHour;
                int mm = _ctx.WorldMinute % 60;
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
}
