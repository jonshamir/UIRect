using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UIRect.Tests
{
    public class UIRectShadowTests
    {
        [Test]
        public void Default_MatchesLegacySingleShadowDefaults()
        {
            var s = UIRectShadow.Default;

            Assert.IsFalse(s.isInner);
            Assert.AreEqual(new Color(0, 0, 0, 0.5f), s.color);
            Assert.AreEqual(10f, s.size);
            Assert.AreEqual(0f, s.spread);
            Assert.AreEqual(new Vector3(0, -5, 0), s.offset);
        }

        [Test]
        public void IsVisible_ZeroSizeAndOffset_IsFalse()
        {
            var s = new UIRectShadow { size = 0, offset = Vector3.zero };

            Assert.IsFalse(s.IsVisible);
        }

        [Test]
        public void IsVisible_WithSizeOrOffset_IsTrue()
        {
            Assert.IsTrue(new UIRectShadow { size = 1 }.IsVisible);
            Assert.IsTrue(new UIRectShadow { spread = 1 }.IsVisible);
            Assert.IsTrue(new UIRectShadow { offset = new Vector3(0, -2, 0) }.IsVisible);
        }

        [Test]
        public void Lerp_InterpolatesAllFields_TargetWinsIsInner()
        {
            var a = new UIRectShadow
            {
                isInner = false,
                color = Color.black,
                size = 0,
                spread = 0,
                offset = Vector3.zero,
            };
            var b = new UIRectShadow
            {
                isInner = true,
                color = Color.white,
                size = 20,
                spread = 8,
                offset = new Vector3(10, -10, 4),
            };

            var r = UIRectShadow.Lerp(a, b, 0.5f);

            Assert.IsTrue(r.isInner, "isInner cannot interpolate; the target value wins.");
            Assert.AreEqual(new Color(0.5f, 0.5f, 0.5f, 1f), r.color);
            Assert.AreEqual(10f, r.size);
            Assert.AreEqual(4f, r.spread);
            Assert.AreEqual(new Vector3(5, -5, 2), r.offset);
        }

        [Test]
        public void Migrate_LegacyHasShadow_InsertsAtFrontAndClearsFlag()
        {
            bool hasShadow = true;
            var shadows = new List<UIRectShadow>
            {
                new UIRectShadow { color = Color.red, size = 3 },
            };

            UIRectShadowMigration.Migrate(ref hasShadow, Color.green, 12f, 2f,
                new Vector3(1, -2, 0), shadows);

            Assert.IsFalse(hasShadow, "Migration must clear the legacy flag so it never re-runs after a save.");
            Assert.AreEqual(2, shadows.Count);
            Assert.AreEqual(Color.green, shadows[0].color, "Legacy shadow must be inserted at the front (topmost).");
            Assert.AreEqual(12f, shadows[0].size);
            Assert.AreEqual(2f, shadows[0].spread);
            Assert.AreEqual(new Vector3(1, -2, 0), shadows[0].offset);
            Assert.IsFalse(shadows[0].isInner);
            Assert.AreEqual(Color.red, shadows[1].color, "Existing entries must be preserved after the migrated one.");
        }

        [Test]
        public void Migrate_HasShadowFalse_IsNoOp()
        {
            bool hasShadow = false;
            var shadows = new List<UIRectShadow>();

            UIRectShadowMigration.Migrate(ref hasShadow, Color.green, 12f, 2f, Vector3.one, shadows);

            Assert.IsEmpty(shadows);
        }

        [Test]
        public void Migrate_CalledTwice_DoesNotDuplicate()
        {
            bool hasShadow = true;
            var shadows = new List<UIRectShadow>();

            UIRectShadowMigration.Migrate(ref hasShadow, Color.green, 12f, 2f, Vector3.one, shadows);
            UIRectShadowMigration.Migrate(ref hasShadow, Color.green, 12f, 2f, Vector3.one, shadows);

            Assert.AreEqual(1, shadows.Count, "hasShadow is the idempotence guard; a second call must be a no-op.");
        }
    }
}
