# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]

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
