# Privacy policy

Effective date: 2026-09-23. Project maintainer: [ESSAIAYMANT](https://github.com/ESSAIAYMANT).

The installer performs local checks of the selected game directory, Steam library locations, file hashes, free space and whether the game is running. It stores backup files and operation journals under `.silksong-arabic-installer` inside the selected game folder. These stay on the user's computer.

The installer does not provide telemetry, advertising, automatic crash uploads or automatic updates. It does not read or change game saves. The translation plugin reads its local dictionaries and font resources; optional development diagnostics write local files only and are not enabled in the release payload.

A support report is saved locally only when the user requests export. The installer redacts the selected game path and current user profile path. Users should still review the report before sharing it; redaction cannot guarantee that every possible personal string is removed. No report is automatically uploaded.

The user may explicitly launch the game through Steam or open a help link. Steam/GitHub then handle that interaction under their own privacy policies. GitHub also hosts this repository, issues and any future release downloads: [GitHub privacy statement](https://docs.github.com/en/site-policy/privacy-policies/github-general-privacy-statement). Signing-service applications concern the maintainer, not automatic collection from players: [SignPath privacy policy](https://signpath.io/privacy-policy).

The installer will not transfer information to other networked systems unless specifically requested by the user or person operating it. Other installed mods, Steam and the game itself are outside this project's control.
