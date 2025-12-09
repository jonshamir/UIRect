using System;
using NUnit.Framework;
using UnityEngine;

namespace JonShamir.UIRectTests
{
    public class ShaderPackerTests
    {
        [Test]
        public void Pack2NormalizedFloats_RoundTrips()
        {
            float a = 0.25f;
            float b = 0.75f;

            float packed = ShaderPacker.Pack2NormalizedFloats(a, b);
            var (unpackedA, unpackedB) = ShaderPacker.Unpack2Floats(packed);

            Assert.AreEqual(a, unpackedA, 0.001f);
            Assert.AreEqual(b, unpackedB, 0.001f);
        }

        [Test]
        public void Pack2NormalizedFloats_ZeroValues_RoundTrips()
        {
            float packed = ShaderPacker.Pack2NormalizedFloats(0f, 0f);
            var (a, b) = ShaderPacker.Unpack2Floats(packed);

            Assert.AreEqual(0f, a, 0.001f);
            Assert.AreEqual(0f, b, 0.001f);
        }

        [Test]
        public void Pack2NormalizedFloats_OneValues_RoundTrips()
        {
            float packed = ShaderPacker.Pack2NormalizedFloats(1f, 1f);
            var (a, b) = ShaderPacker.Unpack2Floats(packed);

            Assert.AreEqual(1f, a, 0.001f);
            Assert.AreEqual(1f, b, 0.001f);
        }

        [Test]
        public void Pack2NormalizedFloats_ThrowsForNegativeValues()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ShaderPacker.Pack2NormalizedFloats(-0.1f, 0.5f));
        }

        [Test]
        public void Pack2NormalizedFloats_ThrowsForValuesAboveOne()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
                ShaderPacker.Pack2NormalizedFloats(0.5f, 1.1f));
        }

        [Test]
        public void PackColor_RoundTrips_OpaqueColors()
        {
            var original = new Color32(255, 128, 64, 200);

            float packed = ShaderPacker.PackColor(original);
            var unpacked = ShaderPacker.UnpackColor(packed);

            Assert.AreEqual(original.r, unpacked.r);
            Assert.AreEqual(original.g, unpacked.g);
            Assert.AreEqual(original.b, unpacked.b);
            Assert.AreEqual(original.a, unpacked.a);
        }

        [Test]
        public void PackColor_RoundTrips_Black()
        {
            var original = new Color32(0, 0, 0, 254);

            float packed = ShaderPacker.PackColor(original);
            var unpacked = ShaderPacker.UnpackColor(packed);

            Assert.AreEqual(0, unpacked.r);
            Assert.AreEqual(0, unpacked.g);
            Assert.AreEqual(0, unpacked.b);
        }

        [Test]
        public void PackColor_RoundTrips_White()
        {
            var original = new Color32(255, 255, 255, 254);

            float packed = ShaderPacker.PackColor(original);
            var unpacked = ShaderPacker.UnpackColor(packed);

            Assert.AreEqual(255, unpacked.r);
            Assert.AreEqual(255, unpacked.g);
            Assert.AreEqual(255, unpacked.b);
        }

        [Test]
        public void PackColor_ClampsAlphaTo254_ToAvoidNaN()
        {
            var original = new Color32(128, 128, 128, 255);

            float packed = ShaderPacker.PackColor(original);
            var unpacked = ShaderPacker.UnpackColor(packed);

            Assert.AreEqual(254, unpacked.a, "Alpha should be clamped to 254");
        }

        [Test]
        public void SingleToUInt32_AndBack_RoundTrips()
        {
            float original = 123.456f;

            uint asUint = ShaderPacker.SingleToUInt32(original);
            float back = ShaderPacker.UInt32ToSingle(asUint);

            Assert.AreEqual(original, back);
        }

        [Test]
        public void Pack2NormalizedFloats_VariousValues_MaintainsPrecision()
        {
            float[] testValues = { 0f, 0.1f, 0.25f, 0.5f, 0.75f, 0.9f, 1f };

            foreach (var a in testValues)
            {
                foreach (var b in testValues)
                {
                    float packed = ShaderPacker.Pack2NormalizedFloats(a, b);
                    var (unpackedA, unpackedB) = ShaderPacker.Unpack2Floats(packed);

                    Assert.AreEqual(a, unpackedA, 0.001f, $"Failed for a={a}");
                    Assert.AreEqual(b, unpackedB, 0.001f, $"Failed for b={b}");
                }
            }
        }
    }
}
