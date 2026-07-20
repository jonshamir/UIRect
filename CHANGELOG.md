# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

### Added
- Backdrop blur ("frosted glass") — an optional, self-contained module (`JonShamir.UIRect.Blur`),
  deliberately kept out of the core UIRect so the base component carries no blur code.
  - `UIRectBackdrop` component (GameObject ▸ UI ▸ UIRect Backdrop): a standalone glass layer — a
    rounded rect filled with the blurred backdrop and an optional tint. Layer a `UIRectImage` on top
    for a border/shadow. Draws via the dedicated `UI/UIRectGlass` shader (screen-space backdrop
    sample + shared rounded-rect coverage), reusing the core mesh path.
  - The blur is published as a global `_UIRectBackdropTex` produced once per camera by a provider.
  - `UIRectBackdropBlurBuiltin` camera component provides the blur in the Built-in Render Pipeline,
    previewing live in both the Game view and the Scene view in edit mode (no Play mode needed).
  - `UIRectBackdropBlurFeature` Renderer Feature provides the same for URP (optional
    `JonShamir.UIRect.URP` assembly, compiled only when URP is installed). Runs natively under
    Unity 6 Render Graph, with a compatibility-mode fallback for older URP.
  - Both providers share a single `UIRectBlurCore` (blit sequence) and `UIRectBlurSettings`
    (downsample / iterations / radius), so they can't drift apart.
  - Blurred rects with no active provider fall back to a neutral gray, and the inspector warns you
    to add one.
  - Works on Screen Space - Camera and World Space canvases.
  - XR: fully supported on URP (single-pass instanced / multiview and multi-pass) via a Blitter-based,
    stereo-aware URP blur shader. On Built-in RP, XR works in multi-pass only; single-pass instanced
    is unsupported and falls back to the neutral gray with a one-time warning.

### Changed
- Minimum Unity version raised to 2021.3 (2020.3 is end-of-life).
- Backdrop blur width is now driven by Downsample and Iterations; Blur Radius is a fine softness
  step (previously large radii could undersample and band).

### Performance
- Backdrop blur uses a 5-sample linear Gaussian (down from 9 taps per pass) and an anti-aliased
  downsample chain instead of a single bilinear copy.

## [0.1.0] - 2024-01-01

### Added
- Initial release of UIRect component
- Rounded corners with independent control for each corner
- Border styling with width, color, and alignment options (inside, middle, outside)
- Soft shadow effects with size, spread, offset, and color control
- Bevel effects for depth
- Built-in animation system with custom easing curves
- `UIRectStyle` struct for defining and applying styles
- `AnimateTo()` method for smooth style transitions
- Custom shader for GPU-accelerated rendering
- Custom editor with organized property panels
- Unity UI menu integration (GameObject > UI > UIRect)

### Technical
- Extends Unity's Image component for compatibility
- SDF-based rounded rectangle rendering in shader
- Material caching for optimal performance
- Supports Canvas rendering modes: Screen Space and World Space

[Unreleased]: https://github.com/jonshamir/UIRect/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/jonshamir/UIRect/releases/tag/v0.1.0
