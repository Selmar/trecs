using NUnit.Framework;
using Trecs.Internal;

namespace Trecs.Tests
{
    [TestFixture]
    public class FixedLoopPauseTests
    {
        [Test]
        public void PauseRequestedMidLoop_StopsAtThatStep()
        {
            using var env = EcsTestHelper.CreateEnvironment();
            var runner = env.World.GetSystemRunner();
            env.World.Tick();
            env.World.LateTick();

            // Several steps due in one tick; EditMode's delta time is zero.
            runner.VariableElapsedTime += 10 * runner.FixedDeltaTime;
            var pauseFrame = env.Accessor.FixedFrame + 2;
            using var sub = env.Accessor.Events.OnFixedUpdateCompleted(() =>
            {
                if (env.Accessor.FixedFrame == pauseFrame)
                {
                    runner.FixedIsPaused = true;
                }
            });

            env.World.Tick();
            env.World.LateTick();

            Assert.AreEqual(pauseFrame, env.Accessor.FixedFrame);
        }
    }
}
