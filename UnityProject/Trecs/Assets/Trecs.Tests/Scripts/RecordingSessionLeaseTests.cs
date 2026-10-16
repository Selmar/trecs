using NUnit.Framework;
using Trecs.Internal;
using NAssert = NUnit.Framework.Assert;

namespace Trecs.Tests
{
    [TestFixture]
    public class RecordingSessionLeaseTests
    {
        [Test]
        public void Leases_ShareOneSession_UntilTheLastIsDisposed()
        {
            using var env = EcsTestHelper.CreateEnvironment(TestTemplates.SimpleAlpha);
            // An open Player window holds its own lease on every world.
            Assume.That(TrecsRecordingSessionRegistry.GetForWorld(env.World), Is.Null);

            var first = TrecsRecordingSessionRegistry.Acquire(env.World);
            var second = TrecsRecordingSessionRegistry.Acquire(env.World);
            NAssert.AreSame(first.Session, second.Session);

            first.Dispose();
            NAssert.AreSame(
                second.Session,
                TrecsRecordingSessionRegistry.GetForWorld(env.World),
                "A released lease must not tear down a session another holds"
            );
            second.Session.StartAutoRecording();
            env.StepFixedFrames(3);
            NAssert.IsTrue(second.Session.AutoRecorder.IsRecording);

            second.Dispose();
            NAssert.IsNull(TrecsRecordingSessionRegistry.GetForWorld(env.World));
        }

        [Test]
        public void Lease_DisposeTwice_ReleasesOnce()
        {
            using var env = EcsTestHelper.CreateEnvironment(TestTemplates.SimpleAlpha);
            Assume.That(TrecsRecordingSessionRegistry.GetForWorld(env.World), Is.Null);

            var first = TrecsRecordingSessionRegistry.Acquire(env.World);
            var second = TrecsRecordingSessionRegistry.Acquire(env.World);
            first.Dispose();
            first.Dispose();

            NAssert.IsNotNull(TrecsRecordingSessionRegistry.GetForWorld(env.World));
            second.Dispose();
            NAssert.IsNull(TrecsRecordingSessionRegistry.GetForWorld(env.World));
        }
    }
}
