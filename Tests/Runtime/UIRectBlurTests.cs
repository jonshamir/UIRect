using NUnit.Framework;

namespace JonShamir.UIRectTests
{
    public class UIRectBlurTests
    {
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
    }
}
