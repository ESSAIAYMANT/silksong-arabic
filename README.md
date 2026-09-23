# Silksong Arabic

Unofficial Arabic localization for **Hollow Knight: Silksong**, with a Windows installer and maintenance tool. Maintained by [Ayman Essai / ESSAIAYMANT](https://github.com/ESSAIAYMANT).

[العربية](README-AR.md) · [Downloads and status](DOWNLOADS.md) · [Build and tests](BUILD.md) · [Code signing policy](CODE_SIGNING.md) · [Privacy](PRIVACY.md)

## Current status

**Source review preview — not a signed, production-approved release.** Installer: **0.3.0 RC3**; translation plugin: **0.1.4**. Target: Windows x64, game **1.0.30000 / Steam build 22479045**. Other game versions are not supported by this candidate.

The local translation contains 5,685 runtime entries. Text coverage does not prove that every scene, line, or layout is correct. This is a community project, not an official Team Cherry release.

The installer detects Steam libraries or accepts a manually selected folder, verifies supported game fingerprints, installs the translation with backup journals, repairs missing owned files, temporarily disables/re-enables the translation, and removes its own changes conservatively. Conflicts are reported instead of silently overwriting user changes. Save files are not modified. Arabic currently replaces the **English** language slot.

## Verification

The existing local RC3 candidate passed **46 engine cases**, including one isolated real-payload file-copy cycle. The public source-only test runner executes **45 synthetic cases** without game binaries. These are filesystem/transaction tests, not GUI or gameplay acceptance. Native RC3 interface testing, clean-machine gameplay and trusted signing remain pending. An earlier unsigned candidate was blocked by Windows Smart App Control; do not disable security protections to run it.

## Repository contents

- `installer/`: Windows Forms installer, transaction engine, reversible toggle and tests.
- `plugin/`: runtime Arabic rendering/localization source; building requires references from a legally owned game installation.
- `translation/`: Arabic localization dictionaries keyed by game localization identifiers.
- `fonts/`: Noto Sans Arabic under its original OFL license.
- `evidence/`: historical local RC3 results and package hashes; these do not claim public release acceptance.

Game executables, game assemblies, saves, credentials and diagnostic captures are excluded. The local RC3 `arabicfont.bundle` is also excluded because it was derived from a game font-bundle template; its independent rebuild/provenance must be resolved before an all-open-source signing submission. Consequently this source preview cannot yet produce a complete installable payload on a clean cloud runner.

## Licensing and third-party material

Read [LICENSE-STATUS.md](LICENSE-STATUS.md) and [THIRD-PARTY.md](THIRD-PARTY.md). Original code is published for inspection; an OSI license has not yet been authorized by the maintainer. Do not infer an open-source license from public visibility. Noto retains OFL. Game narrative, names and trademarks remain with their respective rights holders; no ownership of Team Cherry's work is claimed.

## Code signing policy

See [Code signing policy](CODE_SIGNING.md). **SignPath Foundation approval has not been granted and no Foundation signature is present.** An eligibility application is being prepared. There are no fabricated download counts, endorsements or claims of established adoption.

For bugs, [open an issue](https://github.com/ESSAIAYMANT/silksong-arabic/issues) with game version and a cropped screenshot. Never upload saves, credentials, complete game files or unreviewed logs.
