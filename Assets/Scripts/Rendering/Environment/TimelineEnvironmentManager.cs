#nullable enable
using UnityEngine;
using UnityEngine.Playables;

namespace CraftSharp.Rendering
{
    public class TimelineEnvironmentManager : BaseEnvironmentManager
    {
        private const float TICK_SECONDS = 0.05F;

        private PlayableDirector? playableDirector;

        [SerializeField] private long startTime;

        private int ticks;
        private int lastRecTicks;
        private bool simulate = false;

        private float deltaSeconds = 0F;

        public override void SetRain(bool raining)
        {
            // TODO: Implement
        }

        public override void SetTime(long timeRaw)
        {
            // Simulate if time is not paused (dayLightCycle set to true)
            var shouldSimulate = timeRaw >= 0L;
            // time value is negative if time is paused
            if (timeRaw < 0L) timeRaw = -timeRaw;

            lastRecTicks = (int)(timeRaw % 24000L);

            if (simulate != shouldSimulate)
            {
                simulate = shouldSimulate;

                // Make sure to update time if pause is toggled
                UpdateTime(lastRecTicks);

                if (simulate)
                {
                    playableDirector!.Resume();
                }
                else
                {
                    playableDirector!.Pause();
                }
            }
            else if (Mathf.Abs(ticks - lastRecTicks) > 25F)
            {
                UpdateTime(lastRecTicks);
            }
        }

        void Start()
        {
            playableDirector = GetComponent<PlayableDirector>();
            SetPlayableSpeed(4D / 1200D);

            if (simulate)
            {
                playableDirector!.Resume();
            }
            else
            {
                playableDirector!.Pause();
            }

            SetTime(startTime);
        }

        void Update()
        {
            if (simulate) // Simulate time passing
            {
                deltaSeconds += Time.unscaledDeltaTime;

                if (deltaSeconds > TICK_SECONDS)
                {
                    while (deltaSeconds > TICK_SECONDS)
                    {
                        deltaSeconds -= TICK_SECONDS;
                        ticks++;
                    }

                    UpdateTimeRelated();
                }
            }
        }

        private void SetPlayableSpeed(double speed)
        {
            if (playableDirector != null)
            {
                var playableGraph = playableDirector.playableGraph;
                
                if (!playableGraph.IsValid())
                {
                    playableDirector.RebuildGraph();
                }

                if (playableGraph.IsValid())
                {
                    playableDirector.playableGraph.GetRootPlayable(0).SetSpeed(speed);
                }
            }
        }

        private void UpdateTime(int serverTicks)
        {
            ticks = serverTicks;
            // Reset delta seconds
            deltaSeconds = 0F;

            UpdateTimeRelated();
        }

        private void UpdateTimeRelated()
        {
            double normalizedTOD = ticks / 24000D;

            playableDirector!.time = playableDirector.duration * normalizedTOD;

            //DynamicGI.UpdateEnvironment();
        }

        public static (int hours, int minutes, int seconds) Tick2HMS(int ticks)
        {
            int hours = (ticks / 1000 + 6) % 24;
            int secsInHour = (int)(ticks % 1000 * 3.6F);

            return (hours, secsInHour / 60, secsInHour % 60);
        }

        public static string GetTimeStringFromTicks(int ticks)
        {
            int hours = (ticks / 1000 + 6) % 24;
            int minutes = (int)(ticks % 1000 * 3.6F) / 60;

            return $"{hours:00}:{minutes:00}";
        }

        public override string GetTimeString()
        {
            return $"{GetTimeStringFromTicks(ticks)} ({ticks} / {lastRecTicks})";
        }
    }
}