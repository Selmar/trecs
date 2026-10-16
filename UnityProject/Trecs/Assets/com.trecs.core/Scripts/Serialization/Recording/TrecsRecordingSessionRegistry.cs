using System;
using System.Collections.Generic;

namespace Trecs.Internal
{
    /// <summary>
    /// Tracks every <see cref="TrecsRecordingSession"/> currently alive, one
    /// per Trecs <see cref="World"/>. Mirrors <see cref="WorldRegistry"/> in
    /// shape — editor tooling subscribes to register/unregister events and
    /// looks up the controller for a chosen World.
    ///
    /// A session is shared: the editor and game code each hold a
    /// <see cref="TrecsRecordingSessionLease"/> from <see cref="Acquire"/>.
    /// The first lease builds the session, the last one disposed tears it down.
    /// </summary>
    public static class TrecsRecordingSessionRegistry
    {
        static readonly List<TrecsRecordingSession> _all = new();
        static readonly Dictionary<TrecsRecordingSession, Owned> _owned = new();

        struct Owned
        {
            public int LeaseCount;
            public TrecsRewindBuffer Recorder;
            public SnapshotSerializer Snapshots;
            public SnapshotPayloadPool Pool;
        }

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

        /// <summary>
        /// Takes a lease on <paramref name="world"/>'s session, building the
        /// session and its rewind buffer on the first lease. Settings applied
        /// to the buffer reach every holder. Dispose the lease when done.
        /// </summary>
        public static TrecsRecordingSessionLease Acquire(World world)
        {
            TrecsAssert.That(world != null && !world.IsDisposed, "Acquire: world must be live");
            var session = GetForWorld(world) ?? Build(world);
            var owned = _owned[session];
            owned.LeaseCount++;
            _owned[session] = owned;
            return new TrecsRecordingSessionLease(session);
        }

        internal static void Release(TrecsRecordingSession session)
        {
            var owned = _owned[session];
            owned.LeaseCount--;
            if (owned.LeaseCount > 0)
            {
                _owned[session] = owned;
                return;
            }
            _owned.Remove(session);
            session.Dispose();
            owned.Recorder.Dispose();
            owned.Snapshots.Dispose();
            owned.Pool.Dispose();
        }

        static TrecsRecordingSession Build(World world)
        {
            var pool = new SnapshotPayloadPool();
            var state = new WorldStateSerializer(world);
            var snapshots = new SnapshotSerializer(state, world.SerializerRegistry, world, pool);
            TrecsRewindBuffer recorder = null;
            try
            {
                recorder = new TrecsRewindBuffer(
                    world,
                    state,
                    world.SerializerRegistry,
                    new TrecsRewindBufferSettings(),
                    snapshots,
                    pool
                );
                var session = new TrecsRecordingSession(world, recorder);
                _owned[session] = new Owned
                {
                    Recorder = recorder,
                    Snapshots = snapshots,
                    Pool = pool,
                };
                return session;
            }
            catch
            {
                recorder?.Dispose();
                snapshots.Dispose();
                pool.Dispose();
                throw;
            }
        }

        internal static void Register(TrecsRecordingSession controller)
        {
            TrecsAssert.That(
                GetForWorld(controller.World) == null,
                "TrecsRecordingSessionRegistry: the world already has a session"
            );
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
