# X-Con U.F.O Defence

Unity project structure for a strategy/simulator game inspired by X-Com/UFO Defense.

## Folder Structure
- Assets/Scenes
- Assets/Scripts/Managers
- Assets/Scripts/Systems/Combat
- Assets/Scripts/Systems/Networking
- Assets/Scripts/Systems/Intelligence
- Assets/Scripts/UI
- Assets/Prefabs
- Assets/Art
- Assets/Audio
- Assets/Resources
- Assets/Data

## Getting Started
1. Open this folder in Unity Hub.
2. Add your first scene in Assets/Scenes.
3. Start coding in Assets/Scripts/Managers (GameManager, BaseManager, UFOManager, SquadManager).
4. Use version control (Git) for tracking changes.

## WebGL Build Workflow
1. Build development WebGL output:
	- `scripts/webgl_build.sh dev`
2. Build release-candidate WebGL output:
	- `scripts/webgl_build.sh rc`
3. Serve development build locally:
	- `scripts/webgl_serve_dev.sh 8080`
	- Open `http://localhost:8080` in your browser.
4. Stamp a versioned release artifact:
	- `scripts/webgl_release.sh v0.1.0-rc1`
	- Output goes to `Builds/WebGL/releases/v0.1.0-rc1`.
5. Deploy versioned release artifact to staging:
	- `scripts/webgl_deploy_staging.sh v0.1.0-rc1 user@host:/var/www/xcon/`

Notes:
- The scripts default to Unity `6000.3.10f1` at `/home/yngvar/Unity/Hub/Editor/6000.3.10f1/Editor/Unity`.
- Override Unity path with `UNITY_BIN=/path/to/Unity`.
- Logs are written to `Logs/WebGL_DevBuild.log` and `Logs/WebGL_RCBuild.log`.

## Next Steps
- Add prefabs, art, and audio assets as development progresses.
- Expand scripts for game systems and UI.
- Prototype core gameplay features.

---
Welcome, General Yngvar Thon!