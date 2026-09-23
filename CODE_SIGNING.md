# Code signing policy

## Current status

**Not approved by SignPath Foundation; no trusted public signature yet.** A free-code-signing eligibility request is being prepared. The source preview is not represented as meeting every eligibility condition. See LICENSE-STATUS.md and BUILD.md for unresolved licensing and full-payload rebuild requirements.

Do not bypass Windows Smart App Control, antivirus or signature warnings. An unsigned local candidate is not a production release. Source tests passing does not mean its GUI or gameplay has passed acceptance.

## Proposed responsibilities

Maintainer, committer, reviewer and signing approver: [Ayman Essai / ESSAIAYMANT](https://github.com/ESSAIAYMANT). This is currently a single-maintainer project; no independent human reviewer is claimed. External contributions require maintainer review. SignPath and repository MFA requirements must be satisfied before signing is enabled.

Every future signing request must be approved manually by the maintainer and be traceable to a reviewed source revision and a verifiable automated build. Signing keys or service credentials must never be committed. No signing service is configured in this repository yet.

Only binaries built from this project's own maintained source may be submitted under any approved Foundation subscription. Do not re-sign BepInEx, Unity Doorstop, Harmony or other upstream binaries as this project. Preserve their licenses and provenance. An older private-certificate staging script used locally is deliberately excluded from this Foundation-oriented source preview.

After actual Foundation acceptance and activation, the homepage and release page will be updated with the provider attribution required by the Foundation. That attribution is not used now because it would imply approval that has not occurred.

## Release checks

A distributable release needs reviewed rights/provenance for every payload component, reproducible/verifiable source builds, valid timestamped signatures, matched SHA-256 checksums, tests and manual GUI/gameplay acceptance. Dependency-signature coverage and the precise artifact configuration must be agreed with the provider; signing the outer EXE alone does not imply every embedded DLL is signed or will be accepted by endpoint security.

See [Privacy policy](PRIVACY.md) and [Foundation conditions](https://signpath.org/terms).
