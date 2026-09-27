#!/usr/bin/env python3
"""Minimal MCP client for the local Pixso desktop endpoint.

The Pixso connector is registered in settings.json but is not loaded as a tool
in this session, so this script speaks the streamable-HTTP MCP protocol directly:
initialize -> notifications/initialized -> tools/call.

    python scripts/pixso-mcp.py <tool_name> '<json-args>'
    python scripts/pixso-mcp.py --list
"""

import json
import re
import sys
import urllib.request
from urllib.parse import urlparse

URL = "http://127.0.0.1:3667/mcp"

# This client exists to reach the local Pixso desktop connector and nothing else:
# a loopback host is enforced so a re-pointed URL cannot turn the script into a
# relay for requests to somewhere it was never meant to go.
if urlparse(URL).hostname not in ("127.0.0.1", "localhost", "::1"):
    raise SystemExit("pixso-mcp only talks to the loopback connector")

HEADERS = {
    "Content-Type": "application/json",
    "Accept": "application/json, text/event-stream",
}

session_id = None


def post(payload, want_reply=True):
    req = urllib.request.Request(URL, data=json.dumps(payload).encode(), method="POST")
    for k, v in HEADERS.items():
        req.add_header(k, v)
    if session_id:
        req.add_header("mcp-session-id", session_id)
    with urllib.request.urlopen(req, timeout=180) as resp:
        sid = resp.headers.get("mcp-session-id")
        body = resp.read().decode("utf-8", "replace")
    return sid, body


def parse(body):
    match = re.search(r"data: (\{.*\})\s*\Z", body, re.S)
    return json.loads((match or [None, body])[1] if match else body)


def rpc(method, params=None, rid=1):
    payload = {"jsonrpc": "2.0", "id": rid, "method": method}
    if params is not None:
        payload["params"] = params
    return parse(post(payload)[1])


def main():
    global session_id

    sid, _ = post(
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "openagent-design", "version": "1"},
            },
        }
    )
    session_id = sid
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, want_reply=False)

    if len(sys.argv) < 2 or sys.argv[1] == "--list":
        tools = rpc("tools/list", {}).get("result", {}).get("tools", [])
        print(f"{len(tools)} tools")
        for tool in tools:
            print(" -", tool["name"])
        return 0

    name = sys.argv[1]
    args = json.loads(sys.argv[2]) if len(sys.argv) > 2 else {}
    result = rpc("tools/call", {"name": name, "arguments": args}, rid=2)
    print(json.dumps(result, ensure_ascii=False, indent=2)[:12000])
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
