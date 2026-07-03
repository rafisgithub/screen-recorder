# Publishing Screen Recorder to the Microsoft Store

The release workflow already produces both artifacts a submission can use:

- **`ScreenRecorder-x64.msix`** — the Store-native package. This is the
  recommended route: Partner Center signs it on ingestion (no certificate
  needed), updates are automatic and differential, and install/uninstall is
  fully managed. Built by `installer/Build-Msix.ps1`; identity setup is
  described in [installer/msix/README.md](../installer/msix/README.md).
- **`ScreenRecorderSetup-x64.msi`** — the classic installer, usable for the
  Store's "EXE or MSI" (bring-your-own-installer) app type as a fallback.
  That route requires the MSI to be **code-signed** with a certificate
  chaining to a trusted CA, and a versioned download URL per release
  (`https://github.com/rafisgithub/screen-recorder/releases/download/v<X.Y.Z>/ScreenRecorderSetup-x64.msi`).

## One-time setup (MSIX route)

1. **Partner Center account** — register at
   https://partner.microsoft.com/dashboard (one-time registration fee;
   individual or company; company accounts go through verification).
2. **Reserve the app name** ("Screen Recorder" may be taken — have
   alternatives ready, e.g. "Screen Recorder Studio").
3. **Copy the product identity** from Product management → Product identity
   into the repository variables `MSIX_IDENTITY_NAME`, `MSIX_PUBLISHER`,
   and `MSIX_PUBLISHER_DISPLAY_NAME` so releases stamp the real identity
   (see installer/msix/README.md).
4. **Create the submission**: upload the `.msix` from the GitHub release,
   justify the `runFullTrust` restricted capability as "classic desktop
   application", and fill in the listing — description, screenshots, age
   rating, and a **privacy policy URL** (required; a simple GitHub Pages
   document stating that recordings stay on the user's machine and nothing
   is transmitted is sufficient).
5. **Licensing declaration**: the package bundles the GPL build of FFmpeg
   (license text installed alongside the DLLs). Note it under the listing's
   additional license terms; the app source itself is MIT and public.

## Per-release flow

1. Bump the version (`Directory.Build.props` + `installer/Package.wxs`),
   update `CHANGELOG.md`, and tag `vX.Y.Z`.
2. The release workflow builds the MSI and MSIX and attaches both to the
   GitHub release.
3. In Partner Center, create a new submission with the new `.msix`.
   Certification re-runs automatically.

## Recommended regardless of route: sign the MSI

Store-side signing only covers the MSIX. Direct downloads of the MSI (the
`install.ps1` one-liner and the releases page) still show "Unknown
publisher" on the UAC prompt and can trip SmartScreen until the MSI is
Authenticode-signed. The practical option is **Azure Trusted Signing**
(low monthly cost, GitHub Actions integration via
`azure/trusted-signing-action`); a classic OV/EV certificate also works.
Add the signing step to `.github/workflows/release.yml` between "Build MSI"
and "Create release" once the account exists.

## Worth considering later

- **winget**: with versioned MSI URLs already in place (ideally signed),
  submitting a manifest to https://github.com/microsoft/winget-pkgs makes
  `winget install` work too.
