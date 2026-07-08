using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// Built-in Render Pipeline provider for UIRect backdrop blur. Attach to the camera that renders
/// your UI (works with Screen Space - Camera and World Space canvases). It grabs the camera color
/// before transparents, blurs it, and exposes it as the global <c>_UIRectBackdropTex</c> that
/// UIRect's <c>_USE_BLUR</c> shader variant samples.
///
/// Works in Play mode and in edit mode (<see cref="ExecuteAlways"/>): the effect previews live in
/// the Game view, and - when <see cref="previewInSceneView"/> is on - in the Scene view too. The
/// blur runs once per rendering camera regardless of how many UIRects use it, so cost is decoupled
/// from element count. The blur amount lives in <see cref="settings"/> and is shared, so every
/// blurred panel gets the same blur.
///
/// Capturing at <see cref="CameraEvent.AfterForwardOpaque"/> means the blur reflects opaque scene +
/// skybox behind the panel, but not other transparents drawn afterwards - a two-camera setup
/// (background camera to a RenderTexture) is the alternative if full transparent fidelity is needed.
///
/// The GPU work itself lives in <see cref="UIRectBlurCore"/>, shared with the URP provider. For URP,
/// use the UIRect backdrop-blur Renderer Feature instead (it sets the same global texture).
/// </summary>
[ExecuteAlways]
[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class UIRectBackdropBlurBuiltin : MonoBehaviour
{
    [Tooltip("Blur amount and cost. Identical to the URP provider's settings.")]
    public UIRectBlurSettings settings = UIRectBlurSettings.Default;

    [Tooltip("When the camera color is captured. AfterForwardOpaque keeps the panel's own pixels out of its blur.")]
    public CameraEvent captureEvent = CameraEvent.AfterForwardOpaque;

    [Tooltip("Also preview the blur in the Scene view while editing (editor only).")]
    public bool previewInSceneView = true;

    // Per-camera blur resources. A single component can drive several cameras at once (e.g. the UI
    // camera plus one or more Scene view cameras in edit mode), each with its own sized buffers.
    private class CameraResources
    {
        public CommandBuffer commandBuffer;
        public RenderTexture rtA;
        public RenderTexture rtB;
        public int width, height, iterations, downsample;
        public float radius;
        public bool previewInSceneView;
        public CameraEvent captureEvent;
        public RenderTextureFormat format;
    }

    private Camera _camera;
    private Material _blurMaterial;
    private readonly Dictionary<Camera, CameraResources> _perCamera = new();
    private bool _dirty;

    private void OnEnable()
    {
        _camera = GetComponent<Camera>();
        EnsureMaterial();
        Camera.onPreRender += OnCameraPreRender;
        UIRectBlurCore.RegisterProvider();
    }

    private void OnDisable()
    {
        Camera.onPreRender -= OnCameraPreRender;
        ReleaseAll();
        UIRectBlurCore.UnregisterProvider();
    }

    // Inspector changes: rebuild every camera's buffer on the next render with the new settings.
    private void OnValidate() => _dirty = true;

    private void EnsureMaterial()
    {
        if (_blurMaterial != null)
            return;
        var shader = Shader.Find(UIRectBlurConstants.ShaderName);
        if (shader != null)
            _blurMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
    }

    // Built-in RP global hook, fired right before each camera renders (Game, Scene view, etc.).
    private void OnCameraPreRender(Camera cam)
    {
        if (_blurMaterial == null || !isActiveAndEnabled || cam == null)
            return;

        // Built-in RP can't produce a stereo backdrop under single-pass instanced/multiview XR
        // (cmd.Blit isn't stereo-aware). Skip and leave the neutral gray fallback in place.
        if (IsUnsupportedInstancedXR())
        {
            if (_perCamera.Count > 0)
                ReleaseAll();
            WarnUnsupportedXROnce();
            return;
        }

        if (_dirty)
        {
            ReleaseAll();
            _dirty = false;
        }

        if (!ShouldBlur(cam))
            return;

        GetOrBuild(cam);
    }

    // Multi-pass renders each eye as a conventional 2D target, so it works; only the single-pass
    // (instanced / multiview) modes are unsupported here. Use URP for single-pass XR.
    private static bool IsUnsupportedInstancedXR()
        => UnityEngine.XR.XRSettings.enabled &&
           UnityEngine.XR.XRSettings.stereoRenderingMode != UnityEngine.XR.XRSettings.StereoRenderingMode.MultiPass;

    private static bool _warnedUnsupportedXR;
    private static void WarnUnsupportedXROnce()
    {
        if (_warnedUnsupportedXR)
            return;
        _warnedUnsupportedXR = true;
        Debug.LogWarning("UIRectBackdropBlurBuiltin: backdrop blur is not supported under single-pass " +
                         "instanced/multiview XR on the Built-in Render Pipeline. Use URP for single-pass " +
                         "XR, or set XR to Multi-Pass. Blurred elements fall back to a flat gray.");
    }

    private bool ShouldBlur(Camera cam)
    {
        if (cam == _camera)
            return true;
#if UNITY_EDITOR
        if (previewInSceneView && cam.cameraType == CameraType.SceneView)
            return true;
#endif
        return false;
    }

    private void GetOrBuild(Camera cam)
    {
        int downsample = Mathf.Clamp(settings.downsample, UIRectBlurConstants.MinDownsample, UIRectBlurConstants.MaxDownsample);
        int w = Mathf.Max(1, cam.pixelWidth >> downsample);
        int h = Mathf.Max(1, cam.pixelHeight >> downsample);
        RenderTextureFormat format = GetFormat(cam);

        if (_perCamera.TryGetValue(cam, out var res))
        {
            bool valid = res.width == w && res.height == h && res.iterations == settings.iterations &&
                         res.downsample == downsample && Mathf.Approximately(res.radius, settings.blurRadius) &&
                         res.captureEvent == captureEvent && res.previewInSceneView == previewInSceneView &&
                         res.format == format && res.rtA != null && res.rtA.IsCreated();
            if (valid)
                return;
            Release(cam, res);
        }

        PruneDeadCameras();

        res = new CameraResources
        {
            rtA = CreateRT(w, h, format),
            rtB = CreateRT(w, h, format),
            width = w, height = h, iterations = settings.iterations, downsample = downsample,
            radius = settings.blurRadius, previewInSceneView = previewInSceneView,
            captureEvent = captureEvent, format = format
        };

        // Drop any command buffers we left on this camera before (e.g. after a domain reload), so
        // they don't accumulate. Ours are identified by name.
        RemoveOurCommandBuffers(cam, captureEvent);

        // rtA is persistent (not a temporary RT), so it stays valid for the UI draw later this frame.
        res.commandBuffer = new CommandBuffer { name = UIRectBlurConstants.CommandBufferName };
        UIRectBlurCore.Record(res.commandBuffer, _blurMaterial,
            BuiltinRenderTextureType.CurrentActive, cam.pixelWidth, cam.pixelHeight,
            res.rtA, res.rtB, format, settings);

        cam.AddCommandBuffer(captureEvent, res.commandBuffer);
        _perCamera[cam] = res;
    }

    // Match the camera's HDR setting, falling back to an SDR format where HDR RTs aren't supported.
    private static RenderTextureFormat GetFormat(Camera cam)
    {
        if (cam.allowHDR && SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.DefaultHDR))
            return RenderTextureFormat.DefaultHDR;
        return RenderTextureFormat.Default;
    }

    private static void RemoveOurCommandBuffers(Camera cam, CameraEvent evt)
    {
        foreach (var cb in cam.GetCommandBuffers(evt))
        {
            if (cb.name == UIRectBlurConstants.CommandBufferName)
                cam.RemoveCommandBuffer(evt, cb);
        }
    }

    private static RenderTexture CreateRT(int w, int h, RenderTextureFormat format)
    {
        var rt = new RenderTexture(w, h, 0, format)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = "UIRectBackdrop"
        };
        rt.Create();
        return rt;
    }

    // Drop resources for cameras that no longer exist (e.g. a closed Scene view), releasing their RTs.
    private void PruneDeadCameras()
    {
        List<Camera> dead = null;
        foreach (var kvp in _perCamera)
        {
            if (kvp.Key == null)
                (dead ??= new List<Camera>()).Add(kvp.Key);
        }
        if (dead == null)
            return;
        foreach (var cam in dead)
        {
            Release(cam, _perCamera[cam]);
            _perCamera.Remove(cam);
        }
    }

    private void ReleaseAll()
    {
        foreach (var kvp in _perCamera)
            Release(kvp.Key, kvp.Value);
        _perCamera.Clear();
    }

    private void Release(Camera cam, CameraResources res)
    {
        if (res.commandBuffer != null)
        {
            if (cam != null)
                cam.RemoveCommandBuffer(res.captureEvent, res.commandBuffer);
            res.commandBuffer.Release();
            res.commandBuffer = null;
        }
        ReleaseRT(ref res.rtA);
        ReleaseRT(ref res.rtB);
    }

    private static void ReleaseRT(ref RenderTexture rt)
    {
        if (rt == null)
            return;
        rt.Release();
        if (Application.isPlaying)
            Destroy(rt);
        else
            DestroyImmediate(rt);
        rt = null;
    }
}
