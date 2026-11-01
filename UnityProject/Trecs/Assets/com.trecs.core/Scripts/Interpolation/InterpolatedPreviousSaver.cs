using System;
using Trecs.Internal;
using Unity.Collections.LowLevel.Unsafe;
using EditorBrowsable = System.ComponentModel.EditorBrowsableAttribute;
using EditorBrowsableState = System.ComponentModel.EditorBrowsableState;

namespace Trecs
{
    [EditorBrowsable(EditorBrowsableState.Never)]
    public sealed class InterpolatedPreviousSaver<T> : IInterpolatedPreviousSaver
        where T : unmanaged, IEntityComponent
    {
        WorldAccessor _world;

        public Type ComponentType
        {
            get { return typeof(T); }
        }

        public void Initialize(World world)
        {
            _world = world.CreateAccessor(AccessorRole.Unrestricted);
        }

        // ponytail: serial copy on the main thread. Jobs lose below ~200k entities and win
        // only 3.3 ms vs 2.6 ms at 1M (28 MB, memory-bound, ~8 GB/s). Chunk into jobs if
        // worlds reach that size and those 0.7 ms matter.
        public unsafe void Save()
        {
            TrecsDebugAssert.That(
                !_world.JobScheduler.HasOutstandingJobs,
                "Saving previous values needs every job completed"
            );
            TrecsDebugAssert.That(
                UnsafeUtility.SizeOf<T>() == UnsafeUtility.SizeOf<InterpolatedPrevious<T>>(),
                "InterpolatedPrevious<T> must have the layout of T"
            );

            foreach (
                var group in _world.WorldInfo.GetGroupsWithComponents<T, InterpolatedPrevious<T>>()
            )
            {
                var (currents, count) = _world.GetBufferReadForJobScheduling<T>(group);
                if (count == 0)
                    continue;
                var (previouses, _) = _world.GetBufferWriteForJobScheduling<
                    InterpolatedPrevious<T>
                >(group);

                UnsafeUtility.MemCpy(
                    previouses.GetRawPointer(out _).ToPointer(),
                    currents.GetRawPointer(out _).ToPointer(),
                    count * (long)UnsafeUtility.SizeOf<T>()
                );
            }
        }
    }
}
