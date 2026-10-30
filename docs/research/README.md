# WorldForge Sim — Research and Architecture Audit Index

**2026-10-30 | Documentation-only findings | Issue #19**

This analysis audits the existing GitHub `main` through merged PR #18. It performs official external research, proposes an architecture, and describes test procedures. It does **not** assert that the user's Windows machine has been benchmarked in this session or that premium art quality has already been achieved.

## Documents

| Document | Main question answered |
|---|---|
| [CURRENT_STATE_AUDIT.md](CURRENT_STATE_AUDIT.md) | What actually exists, and which code paths cause risk? |
| [CAMERA_DIAGNOSIS.md](CAMERA_DIAGNOSIS.md) | Why is drag/zoom wrong, and what exact math fixes it? |
| [GRAPHICS_RESEARCH.md](GRAPHICS_RESEARCH.md) | Why does V2 still look primitive, and what art/renderer path improves it? |
| [LARGE_WORLD_ARCHITECTURE.md](LARGE_WORLD_ARCHITECTURE.md) | How do 64x64 data regions, chunk LOD, streaming and save/replay scale to 8192²+? |
| [PERFORMANCE_BASELINE.md](PERFORMANCE_BASELINE.md) | What is measured, unmeasured and reproducibly testable on the H: workstation? |
| [ARCHITECTURE_DECISION.md](ARCHITECTURE_DECISION.md) | Why select Godot hybrid-detail 3D, versus all-3D, 2.5D, Unity URP or Unreal? |
| [IMPLEMENTATION_ROADMAP.md](IMPLEMENTATION_ROADMAP.md) | What are the prioritized milestones, tasks, branches and release gates? |
| [FIRST_VERTICAL_SLICE.md](FIRST_VERTICAL_SLICE.md) | How can a 128x128 authored Coastal Forest slice prove navigation, art and region correctness? |

## Executive findings (Persian)

1. **اول دوربین:** مشکل حرکت روی زاویه ایزومتریک به تبدیل نادرست بردارهای صفحه به مختصات زمین و زوم حول سطح ثابت آب مربوط می‌شود؛ راهکار، برخورد دقیق پرتو با صفحه و ارتفاع واقعی زمین است.
2. **دوم معماری منطقه‌ای:** تغییر کوچک زمین اکنون کل پوشش گیاهی را بازسازی می‌کند. `WorldSim` فعلی هم جهان را به‌صورت یک آرایه یکپارچه نگه می‌دارد و ابعاد آن حداکثر ۱۰۲۴ خانه در هر محور است.
3. **سوم کیفیت هنری واقعی:** مدل‌های فعلی درخت و صخره عمدتاً کره، استوانه و سطح‌های تخت هستند؛ سطح کیفی موردنظر با آرت مستقل، شکل‌های طبیعی، متریال و نورپردازی یکپارچه حاصل می‌شود.
4. **انتخاب مشروط:** Godot با سه‌بعدی با جزئیات نزدیک، LOD منطقه‌ای و نقشه خلاصه دوردست، فعلاً بهترین نسبت قابلیت و هزینه برای این پروژه است. تغییر موتور بدون آزمایش مقایسه‌ای توصیه نمی‌شود.
5. **شکاف تست:** نتیجه CI به معنی اجرای موفق، کیفیت بصری مطلوب یا FPS خوب روی AMD Radeon R9 M395X نیست. نتیجه سخت‌افزاری باید روی `H:\WorldForge-Sim` سنجیده شود.

## Merge policy

A documentation-only PR can be merged after links, source/line references and correctness review, without claiming runtime changes or user visual approval. All actual rendering/camera/world modifications require separate PRs, tests and hardware validation.

**Canonical local root:** `H:\WorldForge-Sim`; no intentional downloads/install/cache/temp/build/logs on C:. Do not reset or overwrite local `project.godot`.
