# lidarr-plugin-antra

Lidarr plugin that adds **Antra** as a lossless source: an indexer plus a
download client that drive the [antra-bridge](../antra-bridge) service.

- Indexer: on album search, calls the bridge `POST /resolve` with the album's
  identity and its release tracklists; a validated match becomes one synthetic
  release (`Antra - FLAC`, protocol `AntraDownloadProtocol`). No match, no
  release.
- Download client: on grab, `POST /download` on the bridge; queue items track
  the bridge job (`/jobs?origin=lidarr`); completed jobs expose the staging
  folder for Lidarr's import; remove/cancel maps to `DELETE /jobs/{id}`.
- Settings: bridge URL + optional token, staging path, host (path-mapping key),
  preferred format, sources, timeout.

## Build (local)

```bash
bash scripts/build-local.sh       # patches vendored AssemblyVersion like CI, then builds
```

Output: `_plugins/net8.0/Lidarr.Plugin.Antra/`. Install by copying
`Lidarr.Plugin.Antra.dll` + `.pdb` + `.deps.json` into
`<Lidarr config>/plugins/maxschramp/Lidarr.Plugin.Antra/` and restarting.

CI (`.github/workflows/build.yml`) builds the same target and attaches
`Lidarr.Plugin.Antra.net8.0.zip` to a draft GitHub release; Lidarr installs
plugins from the repo URL.

Requires Lidarr nightly/plugins branch (`ext/Lidarr` submodule pins the
`plugins` branch). See `../antra-bridge/docs/RUNBOOK.md` for the full setup.
