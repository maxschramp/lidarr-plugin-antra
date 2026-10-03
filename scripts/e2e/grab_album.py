"""
E2E: add the test artist (if missing), monitor the test albums, fire album
searches. Prints the album ids so the watcher can follow them.
"""
import sys
import time

import requests

LIDARR = "http://127.0.0.1:8688"
KEY = {"X-Api-Key": "<dev-api-key>"}
LIBRARY = "<antra-lidarr>\\dev\\library"

TARGETS = [
    ("Daft Punk", "Random Access Memories", 2013),
    ("Susumu Yokota", "Sakura", 1999),
]
# NOTE: foreignArtistId is resolved via lookup at runtime.


def get(path):
    resp = requests.get(LIDARR + path, headers=KEY, timeout=30)
    resp.raise_for_status()
    return resp.json()


def send(method, path, payload=None, params=None):
    resp = requests.request(method, LIDARR + path, headers=KEY, json=payload, params=params, timeout=120)
    return resp


def ensure_artist(artist_name):
    existing = [a for a in get("/api/v1/artist") if a["artistName"].lower() == artist_name.lower()]
    if existing:
        return existing[0]

    results = get(f"/api/v1/artist/lookup?term={artist_name.replace(' ', '+')}")
    match = next((a for a in results if a["artistName"].lower() == artist_name.lower()), None)
    if not match:
        raise RuntimeError(f"artist lookup found nothing for {artist_name}")

    payload = {
        "foreignArtistId": match["foreignArtistId"],
        "artistName": match["artistName"],
        "qualityProfileId": 2,   # Lossless
        "metadataProfileId": 1,  # Standard
        "rootFolderPath": LIBRARY,
        "monitored": True,
        "monitorNewItems": "none",
        "addOptions": {"monitor": "none", "searchForMissingAlbums": False},
        "tags": [],
    }
    r = send("POST", "/api/v1/artist", payload)
    print("artist add:", r.status_code, r.text[:200])
    r.raise_for_status()
    return r.json()


def find_album(artist_id, title, year):
    wanted = title.lower()
    deadline = time.time() + 120
    while time.time() < deadline:
        albums = get(f"/api/v1/album?artistId={artist_id}")
        candidates = [a for a in albums if a["title"].lower() == wanted]
        if candidates:
            break
        time.sleep(3)
    else:
        raise RuntimeError(f"album '{title}' not found for artist {artist_id} (timed out)")
    if year:
        exact = [a for a in candidates if (a.get("releaseDate") or "").startswith(str(year))]
        if exact:
            return exact[0]
    return candidates[0]


def main():
    seen_artists = {}
    for artist_name, title, year in TARGETS:
        if artist_name not in seen_artists:
            seen_artists[artist_name] = ensure_artist(artist_name)
            print(f"artist '{artist_name}': id={seen_artists[artist_name]['id']}")
        artist = seen_artists[artist_name]
        album = find_album(artist["id"], title, year)
        if not album["monitored"]:
            album["monitored"] = True
            r = send("PUT", f"/api/v1/album/{album['id']}", album)
            print("album monitored:", r.status_code)
        print(f"album: id={album['id']} '{album['title']}' monitored={album['monitored']}")
        r = send("POST", "/api/v1/command", {"name": "AlbumSearch", "albumIds": [album["id"]]})
        print("album search command:", r.status_code, r.text[:120].replace("\n", " "))


if __name__ == "__main__":
    main()
