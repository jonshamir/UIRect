# Contributing to UIRect

Thanks for your interest in improving UIRect! Contributions of all kinds are welcome —
bug reports, feature ideas, documentation fixes, and pull requests.

## Reporting issues

- Search the [existing issues](https://github.com/jonshamir/UIRect/issues) first to avoid duplicates.
- When filing a bug, include your Unity version, the render pipeline (Built-in / URP), and
  a minimal set of steps (or a small repro scene) that reproduces the problem.

## Development setup

1. Fork and clone the repository.
2. Open the repository root in the Unity Editor (2021.3 or later). The package lives under
   `Packages/com.jonshamir.uirect` and is picked up automatically as an embedded package.
3. Make your changes in a feature branch.

## Running tests

Run the test suites from **Window > General > Test Runner** (both the EditMode and PlayMode
tabs). All tests in `JonShamir.UIRect.Tests` and `JonShamir.UIRect.Editor.Tests` should pass
before you open a pull request. The same suites run in CI on every pull request.

## Pull requests

- Keep pull requests focused; one logical change per PR is easiest to review.
- Match the surrounding code style and keep public types documented with XML comments.
- All runtime and editor types live in the `UIRect` namespace.
- Update `CHANGELOG.md` under the `[Unreleased]` heading when your change is user-facing.
- Make sure the project compiles with no new warnings and that the tests pass.

## License

By contributing, you agree that your contributions will be licensed under the
[MIT License](LICENSE) that covers this project.
