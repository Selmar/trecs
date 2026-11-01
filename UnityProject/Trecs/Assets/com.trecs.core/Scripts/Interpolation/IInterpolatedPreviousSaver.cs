using System;

namespace Trecs
{
    /// <summary>
    /// Contract for saving a component's current values into its
    /// <see cref="InterpolatedPrevious{T}"/> counterpart before the next fixed update.
    /// </summary>
    internal interface IInterpolatedPreviousSaver
    {
        void Initialize(World world);
        void Save();

        public Type ComponentType { get; }
    }
}
