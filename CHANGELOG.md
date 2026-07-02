# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

## [0.1.0] - 2026-07-01

### Added
- Initial release of UIRect component
- Rounded corners with independent control for each corner
- Border styling with width, color, and alignment options (inside, middle, outside)
- Soft shadow effects with size, spread, offset, and color control
- Bevel effects for depth
- `UIRectRawImage` component - RawImage-backed variant for videos, RenderTextures, and other dynamic textures
- Built-in animation system with custom easing curves
- `UIRectStyle` struct for defining and applying styles
- `AnimateTo()` method for smooth style transitions
- Custom shader for GPU-accelerated rendering
- Custom editor with organized property panels
- Unity UI menu integration (GameObject > UI > UIRect and UIRectRawImage)
- SphereMenu sample demonstrating world-space menus and the animation API

### Technical
- Extends Unity's Image and RawImage components for compatibility
- Shared, graphic-agnostic rendering core (`UIRectRenderer`) used by both components
- All public types are under the `UIRect` namespace (components `UIRectImage` and `UIRectRawImage`)
- SDF-based rounded rectangle rendering in shader
- Material caching for optimal performance
- Animations are driven by unscaled time, so they run while the game is paused
- Supports Canvas rendering modes: Screen Space and World Space

[Unreleased]: https://github.com/jonshamir/UIRect/compare/v0.1.0...HEAD
[0.1.0]: https://github.com/jonshamir/UIRect/releases/tag/v0.1.0
