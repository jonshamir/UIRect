using NUnit.Framework;
using UnityEngine;

namespace UIRect.Blur.Tests
{
    public class UIRectBlurTests
    {
        [Test]
        public void GlassShader_Resolves()
        {
            Assert.IsNotNull(Shader.Find("UI/UIRectGlass"), "UI/UIRectGlass shader not found in project");
        }

        [Test]
        public void Backdrop_DefaultMaterial_UsesGlassShader()
        {
            var go = new GameObject("BackdropTest", typeof(RectTransform));
            try
            {
                var backdrop = go.AddComponent<UIRectBackdrop>();
                var mat = backdrop.defaultMaterial;
                Assert.IsNotNull(mat);
                Assert.AreEqual("UI/UIRectGlass", mat.shader.name);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
        }

        [Test]
        public void BlurSettings_Default_MatchesConstants()
        {
            var d = UIRectBlurSettings.Default;
            Assert.AreEqual(UIRectBlurConstants.DefaultDownsample, d.downsample);
            Assert.AreEqual(UIRectBlurConstants.DefaultIterations, d.iterations);
            Assert.AreEqual(UIRectBlurConstants.DefaultBlurRadius, d.blurRadius, 1e-6f);
        }

        [Test]
        public void BlurConstants_DefaultsWithinRange()
        {
            Assert.LessOrEqual(UIRectBlurConstants.MinDownsample, UIRectBlurConstants.DefaultDownsample);
            Assert.LessOrEqual(UIRectBlurConstants.DefaultDownsample, UIRectBlurConstants.MaxDownsample);

            Assert.LessOrEqual(UIRectBlurConstants.MinIterations, UIRectBlurConstants.DefaultIterations);
            Assert.LessOrEqual(UIRectBlurConstants.DefaultIterations, UIRectBlurConstants.MaxIterations);

            Assert.LessOrEqual(UIRectBlurConstants.MinBlurRadius, UIRectBlurConstants.DefaultBlurRadius);
            Assert.LessOrEqual(UIRectBlurConstants.DefaultBlurRadius, UIRectBlurConstants.MaxBlurRadius);
        }

        [Test]
        public void ProviderRegistry_RegisterUnregister_IsBalanced()
        {
            int before = UIRectBlurCore.ActiveProviderCount;
            UIRectBlurCore.RegisterProvider();
            Assert.AreEqual(before + 1, UIRectBlurCore.ActiveProviderCount);
            UIRectBlurCore.UnregisterProvider();
            Assert.AreEqual(before, UIRectBlurCore.ActiveProviderCount);
        }

        [Test]
        public void ProviderRegistry_Unregister_ClampsAtZero()
        {
            int before = UIRectBlurCore.ActiveProviderCount;
            for (int i = 0; i < before; i++)
                UIRectBlurCore.UnregisterProvider();
            UIRectBlurCore.UnregisterProvider(); // one extra - must not go negative
            Assert.AreEqual(0, UIRectBlurCore.ActiveProviderCount);
            // Restore so we don't perturb other tests / live providers.
            for (int i = 0; i < before; i++)
                UIRectBlurCore.RegisterProvider();
        }

        [Test]
        public void BackdropRegistry_Unregister_ClampsAtZero()
        {
            int before = UIRectBlurCore.ActiveBackdropCount;
            for (int i = 0; i < before; i++)
                UIRectBlurCore.UnregisterBackdrop();
            UIRectBlurCore.UnregisterBackdrop(); // one extra - must not go negative
            Assert.AreEqual(0, UIRectBlurCore.ActiveBackdropCount);
            for (int i = 0; i < before; i++)
                UIRectBlurCore.RegisterBackdrop();
        }

        // The providers' early-out hangs off HasWork, so an enabled backdrop must register itself and a
        // disabled one must release its slot - otherwise blur either does dead work or silently stops.
        [Test]
        public void Backdrop_EnabledState_DrivesHasWork()
        {
            int before = UIRectBlurCore.ActiveBackdropCount;
            var go = new GameObject("BackdropRegistryTest", typeof(RectTransform));
            try
            {
                var backdrop = go.AddComponent<UIRectBackdrop>();
                Assert.AreEqual(before + 1, UIRectBlurCore.ActiveBackdropCount,
                    "an enabled UIRectBackdrop should register as a consumer");
                Assert.IsTrue(UIRectBlurCore.HasWork);

                backdrop.enabled = false;
                Assert.AreEqual(before, UIRectBlurCore.ActiveBackdropCount,
                    "disabling a UIRectBackdrop should release its consumer slot");

                backdrop.enabled = true;
                Assert.AreEqual(before + 1, UIRectBlurCore.ActiveBackdropCount);
            }
            finally
            {
                Object.DestroyImmediate(go);
            }
            Assert.AreEqual(before, UIRectBlurCore.ActiveBackdropCount,
                "destroying a UIRectBackdrop should release its consumer slot");
        }
    }
}
