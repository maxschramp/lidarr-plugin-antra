# E2E evidence: dev instance (2026-10-03)

Environment: Lidarr 3.1.6.5078 (nightly, plugins) native on the dev machine,
port 8688, data dir `dev/config`, library `dev/library`. Bridge 0.1.0 on
127.0.0.1:8890 driving the Antra engine (upstream 07aeef1) in-process.
Plugin built local (`scripts/build-local.sh`), installed at
`dev/config/plugins/maxschramp/Lidarr.Plugin.Antra/`.

## Album 1: Daft Punk - Random Access Memories (2013), mainstream

1. Search: `AlbumSearch` -> indexer Antra -> bridge `/resolve` (match +
   availability probe). Release: "Daft Punk - Random Access Memories (2013)
   [FLAC] [Antra]".
2. Grab: "Report sent to Antra from indexer Antra" ->
   `AntraProxy|Queued Antra bridge job 645a698c...`.
3. Bridge job `645a698c` completed 13/13 (files staged by the bridge proof
   run; resume/skip path).
4. Import: `downloadImported` + 13 x `trackFileImported`.
   File check: 24-bit/44.1k FLAC, artist/album/title/date/genre correct.
5. Raw logs: `evidence/bridge-proof-full.log` (fresh full-album download,
   13/13, sources qobuz_mirror + apple, tag check pass),
   `dev/config/logs/lidarr.txt`.

## Album 2: Susumu Yokota - Sakura (1999/2000), obscure

1. Search + grab: same chain; bridge job `27e06d48...`.
2. Bridge job completed 12/12: 4 resumed, 8 fresh via tidal_mirror, zero
   failures (this is the end-to-end fresh download through the
   Lidarr-triggered job).
3. Import: `downloadImported` + 12 x `trackFileImported`.
   File check: 16-bit/44.1k FLAC, correct tags.

Lidarr history totals across both: 2 downloadImported, 25 trackFileImported,
0 failures. Queue drains cleanly after import.

## Failure modes verified

- Bridge down: indexer Test and download client Test both return clean 400
  validation errors with the connection error; Lidarr stays healthy; both
  green again after restart (`scripts/e2e/failure_drill.py`).
- Unmatchable album: `/resolve` returns ok=false with a candidate report; the
  search yields no Antra release (no wrong-edition grabs) - proven in Phase 0
  and by the earlier 22-track failure (fixed via multi-tracklist support).
- Cancel: job cancelled mid-download, clean `cancelled` status.
- Rate limit: surfaced as `rate_limited` job status + cooldown in /health
  (mechanism in place; not hit during dev - free tier stayed under limits).
