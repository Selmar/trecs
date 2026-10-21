using System.Threading;
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

        [Test]
        public void SkipForward_KeepsFixedElapsedTimeAtTheSteps()
        {
            using var env = EcsTestHelper.CreateEnvironment(
                new WorldSettings
                {
                    MaxSecondsForFixedUpdatePerFrame = 0.001f,
                    WarnOnFixedUpdateFallingBehind = false,
                }
            );
            var runner = env.World.GetSystemRunner();
            env.World.Tick();
            env.World.LateTick();

            // Each step outlasts the budget, so the tick runs one of the due steps and skips the rest.
            runner.VariableElapsedTime += 10 * runner.FixedDeltaTime;
            var frame = env.Accessor.FixedFrame;
            using var sub = env.Accessor.Events.OnFixedUpdateCompleted(() => Thread.Sleep(5));

            env.World.Tick();
            env.World.LateTick();

            Assert.AreEqual(frame + 1, env.Accessor.FixedFrame, "the tick did not skip");
            Assert.AreEqual(
                env.Accessor.FixedFrame * runner.FixedDeltaTime,
                runner.FixedElapsedTime,
                1e-5f
            );
        }
    }
}
