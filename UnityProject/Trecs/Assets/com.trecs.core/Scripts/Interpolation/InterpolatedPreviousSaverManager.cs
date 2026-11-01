using System;
using System.Collections.Generic;

namespace Trecs.Internal
{
    internal sealed class InterpolatedPreviousSaverManager
    {
        readonly List<IInterpolatedPreviousSaver> _savers;

        public InterpolatedPreviousSaverManager(List<IInterpolatedPreviousSaver> savers)
        {
            _savers = savers;
        }

        internal void Initialize(World world)
        {
#if DEBUG
            var registeredTypes = new HashSet<Type>();
            foreach (var saver in _savers)
            {
                if (!registeredTypes.Add(saver.ComponentType))
                {
                    throw new TrecsException(
                        $"Multiple IInterpolatedPreviousSavers registered for component type {saver.ComponentType}"
                    );
                }
            }
#endif
            foreach (var saver in _savers)
            {
                saver.Initialize(world);
            }
        }

        internal void SaveAll()
        {
            for (int i = 0; i < _savers.Count; i++)
            {
                _savers[i].Save();
            }
        }
    }
}
