# The Place Department (Unity source): lanes, local dev and deploys

Generated from `hawkesstudio/tools/lanes/repos.json` (`node ../hawkesstudio/tools/lanes/lanes.mjs docs`); the whole
picture for every repo is `hawkesstudio/docs/lanes.md`.

**Branches: `dev` -> `beta` -> `main`.** Work on `dev` (local). `beta` is for testing. `main` is live.
Agents' fixes land on `beta` (pull requests Studio merges); `sync.bat` brings them into `dev`.

| | Address |
|---|---|
| Local | [http://localhost:4173/Game/](http://localhost:4173/Game/) |
| Beta | [https://sites.hawkesgen.ai/o/j-studio-4f12/the-place-department/Game/](https://sites.hawkesgen.ai/o/j-studio-4f12/the-place-department/Game/) (the website's beta, playing the newest build); this project's own build page: [https://sites.hawkesgen.ai/o/j-studio-4f12/place-department-unity/](https://sites.hawkesgen.ai/o/j-studio-4f12/place-department-unity/) |
| Live | [https://theplacedepartment.com/](https://theplacedepartment.com/), [https://theplacedepartment.com/Game/](https://theplacedepartment.com/Game/) |
| Deploys (GitHub Actions) | [github.com/jgriffith3433/breakingthrough/actions](https://github.com/jgriffith3433/breakingthrough/actions) |

**How it deploys:** Push beta plus a build-* tag (deploy-beta does both), or build from Studio: Gen builds that commit on Cloud Run (`WebGlBuild.CloudBuild`) and Studio keeps the build at its own public address (`sites.hawkesgen.ai/builds/o/j-studio-4f12/place-department-unity/b/web/<n>/`). It commits only a pointer onto theplacedepartmentwebsite's beta: `Game/build.json` and `window.HAWKES_BUILD_URL` in `Game/index.html`. Beta and live never share a file that changes: each build number is its own folder, beta's page names the newest, live's names the one that went live. Going live (Studio) fast-forwards both repos' main, so theplacedepartment.com/Game/ then names that build.

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
