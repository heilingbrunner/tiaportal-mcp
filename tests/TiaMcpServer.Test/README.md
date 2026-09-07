# TiaMcpServer.Test

MSTest project verifying portal connectivity, project handling, devices, and MCP server behavior.

## Environment Prerequisites

- .NET Framework 4.8 installed
- Siemens TIA Portal V21 installed and running
- User in Windows group "Siemens TIA Openness"
- Env var `TiaPortalLocation` set to `C:\\Program Files\\Siemens\\Automation\\Portal V21`

## Test Assets
- `assets/TestProject1.zap20` – archived local project used in tests. Retrieve it with TIA Portal V21 (which upgrades it to `.ap21`) and point `Settings.cs` at the retrieved project.
- A multi-user local session (`.als21`) – create this manually for session tests and point `Settings.cs` at it.

See `Settings.cs` for configuration options such as project paths and timeouts.

## Test Execution Policy

- Offer to run tests, but only execute them after explicit user confirmation. See root `AGENTS.md` for details.
