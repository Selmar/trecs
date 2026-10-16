#pragma warning disable TRECS128

using System;
using System.Collections.Generic;

namespace Trecs.Internal
{
    /// <summary>
    /// Refcounted helper that holds a <see cref="TrecsRecordingSessionLease"/>
    /// on every active <see cref="World"/> while at least one
    /// <see cref="TrecsPlayerWindow"/> is open, and releases them when the last
    /// window closes. The session itself is shared with game code that leased
    /// it too, and outlives the window while game code holds it.
    ///
    /// User composition roots no longer need to wire these types up manually
    /// — the editor side discovers Worlds via <see cref="WorldRegistry"/> and
    /// uses each world's <see cref="World.SerializerRegistry"/> directly.
    ///
    /// When the Player window is closed there is zero editor overhead — no
    /// per-fixed-frame subscriptions, no scratch buffers, no accessor
    /// allocation.
    /// </summary>
    static class TrecsEditorRecordingAutoAttach
    {
        static readonly TrecsLog _log = TrecsLog.Default;

        static int _activationCount;
        static readonly Dictionary<World, AttachedSet> _attached = new();

        struct AttachedSet
        {
            public TrecsRecordingSessionLease Lease;
            public TrecsSnapshotLibrary SnapshotLibrary;

            // The library's own serializer and pool, separate from the session's.
            public SnapshotSerializer Snapshots;
            public SnapshotPayloadPool Pool;
        }

        /// <summary>
        /// Look up the snapshot library for <paramref name="world"/>, or
        /// null if no world is attached. Editor windows fetch the library
        /// through here rather than constructing their own — the lifetime
        /// is owned by the attach refcount.
        /// </summary>
        public static TrecsSnapshotLibrary GetSnapshotLibrary(World world)
        {
            if (world == null || !_attached.TryGetValue(world, out var attached))
            {
                return null;
            }
            return attached.SnapshotLibrary;
        }

        /// <summary>
        /// Called by <see cref="TrecsPlayerWindow.OnEnable"/>. First call wires
        /// up the WorldRegistry subscriptions and attaches to every currently
        /// active world; subsequent calls just bump the refcount so multiple
        /// open windows share one set of controllers.
        /// </summary>
        public static void Activate()
        {
            _activationCount++;
            if (_activationCount > 1)
            {
                return;
            }

            WorldRegistry.WorldRegistered += AttachForWorld;
            WorldRegistry.WorldUnregistered += DetachForWorld;
            foreach (var world in WorldRegistry.ActiveWorlds)
            {
                AttachForWorld(world);
            }
        }

        /// <summary>
        /// Called by <see cref="TrecsPlayerWindow.OnDisable"/>. Last call
        /// disposes every attached controller and unhooks the WorldRegistry
        /// subscriptions so no editor cost survives the window closing.
        /// </summary>
        public static void Deactivate()
        {
            TrecsDebugAssert.That(_activationCount > 0, "Deactivate without matching Activate");
            _activationCount--;
            if (_activationCount > 0)
            {
                return;
            }

            WorldRegistry.WorldRegistered -= AttachForWorld;
            WorldRegistry.WorldUnregistered -= DetachForWorld;
            foreach (var attached in _attached.Values)
            {
                attached.Lease.Dispose();
                attached.Snapshots.Dispose();
                attached.Pool.Dispose();
            }
            _attached.Clear();
        }

        static void AttachForWorld(World world)
        {
            if (_attached.ContainsKey(world))
            {
                return;
            }

            TrecsRecordingSessionLease lease = null;
            var pool = new SnapshotPayloadPool();
            SnapshotSerializer snapshots = null;
            try
            {
                // Builds the session unless game code leased it first. Fires
                // ControllerRegistered on build, which TrecsGameStateActivator
                // listens to so that auto-record-on-open kicks in.
                lease = TrecsRecordingSessionRegistry.Acquire(world);
                snapshots = new SnapshotSerializer(
                    new WorldStateSerializer(world),
                    world.SerializerRegistry,
                    world,
                    pool
                );
                _attached[world] = new AttachedSet
                {
                    Lease = lease,
                    SnapshotLibrary = new TrecsSnapshotLibrary(world, snapshots, lease.Session),
                    Snapshots = snapshots,
                    Pool = pool,
                };
            }
            catch (Exception e)
            {
                // Swallow further failures so a Dispose-throw doesn't mask the
                // original error.
                try
                {
                    snapshots?.Dispose();
                }
                catch { }
                pool.Dispose();
                try
                {
                    lease?.Dispose();
                }
                catch { }
                _log.Error("Failed to auto-attach Trecs Player to world {0}: {1}", world, e);
                return;
            }
            _log.Trace("Auto-attached Trecs Player to world {0}", world);
        }

        static void DetachForWorld(World world)
        {
            if (!_attached.TryGetValue(world, out var attached))
            {
                return;
            }
            _attached.Remove(world);
            attached.Lease.Dispose();
            attached.Snapshots.Dispose();
            attached.Pool.Dispose();
            _log.Trace("Auto-detached Trecs Player from world {0}", world);
        }
    }
}
