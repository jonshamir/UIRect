using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Pipeline-agnostic backdrop-blur GPU recording, shared by the Built-in RP provider
/// (<c>UIRectBackdropBlurBuiltin</c>) and the URP <c>UIRectBackdropBlurFeature</c>. Given a command
/// buffer, the shared blur material, a source color, and two provider-owned render targets sized to
/// the downsampled resolution, it records:
/// <list type="number">
/// <item>a bilinear <b>downsample chain</b> from the source down to the target size (successive
/// halving, so high downsample factors don't alias the way a single bilinear blit would);</item>
/// <item>a separable Gaussian <b>ping-pong</b> (horizontal+vertical per iteration) between
/// <paramref name="dst"/> and <paramref name="scratch"/>;</item>
/// <item>publishes the result as the global <c>_UIRectBackdropTex</c> the UIRect <c>_USE_BLUR</c>
/// variant samples.</item>
/// </list>
/// Uses only <see cref="CommandBuffer"/> + <see cref="RenderTargetIdentifier"/>, so it lives in the
/// pipeline-agnostic Runtime assembly and both providers reuse it. Intermediate downsample levels use
/// <c>cmd.GetTemporaryRT</c> (valid on both pipelines); only <paramref name="dst"/>/<paramref name="scratch"/>
/// are provider-owned, so <paramref name="dst"/> survives for the later UI draw in the same frame.
/// </summary>
public static class UIRectBlurCore
{
    // Temp RT ids for the downsample chain, one per intermediate level (1..MaxDownsample-1).
    // Precomputed so the per-frame URP path never allocates a string for Shader.PropertyToID.
    private static readonly int[] TempIDs =
    {
        0, // level 0 (full res) is the source, never a temp
        Shader.PropertyToID("_UIRectBlurDown1"),
        Shader.PropertyToID("_UIRectBlurDown2"),
        Shader.PropertyToID("_UIRectBlurDown3"),
    };

    // --- Provider registry ------------------------------------------------------------------------
    // How many blur providers are currently live (both pipelines). The UIRect inspectors warn when a
    // blurred element exists but nothing is filling _UIRectBackdropTex.

    /// <summary>Number of live backdrop-blur providers across both render pipelines.</summary>
    public static int ActiveProviderCount { get; private set; }

    public static void RegisterProvider() => ActiveProviderCount++;
    public static void UnregisterProvider() => ActiveProviderCount = Mathf.Max(0, ActiveProviderCount - 1);

    // --- Fallback texture -------------------------------------------------------------------------
    // Bound once at load so a blurred element with no active provider degrades to a flat neutral gray
    // instead of sampling an undefined global (which reads as black/garbage on many platforms).

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void BindFallbackTexture()
        => Shader.SetGlobalTexture(UIRectBlurConstants.BackdropTexID, Texture2D.grayTexture);

#if UNITY_EDITOR
    [UnityEditor.InitializeOnLoadMethod]
    private static void BindFallbackTextureEditor()
        => Shader.SetGlobalTexture(UIRectBlurConstants.BackdropTexID, Texture2D.grayTexture);
#endif

    // --- Recording --------------------------------------------------------------------------------

    /// <summary>Records the full downsample + blur + publish sequence into <paramref name="cmd"/>.</summary>
    /// <param name="cmd">Command buffer to record into (built once for Built-in, per-frame for URP).</param>
    /// <param name="material">The <see cref="UIRectBlurConstants.ShaderName"/> material.</param>
    /// <param name="source">Full-resolution camera color to capture.</param>
    /// <param name="srcWidth">Full source width in pixels.</param>
    /// <param name="srcHeight">Full source height in pixels.</param>
    /// <param name="dst">Provider-owned RT at the downsampled size; holds the blurred result on return.</param>
    /// <param name="scratch">Provider-owned RT at the downsampled size, used for ping-pong.</param>
    /// <param name="format">Format used for the intermediate downsample-chain RTs (match <paramref name="dst"/>).</param>
    /// <param name="settings">Downsample / iterations / radius.</param>
    /// <param name="setGlobalTexture">
    /// When true (Built-in RP and the URP compatibility path), publishes <paramref name="dst"/> as the
    /// global <c>_UIRectBackdropTex</c> via <c>cmd.SetGlobalTexture</c>. The URP Render Graph path passes
    /// false and publishes through the graph builder instead (<c>SetGlobalTextureAfterPass</c>).
    /// </param>
    public static void Record(
        CommandBuffer cmd, Material material,
        RenderTargetIdentifier source, int srcWidth, int srcHeight,
        RenderTargetIdentifier dst, RenderTargetIdentifier scratch,
        RenderTextureFormat format, in UIRectBlurSettings settings, bool setGlobalTexture = true)
    {
        int downsample = Mathf.Clamp(settings.downsample, UIRectBlurConstants.MinDownsample, UIRectBlurConstants.MaxDownsample);
        int iterations = Mathf.Clamp(settings.iterations, UIRectBlurConstants.MinIterations, UIRectBlurConstants.MaxIterations);

        // Downsample chain: source -> temp(1) -> temp(2) -> ... -> dst, halving each step so a large
        // downsample factor is a real box pyramid rather than one aliasing bilinear tap.
        RenderTargetIdentifier prev = source;
        for (int level = 1; level < downsample; level++)
        {
            int lw = Mathf.Max(1, srcWidth >> level);
            int lh = Mathf.Max(1, srcHeight >> level);
            cmd.GetTemporaryRT(TempIDs[level], lw, lh, 0, FilterMode.Bilinear, format);
            cmd.Blit(prev, TempIDs[level]);
            prev = TempIDs[level];
        }
        cmd.Blit(prev, dst); // final halving into dst (or a plain full-res copy when downsample == 0)
        for (int level = 1; level < downsample; level++)
            cmd.ReleaseTemporaryRT(TempIDs[level]);

        // Separable Gaussian ping-pong at the downsampled resolution. Each iteration is one horizontal
        // + one vertical pass; the result lands back in dst so the last-written target is deterministic.
        int w = Mathf.Max(1, srcWidth >> downsample);
        int h = Mathf.Max(1, srcHeight >> downsample);
        float radius = settings.blurRadius;
        for (int i = 0; i < iterations; i++)
        {
            cmd.SetGlobalVector(UIRectBlurConstants.BlurDirID, new Vector4(radius / w, 0f, 0f, 0f));
            cmd.Blit(dst, scratch, material);
            cmd.SetGlobalVector(UIRectBlurConstants.BlurDirID, new Vector4(0f, radius / h, 0f, 0f));
            cmd.Blit(scratch, dst, material);
        }

        if (setGlobalTexture)
            cmd.SetGlobalTexture(UIRectBlurConstants.BackdropTexID, dst);
    }
}
