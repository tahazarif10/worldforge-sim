"""Documentation source-of-truth CI for the V3 milestone planning system.

Static links and status structure only. This does NOT prove any Windows runtime,
GPU FPS, screenshots or that planned features exist.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
documents = [
    "README.md",
    "AGENTS.md",
    "CONTRIBUTING.md",
    ".github/PULL_REQUEST_TEMPLATE.md",
    ".github/ISSUE_TEMPLATE/engineering-task.md",
    "docs/README.md",
    "docs/V3_3_ACCEPTANCE_RUNNER.md",
    "docs/V3_4_VIEWPORT_STREAMING.md",
    "docs/V3_4_MESH_WORKERS.md",
    "docs/decisions/0008-immutable-mesh-workers-and-publication-budgets.md",
    "docs/decisions/0003-bounded-viewport-demand-and-generation-leases.md",
    "docs/decisions/0004-continuous-surface-fallback.md",
    "docs/ROADMAP_FA.md",
    "docs/REPOSITORY_AUDIT_2026-11-01.md",
    "docs/decisions/0007-measurement-evidence-and-scenario-identity.md",
    "docs/ROADMAP.md",
    "docs/PROJECT_STATE.md",
    "docs/ENGINEERING_WORKFLOW.md",
    "docs/V3_ARCHITECTURE.md",
    "docs/V3_TECHNICAL_RESEARCH_2026.md",
    "docs/V3_ACCEPTANCE_MATRIX.md",
    "docs/PROJECT_HISTORY_2026-10-30.md",
    "docs/decisions/README.md",
    "docs/decisions/0001-preserve-v2-renderer-and-compatibility.md",
]
problems = []

for name in documents:
    path = root / name
    if not path.is_file():
        problems.append(f"Required document missing: {name}")
        continue

    data = path.read_text(encoding="utf-8")
    if not data.strip():
        problems.append(f"Empty document: {name}")
    if r"\`" in data and name != "docs/PROJECT_HISTORY_2026-10-30.md":
        problems.append(f"Escaped Markdown backtick in {name}")

    # Only new/updated planning docs: older history remains archival.
    if name == "docs/PROJECT_HISTORY_2026-10-30.md":
        continue

    for raw_link in re.findall(r"\[[^\]]+\]\(([^)\n]+)\)", data):
        target = raw_link.split("#", 1)[0].split("?", 1)[0]
        if not target or target.startswith(("https://", "http://", "mailto:", "//")):
            continue
        # Relative local markdown references must resolve within repo.
        relative = (path.parent / target).resolve()
        if root.resolve() not in relative.parents and relative != root.resolve():
            problems.append(f"Document reference escapes repository: {name} -> {target}")
        elif not relative.exists():
            problems.append(f"Broken document link: {name} -> {target}")

roadmap = (root / "docs/ROADMAP.md").read_text(encoding="utf-8")
state = (root / "docs/PROJECT_STATE.md").read_text(encoding="utf-8")
readme = (root / "README.md").read_text(encoding="utf-8")
policy = (root / "AGENTS.md").read_text(encoding="utf-8")
research = (root / "docs/V3_TECHNICAL_RESEARCH_2026.md").read_text(encoding="utf-8")
persian = (root / "docs/ROADMAP_FA.md").read_text(encoding="utf-8")
for section in ["V3.3", "V3.4", "V3.5", "V3.6", "V3.7", "V4", "V5"]:
    if section not in roadmap:
        problems.append(f"Roadmap missing stage {section}")
    if section not in persian:
        problems.append(f"Persian roadmap missing stage {section}")
# Flat evidence frontmatter is intentionally checked without a YAML dependency.
# A valid identifier proves structure, never that the referenced run passed.
parts = state.split("---", 2)
if not state.startswith("---\n") or len(parts) != 3:
    problems.append("PROJECT_STATE must begin with complete structured frontmatter")
    frontmatter = ""
else:
    frontmatter = parts[1]
fields = dict(re.findall(r"^([a-z_]+):[ \t]*(.*)$", frontmatter, re.M))
required_fields = [
    "state_schema", "reference_code_commit", "audited_repository_commit",
    "active_milestone", "last_verified_date", "next_gate", "open_blockers",
    "baseline_core_ci_commit", "baseline_core_ci_run",
    "baseline_engine_ci_commit", "baseline_engine_ci_run",
    "windows_verified_commit", "user_accepted_visual_commit",
]
for key in required_fields:
    if key not in fields or not fields[key].strip():
        problems.append(f"PROJECT_STATE missing evidence field: {key}")
for key in [
    "reference_code_commit", "audited_repository_commit",
    "baseline_core_ci_commit", "baseline_engine_ci_commit",
    "windows_verified_commit", "user_accepted_visual_commit",
]:
    value = fields.get(key, "")
    unknown_allowed = key in ("windows_verified_commit", "user_accepted_visual_commit")
    if not re.fullmatch(r"[0-9a-f]{40}", value) and not (unknown_allowed and value == "null"):
        problems.append(f"Invalid evidence commit identity: {key}")
for key in ("baseline_core_ci_run", "baseline_engine_ci_run"):
    if not re.fullmatch(r"[1-9][0-9]*", fields.get(key, "")):
        problems.append(f"Invalid baseline CI run identifier: {key}")
milestone = fields.get("active_milestone", "")
if not re.fullmatch(r"V[0-9]+(?:\.[0-9]+){0,2}", milestone) or milestone not in roadmap:
    problems.append("Active milestone must be a named stage in the roadmap")
if not re.fullmatch(r"\d{4}-\d{2}-\d{2}", fields.get("last_verified_date", "")):
    problems.append("Evidence review date must use YYYY-MM-DD")
if "V3 is withdrawn" not in readme and "V3 regional demo is **rejected**" not in readme:
    problems.append("README must unambiguously quarantine failed V3 preview")
if "project.godot" not in policy or "R9 M395X" not in policy:
    problems.append("Agent guardrails lost local-work or GPU safety")
if "docs.godotengine.org" not in research or "quaternius.com" not in research:
    problems.append("Technical research must include verified official references")
if "User-facing" not in roadmap and "user-facing" not in roadmap:
    problems.append("Roadmap missing product acceptance distinction")

if problems:
    for issue in problems:
        print("FAIL", issue)
    raise SystemExit(1)

print(f"PASS documentation registry, history, status, gate separation and relative links ({len(documents)} files)")
