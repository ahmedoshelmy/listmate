#!/usr/bin/env bash
# End-to-end check against a running stack (docker compose up -d --build --wait):
# two users, one shared list, an item added by A and read by B, all through the nginx front door.
set -euo pipefail
BASE="${1:-http://localhost:8080}"
PASSWORD="Smoke-test-1"
run="$(date +%s)"

json() { python3 -c "import json,sys; print(json.load(sys.stdin)$1)"; }
post() { curl -fsS -X POST "$BASE$1" -H 'Content-Type: application/json' ${3:+-H "Authorization: Bearer $3"} -d "$2"; }

API="${2:-http://localhost:5180}"
curl -fsS "$BASE/healthz" >/dev/null && echo "web healthy"
curl -fsS "$API/health/live" >/dev/null && curl -fsS "$API/health/ready" >/dev/null && echo "api live + ready"
[ "$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/api/lists")" = 401 ] && echo "anonymous request gets 401"

token_for() {
  local email="$1@example.com"
  post /api/auth/register "{\"email\":\"$email\",\"password\":\"$PASSWORD\"}" >/dev/null
  post /api/auth/login "{\"email\":\"$email\",\"password\":\"$PASSWORD\"}" | json "['accessToken']"
}

A=$(token_for "smoke-a-$run")
B=$(token_for "smoke-b-$run")
echo "registered two users"

LIST=$(post /api/lists '{"name":"Weekly groceries"}' "$A")
LIST_ID=$(echo "$LIST" | json "['id']")
CODE=$(echo "$LIST" | json "['inviteCode']")
post /api/lists/join "{\"inviteCode\":\"$CODE\"}" "$B" >/dev/null
echo "B joined list with invite code $CODE"

ITEM_ID=$(python3 -c "import uuid; print(uuid.uuid4())")
MUTATION_ID=$(python3 -c "import uuid; print(uuid.uuid4())")
BODY="{\"mutations\":[{\"mutationId\":\"$MUTATION_ID\",\"type\":\"add\",\"listId\":\"$LIST_ID\",\"itemId\":\"$ITEM_ID\",\"text\":\"2x milk\"}]}"
post /api/sync "$BODY" "$A" | json "['results'][0]['status']" | grep -qx applied
post /api/sync "$BODY" "$A" | json "['results'][0]['status']" | grep -qx duplicate
echo "A added '2x milk' (replay was a duplicate)"

SEEN=$(curl -fsS "$BASE/api/lists/$LIST_ID" -H "Authorization: Bearer $B" | json "['items'][0]")
echo "B sees: $SEEN"
echo "$SEEN" | grep -q "'quantity': 2"

post "/api/lists/$LIST_ID/leave" "" "$B" >/dev/null
[ "$(curl -sS -o /dev/null -w '%{http_code}' "$BASE/api/lists/$LIST_ID" -H "Authorization: Bearer $B")" = 404 ]
MEMBERS=$(curl -fsS "$BASE/api/lists/$LIST_ID/members" -H "Authorization: Bearer $A" | json "")
echo "B left the list and lost access; members now: $MEMBERS"
echo "SMOKE PASS"
