using System;

namespace Trecs.Internal
{
    /// <summary>
    /// One holder's share of a world's <see cref="TrecsRecordingSession"/>,
    /// from <see cref="TrecsRecordingSessionRegistry.Acquire"/>. The session
    /// stays alive until every lease on it is disposed.
    /// </summary>
    public sealed class TrecsRecordingSessionLease : IDisposable
    {
        TrecsRecordingSession _session;

        internal TrecsRecordingSessionLease(TrecsRecordingSession session)
        {
            _session = session;
        }

        public TrecsRecordingSession Session
        {
            get
            {
                TrecsAssert.That(_session != null, "TrecsRecordingSessionLease: already disposed");
                return _session;
            }
        }

        public void Dispose()
        {
            if (_session == null)
            {
                return;
            }
            TrecsRecordingSessionRegistry.Release(_session);
            _session = null;
        }
    }
}
