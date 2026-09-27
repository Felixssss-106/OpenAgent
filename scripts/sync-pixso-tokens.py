#!/usr/bin/env python3
"""Pull the design variables out of Pixso and make design/tokens.css agree with them.

Pixso is the source of truth for colour, radius and spacing; it says nothing about
type scale, easing, duration or the tool risk levels, so this rewrites only the
declarations Pixso owns and leaves the rest of tokens.css as authored.

    python scripts/sync-pixso-tokens.py            # requires Pixso desktop running

Writes design/pixso-variables.json (the raw pull, so the change is reviewable) and
rewrites the two [data-theme="…"] colour blocks plus --r-* / --s-* in :root.
"""

import json
import re
import urllib.request
from pathlib import Path
from urllib.parse import urlparse

ROOT = Path(__file__).resolve().parent.parent
TOKENS = ROOT / "design" / "tokens.css"
SNAPSHOT = ROOT / "design" / "pixso-variables.json"
MCP_URL = "http://127.0.0.1:3667/mcp"

# Same containment as pixso-mcp.py: the sync target is the local Pixso desktop
# connector, and only the loopback qualifies.
if urlparse(MCP_URL).hostname not in ("127.0.0.1", "localhost", "::1"):
    raise SystemExit("token sync only talks to the loopback connector")

# Pixso variable -> tokens.css custom property. The XAML keys downstream stay as
# they are, so only the values move; "primary" is the same role the code calls
# "accent".
COLOUR_MAP = {
    "bg-canvas": "bg-canvas",
    "bg-surface": "bg-surface",
    "bg-sunken": "bg-sunken",
    "bg-inset": "bg-inset",
    "border-subtle": "border-subtle",
    "border-default": "border-default",
    "border-strong": "border-strong",
    "text-primary": "text-primary",
    "text-secondary": "text-secondary",
    "text-tertiary": "text-tertiary",
    "text-quaternary": "text-quaternary",
    "primary": "accent",
    "primary-hover": "accent-hover",
    "primary-quiet": "accent-quiet",
    "primary-foreground": "on-accent",
    "sidebar": "sidebar",
    "sidebar-accent": "sidebar-accent",
    "tab-highlight": "tab-highlight",
    "icon": "icon",
    "icon-muted": "icon-muted",
    "success": "status-online",
    "success-surface": "status-online-bg",
    "error": "status-error",
    "error-surface": "status-error-bg",
    "warning": "status-pending",
    "warning-surface": "status-pending-bg",
}

# Pixso's radius ramp is coarser than the one tokens.css grew with, but the four
# steps line up by name and meaning, so the mapping is unambiguous.
RADIUS_MAP = {"radius-sm": "r-sm", "radius-m": "r-md", "radius-lg": "r-lg", "radius-pill": "r-full"}

# Deliberately NOT synced: Pixso numbers its spacing scale space-1..8 as
# 4,8,12,16,24,32,48,64 while tokens.css uses s-1..s-16 where s-5=20 and s-8=32.
# Mapping them by name would silently change what existing references mean, so the
# spacing conflict is reported for a human decision instead of rewritten here.
SPACING_NOTE = (
    "Pixso space-1..8 = {pixso} vs tokens.css s-1..s-16 = {css}; "
    "numbering differs, so spacing was NOT synced automatically."
)


def mcp_tool_call(name, arguments):
    headers = {"Content-Type": "application/json", "Accept": "application/json, text/event-stream"}

    def post(payload, session=None):
        req = urllib.request.Request(MCP_URL, data=json.dumps(payload).encode(), method="POST")
        for k, v in headers.items():
            req.add_header(k, v)
        if session:
            req.add_header("mcp-session-id", session)
        with urllib.request.urlopen(req, timeout=180) as resp:
            return resp.headers.get("mcp-session-id"), resp.read().decode("utf-8", "replace")

    session, _ = post(
        {
            "jsonrpc": "2.0",
            "id": 1,
            "method": "initialize",
            "params": {
                "protocolVersion": "2024-11-05",
                "capabilities": {},
                "clientInfo": {"name": "openagent-token-sync", "version": "1"},
            },
        }
    )
    post({"jsonrpc": "2.0", "method": "notifications/initialized"}, session)
    _, body = post(
        {"jsonrpc": "2.0", "id": 2, "method": "tools/call", "params": {"name": name, "arguments": arguments}},
        session,
    )
    match = re.search(r"data: (\{.*\})\s*\Z", body, re.S)
    payload = json.loads((match.group(1) if match else body))
    text = payload["result"]["content"][0]["text"]
    return json.loads(text)


