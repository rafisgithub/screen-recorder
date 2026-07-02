# MSIX packaging (Microsoft Store)

`../Build-Msix.ps1` stages the win-x64 publish output plus these assets and
packs `installer/bin/ScreenRecorder-x64.msix` with `makeappx`. The package is
intentionally **unsigned** — Partner Center signs it on ingestion.

## Setting the real package identity

`AppxManifest.xml` ships with placeholder identity values. The Store rejects
a package whose identity doesn't match your reserved app, so after reserving
the app name in Partner Center, copy the values from
**Product management → Product identity**:

| Manifest field                  | Partner Center value            |
|---------------------------------|---------------------------------|
| `Identity/@Name`                | Package/Identity/Name           |
| `Identity/@Publisher`           | Package/Identity/Publisher      |
| `Properties/PublisherDisplayName` | Package/Property/PublisherDisplayName |

Either edit `AppxManifest.xml` directly, or set the repository variables
`MSIX_IDENTITY_NAME`, `MSIX_PUBLISHER`, and `MSIX_PUBLISHER_DISPLAY_NAME`
(Settings → Secrets and variables → Actions → Variables) — the release
workflow passes them to `Build-Msix.ps1`, which stamps them at pack time.

## Local smoke test

The unsigned .msix cannot be double-click installed. With Developer Mode
enabled, register the loose layout instead:

```powershell
Add-AppxPackage -Register installer\bin\msix-layout\AppxManifest.xml
```

Full submission walkthrough: [docs/ms-store-submission.md](../../docs/ms-store-submission.md).
