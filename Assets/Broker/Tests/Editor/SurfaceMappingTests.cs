using System;
using Broker.Core;
using NUnit.Framework;

namespace Broker.Tests
{
    public sealed class SurfaceMappingTests
    {
        [TestCase(0, 3, 2)] [TestCase(1, 1, 3)] [TestCase(2, 1, 2)]
        public void EachFaceUsesItsPhysicalDimensions(int axis, float expectedU, float expectedV)
        {
            var uv = SurfaceMapping.Box(0.5f, 0.5f, 0.5f, axis, 4, 8, 12, 2);
            Assert.That(uv.U, Is.EqualTo(expectedU)); Assert.That(uv.V, Is.EqualTo(expectedV));
        }
        [Test] public void LongerWallRepeatsInsteadOfStretching()
        {
            var shortWall = SurfaceMapping.Box(0.5f, 0.5f, 0.5f, 2, 2, 3, 0.2f, 1);
            var longWall = SurfaceMapping.Box(0.5f, 0.5f, 0.5f, 2, 12, 3, 0.2f, 1);
            Assert.That(longWall.U, Is.EqualTo(shortWall.U * 6)); Assert.That(longWall.V, Is.EqualTo(shortWall.V));
        }
        [Test] public void NegativeCoordinatesAreNotClamped()
        {
            var uv = SurfaceMapping.Box(-0.5f, 0, -0.5f, 1, 4, 1, 6, 2);
            Assert.That(uv.U, Is.EqualTo(-1)); Assert.That(uv.V, Is.EqualTo(-1.5f));
        }
        [TestCase(0f)] [TestCase(-1f)] [TestCase(float.NaN)] [TestCase(float.PositiveInfinity)]
        public void InvalidTilingIsRejected(float tile)
        { Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceMapping.Box(0, 0, 0, 1, 1, 1, 1, tile)); }
        [Test] public void InvalidAxisIsRejected()
        { Assert.Throws<ArgumentOutOfRangeException>(() => SurfaceMapping.Box(0, 0, 0, 3, 1, 1, 1, 1)); }
    }
}