def replace_theme_block(css, selector, declarations):
    """Rebuild one [data-theme="…"] block: Pixso's declarations first, then whatever
    the file already had that Pixso does not own (risk ramp, --status-offline, …).

    All three parts are captured explicitly — slicing around the match previously
    dropped the closing brace and nested the next block inside this one.
    """
    head = f"{selector} {{"
    # Anchor at line start: '[data-theme="light"] {' is also a suffix of the
    # palette selectors, and an unanchored index would rewrite the wrong block.
    found = re.search(r"(?m)^" + re.escape(head), css)
    if not found:
        raise ValueError(f"block not found: {selector}")
    start = found.start()
    end = css.index("\n}", start)
    body = css[start + len(head) : end]

    written = {line.split(":")[0].strip() for line in declarations}
    kept = []
    for declaration in body.split(";"):
        declaration = declaration.strip()
        if not declaration.startswith("--"):
            continue
        if declaration.split(":")[0].strip() in written:
            continue
        kept.append(re.sub(r"\s+", " ", declaration))

    lines = declarations + kept
    return css[:start] + head + "\n  " + ";\n  ".join(lines) + ";\n" + css[end + 1 :]


def replace_radius_line(css, numbers):
    """Rewrite the packed --r-* line wholesale; per-declaration substitution would
    leave orphan values behind because several declarations share one line."""
    # Capture the indent only; the whole declaration run is replaced.
    pattern = r"(?m)^(\s*)--r-xs:[^\n]*--r-full:[^;\n]*;"
    values = {
        "r-sm": int(numbers["radius-sm"]["value"]),
        "r-md": int(numbers["radius-m"]["value"]),
        "r-lg": int(numbers["radius-lg"]["value"]),
        "r-full": int(numbers["radius-pill"]["value"]),
    }
    line = (
        f"--r-xs: 6px; --r-sm: {values['r-sm']}px; --r-md: {values['r-md']}px; "
        f"--r-lg: {values['r-lg']}px; --r-xl: {values['r-lg']}px; --r-full: {values['r-full']}px;"
    )
    new, count = re.subn(pattern, lambda m: m.group(1) + line, css, count=1)
    if count == 0:
        raise ValueError("--r-* line not found in :root")
    return new


def main():
    data = mcp_tool_call("read_variables", {})
    variables = data["variables"]
    SNAPSHOT.write_text(json.dumps(data, ensure_ascii=False, indent=2), encoding="utf-8")
    print(f"snapshot -> {SNAPSHOT.relative_to(ROOT)} ({len(variables)} variables)")

    colours = {k: v for k, v in variables.items() if v.get("type") == "color"}
    numbers = {k: v for k, v in variables.items() if v.get("type") == "number"}

    def colour_lines(mode):
        out = []
        for pixso, token in COLOUR_MAP.items():
            value = colours[pixso]["values"][mode]
            out.append(f"--{token}: {value}")
        return out

    number_lines = []

    print(
        SPACING_NOTE.format(
            pixso=[numbers[f"space-{n}"]["value"] for n in range(1, 9)],
            css="s-1..s-16",
        )
    )

    css = TOKENS.read_text(encoding="utf-8")

    for mode, selector in (("Light", '[data-theme="light"]'), ("Dark", '[data-theme="dark"]')):
        css = replace_theme_block(css, selector, colour_lines(mode))

    css = replace_radius_line(css, numbers)

    TOKENS.write_text(css, encoding="utf-8")
    print(f"rewrote {TOKENS.relative_to(ROOT)}: {len(colour_lines('Light'))} colours x2 themes + radius ramp")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
