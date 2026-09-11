# UnityArtistCLI host contract tests

These tests exercise the independent `unity-artist` executable without requiring a
licensed Editor. They assert the JSON envelope, exact release matrix, approval
preconditions, and the required Unity 2022.3 Built-in versus URP preflight.

The test intentionally does not claim connected Editor or Pipeline success. That
evidence is collected by the Unity Editor matrix workflow when a licensed fixture is
available.
