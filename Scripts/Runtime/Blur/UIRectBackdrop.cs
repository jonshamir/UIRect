using UnityEngine;
using UnityEngine.UI;

namespace UIRect
{
    /// <summary>
    /// A frosted-glass panel: a rounded rectangle filled with the globally-blurred backdrop
    /// (<c>_UIRectBackdropTex</c>, produced once per camera by a blur provider), optionally tinted.
    ///
    /// This is the standalone "glass layer" - deliberately separate from <see cref="UIRectImage"/> so
    /// the core UIRect carries no blur code. It only does rounded-rect coverage + backdrop + tint; for a
    /// border, shadow or bevel, layer a <see cref="UIRectImage"/> on top. Needs a blur provider in the
    /// scene (<c>UIRectBackdropBlurBuiltin</c> on Built-in RP, or the URP backdrop-blur Renderer Feature);
    /// without one it falls back to a flat neutral gray.
    ///
    /// Reuses the core mesh path (<see cref="UIRectRenderer.Populate"/>) for radii packing and pivot-aware
    /// scaling, and the <c>UI/UIRectGlass</c> shader for the screen-space composite (XR-correct).
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class UIRectBackdrop : MaskableGraphic
    {
        [Tooltip("Corner radii in pixels. Uniform unless Independent Corners is on (top-left | top-right | bottom-right | bottom-left).")]
        public Vector4 radius = new(15, 15, 15, 15);

        [Tooltip("Edit each corner radius separately.")]
        public bool independentCorners = false;

        [Range(0, 1)]
        [Tooltip("How strongly the tint color covers the blurred backdrop (0 = pure blur, 1 = solid tint).")]
        public float tintStrength = 0f;

        public Vector2 Size => rectTransform.rect.size;

        // The glass shader has no per-element keyword variants, so a single shared material serves every
        // UIRectBackdrop. Released on domain reload / quit, mirroring UIRectRenderer's material cache.
        private const string ShaderName = "UI/UIRectGlass";
        private static Material _glassMaterial;

        public override Material defaultMaterial
        {
            get
            {
                if (_glassMaterial == null)
                {
                    var shader = Shader.Find(ShaderName);
                    if (shader != null)
                        _glassMaterial = new Material(shader) { name = "UIRectGlass (shared)" };
                }
                return _glassMaterial != null ? _glassMaterial : base.defaultMaterial;
            }
        }

        // No content texture: the fill comes from the screen-space backdrop, not this graphic's texture.
        public override Texture mainTexture => s_WhiteTexture;

        // Providers do nothing while no backdrop is enabled, so they need to know we exist. Registering
        // here rather than in Awake/OnDestroy means disabling the component (or its GameObject) also
        // stops the blur work, and ExecuteAlways keeps the count correct in edit mode.
        protected override void OnEnable()
        {
            base.OnEnable();
            UIRectBlurCore.RegisterBackdrop();
        }

        protected override void OnDisable()
        {
            UIRectBlurCore.UnregisterBackdrop();
            base.OnDisable();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            base.OnPopulateMesh(vh); // emits the base quad + UVs the renderer rewrites
            var p = new UIRectRenderParams
            {
                size = Size,
                color = color,                                             // .a = element opacity
                fillColor = new Color(color.r, color.g, color.b, tintStrength), // tint rgb + strength
                radius = radius,
                borderAlign = BorderAlign.Inside,
                // border / shadow / bevel intentionally left at defaults (this layer draws none)
            };
            UIRectRenderer.Populate(vh, p);
        }

        private static void ReleaseMaterial()
        {
            if (_glassMaterial == null)
                return;
            if (Application.isPlaying)
                Destroy(_glassMaterial);
            else
                DestroyImmediate(_glassMaterial);
            _glassMaterial = null;
        }

    #if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorCleanup()
            => UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += ReleaseMaterial;
    #endif

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void RegisterRuntimeCleanup()
            => Application.quitting += ReleaseMaterial;
    }
}
