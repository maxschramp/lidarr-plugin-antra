"""
E2E baseline setup for the dev Lidarr instance (idempotent):
  1. root folder for the dev library
  2. enable the Antra protocol in the default delay profile
  3. Antra indexer (tests first, then adds if missing)
  4. Antra download client (tests first, then adds if missing)
Run with the both dev Lidarr and the bridge up.
"""
import sys

import requests

LIDARR = "http://127.0.0.1:8688"
KEY = {"X-Api-Key": "<dev-api-key>"}
BRIDGE = "http://127.0.0.1:8890"
STAGING = "<antra-lidarr>\\staging"
LIBRARY = "<antra-lidarr>\\dev\\library"


def get(path):
    resp = requests.get(LIDARR + path, headers=KEY, timeout=30)
    resp.raise_for_status()
    return resp.json()


def send(method, path, payload):
    resp = requests.request(method, LIDARR + path, headers=KEY, json=payload, timeout=90)
    return resp


def main():
    # 1. root folder
    folders = get("/api/v1/rootfolder")
    if not any(f["path"].endswith("dev\\library") for f in folders):
        r = send("POST", "/api/v1/rootfolder", {"name": "Dev", "path": LIBRARY,
                                                "defaultMetadataProfileId": 1,
                                                "defaultQualityProfileId": 2,
                                                "defaultMonitorOption": "none",
                                                "defaultNewItemMonitorOption": "none"})
        print("rootfolder add:", r.status_code, r.text[:200])
    else:
        print("rootfolder: exists")

    # 2. delay profile: allow the Antra protocol
    for profile in get("/api/v1/delayprofile"):
        changed = False
        for item in profile.get("items", []):
            if item.get("protocol") == "AntraDownloadProtocol" and not item.get("allowed"):
                item["allowed"] = True
                changed = True
        if changed:
            r = send("PUT", f"/api/v1/delayprofile/{profile['id']}", profile)
            print("delayprofile update:", r.status_code, r.text[:120])
        # else unchanged

    # 3. indexer
    indexer_payload = {
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
            {"name": "timeoutSeconds", "value": 240},
            {"name": "earlyReleaseLimit", "value": None},
        ],
    }
    if not any(i["name"] == "Antra" for i in get("/api/v1/indexer")):
        t = send("POST", "/api/v1/indexer/test", indexer_payload)
        print("indexer test:", t.status_code, t.text[:300])
        if t.status_code == 200:
            r = send("POST", "/api/v1/indexer", indexer_payload)
            print("indexer add:", r.status_code, r.text[:200])
        else:
            print("indexer NOT added (test failed)")
    else:
        print("indexer: exists")

    # 4. download client
    client_payload = {
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
            {"name": "timeoutSeconds", "value": 120},
        ],
    }
    if not any(c["name"] == "Antra" for c in get("/api/v1/downloadclient")):
        t = send("POST", "/api/v1/downloadclient/test", client_payload)
        print("downloadclient test:", t.status_code, t.text[:400])
        if t.status_code == 200:
            r = send("POST", "/api/v1/downloadclient", client_payload)
            print("downloadclient add:", r.status_code, r.text[:200])
        else:
            print("downloadclient NOT added (test failed)")
    else:
        print("downloadclient: exists")

    print("\nfinal state:")
    print(" indexers:", [(i["name"], i["protocol"]) for i in get("/api/v1/indexer")])
    print(" clients :", [(c["name"], c["protocol"], c["enable"]) for c in get("/api/v1/downloadclient")])
    sys.exit(0)


if __name__ == "__main__":
    main()
