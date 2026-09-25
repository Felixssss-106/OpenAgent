# Security Policy

## Supported versions

| Version | Supported |
|---|---|
| 0.1.x (current development) | yes |

## Reporting a vulnerability

**Please do not open a public GitHub issue for security vulnerabilities.**

Report privately through GitHub Security Advisories:
<https://github.com/openagent/openagent/security/advisories/new>

If that is not possible, open a draft advisory or contact a maintainer directly.
Please include:

- affected version / commit
- reproduction steps
- impact (what an attacker can achieve)
- any suggested fix

We aim to acknowledge reports within 7 days and to ship a fix or a mitigation
plan within 30 days, depending on severity.

## Scope

In scope:

- pairing and device authentication
- the encrypted device channel and session key handling
- permission and approval bypasses (a tool executing without required approval)
- path validation bypasses in file tools
- shell / command classification bypasses
- credential storage (API keys, device private keys)
- Relay behaviour that leaks plaintext it should not see

Out of scope:

- vulnerabilities in third-party Agent CLIs integrated as optional providers
- issues that require the attacker to already control the user's account
- theoretic issues without a demonstrated path to impact

## Security principles this project follows

- Least privilege: the default permission mode is read-only / ask-before-actions.
- No insecure default: no open inbound port is required; no plaintext public HTTP.
- Secrets never live in SQLite, JSON, or logs. They go through OS secure storage.
- No telemetry by default.
- Do not invent cryptography. X25519 / Ed25519 / AES-GCM / SHA-256 / HKDF only,
  from mature implementations.
