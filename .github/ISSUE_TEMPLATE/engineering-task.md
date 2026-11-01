---
name: Engineering milestone slice
about: A bounded change with dependencies, evidence and an acceptance gate
title: "[Vx.y.z] "
---

## Problem and expected result

What observable problem does this solve? What would falsify the proposed explanation?

## Scope and dependencies

- Parent tracker / milestone:
- Baseline commit:
- Required earlier gate:
- Affected modules:
- Relevant ADR / research:

## Deliverables

- [ ] Implementation or document deliverable
- [ ] Relevant correctness and failure-case checks
- [ ] Applicable CI on the candidate head
- [ ] Current state and roadmap synchronized

## Acceptance evidence

List exact scenarios, expected result and evidence destination. Separate automated correctness from Windows art, camera and performance. Use PENDING for unperformed checks.

## Constraints and rollback

Preserve the approved V2/Compatibility baseline and user-local project.godot. State memory/work budgets where relevant and how the change is disabled or reverted without deleting user work.

## Completion

Close only when this issue's own acceptance criteria are met. Linking a merged infrastructure PR does not complete a dependent physical acceptance issue.
