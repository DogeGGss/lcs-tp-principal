"""
Chequea el Project (v2) de GitHub, detecta qué historias de usuario
cambiaron de estado (campo "Status") desde la última corrida, y agrega
una fila a un CSV con cuánto tiempo estuvieron en el estado anterior.

Pensado para correr periódicamente vía GitHub Actions (ver
.github/workflows/status-time-tracker.yml). El CSV queda versionado
en el propio repo (.github/project_status_log.csv) y se puede abrir
directo con Excel / Google Sheets.
"""

import os
import csv
import json
from datetime import datetime, timezone

import requests

GITHUB_TOKEN = os.environ["PROJECT_PAT"]

# --- CONFIGURÁ ESTOS 3 VALORES ---
OWNER = "DogeGGss"
OWNER_TYPE = "user"
PROJECT_NUMBER = 2               # el número que aparece en la URL del proyecto
                                  # (https://github.com/orgs/<org>/projects/N)
STATUS_FIELD_NAME = "Status"     # el nombre exacto del campo en el board
# ----------------------------------

SNAPSHOT_PATH = ".github/project_status_snapshot.json"
LOG_CSV_PATH = ".github/project_status_log.csv"
CSV_HEADERS = [
    "fecha_utc",
    "us_numero",
    "us_titulo",
    "estado_anterior",
    "estado_nuevo",
    "tiempo_en_estado_anterior",
    "tiempo_en_estado_anterior_segundos",
    "url",
]
GRAPHQL_URL = "https://api.github.com/graphql"


def gql(query, variables):
    r = requests.post(
        GRAPHQL_URL,
        json={"query": query, "variables": variables},
        headers={"Authorization": f"Bearer {GITHUB_TOKEN}"},
        timeout=30,
    )
    r.raise_for_status()
    data = r.json()
    if "errors" in data:
        raise RuntimeError(data["errors"])
    return data["data"]


def fetch_items():
    field = "organization" if OWNER_TYPE == "organization" else "user"
    query = f"""
    query($login: String!, $number: Int!, $cursor: String) {{
      {field}(login: $login) {{
        projectV2(number: $number) {{
          items(first: 100, after: $cursor) {{
            pageInfo {{ hasNextPage endCursor }}
            nodes {{
              id
              content {{
                ... on Issue {{
                  number
                  title
                  url
                }}
              }}
              fieldValueByName(name: "{STATUS_FIELD_NAME}") {{
                ... on ProjectV2ItemFieldSingleSelectValue {{
                  name
                }}
              }}
            }}
          }}
        }}
      }}
    }}
    """
    items = []
    cursor = None
    while True:
        data = gql(query, {"login": OWNER, "number": PROJECT_NUMBER, "cursor": cursor})
        conn = data[field]["projectV2"]["items"]
        items.extend(conn["nodes"])
        if conn["pageInfo"]["hasNextPage"]:
            cursor = conn["pageInfo"]["endCursor"]
        else:
            break
    return items


def load_snapshot():
    if os.path.exists(SNAPSHOT_PATH):
        with open(SNAPSHOT_PATH, encoding="utf-8") as f:
            return json.load(f)
    return {}


def save_snapshot(snapshot):
    os.makedirs(os.path.dirname(SNAPSHOT_PATH), exist_ok=True)
    with open(SNAPSHOT_PATH, "w", encoding="utf-8") as f:
        json.dump(snapshot, f, indent=2, ensure_ascii=False)


def format_duration(seconds):
    days, rem = divmod(int(seconds), 86400)
    hours, rem = divmod(rem, 3600)
    minutes, _ = divmod(rem, 60)
    parts = []
    if days:
        parts.append(f"{days}d")
    if hours:
        parts.append(f"{hours}h")
    if minutes or not parts:
        parts.append(f"{minutes}m")
    return " ".join(parts)


def append_to_csv(item, old_status, new_status, elapsed_seconds, now):
    issue = item.get("content") or {}
    row = [
        now.isoformat(),
        issue.get("number", ""),
        issue.get("title", "Sin título"),
        old_status,
        new_status,
        format_duration(elapsed_seconds),
        int(elapsed_seconds),
        issue.get("url", ""),
    ]

    os.makedirs(os.path.dirname(LOG_CSV_PATH), exist_ok=True)
    file_exists = os.path.exists(LOG_CSV_PATH)
    with open(LOG_CSV_PATH, "a", newline="", encoding="utf-8") as f:
        writer = csv.writer(f)
        if not file_exists:
            writer.writerow(CSV_HEADERS)
        writer.writerow(row)


def main():
    items = fetch_items()
    snapshot = load_snapshot()
    now = datetime.now(timezone.utc)
    changed = False

    for item in items:
        item_id = item["id"]
        status_field = item.get("fieldValueByName")
        current_status = status_field["name"] if status_field else "Sin estado"

        prev = snapshot.get(item_id)
        if prev is None:
            # Primera vez que vemos este ítem: solo lo registramos,
            # no escribimos fila en el CSV (evita loguear todo el
            # backlog existente como si hubiera "cambiado" en este momento).
            snapshot[item_id] = {"status": current_status, "since": now.isoformat()}
            changed = True
            continue

        if prev["status"] != current_status:
            since = datetime.fromisoformat(prev["since"])
            elapsed = (now - since).total_seconds()
            append_to_csv(item, prev["status"], current_status, elapsed, now)
            snapshot[item_id] = {"status": current_status, "since": now.isoformat()}
            changed = True

    if changed:
        save_snapshot(snapshot)
        print("Snapshot / CSV actualizados.")
    else:
        print("Sin cambios de estado.")


if __name__ == "__main__":
    main()
