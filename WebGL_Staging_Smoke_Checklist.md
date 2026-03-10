# WebGL Staging Smoke Checklist

Use this for each release candidate deployed to staging.

## Session Info
- Release tag:
- Staging URL:
- Date:
- Tester:
- Browser:
- OS:

## Preflight
- [ ] Release artifact exists locally (`Builds/WebGL/releases/<tag>`).
- [ ] Deployed to staging from versioned artifact.
- [ ] Staging URL is HTTPS.
- [ ] Hard refresh cache done (Ctrl+Shift+R).
- [ ] Private/incognito window test prepared.

## Load And Startup
- [ ] Page loads without blank screen.
- [ ] Initial loading progress completes.
- [ ] First interactive state reached.
- [ ] No fatal error shown in browser console.
- [ ] No missing file/network 404 for build artifacts.

Notes:
- Time to first interactive (approx):
- Console warnings/errors summary:

## Input And Focus
- [ ] Click-to-focus works on first try.
- [ ] Keyboard input works after focus.
- [ ] Mouse input works correctly.
- [ ] Input still works after alt-tab away and back.
- [ ] Input still works after pressing Escape (if relevant in your flow).

## Display And Layout
- [ ] Correct aspect ratio at initial window size.
- [ ] Resize browser window keeps UI usable.
- [ ] Fullscreen enter works.
- [ ] Fullscreen exit returns to usable layout.
- [ ] Text remains readable and not clipped.

## Core Gameplay Loop (10-15 min)
- [ ] Start game/session from initial UI.
- [ ] Perform core loop actions (movement/combat/management as applicable).
- [ ] Transition between key screens/scenes without failure.
- [ ] Save/load related flow behaves as expected (if implemented in WebGL path).
- [ ] No progressive slowdown or runaway memory behavior observed.

Duration tested:

## Audio
- [ ] Audio starts when expected.
- [ ] No crackle/dropout under normal gameplay load.
- [ ] Volume balance acceptable.
- [ ] Mute/unmute behavior works (if implemented).

## Browser Matrix
### Chrome
- [ ] Smoke pass complete
- [ ] Critical issues: none / listed below

### Firefox
- [ ] Smoke pass complete
- [ ] Critical issues: none / listed below

(Optional) Edge
- [ ] Smoke pass complete

## Network And Hosting Sanity
- [ ] Build files served successfully (`.wasm`, `.data`, `.js`).
- [ ] Compression behavior correct for hosted output.
- [ ] Hashed file naming present in deployed build.
- [ ] Reload behavior is stable (no stale asset mismatch observed).

## Go/No-Go Gate
- [ ] GO: No critical blockers for release.
- [ ] NO-GO: Critical blockers exist.

Decision:
- Go / No-Go:
- Approved by:

## Issue Log Template
Use one entry per issue.

### Issue <ID>
- Severity: Critical / Major / Minor
- Browser + OS:
- Steps to reproduce:
1. 
2. 
3. 
- Expected:
- Actual:
- Console errors (copy exact lines):
- Screenshot/video:
- Owner:
- Status: Open / Fixed / Verified

## Final Summary
- Critical issues count:
- Major issues count:
- Minor issues count:
- Retest required: Yes / No
- Next action:
