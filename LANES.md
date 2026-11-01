# The Place Department (Unity source): lanes, local dev and deploys

Generated from `hawkesstudio/tools/lanes/repos.json` (`node ../hawkesstudio/tools/lanes/lanes.mjs docs`); the whole
picture for every repo is `hawkesstudio/docs/lanes.md`.

**Branches: `dev` -> `beta` -> `main`.** Work on `dev` (local). `beta` is for testing. `main` is live.
Agents' fixes land on `beta` (pull requests Studio merges); `sync.bat` brings them into `dev`.

| | Address |
|---|---|
| Local | [http://localhost:4173/Game/](http://localhost:4173/Game/) |
| Beta | [https://placedept.hawkesgen.ai/Game/](https://placedept.hawkesgen.ai/Game/) |
| Live | [https://theplacedepartment.com/](https://theplacedepartment.com/), [https://theplacedepartment.com/Game/](https://theplacedepartment.com/Game/) |
| Deploys (GitHub Actions) | [github.com/jgriffith3433/breakingthrough/actions](https://github.com/jgriffith3433/breakingthrough/actions) |

**How it deploys:** Push beta plus a build-* tag (deploy-beta does both): Gen builds that commit on Cloud Run and pushes Game/v<n> to theplacedepartmentwebsite's beta; Studio playtests it. Push main: Studio publishes that build to theplacedepartmentwebsite's main (GitHub Pages, live).

| Command (double-click, or run in a terminal here) | What it does |
|---|---|
| `dev.bat` | Switch to `dev`, bring it up to date, open the project in Unity, print the links |
| `deploy-beta.bat [patch\|minor\|major]` | Send `dev` to `beta`, tagged `vX.Y.Z-beta.N`, plus a `build-*` tag that builds the game on Google Cloud |
| `deploy-prod.bat [patch\|minor\|major]` | Send `beta` to `main` (live), tagged `vX.Y.Z`; asks you to type DEPLOY |
| `status.bat` | What's waiting for beta and for live, the last tags |
| `links.bat [local\|beta\|prod]` | The addresses; with a lane, open them |
| `sync.bat` | Bring `beta`'s newer commits (agents' fixes) into `dev` |

Versions: live tags are `vX.Y.Z`; each beta deploy on the way is `vX.Y.Z-beta.N`. The default step is minor; pass
`patch` or `major` to choose. `deploy-prod` gives beta's commit the version its beta tag was heading to.
