using UnityEngine;

namespace UIRect
{
    /// <summary>
    /// The three knobs shared by both backdrop-blur providers. Serializable so the URP feature's
    /// <c>Settings</c> and the Built-in <c>UIRectBackdropBlurBuiltin</c> component embed the exact same
    /// fields, ranges, and defaults (see <see cref="UIRectBlurConstants"/>). Passed to
    /// <see cref="UIRectBlurCore.Record"/>, which does the actual GPU work.
    /// </summary>
    [System.Serializable]
    public struct UIRectBlurSettings
    {
        [Tooltip("Resolution halvings applied before blurring (0 = full res, 1 = half, 2 = quarter). " +
                 "Higher = cheaper and softer. Primary blur-width control.")]
        [Range(UIRectBlurConstants.MinDownsample, UIRectBlurConstants.MaxDownsample)]
        public int downsample;

        [Tooltip("Number of horizontal+vertical blur passes. Higher = wider, smoother blur. " +
                 "Primary blur-width control.")]
        [Range(UIRectBlurConstants.MinIterations, UIRectBlurConstants.MaxIterations)]
        public int iterations;

        [Tooltip("Per-pass blur step, in texels of the downsampled buffer. A softness nudge only - " +
                 "drive blur width with Downsample and Iterations. Large values undersample.")]
        [Range(UIRectBlurConstants.MinBlurRadius, UIRectBlurConstants.MaxBlurRadius)]
        public float blurRadius;

        public static UIRectBlurSettings Default => new UIRectBlurSettings
        {
            downsample = UIRectBlurConstants.DefaultDownsample,
            iterations = UIRectBlurConstants.DefaultIterations,
            blurRadius = UIRectBlurConstants.DefaultBlurRadius,
        };
    }
}
