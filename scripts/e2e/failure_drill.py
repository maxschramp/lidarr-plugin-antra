"""
Bridge-down failure drill: run the same Test calls Lidarr's UI uses for the
Antra indexer and the Antra download client, and print the validation
messages. Expected states:

  - bridge up:   both HTTP 200 with an empty body
  - bridge down: both HTTP 400 with a clean "not reachable" error message

Configuration via environment variables:
  LIDARR_URL           Lidarr base URL     (default http://127.0.0.1:8688)
  LIDARR_API_KEY       Lidarr API key      (required)
  BRIDGE_URL           bridge base URL     (default http://127.0.0.1:8890)
  ANTRA_LIDARR_ROOT    dev checkout root   (default C:\\antra-lidarr)
  ANTRA_STAGING        staging dir as Lidarr sees it (default <root>\\staging)
"""
import os
import sys

import requests

ROOT = os.environ.get("ANTRA_LIDARR_ROOT", "C:\\antra-lidarr")
LIDARR = os.environ.get("LIDARR_URL", "http://127.0.0.1:8688")
API_KEY = os.environ.get("LIDARR_API_KEY", "")
BRIDGE = os.environ.get("BRIDGE_URL", "http://127.0.0.1:8890")
STAGING = os.environ.get("ANTRA_STAGING", ROOT + "\\staging")

if not API_KEY:
    sys.exit("set LIDARR_API_KEY (the dev instance's API key)")

KEY = {"X-Api-Key": API_KEY}

INDEXER = {
    "name": "Antra",
    "implementation": "Antra",
    "implementationName": "Antra",
    "configContract": "AntraIndexerSettings",
    "protocol": "AntraDownloadProtocol",
    "priority": 25,
    "enableRss": False,
    "enableAutomaticSearch": True,
    "enableInteractiveSearch": True,
    "fields": [
        {"name": "baseUrl", "value": BRIDGE},
        {"name": "bridgeApiToken", "value": ""},
        {"name": "timeoutSeconds", "value": 30},
        {"name": "earlyReleaseLimit", "value": None},
    ],
}

DOWNLOADCLIENT = {
    "name": "Antra",
    "implementation": "Antra",
    "implementationName": "Antra",
    "configContract": "AntraSettings",
    "protocol": "AntraDownloadProtocol",
    "priority": 1,
    "enable": True,
    "removeCompletedDownloads": False,
    "removeFailedDownloads": False,
    "fields": [
        {"name": "bridgeUrl", "value": BRIDGE},
        {"name": "bridgeApiToken", "value": ""},
        {"name": "stagingPath", "value": STAGING},
        {"name": "host", "value": "localhost"},
        {"name": "format", "value": "flac"},
        {"name": "sources", "value": "auto"},
        {"name": "timeoutSeconds", "value": 30},
    ],
}


def existing_id(kind, name):
    rows = requests.get(f"{LIDARR}/api/v1/{kind}", headers=KEY, timeout=30).json()
    for row in rows:
        if row.get("name") == name and row.get("implementation") == "Antra":
            return row.get("id")
    return None


for label, kind, path, payload in [
    ("indexer", "indexer", "/api/v1/indexer/test", INDEXER),
    ("downloadclient", "downloadclient", "/api/v1/downloadclient/test", DOWNLOADCLIENT),
]:
    pid = existing_id(kind, "Antra")
    if pid:
        payload["id"] = pid
    try:
        r = requests.post(LIDARR + path, headers=KEY, json=payload, timeout=90)
        print(f"{label}: HTTP {r.status_code}")
        try:
            body = r.json()
            if isinstance(body, list):
                for e in body:
                    print("   errorMessage:", e.get("errorMessage"))
            else:
                print("   body:", str(body)[:200])
        except ValueError:
            print("   body:", r.text[:200])
    except requests.exceptions.RequestException as exc:
        print(f"{label}: request raised {exc}")
