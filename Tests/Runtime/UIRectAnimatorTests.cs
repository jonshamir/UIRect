using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace UIRect.Tests
{
    public class UIRectAnimatorTests
    {
        private static UIRectStyle StyleWithShadows(params float[] sizes)
        {
            var shadows = new List<UIRectShadow>(sizes.Length);
            foreach (var s in sizes)
                shadows.Add(new UIRectShadow { color = Color.black, size = s });
            return new UIRectStyle { Shadows = shadows };
        }

        // The core regression guard: the animator lerps into a reused buffer, so the shadow
        // list handed out is the SAME instance every frame — no per-frame allocation.
        [Test]
        public void Tick_ReusesShadowBuffer_AcrossFrames()
        {
            var animator = new UIRectAnimator();
            animator.AnimateTo(StyleWithShadows(0f, 0f), StyleWithShadows(20f, 20f),
                duration: 1000f, easeCurve: AnimationCurve.Linear(0, 0, 1, 1), onComplete: null);

            Assert.IsTrue(animator.Tick(out var first));
            Assert.IsTrue(animator.Tick(out var second));

            Assert.AreSame(first.Shadows, second.Shadows,
                "Successive ticks must return the same list instance (buffer reuse ⇒ no allocation).");
            Assert.AreEqual(2, second.Shadows.Count, "The reused buffer must be refilled to the correct count.");
        }

        // The buffered path must interpolate identically to the allocating UIRectStyle.Lerp.
        [Test]
        public void Tick_InterpolatesShadows_LikeLerp()
        {
            var from = StyleWithShadows(0f);
            var to = StyleWithShadows(20f);
            var curve = AnimationCurve.Linear(0, 0, 1, 1);

            var animator = new UIRectAnimator();
            animator.AnimateTo(from, to, duration: 1000f, easeCurve: curve, onComplete: null);
            animator.Tick(out var current);

            // First tick fires at ~t=0 (elapsed ~0), so the eased value is ~0 ⇒ matches the source.
            var expected = UIRectStyle.Lerp(from, to, curve.Evaluate(0f));
            Assert.AreEqual(expected.Shadows.Count, current.Shadows.Count);
            Assert.AreEqual(expected.Shadows[0].size, current.Shadows[0].size, 1e-4f);
            Assert.AreEqual(expected.Shadows[0].color, current.Shadows[0].color);
        }

        // duration <= 0 snaps to the target on the first tick and completes.
        [Test]
        public void Tick_FinalFrame_SnapsToTargetShadows()
        {
            var from = StyleWithShadows(0f);
            var to = StyleWithShadows(20f);

            var animator = new UIRectAnimator();
            animator.AnimateTo(from, to, duration: 0f, easeCurve: AnimationCurve.Linear(0, 0, 1, 1), onComplete: null);

            Assert.IsTrue(animator.Tick(out var current));
            Assert.AreEqual(20f, current.Shadows[0].size, 1e-4f, "A zero-duration tick must land exactly on the target.");
            Assert.IsFalse(animator.IsAnimating, "The animation must complete on the snapping frame.");
        }

        // A null-shadow endpoint propagates to a null result so ApplyStyle leaves host shadows untouched.
        [Test]
        public void Tick_NullTargetShadows_LeavesResultNull()
        {
            var from = StyleWithShadows(5f);
            var to = new UIRectStyle { FillColor = Color.white }; // Shadows == null

            var animator = new UIRectAnimator();
            animator.AnimateTo(from, to, duration: 1000f, easeCurve: AnimationCurve.Linear(0, 0, 1, 1), onComplete: null);
            animator.Tick(out var current);

            Assert.IsNull(current.Shadows, "A null-shadow endpoint must yield a null result (buffer untouched).");
        }
    }
}
