using System;
using System.ComponentModel;

namespace Unity.Core
{
    public readonly struct TimeData
    {
        /// <summary>
        /// The total cumulative elapsed time in seconds.
        /// </summary>
        public readonly double ElapsedTime;

        /// <summary>
        /// The time in seconds since the last time-updating event occurred. (For example, a frame.)
        /// </summary>
        public readonly float DeltaTime;

        /// <summary>
        /// The number of time-incrementing events (for example, frames or fixed update steps) that
        /// have occurred.
        /// </summary>
        public readonly int EventCount;

        /// <summary>
        /// Create a new TimeData struct with the given values.
        /// </summary>
        /// <param name="elapsedTime">Time since the start of time collection.</param>
        /// <param name="deltaTime">Elapsed time since the last time-updating event occurred.</param>
        /// <param name="eventCount">The number of time-updating events that have occurred.</param>
        public TimeData(double elapsedTime, float deltaTime, int eventCount)
        {
            ElapsedTime = elapsedTime;
            DeltaTime = deltaTime;
            EventCount = eventCount;
        }

        // These are legacy names; they're in such prevalent use that they're
        // not marked Obsolete, but they should be at some point.

        //[Obsolete("deltaTime has been renamed to DeltaTime")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public float deltaTime => DeltaTime;

        //[Obsolete("time has been renamed to Time")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public float time => (float)ElapsedTime;

        //[Obsolete("frameCount has been renamed to EventCount")]
        [EditorBrowsable(EditorBrowsableState.Never)]
        public int frameCount => EventCount;
    }
}
