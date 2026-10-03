"""Bridge-down failure drill: run the same Test calls Lidarr's UI uses."""
import requests

K = {"X-Api-Key": "<dev-api-key>"}
B = "http://127.0.0.1:8688"

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
        {"name": "baseUrl", "value": "http://127.0.0.1:8890"},
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
        {"name": "bridgeUrl", "value": "http://127.0.0.1:8890"},
        {"name": "bridgeApiToken", "value": ""},
        {"name": "stagingPath", "value": "<antra-lidarr>\\staging"},
        {"name": "host", "value": "localhost"},
        {"name": "format", "value": "flac"},
        {"name": "sources", "value": "auto"},
        {"name": "timeoutSeconds", "value": 30},
    ],
}

def existing_id(kind, name):
    rows = requests.get(f"{B}/api/v1/{kind}", headers=K, timeout=30).json()
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
        r = requests.post(B + path, headers=K, json=payload, timeout=90)
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
