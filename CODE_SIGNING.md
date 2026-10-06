# Code signing policy

## Status

Pokémon Play is preparing an application for free code signing through
[SignPath Foundation](https://signpath.org/apply.html). The project has not been
accepted and has no SignPath certificate. Existing v172.0.5 downloads are unsigned.
No approval date or Windows compatibility guarantee is claimed.

## Responsibilities

- Author, committer and reviewer: [Juanblack1](https://github.com/Juanblack1).
- Release and signing approver: Juanblack1.
- Build: GitHub Actions in this repository, from a reviewed release tag.

Signing approval belongs to the maintainer, never to an automated coding assistant.
Before applying, the maintainer must confirm MFA on GitHub and the signing service.
Every production signing request requires maintainer approval.

## Scope

Only artifacts built from Pokémon Play source receive its signing identity:
runtime `Pokemons Play.exe` and `Pokemons Play.dll`, portable launcher
`Pokemons Play.exe`, `PokemonPlayUpdater.exe`, and the project installer.

PKHeX.Core, .NET, emulators and cores retain their upstream identity and licenses.
Do not sign them with the Pokémon Play certificate. Unsigned upstream binaries
can still be blocked separately by Windows. ROMs, BIOS, firmware, console keys,
user saves and private bundles are excluded from submissions and public releases.

The optional PFX script uses an explicit project-file allowlist. It is not a
SignPath integration. Configure organization, project, artifact configuration,
signing policy and restricted CI credentials after acceptance. SignPath manages
its certificate; it does not provide a downloadable PFX.

## Release order

1. Build and test a reviewed tag with dependency/source provenance.
2. Submit project binaries from the CI artifact, await maintainer approval,
   retrieve signed files and verify Authenticode signatures.
3. Assemble update/portable packages and installer from the signed files.
4. Sign and verify the installer; calculate SHA-256 after signing.
5. Test installation and launch with Smart App Control active before claiming
   the blocking issue is resolved. Publish completed assets together.

Do not disable Windows protection or use self-signed certificates as substitutes
for publicly trusted signatures. See [Privacy](PRIVACY.md) and [GPL-3.0](LICENSE).
After acceptance, identify the actual provider and certificate publisher here.
