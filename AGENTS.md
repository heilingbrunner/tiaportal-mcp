# Agents Guide

This repository can be used with agentic coding assistants. Follow these guidelines to collaborate safely and efficiently.

## Test Execution Policy

- Offer to run tests, but only run them after explicit user confirmation.
- Tests may require user‑specific environment conditions (e.g., installed TIA Portal, licenses, PLC project assets), so do not assume they will pass in your environment.
- When offering to run tests, clearly state prerequisites and potential side effects.
- If the user declines or does not respond, provide concise instructions for the user to run tests locally instead of running them yourself.

### Standard Commands

```powershell
dotnet test
```

If tests need to write to temporary locations or access external resources, note these requirements up front.

## How To Ask For Confirmation

Use clear, actionable language. For example:

- "I can run `dotnet test` to validate the changes. Some tests require TIA Portal and project assets on this machine. Do you want me to run them now?"
- If approved: proceed and summarize results. If not approved: provide steps the user can run.

## Environment Considerations

- Respect the user's environment constraints (e.g., offline, restricted permissions, licensed software).
- If a command fails due to environment limitations, do not retry destructively; report the exact failure and suggest alternatives.
- Document any meaningful limitations or deviations in the commit message or relevant README, per the Contributor Guidelines.


## Formatting & Encoding

- Preserve existing indentation style (tabs vs. spaces).
- Do not modify file encodings; keep UTF-8 BOM where present.
- Ensure Windows CRLF line endings are retained when editing files.
