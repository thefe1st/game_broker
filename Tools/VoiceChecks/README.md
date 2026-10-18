# Headless voice-flow checks

Run with .NET 8: `dotnet test Tools/VoiceChecks/VoiceChecks.csproj`.

The project compiles the real Core and Runtime/Voice sources against **narrow test doubles** in UnityFakes.cs. This is deliberate: assertions cover control flow, local-only recording branches, explicit device selection, cancellation, bounded health responses and safe errors. They do **not** prove Unity API compatibility, real recording/playback, permissions, JsonUtility serialization or rendering. Do not copy the doubles into Assets. Unity creates its own microphone clips and uses UnityWebRequest in the actual game.

The fakes do not access a network, sound device or API key. Build artifacts are ignored. Separate CoreChecks tests exercise engine-free rules with NUnit. Real-editor QA is documented in Docs/QA_VOICE_AND_RESETS.md.
