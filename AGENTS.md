# WorldForge-Sim engineering policy

1. Source repo: tahazarif10/worldforge-sim. Canonical local checkout: H:\WorldForge-Sim.
2. Never intentionally download or install project binaries, assets, dependencies, packages, caches or builds onto C:. Use H:; verify paths before running scripts. Avoid saying third-party/Windows writes on C: can be absolutely prevented.
3. Before every task read README.md, docs/ROADMAP.md and docs/PROJECT_STATE.md; inspect branch and status.
4. Keep core world simulation (src/WorldSim) independent from Godot rendering/input.
5. Make small PRs; add deterministic tests; no invented test results. Run actual Windows build and visual checks before merging.
6. No WorldBox copyrighted artwork, source code, UI assets or branding.
7. Maintain docs/PROJECT_STATE.md with verified results, known issues and next actions.
