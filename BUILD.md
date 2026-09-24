# Build and test

## Installer engine tests (no game installation needed)

On Windows with the .NET Framework 4.x compiler available:

```powershell
./test-source.ps1
```

This compiles the original RC3 transaction engine and test harness into an isolated output directory and executes **45 synthetic cases**. It does not launch the game or write to an installed game. A different output directory is used per run. The 46th historical local case needed a real payload and copies of three owned game files and is not executed by this source-only runner.

## Full installer

`installer/build.ps1` needs `payload.zip` and `package.xml` in its InputDirectory. They are intentionally not supplied as a ready-to-install payload in this source preview. The manifest in `evidence/` documents the old local RC3 inputs, but is not itself an installable payload.

Before cloud release builds can be enabled: replace the game-template-derived font bundle with an independently built font asset; specify authorized dependency downloads and checksums; build the plugin from source with legitimate reference inputs; assemble the translations, notices and loader; regenerate every manifest hash; then build and test the installer. Never upload game executables, assemblies, credentials or local real-game test fixtures to GitHub Actions.

The plugin project accepts `GameManaged` and `BepCore` properties. Example for a legally owned matching installation:

```powershell
dotnet build plugin/SilksongArabic.csproj -c Release -p:GameManaged="C:/path/to/game/Hollow Knight Silksong_Data/Managed" -p:BepCore="C:/path/to/BepInEx/core"
```

This compiles only the plugin DLL; it is not a complete usable translation package. A .NET SDK supporting netstandard2.1 is required. Do not publish copied game references.

`.github/workflows/source-tests.yml` compiles and runs the 45 synthetic cases on a Windows GitHub-hosted runner. It is a source regression workflow, not a full-payload build or signing workflow. See Actions for actual run results. No signing workflow is active; byte-for-byte reproducibility of RC3 is not claimed.
