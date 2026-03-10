# WebGL First Release Runbook (One Page)

Use this as your copy-paste checklist for a release candidate.

## 0) Open project folder
```bash
cd /home/yngvar/visualcode/workspace/x-con_ufo_defence
```

## 1) Confirm clean git state
```bash
git status --short
```
Expected: no output (clean) before starting release commands.

## 2) Build dev (quick sanity)
```bash
scripts/webgl_build.sh dev
```
Check result:
```bash
grep -nE "Build Finished, Result|WebGL dev build result" Logs/WebGL_DevBuild.log | tail -n 5
```
Expected: `Build Finished, Result: Success.`

## 3) Build and stamp release artifact
Pick a new tag each attempt, for example `v0.1.0-rc1`, `v0.1.0-rc2`, etc.

```bash
scripts/webgl_release.sh v0.1.0-rc1
```
Check artifact exists:
```bash
ls -la Builds/WebGL/releases/v0.1.0-rc1
```

## 4) Deploy to staging
Replace the destination with your real server path.

```bash
scripts/webgl_deploy_staging.sh v0.1.0-rc1 user@host:/var/www/xcon/
```

## 5) Run staging smoke pass
Use:
- `WebGL_Staging_Smoke_Checklist.md`

Minimum checks:
- Load/startup works.
- Input/focus works.
- Fullscreen/resize works.
- 10-15 minute core loop stable.
- No critical browser console errors.
- Chrome and Firefox both pass.

## 6) Go / No-Go decision
- If no critical issues: mark **GO** and ship same artifact.
- If issues found: mark **NO-GO**, fix code, create next tag (`rc2`), repeat from step 2.

## 7) Optional quick size check
```bash
du -sh Builds/WebGL/dev Builds/WebGL/rc Builds/WebGL/releases/v0.1.0-rc1
```
Use this to track release size growth over time.

## Common Problems
1. Build fails with no enabled scenes:
- The build runner already falls back to `Assets/x-con_ufo_defence.unity`.
- Confirm that scene exists.

2. `rsync` not found:
```bash
sudo apt update && sudo apt install -y rsync
```

3. Unity binary path is different on your machine:
```bash
UNITY_BIN=/path/to/Unity scripts/webgl_build.sh dev
UNITY_BIN=/path/to/Unity scripts/webgl_release.sh v0.1.0-rc1
```

## Quick Command Block (release day)
```bash
cd /home/yngvar/visualcode/workspace/x-con_ufo_defence

git status --short
scripts/webgl_build.sh dev
scripts/webgl_release.sh v0.1.0-rc1
scripts/webgl_deploy_staging.sh v0.1.0-rc1 user@host:/var/www/xcon/
```
