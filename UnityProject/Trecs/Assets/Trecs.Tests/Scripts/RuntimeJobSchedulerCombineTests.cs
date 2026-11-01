using NUnit.Framework;
using Trecs.Internal;
using Unity.Jobs;
using NAssert = NUnit.Framework.Assert;

namespace Trecs.Tests
{
    [TestFixture]
    public class RuntimeJobSchedulerCombineTests
    {
        struct SlowJob : IJob
        {
            public void Execute() => System.Threading.Thread.Sleep(20);
        }

        [Test]
        public void Include_RepeatedHandle_CombinesOnce_DependsOnEveryHandle()
        {
            var scheduler = new RuntimeJobScheduler();
            var resource = ResourceId.Component(TypeId<TestInt>.Value);
            var first = new SlowJob().Schedule();
            var second = new SlowJob().Schedule();
            scheduler.TrackJobWrite(first, resource, GroupIndex.FromIndex(0));
            scheduler.TrackJobWrite(first, resource, GroupIndex.FromIndex(1));
            scheduler.TrackJobWrite(second, resource, GroupIndex.FromIndex(2));

            var deps = scheduler.IncludeReadDep(default, resource, GroupIndex.FromIndex(0));
            NAssert.IsTrue(deps.Equals(first), "one handle needs no combine");

            deps = scheduler.IncludeReadDep(deps, resource, GroupIndex.FromIndex(1));
            NAssert.IsTrue(deps.Equals(first), "the same handle again needs no combine");

            deps = scheduler.IncludeReadDep(deps, resource, GroupIndex.FromIndex(2));
            NAssert.IsFalse(deps.Equals(first), "a second handle needs a combine");

            var combined = deps;
            deps = scheduler.IncludeReadDep(deps, resource, GroupIndex.FromIndex(0));
            NAssert.IsTrue(deps.Equals(combined), "a handle already combined adds nothing");

            deps.Complete();
            NAssert.IsTrue(first.IsCompleted && second.IsCompleted, "the combine lost a handle");
            scheduler.CompleteAllOutstanding();
        }
    }
}
