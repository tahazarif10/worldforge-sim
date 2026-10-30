# Project state - 2026-10-30

Repository: tahazarif10/worldforge-sim.
Local expected path: H:\WorldForge-Sim.

M0 initial Godot 4.7.2 .NET project + simulation core + CLI smoke checks + Windows H:-based environment scripts proposed.

**Verification:** GitHub files and source code added to PR branch, but no local Godot/.NET toolchain exists in this execution environment. Compilation, Godot launch, interactive game behavior and Windows H:-path enforcement have **not been tested**. Do not merge as accepted M0 yet.

Windows evidence from user on 2026-10-30: .NET SDK 10.0.403 independently installed and verified at `H:\WorldForge-Sim\Tools\dotnet\dotnet.exe`. Existing C:-based .NET was not changed. Godot installation and game build still unverified. Standardize on .NET 10 LTS (net10.0) instead of net8.0 nearing end of support. Windows install script is untested.

Next: pull branch on user's H: drive; run scripts/install-godot.ps1; check code signature and _sc_ marker; run scripts/acceptance.ps1 with the H:-based Godot exe and inspect Godot F5 result; fix actual errors, then merge.

2026-10-30 user acceptance attempt: H:-based SDK recognized, but original Windows PowerShell 5.1 acceptance script failed immediately after dotnet --info with `System.Management.Automation.RemoteException` (no compile result). Likely stderr redirection `*>>` with `$ErrorActionPreference='Stop'`. Fixed in `scripts/acceptance.ps1` using Start-Process separate stdout/stderr and explicit exit-code checks. The fix has not yet been validated on Windows. Await rerun; check `artifacts/acceptance/*.{stdout,stderr}.txt` and `result.txt`.

2026-10-30 second Windows acceptance: `dotnet-info` PASS; `core-build` PASS with 0 warnings and 0 errors; all 7 `core-smoke` assertions appeared in core-smoke.stdout.txt, stderr empty, but `result.txt` stopped before recording the exit code for core-smoke and no Godot build started. Most likely `Start-Process -Wait` blocked on descendant process; updated script to wait only for the process handle with timeout. Not yet verified; GitHub git pull reported `Empty reply from server` on user machine. If update cannot be fetched, run H:-based dotnet build directly from an H:-configured session.

2026-10-30 Windows local user evidence: `H:\WorldForge-Sim\Tools\dotnet\dotnet.exe build .\WorldForgeSim.csproj -c Debug --disable-build-servers -p:UseSharedCompilation=false` returned successful restoration and builds of WorldSim net10.0 and WorldForgeSim net10.0 (15.6s total). Outputs under `H:\WorldForge-Sim\src\WorldSim\bin\Debug\net10.0\` and `H:\WorldForge-Sim\.godot\mono\temp\bin\Debug\`. Core smoke tests previously all reported PASS; full scripted acceptance remains incomplete because Start-Process wait was blocking; revised script exists in repository but has not been pulled/tested locally. Next acceptance item: open H:-portable Godot via `scripts/run-editor.ps1`, F5, inspect rendering/input/errors and verify runtime behavior. Do not merge M0 before visual test.
