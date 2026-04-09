# Steam Readiness Base

This repo now has a Windows-first Steam publishing baseline for the current playable client in `src/Sectorbound.Client`.

## What Valve Reviews

Valve's current Steamworks documentation says the following are part of review:

- Your store page should only describe features and content that will be available at launch.
- Your product must start correctly on every operating system you list as supported on the store page.
- Any supported features selected on the store page must already exist in the build you submit.
- Before review, you must complete the Content Survey, including mature content disclosures and any generative AI disclosures that apply.
- If you launch in Early Access, you must complete the Early Access Q&A and keep it accurate and current.

Official docs:

- [Review Process](https://partner.steamgames.com/doc/store/Review_Process)
- [Content Survey](https://partner.steamgames.com/doc/gettingstarted/contentsurvey?l=english)
- [Early Access](https://partner.steamgames.com/doc/store/earlyaccess)
- [Common Redistributables](https://partner.steamgames.com/doc/features/common_redist)

## Current Repo Decisions

- Steam target platform is currently `Windows` only.
- The client should be published from `src/Sectorbound.Client`, not from the console prototype.
- `scripts/publish-steam.ps1` produces a self-contained `win-x64` build under `artifacts/steam/win-x64`.
- Saves and logs live under `%LOCALAPPDATA%\Sectorbound` so installed builds are not tied to the repo folder.
- Legacy `%LOCALAPPDATA%\SectorboundPrototype` data is migrated forward automatically when possible.

## Publish Workflow

Run this from the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\publish-steam.ps1
```

The default output is a self-contained Windows build so the game does not depend on a preinstalled .NET runtime on the player's machine. Steam also supports Common Redistributables, including `.NET`, but self-contained publishing gives us a simpler default review build for now.

## Store Page Guardrails

- Do not mark Linux or macOS support until we have real tested builds for those platforms.
- Do not promise multiplayer, controller support, or story content until those features are actually live in the submitted build.
- If any generative AI is used in shipped content or runtime generation later, disclose it in the Content Survey.
- If the game ever adds in-game payments, those transactions must use Steam-supported payment flows.

## Before A Real Steam Submission

- Test the published build on a clean Windows machine or VM.
- Replace prototype screenshots with captures from the real submitted build.
- Add final branding assets, executable icon, and store capsule art.
- Review the Early Access path separately if you plan to launch before feature completion.
