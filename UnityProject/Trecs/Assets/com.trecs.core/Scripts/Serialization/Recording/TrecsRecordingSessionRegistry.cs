using System;
using System.Collections.Generic;

namespace Trecs.Internal
{
    /// <summary>
    /// Tracks every <see cref="TrecsRecordingSession"/> currently alive (one
    /// per Trecs <see cref="World"/>). Mirrors <see cref="WorldRegistry"/> in
    /// shape — editor tooling subscribes to register/unregister events and
    /// looks up the controller for a chosen World. One session per world:
    /// game code and the editor look a world's session up here before
    /// creating one, and only its creator disposes it.
    /// </summary>
    public static class TrecsRecordingSessionRegistry
    {
        static readonly List<TrecsRecordingSession> _all = new();

        public static IReadOnlyList<TrecsRecordingSession> All => _all;

        public static event Action<TrecsRecordingSession> ControllerRegistered;
        public static event Action<TrecsRecordingSession> ControllerUnregistered;

        public static TrecsRecordingSession GetForWorld(World world)
        {
            for (int i = 0; i < _all.Count; i++)
            {
                if (_all[i].World == world)
                {
                    return _all[i];
                }
            }
            return null;
        }

        internal static void Register(TrecsRecordingSession controller)
        {
            _all.Add(controller);
            ControllerRegistered?.Invoke(controller);
        }

        internal static void Unregister(TrecsRecordingSession controller)
        {
            if (_all.Remove(controller))
            {
                ControllerUnregistered?.Invoke(controller);
            }
        }
    }
}
