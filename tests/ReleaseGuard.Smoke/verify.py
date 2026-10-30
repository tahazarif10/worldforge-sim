"""Release guard: V3 regression must not silently replace user-approved V2.
Runs under Python stdlib in core CI; protects launcher documentation and
the opt-in-only experimental route. Not a Godot visual benchmark.
"""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
launcher = (root / "scripts/run-regional-world.ps1").read_text(encoding="utf-8")
verify = (root / "scripts/verify-v3-regional.ps1").read_text(encoding="utf-8")
v2 = (root / "scripts/run-visual-lab.ps1").read_text(encoding="utf-8")
readme = (root / "README.md").read_text(encoding="utf-8")
docs = (root / "docs/V3_REJECTION_POSTMORTEM.md").read_text(encoding="utf-8")

assert "param([switch]$ExperimentalV3)" in launcher, "V3 direct launch must be explicitly gated"
assert "if (-not $ExperimentalV3)" in launcher, "V3 must not be the normal launcher route"
assert re.search(r"if \(-not \$ExperimentalV3\)\s*\{(?:(?!\n\}).)*run-visual-lab\.ps1", launcher, re.S), (
    "default launcher must delegate to approved V2 before any V3 command"
)
assert "RegionalWorldLab3D.tscn" in launcher, "separate experimental lab should be retained"
assert "param([switch]$NoLaunch, [switch]$ExperimentalV3)" in verify
assert "run-regional-world.ps1') -ExperimentalV3:$ExperimentalV3" in verify
assert "run-visual-lab.ps1" in v2 and "Scenes/VisualLab3D.tscn" in v2
assert "V3 is withdrawn" in readme
assert "both visual appearance and movement/camera quality" in docs
assert (root / "Scenes/Main.tscn").exists()
print("PASS V3 experimental launch is opt-in; V2 is the user-facing default")
