# Security Policy

## Supported Versions

Security updates are provided for the latest minor release series only.

| Version | Supported |
|---------|-----------|
| 0.1.x   | ✓         |

## Threat Model

`nwd-mcp` runs on `127.0.0.1` only. The attack surface is:

- Local processes that can read the discovery files (`%LOCALAPPDATA%\Bimwright\nwd-mcp\navis-2022-*.json`..`navis-2027-*.json`)
- Local processes that can connect to the local TCP port
- Code executed via `nwd_send_code` or materialized by the ToolBaker engine

## Mitigations in place

### Per-session token authentication
- Each Navisworks session generates a 32-byte cryptographic random token.
- Token is persisted alongside port info in the discovery file.
- Every request must include the valid token — otherwise rejected (UNAUTHORIZED).
- Constant-time string comparison prevents timing attacks.

### Input validation
- `--target` validated: one of `2022`, `2023`, `2024`, `2025`, `2026`, `2027` (4-digit calendar years). Legacy R-codes are rejected.
- Handler parameters validated via standard types before dispatch.
- Local TCP line size limit: 1 MiB per message.

### Secret masking
- `SecretMasker` redacts API keys, Bearer tokens, passwords, and authentication tokens in log output.
- `ErrorSanitizer` strips Windows/UNC absolute paths from errors sent to the model — filenames preserved.

### Network binding
- TCP listener: `127.0.0.1` only (not `0.0.0.0`).
- Any non-localhost plugin bind requires explicit environment configuration.

### Dynamic code paths (`nwd_send_code`, ToolBaker)
- `nwd_send_code` is disabled by default. It requires both a server-side flag (`--enable-send-code` or `BIMWRIGHT_NWD_ENABLE_SEND_CODE=1`) AND a plug-in environment variable (`BIMWRIGHT_NWD_PLUGIN_ENABLE_SEND_CODE=1`) to execute.
- Use `--read-only` or `--disable-toolbaker` when a host profile should not expose dynamic-code execution.
- ToolBaker bakes require user approval per tool + operate under the host Navisworks process trust boundary.

## Reporting a vulnerability

**Please do not open a public GitHub issue for security-sensitive reports.**

Use one of these private channels:

1. **GitHub private vulnerability report** — go to the Security tab of the repository and submit a new advisory draft. This is the preferred path.
2. **Email the maintainer** — contact via the address on the commit history.

Include:
- Version (server + plugin) and Navisworks year.
- Reproduction steps.
- Impact assessment (local vs remote, auth required, user interaction).

Do not publish proof-of-concept exploits in public channels until a fix has shipped.

## Disclosure timeline

- Acknowledgement within 72 hours of report.
- Assessment + fix target within 14 days for high-severity issues (auth bypass, RCE).
- Coordinated disclosure via GitHub Security Advisory with CVE assignment where applicable.
