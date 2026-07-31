using System;
using System.Collections.Generic;
using NUnit.Framework;
using NAssert = NUnit.Framework.Assert;

namespace Trecs.Tests
{
    // Ordering on the identity types is only required to be a total order consistent
    // with equality — sort position itself carries no meaning. Native container helpers
    // (GetUniqueKeyArray, sorted key scans) need it; nothing should depend on which id
    // lands first.
    [TestFixture]
    public class IdentityOrderingTests
    {
        [Test]
        public void TypeId_CompareTo_MatchesRawValueOrder()
        {
            var low = new TypeId(-5);
            var high = new TypeId(7);

            NAssert.Less(low.CompareTo(high), 0);
            NAssert.Greater(high.CompareTo(low), 0);
            NAssert.AreEqual(0, low.CompareTo(new TypeId(-5)));
        }

        [Test]
        public void Tag_CompareTo_ConsistentWithEquality()
        {
            AssertTotalOrder(TestTags.Alpha, TestTags.Beta, TestTags.Gamma);
            NAssert.AreEqual(0, TestTags.Alpha.CompareTo(Tag<TestAlpha>.Value));
        }

        [Test]
        public void ComponentTypeId_CompareTo_ConsistentWithEquality()
        {
            AssertTotalOrder(
                ComponentTypeId<TestInt>.Value,
                ComponentTypeId<TestFloat>.Value,
                ComponentTypeId<TestVec>.Value
            );
            NAssert.AreEqual(
                0,
                ComponentTypeId<TestInt>.Value.CompareTo(ComponentTypeId<TestInt>.Value)
            );
        }

        [Test]
        public void BlobId_CompareTo_MatchesRawValueOrder()
        {
            var low = new BlobId(-3L);
            var high = new BlobId(long.MaxValue);

            NAssert.Less(low.CompareTo(high), 0);
            NAssert.Greater(high.CompareTo(low), 0);
            NAssert.AreEqual(0, high.CompareTo(new BlobId(long.MaxValue)));
        }

        // Distinct ids must sort strictly — no two of them compare equal.
        static void AssertTotalOrder<T>(T a, T b, T c)
            where T : struct, IComparable<T>
        {
            var sorted = new List<T> { a, b, c };
            sorted.Sort();

            NAssert.Less(sorted[0].CompareTo(sorted[1]), 0);
            NAssert.Less(sorted[1].CompareTo(sorted[2]), 0);
        }
    }
}
