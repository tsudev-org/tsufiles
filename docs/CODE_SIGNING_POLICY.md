# Code signing policy

TsuFiles is free, open-source software (MIT) by [TsuDEV](https://tsudev.com): a desktop file converter for Windows,
macOS and Linux. This page describes how its Windows release files are built and signed.

## Signing provider

Status: the project intends to apply to the SignPath Foundation for free code signing; Windows release files are not
Authenticode-signed yet (SmartScreen may warn on first run). Once the project is accepted, every Windows release will be
signed and this section will read:

> Free code signing provided by [SignPath.io](https://about.signpath.io), certificate by [SignPath Foundation](https://signpath.org)

## What is signed

Only files built from this repository by the release workflow
[`release.yml`](https://github.com/tsudev-org/tsufiles/blob/main/.github/workflows/release.yml) on GitHub Actions, from a
version tag `v<version>`:

- `TsuFiles.exe` and the other .NET assemblies of the application, built from this repository's sources.
- `tsudev-files-<version>-win-Setup.exe`: the installer, built from
  [`src/TsuFiles.Setup`](https://github.com/tsudev-org/tsufiles/tree/main/src/TsuFiles.Setup) in this repository. The
  same program is the uninstaller (`uninstall.exe`).
- `tsudev-files-<version>-win-Portable.zip`: the same application files, unpacked by the user.

Third-party binaries bundled with the application (the .NET runtime, Avalonia, SkiaSharp, Magick.NET, PDFium and the
other libraries listed in [THIRD-PARTY-NOTICES.md](https://github.com/tsudev-org/tsufiles/blob/main/THIRD-PARTY-NOTICES.md))
are upstream files. They are listed in the CycloneDX SBOM attached to each release and are not signed as our own.
Optional engines (LibreOffice, FFmpeg, Pandoc and others) are not part of the release: the application downloads them
only when the user asks, from their official sources, and checks a pinned SHA-256 before use.

## Team roles

| Role | Who |
| --- | --- |
| Authors (committers) | Members of the [tsudev-org](https://github.com/orgs/tsudev-org/people) GitHub organization with write access to this repository |
| Reviewers | Repository maintainers. Every change reaches `main` through a pull request; the branch ruleset requires passing CI (build and tests on Windows, Linux and macOS, CodeQL, secret scanning, dependency review) |
| Approvers | Owners of the tsudev-org organization. Each signing request is approved manually, one release at a time |

All team members use multi-factor authentication. Two-factor authentication is enforced for every member of the
tsudev-org GitHub organization, and is required for SignPath accounts.

## Privacy

TsuFiles converts files on the user's computer. Files are never uploaded; there is no telemetry, no analytics and no
account. The program connects to other networked systems only in these cases:

- **Update check** (on by default, can be turned off in Settings): reads the latest release from the GitHub API and
  `https://files.tsudev.com/phien-ban.json`. No user data is sent; the request carries only what any HTTPS request
  carries (IP address, user agent).
- **Optional engines**: downloaded from their official sources only when the user installs one.
- **Share with phone / LAN send**: off by default; started by the user, local network only.

With the update check turned off, the program does not transfer any information to other networked systems unless
specifically requested by the user. Privacy policy: [tsudev.com/privacy](https://tsudev.com/privacy). Security
architecture: [files.tsudev.com/bao-mat/kien-truc/](https://files.tsudev.com/bao-mat/kien-truc/).

## System changes and uninstall

By default the installer installs for all users into `Program Files` (administrator rights); the user can choose
"this user only" (`%LocalAppData%\Programs`, no administrator rights). It adds Start Menu shortcuts and, by the user's
choice in the installer, a desktop shortcut, the Explorer context menu entry (on Windows 11 the top-level entry is a
sparse MSIX package built from this repository), start with Windows and the `tsufiles` command on the PATH. It
registers a standard uninstall entry (Settings, Apps). It does not install services or drivers. Uninstalling removes the
program files, shortcuts and the entries above; the user decides whether to delete their settings. A portable zip that
changes nothing on the system is also published.

## Verifying a download

- Every release has `*SHA256SUMS.txt` files listing the SHA-256 of each release file, each with a `.sig` ECDSA P-256
  signature made with the TsuFiles update key; the built-in updater refuses files whose signature does not verify.
- Build provenance (SLSA) and the SBOM are attested: `gh attestation verify <file> --repo tsudev-org/tsufiles`.
- Release files are scanned on VirusTotal before the release is published; report links are in the release notes.
- Public evidence (OpenSSF Scorecard, Best Practices, scans): [files.tsudev.com/security/](https://files.tsudev.com/security/).

## Reporting a problem

Report a security problem or a suspected mis-signed file privately as described in
[SECURITY.md](https://github.com/tsudev-org/tsufiles/blob/main/SECURITY.md). The team will cooperate with any
investigation by SignPath Foundation.
