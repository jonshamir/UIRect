using UnityEngine;

namespace UIRect
{
    /// <summary>
    /// Shared identifiers and inspector limits for the UIRect backdrop-blur providers
    /// (<c>UIRectBackdropBlurBuiltin</c> on Built-in RP, the URP <c>UIRectBackdropBlurFeature</c>).
    /// Kept in one place so the two providers can't drift apart on shader/property names or ranges.
    /// </summary>
    public static class UIRectBlurConstants
    {
        /// <summary>The Built-in RP separable-Gaussian shader (blitted via CommandBuffer).</summary>
        public const string ShaderName = "Hidden/UIRect/BackdropBlur";

        /// <summary>The URP separable-Gaussian shader (driven by Blitter; XR / single-pass instanced aware).</summary>
        public const string ShaderNameURP = "Hidden/UIRect/BackdropBlurURP";

        /// <summary>Name used to identify the Built-in RP command buffer for add/remove.</summary>
        public const string CommandBufferName = "UIRect Backdrop Blur";

        /// <summary>Global blurred camera-color texture the <c>UI/UIRectGlass</c> shader samples.</summary>
        public static readonly int BackdropTexID = Shader.PropertyToID("_UIRectBackdropTex");

        /// <summary>Per-pass blur step in UV space: (radius/width, 0) horizontal or (0, radius/height) vertical.</summary>
        public static readonly int BlurDirID = Shader.PropertyToID("_BlurDir");

        // Inspector limits/defaults, shared so both providers stay identical.
        public const int MinDownsample = 0;
        public const int MaxDownsample = 4;
        public const int DefaultDownsample = 2;

        public const int MinIterations = 1;
        public const int MaxIterations = 8;
        public const int DefaultIterations = 3;

        public const float MinBlurRadius = 0.5f;
        public const float MaxBlurRadius = 2f;
        public const float DefaultBlurRadius = 1f;
    }
}
