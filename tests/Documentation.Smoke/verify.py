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
for section in ["V3.3", "V3.4", "V3.5", "V3.6", "V3.7", "V4", "V5"]:
    if section not in roadmap:
        problems.append(f"Roadmap missing stage {section}")
for key in ["state_schema", "reference_code_commit", "active_milestone",
            "last_verified_date", "next_gate", "open_blockers"]:
    if not re.search(r"^" + re.escape(key) + r":", state, re.M):
        problems.append(f"PROJECT_STATE missing YAML key: {key}")
if not state.startswith("---\n"):
    problems.append("PROJECT_STATE must begin with structured frontmatter")
if "V3.3.0" not in state:
    problems.append("PROJECT_STATE must identify next physical milestone")
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
