# Packaging templates for native ytx binaries

**Status: exploratory.** Nothing here is wired up yet. These templates assume the `native.yml`
workflow has attached NativeAOT archives and a `SHA256SUMS` file to a GitHub Release. The NuGet
tool (`dotnet tool install -g solrevdev.ytx`) is unchanged and stays the primary install route.

## Release assets these templates expect

For release `vX.Y.Z`, `native.yml` uploads:

| Asset | Contents |
|---|---|
| `ytx-X.Y.Z-osx-arm64.tar.gz` | `ytx`, `README.md` |
| `ytx-X.Y.Z-osx-x64.tar.gz` | `ytx`, `README.md` |
| `ytx-X.Y.Z-linux-x64.tar.gz` | `ytx`, `README.md` (built on Ubuntu 22.04 for a lower glibc floor) |
| `ytx-X.Y.Z-linux-arm64.tar.gz` | `ytx`, `README.md` |
| `ytx-X.Y.Z-win-x64.zip` | `ytx.exe`, `README.md` |
| `SHA256SUMS` | `sha256sum` output for every archive |

The workflow attaches assets when a release is published by a person, or when it is dispatched with
`tag=vX.Y.Z`. A release that `publish.yml` creates with `GITHUB_TOKEN` does **not** trigger it on its
own. See the comment at the top of `.github/workflows/native.yml`.

## Name clash

[koguchic/ytx](https://github.com/koguchic/ytx) is an unrelated Rust CLI, installed with
`cargo install ytx-cli`, that also installs a command called `ytx`. On 8 Oct 2026 neither project
had a formula in homebrew-core, and koguchic/ytx has no tap or Scoop bucket.

- Always document the fully qualified names: `brew install solrevdev/tap/ytx` and
  `scoop install solrevdev/ytx`.
- If ytx is ever submitted to homebrew-core or the Scoop main bucket, expect a naming discussion.
  A fallback name such as `solrevdev-ytx` would keep the `ytx` command but not the short formula name.
- Both tools put `ytx` on `PATH`. Users who install both get whichever comes first.

## Homebrew tap

Template: [`homebrew/ytx.rb`](homebrew/ytx.rb).

1. Create the public repository `solrevdev/homebrew-tap` (the `homebrew-` prefix is required).
2. Copy the template to `Formula/ytx.rb` and fill in the version and four `sha256` values from
   `SHA256SUMS`.
3. Check it: `brew install --build-from-source solrevdev/tap/ytx`, `brew test ytx` and
   `brew audit --strict --online solrevdev/tap/ytx`.
4. Users install with `brew tap solrevdev/tap` then `brew install ytx`, or in one step with
   `brew install solrevdev/tap/ytx`.

Notes:

- The binaries are only ad-hoc signed. Homebrew downloads with `curl`, so macOS does not quarantine
  them and Gatekeeper does not block them. A browser download of the same archive is quarantined.
  Avoiding that needs an Apple Developer ID and notarisation.
- On Linux, the binary loads the system OpenSSL 3 (`libssl.so.3`) at run time and needs CA
  certificates. Desktop distributions have both. A bare `ubuntu:24.04` container fails with
  "The SSL connection could not be established" until `libssl3t64` and `ca-certificates` are installed.
- `InvariantGlobalization` is on for AOT builds, so ICU is not needed.

### Automating the tap update

After `native.yml` uploads the assets, a job can rewrite the formula and push it to the tap:

- Read `SHA256SUMS` from the release, substitute the placeholders in this template and commit
  `Formula/ytx.rb` to `solrevdev/homebrew-tap`.
- **Secret needed:** `HOMEBREW_TAP_TOKEN`, a fine-grained personal access token limited to the
  `solrevdev/homebrew-tap` repository with **Contents: read and write**. The workflow's own
  `GITHUB_TOKEN` cannot push to another repository.
- Alternatives to a PAT: a GitHub App installed on the tap repository (short-lived tokens via
  `actions/create-github-app-token`), or a deploy key with write access stored as a secret.
- `brew bump-formula-pr` also works, but it opens a pull request and is aimed at homebrew-core.

## Scoop bucket

Template: [`scoop/ytx.json`](scoop/ytx.json).

1. Create the public repository `solrevdev/scoop-bucket`. The
   [ScoopInstaller/BucketTemplate](https://github.com/ScoopInstaller/BucketTemplate) includes an
   "Excavator" workflow that runs `checkver` and `autoupdate` on a schedule.
2. Copy the template to `bucket/ytx.json`, fill in the version and the `win-x64` hash.
3. Users install with `scoop bucket add solrevdev https://github.com/solrevdev/scoop-bucket`
   then `scoop install solrevdev/ytx`.

`checkver` reads the latest GitHub Release. `autoupdate` builds the new URL and takes the hash from
`SHA256SUMS` by matching the archive name. With Excavator running inside the bucket repository, the
bucket updates itself using that repository's own `GITHUB_TOKEN`. **No cross-repository secret is
needed.** The trade-off is a delay of up to the Excavator schedule (every 4 hours in the template).

Only `win-x64` is built today. Add a `"arm64"` block if `win-arm64` is added to the matrix.

## winget

winget is the default package manager on Windows 10 and 11, so it reaches more users than Scoop.

- Package identifier: `solrevdev.ytx`. Use the zip with `InstallerType: zip`,
  `NestedInstallerType: portable` and `PortableCommandAlias: ytx`.
- The first version must be submitted as a pull request to
  [microsoft/winget-pkgs](https://github.com/microsoft/winget-pkgs), for example with
  `wingetcreate new <zip url>`. It goes through automated validation and moderator review.
- Later versions can be automated with `wingetcreate update solrevdev.ytx --version X.Y.Z --urls <zip url> --submit`,
  with the token in the `WINGET_CREATE_GITHUB_TOKEN` environment variable (the `--token` flag can
  end up in logs), or with a community action such as winget-releaser.
- **Secret needed:** a personal access token that can fork `microsoft/winget-pkgs` and open pull
  requests from that fork. `wingetcreate` documents a classic token with the `public_repo` scope.
- Unsigned portable executables are accepted, but SmartScreen may warn on first run.

## Secrets summary

| Channel | Secret | Scope |
|---|---|---|
| GitHub Release assets | none | `GITHUB_TOKEN` with `contents: write` in `native.yml` |
| Homebrew tap push | `HOMEBREW_TAP_TOKEN` (or a GitHub App) | Contents: read and write on `solrevdev/homebrew-tap` only |
| Scoop bucket | none if Excavator runs in the bucket | the bucket's own `GITHUB_TOKEN` |
| winget | `WINGET_TOKEN` | classic PAT, `public_repo`, to fork and open PRs on `microsoft/winget-pkgs` |
| Dispatch `native.yml` from `publish.yml` | none | add `actions: write` to the publishing job |

## Later option: a RID-specific .NET tool

.NET 10 can pack a tool as one package per runtime identifier, with NativeAOT binaries inside and a
framework-dependent `any` fallback. `dotnet tool install` then links straight to the native binary.
This was tested locally (see the investigation write-up) but it breaks installs on the .NET 8 and 9
SDKs, so it belongs in a 2.0 release, if at all.
