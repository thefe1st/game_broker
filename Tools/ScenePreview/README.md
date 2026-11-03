# Scene preview — offline stills of the procedural office and city

Run from repository root (.NET 8; PNG output and text overlays need ImageMagick `magick`
with Liberation Sans):

```bash
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- preview
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- preview sunset-west office --fast
dotnet run --project Tools/ScenePreview/ScenePreview.csproj -c Release -- --check
```

The first form writes every shot to `preview/*.png`; the second renders only the named shots.
Shots: `plaza-north`, `plaza-office`, `sunset-west`, `east-street`, `boulevard`, `office`,
`office-employee`, `aerial`. `--fast` turns off 2×2 supersampling, `--no-hud` skips the HUD.

The project compiles the **actual Core, OfficeScene partials, SurfaceLibrary and
OfficeInteractable** at C# 9 (the Unity 6 language level) against narrow engine stand-ins in
`UnityStubs.cs` and `PreviewFakes.cs`. The builders run as in Play: same voxel meshes,
pixel textures, lights, sky and fog values, same traffic positions for a given time.
`Renderer.cs` is a small CPU rasteriser that approximates the built-in forward path:
Standard BRDF in gamma space, trilight ambient, Unity's procedural skybox with the
high-quality sun, sky reflections, four soft shadow cascades, point lights, linear fog and
2×2 SSAA. World TextMesh labels and the HUD are drawn by ImageMagick, NOT by Unity fonts/IMGUI.

`--check` renders each shot at 240×160 and exits with 1 on builder warnings, an empty view
(less than 25% geometry) or a flat image (luminance spread below 0.08). It writes no files and
does not need ImageMagick; CI runs it as the `scene-preview` job.

This is NOT a Unity render or build. Shader variants, the real Quality-level shadow settings,
GPU filtering/mipmaps, colour management, IMGUI layout, font metrics, colliders, physics and
frame time still require Play Mode in the editor (checklist in `Docs/3D_SETUP.md`). Use the
stills to compare composition, palette and lighting between commits; a passing check does not
prove that the scene looks identical in Unity. Do not copy the stand-ins into Assets.
