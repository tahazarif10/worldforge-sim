# Contributing

Read [AGENTS.md](AGENTS.md), the [documentation guide](docs/README.md), [current state](docs/PROJECT_STATE.md) and [engineering workflow](docs/ENGINEERING_WORKFLOW.md) before changing the project.

Use one focused issue and branch per milestone slice. Record the baseline SHA, intended behavior, relevant research/ADR, verification and rollback in the PR. Preserve the approved V2 scene, Compatibility renderer and user-local `project.godot`. Windows project tools and intentional outputs belong on H:.

Core CI and applicable official-engine CI must pass for the candidate. A source merge does not grant user-facing visual or performance acceptance; retain explicit pending physical gates. Documentation-only changes use applicable checks without fabricated Windows results.

Use the [engineering issue template](.github/ISSUE_TEMPLATE/engineering-task.md) and [PR template](.github/PULL_REQUEST_TEMPLATE.md). Keep future work in the [roadmap](docs/ROADMAP.md), actual evidence in [project state](docs/PROJECT_STATE.md), and architectural reasons in [ADRs](docs/decisions/README.md).
