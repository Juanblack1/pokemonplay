# SignPath application preparation

Status: prepared, not submitted or approved.

Repository: https://github.com/Juanblack1/pokemonplay

Downloads: https://github.com/Juanblack1/pokemonplay/releases

License: GPL-3.0 for project-owned code; upstream components retain their licenses.

Suggested description:

> Pokémon Play is an open-source Windows launcher for user-provided Pokémon games,
> save profiles and backups, emulator settings and a local Pokémon collection.
> It uses PKHeX.Core for save editing and legality analysis. Public releases include
> open-source emulators and their corresponding source/license information, but
> no ROMs, BIOS, firmware, console keys or user data. GitHub Actions builds project
> binaries from this public repository. Windows Smart App Control blocks recent
> unsigned project assemblies. We request public-trust signing of our own launcher,
> application, updater and installer.

## Maintainer steps

1. Confirm MFA and review the published signing/privacy policies.
2. Apply at https://signpath.org/apply.html with your own contact details.
3. Disclose that initial source was reconstructed from the maintainer's own
   executable, as documented in `recovered-source/README.md`.
4. Await eligibility/reputation review. Approval and timing are not guaranteed.
5. If accepted, configure project/artifact/signing policies, restricted CI access
   and manual approvals. Attribute the provider only after acceptance.
6. Integrate submission/retrieval before packaging and require verification.
   The existing PFX script is not the SignPath backend.

The form requires maintainer contact information and service-term acceptance.
No application has been sent. Do not submit personal download bundles,
third-party binaries for re-signing, or account credentials.
