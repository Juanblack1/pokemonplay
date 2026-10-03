# C15 — launcher/frontend emulation end-to-end

## Outcome and scope

Prove that visible Library → Play launches the real bundled RetroArch inside the real launcher and that the application's virtual controls reach guest pixels. Use an original CC0 GBA diagnostic. Observe both RetroArch's internal frame and actual desktop pixels of its incorporated child window. Core-only/unembedded results remain diagnostics. Physical controllers, commercial/personal ROMs, audio and touch are not proven by this gate.

Research/reviews: output/frontend-emulation-research.md, output/frontend-e2e-architecture.md and output/frontend-e2e-qa-review.md. Sources pin RetroArch 1.22.2 at 69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576 and mGBA at 26b7884bc25a5933960f3cdcd98bac1ae14d42e2. No frontend/guest execution evidence yet.

## Requirements and acceptance

R1 — Owned identity/data. Each positive or negative uses a new GUID root; harness apphost runs from its PokemonPlayRuntime directory so AppPaths.Root equals library/GameCard/launch/config/bridge root. Set TEMP/TMP/POKEMONPLAY_RETROARCH_TEMP before app types load. Record tested commit/tree and harness/app/RetroArch/core/ROM hashes. No credentials, personal data, commercial logo/BIOS/runtime or local App Control changes. Own mode3 profile and actual bindings are required. Abort if a preexisting RetroArch candidate exists; do not kill it.

R2 — Actual entry. Seed Settings/ImportedPokemonGames.json using ImportedPokemonGame fields Id/RomPath/Title/BaseGame/Generation/IsHackRom (Generation3, clearly synthetic original diagnostic). The real catalog builds the visible GameCard. Mouse clicks its actual Play region; no private Launch invocation or PerformClick substitute. Require actual LauncherForm.RegisterGameSession, production PrepareGame/CreateLaunch/StartGame/TryEmbed, default ProcessStarter and production bridge/timer/SendInput. Import dialog itself is outside acceptance. Host-only execution cannot pass Library→Play.

R3 — Embedding/focus. Correlate spawned Process identity (PID/start-time/path/hash), bridge PID and HWND, rather than adopting a process by name. Require WS_CHILD, visible bounds and complete ancestry child→gamePanel→GameHostForm→LauncherForm. Record geometry/DPI, foreground PID, active form identity, ContainsFocus and exact bridge focus-guard operands throughout down/hold. Keep pause_nonactive=true. TopLevel=false host means ActiveForm cannot be assumed to be the host. Missing interactive desktop/focus is an explicit failed gate, never a reason to fake provider/focus or silently change policy.

R4 — Guest state and cadence. Original deterministic ROM renders all ten normalized KEYINPUT bits independently (A/B/Select/Start/Right/Left/Up/Down/R/L), fixed magic/calibration border and a uint32 visual counter advanced once per VBlank. Publish mask and counter together using buffering that avoids torn observations. Decoder is independent of launcher mappings and returns valid mask/counter before expected-mask comparison. Require phases neutral(mask0), A-held(1), A-release(0), B-held(2), B-release(0) in one live process, checking all ten bits. Normal mouse down/up at verified real pad hit regions must maintain raw action/Capture across multiple real bridge ticks. No positive assignment to VirtualActions or direct keyboard injection by harness.

Every phase needs two distinct guest counter values later than the baseline in both internal and desktop layers. Modular advance is 0<delta<2^31, including wrap. Same-color new files/timestamps/hashes do not prove advance. Match full masks across layers, with circular counter distance min(delta(a,b),delta(b,a)) <=12 VBlanks and timestamp lag <=500ms initially. Any relaxation requires measured evidence and spec review.

R5 — Capture ownership/attribution. SCREENSHOT-only UDP is explicitly permitted test instrumentation in dedicated CI; accounts/remote features are disabled. Harness sends only SCREENSHOT to127.0.0.1 on an exclusive recorded port. Fixed source does not prove listener binds only loopback; record endpoint and listener owning spawned PID. No firewall/policy changes or claim of completely disabled networking. Use fresh owned inbox, one pending request, request/arrival times, and one new fully decodable file. Multiple/late ambiguous arrivals fail. Timeout invalidates the run, never retries a request and attributes a late image to a later phase. Initial request spacing1.1s. Capture visible child desktop pixels via CopyFromScreen/BitBlt around100ms into a bounded2s ring; calibrate actual viewport/letterbox using guest border/magic. Internal PNG or PrintWindow alone cannot prove composition.

R6 — Valid negative. In a separate fresh process, subscribe the real virtualPad.VirtualChanged event. After normal A down, record Actions[4]/mouseAction/Capture raw A=true, then clear only VirtualActions[4] before the bridge consumes it; remain subscribed during the hold. No provider/array/timer/focus/injection replacement. Oracle still expects mask1; legitimate observed mask0 must decode with advancing counter, valid boot/embedding/focus/guard and capture. Emit only guest_hold_color as the expected failure. Extra bits, loss of raw capture, guard=false, missing binary/driver/frame, generic timeouts or cleanup failures invalidate the negative. Keep failed subcheck failed while separately recording expected-negative validity.

R7 — Driver/config fidelity. Default distributed video/input drivers are mandatory. Do not write video_driver/input_driver/input_joypad_driver/video_context_driver in required runs. GDI/dinput may be a separately labelled diagnostic, never a replacement or single aggregated pass; no assumed WARP. Capture effective-driver logs.

Instrument only the owned temporary session config through synchronous contentHost.ControlAdded before PresentGameSession calls Show/StartGame. Preserve default ProcessStarter, FileName/Arguments/WorkingDirectory/UseShellExecute. Save original/final/hash/key-diff; abort if ownership/order is unproven. Only source-validated whitelist keys may change for accounts/remote features, screenshot UDP/owned paths, core BIOS options, logging, automatic overrides/remaps/shaders and overlays. Exact source-verified table follows below; unknown required keys fail configuration. Protected keys: all binds/hotkey, default drivers, pause_nonactive, fullscreen/decorations, saves/system/core/assets paths. Disable later automatic overrides and keep owned directories empty. Label precedence evidence as pinned source + config files/logs, not direct memory readback.

R8 — Bounded result/cleanup/release. External supervisor independent of STA wall clock: per-run boot/embedding30s, neutral15s, each hold/release10s, cleanup10s, total120s. Positive and negative have separate roots/budgets; optional diagnostic third run also120s. Execution job allowance8min including collection; provision/build outside, upload always. Record partial results even STA hangs. Cleanup mouse-up/bridge release and verify guest neutral where possible, then close only exact spawned PID+start-time/path. Emergency kill does not prove key-up. Never kill by name, delete a live fixture or touch unrelated processes.

Schema1 separates passed/failed/not_run with reasons/evidence for boot, process_identity, root_identity, embedding, focus, cadence, all five phases, negative_validity, cleanup and default_driver. Include capture IDs/QPC/times/counters/masks/paths and config/identity evidence. Acceptance needs complete default positive, valid negative, both cleanups, and every requirement above. Do not infer convergence from pure generator tests.

Reproduce any product defect with focused red native evidence before fixing the cause. Full application/new-core/package/updater/installer/data-preservation checks plus exact tested merge tree precede publication. No next-cycle merge/tag/release until v171.10.7 is publicly hash-verified; final public update needs asset/hash/manifest proof.

## Owned tasks and state

S1 implemented and locally verified (library_research, independently checked by root): only tools/original_gba_frontend_test.py and tools/test_original_gba_frontend.py. Generator + pure independent oracle; meaningful A+B/invalid/stale/wrap/regression/ambiguous-counter tests. Python construction/oracle success proves no ARM execution; real guest frames require CI.
S2 architecture accepted, exact whitelist statically verified (installer owns read-only output architecture report). Close table and implement owned harness/supervisor after review; no product hooks for convenience. Root owns this spec and eventual CI/supervisor; source ownership will be assigned explicitly.
S3 actual Windows CI default positive + valid negative; retain failures and reproduce root cause, including embedded-host focus hypothesis. Default/platform failure leaves requirement incomplete.
S4 full integration gates and ordered release proof.

Sequencing: immutable v171.10.7 tag at df36b8885664df4d2d7df6ccb6a224f5b1406221 cannot be changed by independent C15 test tooling on codex/frontend-emulation-e2e. This permits S1 during its pipeline while preserving ordered publication. No production changes or C15 acceptance claim.


## S2 exact configuration whitelist (static source verification)

A tabela é a única lista de alterações permitidas no config de sessão para positivo/negativo. Booleanos são strings cfg `"false"`/`"true"`; números decimais também ficam entre aspas. `<run>` é raiz canônica GUID owned; `<port>` é inteiro 49152–65535 selecionado sem conflito, posteriormente corroborado pelo listener. Criar pastas antes de iniciar: screenshot_directory inexistente pode ser ignorado pelo frontend. Não ampliar whitelist automaticamente diante de warning. Nomes e tipos conferidos em [configuration.c fixado](https://github.com/libretro/RetroArch/blob/69a4f0ea1e8aaf442ae4858f2e7f2b31a1776576/configuration.c); referências de linhas nesta tabela são desse pin, não de main upstream.

| Chaves exatas permitidas | Valor final | Referência no pin |
| --- | --- | --- |
| cheevos_enable, cheevos_hardcore_mode_enable | false | 2016, 2018 |
| cheevos_username, cheevos_password, cheevos_token | string vazia | 1513–1515 |
| cloud_sync_enable, discord_allow, ai_service_enable | false | 1725, 1731, 1741 |
| network_on_demand_thumbnails, network_remote_enable | false | 2138, 2140 |
| netplay_public_announce, netplay_nat_traversal, netplay_use_mitm_server | false | 2113, 2115, 2120 |
| network_cmd_enable | true | 2106 |
| network_cmd_port | `<port>` | 2475 |
| stdin_cmd_enable | false | 2107 |
| screenshot_directory | `<run>/captures/inbox` | 1578; validação 4000–4007 |
| video_gpu_screenshot | false | 1818 |
| core_options_path | `<run>/Settings/Emulators/RetroArch/diagnostic-core-options.cfg` | 1554 |
| global_core_options | true | 1704 |
| game_specific_options | false | 1700 |
| auto_overrides_enable, auto_remaps_enable, auto_shaders_enable | false | 1701, 1702, 1705 |
| rgui_config_directory | `<run>/Settings/Emulators/RetroArch/overrides-empty` | 1601 |
| input_remapping_directory | `<run>/Settings/Emulators/RetroArch/remaps-empty` | 1569 |
| video_shader_dir | `<run>/Settings/Emulators/RetroArch/shaders-empty` | 1562 |
| video_shader_enable | false | 1784 |
| input_overlay_enable, input_overlay_enable_autopreferred | false | 2037–2038 |
| video_font_enable, menu_enable_widgets | false | 1808, 1828 |
| log_verbosity, log_to_file | true | 3540, 1738 |
| log_to_file_timestamp | false | 1740 |
| frontend_log_level, libretro_log_level | 0 | 2276–2277 |
| log_dir | `<run>/evidence/retroarch-log` | 1630 |

O conjunto de serviço remoto da tabela é prevenção explícita com fixture vazia, não controle de egress. Não iniciar multiplayer/stream/cloud/update/menu online. Não adicionar username/URL/secret real a nenhuma configuração. A base cfg é criada do zero na fixture e contém apenas configuração diagnóstica conhecida; nenhuma base de instalação ou usuário é copiada. Tokens/passwords/contas da base e sessão têm que estar ausentes ou vazios; um valor não vazio aborta antes da coleta, sem publicar valor em diff/log. Não “sanitizar” silenciosamente credenciais de origem desconhecida e continuar. As três chaves cheevos vazias são defesa explícita, não requisito para importar perfis reais.

Não alterar `video_smooth`, threading/vsync/scale/context/fullscreen/decorations para facilitar o oráculo: calibração e tolerância de cor lidam com escala default. O texto anterior sobre smoothing não concede exceção. Shader/overlay/OSD da tabela são a instrumentação visual declarada; não representam driver idêntico byte a byte nem validam todos os efeitos visuais de produção. Não adicionar dezenas de flags de notification: video_font_enable/menu_enable_widgets são a supressão permitida; pixels contaminados continuam falhando captura.

Core options é arquivo separado, com somente `mgba_use_bios = "OFF"` e `mgba_skip_bios = "ON"`, valores reconhecidos no [header mGBA fixado](https://github.com/mgba-emu/mgba/blob/26b7884bc25a5933960f3cdcd98bac1ae14d42e2/src/platform/libretro/libretro_core_options.h). Não alterar renderer/core/input ou fornecer BIOS. Global_core_options=true/game_specific_options=false e pastas owned vazias previnem configuração específica preexistente; CI precisa corroborar que o core abriu esse arquivo e não um `.opt` alternativo. Ausência de evidência necessária é configuration failed, não fallback para defaults presumidos.

Não há chave `core_options_directory`, `log_directory` ou `video_log_dir` confirmada neste configuration.c; são proibidas. `log_dir` é o nome reconhecido. HAVE_COMMAND/HAVE_CHEEVOS/HAVE_NETWORKGAMEPAD e demais condicionais são disponibilidade de compilação: registrar opção ausente como unsupported. Cheevos/cloud ausentes por feature compilada desligada são N/A somente com prova de identidade/build; desconhecido não equivale desligado. Falta de HAVE_COMMAND/SCREENSHOT, caminho de log não efetivo ou impossibilidade de comprovar a precedência necessária mantém capture/configuration failed. Não inventar flags equivalentes. Sem BIOS/core-options verificados, boot não recebe passed.

### Comparação protegida obrigatória antes de Process.Start

Salvar original/final do único arquivo temporário e mapa parseado case-sensitive; rejeitar chave duplicada antes/depois. Diff semântico tem de ser subconjunto exato da tabela, e cada chave de tabela presente tem que possuir valor esperado. Todo item fora da tabela mantém valor e presença originais. Não remover linhas desconhecidas para obter default; desconhecida necessária exige revisão explícita. Comparação cobre especialmente todos os `input_*` fora das três exceções de overlay/remapping_directory, todos os `*_driver`, `video_context_driver`, `pause_nonactive`, saves/system/libretro/assets, fullscreen/decorations e hotkeys. Drivers ausentes permanecem ausentes: adicionar driver é alteração protegida mesmo se valor coincidir com default presumido.

Além do diff: pause_nonactive=true original/final; input_player1 binds/botão/axis/hotkey/combo idênticos; savefile_directory/savestate_directory/system_directory originais idênticos e owned; libretro_directory/libretro_info_path/assets_directory preservados; argumentos core/config/ROM e FileName/WorkingDirectory/UseShellExecute não mudam. Evidência final registra hashes e nomes das chaves, nunca conteúdo confidencial. Reprova antes do native start qualquer violação. Ajuste de `config_save_on_exit` não é permitido: original já define false e deve continuar false.

O source pin lê appendconfig antes de settings e permite override posterior. Whitelist desliga os três carregamentos automáticos; rgui_config_directory/remapping/shader directories vazios são owned e verificados. Não usar cfg de core/jogo ou CLI adicional depois da sessão para ganhar precedência. Antes de carregar tipos app/RA, limpar da **environment block owned do processo CI** `LIBRETRO_VIDEO_SHADER_DIRECTORY`, `LIBRETRO_VIDEO_FILTER_DIRECTORY`, `LIBRETRO_ASSETS_DIRECTORY`, `LIBRETRO_AUTOCONFIG_DIRECTORY`, `LIBRETRO_CHEATS_DIRECTORY`, `LIBRETRO_DATABASE_DIRECTORY`, `LIBRETRO_SYSTEM_DIRECTORY` e `LIBRETRO_DIRECTORY`, que podem redirecionar paths; verificar nomes presentes na fonte antes da implementação. Isso não altera ambiente global nem configurações pessoais. Nome cuja influência não tenha sido validada fica gap, não prova de isolamento; nenhum desses permite alterar binds/driver/pause.


The eight LIBRETRO environment names above were confirmed in the pinned configuration.c at3841–3867 and4023–4042. mgba_skip_bios=ON is ignored when mgba_use_bios=OFF; BIOS isolation is use_bios=OFF and empty system, not skip alone.

S1 evidence: root reran Python -B unittest discover for test_original_gba_frontend.py:10 tests passed. Mode4 double-buffer payload3300bytes in deterministic32KiB original ROM; ten input regions, four-color magic/yellow border and32 uint32counter blocks. SHA25641be7710d767c385f5d8dc7a05b37ebde060ccc74c7b59e61455eb31b157d812. ARM execution, renderer timing and every-VBlank publication remain not_run until CI.


## Current verification checkpoint

C14/v171.10.7 public release run37104169650 completed successfully, exact merge df36b8885664df4d2d7df6ccb6a224f5b1406221. Public setup/update downloaded and compared with sidecars/API digests and manifest. Update fixture SHA2566f57a01804e76671e0095368d0665c04a105fb378fde4ab837ed631bf82f7654 (451850894bytes). The ordered-publication prerequisite is closed.

C15 harness initial Release build passed without warnings/errors; this is compile evidence only. Independent QA required correction of negative semantics, key-release state and decoder fidelity before native CI. Root supervisor parses and25pure guard checks pass (duplicate/missing scenario, root/nonce/hash changes, absent/failed cleanup and emergency/timeout cannot produce green). Dedicated workflow pins the verified public bundle and uploads failure evidence. No local native run, no frontend success, no product defect reproduction or C15 convergence claimed.
Initial native CI is evidence collection: R7 effective-input-driver proof remains intentionally not_run rather than accepting generic pre-init log mentions. The current gate cannot declare convergence until this contract is closed. Positive/default and negative runtime observations are still required; a red caused by this known instrumentation gap is not a reproduced product defect.

First native CI 37123821767 on e4eb451 failed before emulator launch: both scenarios reported boot "Sequence contains no matching element"; cleanup confirmed no owned process started. The one-entry imported catalog was serialized as an object by PowerShell pipeline enumeration. A regression test of the actual JSON writer reproduced the failure locally, then -InputObject preserved empty/single/multiple arrays and object roots. All29 pure supervisor checks now pass. This is fixture repair, not a reproduced product emulator defect. Superseded full run37123821751 cancellation requested; final native and full verification remain required on the corrected head.

Second native CI37124289220 at1746ebf also failed before emulator launch. Independent QA found the fixture Id was not a GUID N as required by ImportedGameCatalog.Load. New-DiagnosticCatalog now emits a GUID N; actual writer/generator round-trip contract is checked (30 pure guards passed). Harness also validates actual ImportedGameCatalog.Entries/Build before UI and reports configuration preflight failures explicitly. Fresh build passed0errors with1 existing InputWorkbench.cs108 warning. Native default and effective-driver evidence remain pending. Source review rejected sampled winraw-input HWND alone as effective-driver proof; no gate relaxation or new UDP.

Third native CI37124537137 at44a6e7d passed actual catalog preflight but timed out before session/process observation. Recorded Play point was(146,1074); original check covered only card bounds, not desktop or ancestor viewport. Harness now scrolls the actual card through all AutoScroll ancestors, awaits layout, requires all client clips+actual screen+WindowFromPoint owned card/root hit before mouseDown, and saves full desktop/geometry on success or failure. No private Launch/focus invocation. Evidence inventory now includes only files that actually exist. Fresh build0errors/1existingCS0108 warning; native rerun still required. This is instrumentation correction; no product input defect reproduced.

Fourth CI37124864846 atae94c825e41d1a7acf4294a104bf2d8c30ca04e7 proved native Play hit(card owned,1024x768screen,point146/668) and started bundled RetroArch through production starter. It then failed compound embedding validation before any paired guest frame/input proof. Default log observed D3D11 on Microsoft Basic Render Driver; audio endpoint failed and RA continued without audio. Owned process closed normally but no guest-release proof, so negative was correctly skipped. These are observed runtime facts, not driver/input convergence. New instrumentation records individual embedding predicates/full HWND ancestry/bounds before rejecting. No readiness wait added without measured transition; production unchanged.

Fifth CI37125200678 at27ac9e4 exposed a diagnostic serialization defect: anonymous fields visible/Visible collided under System.Text.Json constructor binding, preventing embedding facts. External120s watchdog rejected the run and skipped the negative. Facts now use explicit nativeVisible/hostVisible names with a real serialization contract check (also before frontend initialization); CI invokes that check before native frontend. Evidence-write errors no longer prevent owned cleanup/UI shutdown. R7 and process/input gates are unchanged; no product input defect or successful ROM frame proof claimed.

## S3 measured native embedding repair contract

Sixth native CI37125732016 (1a8e737) passed the real JSON serialization contract and started the owned bundled frontend. It measured style0xD6000000 with CHILD+POPUP, declaredEmbedded=true, GetParent=0, visible owned HWND and contained geometry. Microsoft defines CHILD and POPUP as mutually exclusive; the production transformation ORed CHILD after SetParent without clearing POPUP or checking the operation. GetParent's popup-owner interpretation means this snapshot does not independently prove SetParent failed. The incompatible style transformation is a concrete source/runtime defect.

S3 fixes only this embedding transformation: preserve every other style bit, clear POPUP and set CHILD before SetParent, capture immediate Win32 errors, verify actual GA_PARENT/IsChild/owned PID and required styles before setting emulatorEmbedded or constructing the input bridge. Record last attempt facts for the harness without writing personal data/files by default. Failed operations must not become success or adopt another process/window; preserve controlled close/failure behavior and restore original style if reparent fails safely. Do not change video/input drivers, pause settings, global DPI or launcher ProcessStarter.

Harness fidelity correction: use the same bootstrap as actual main Program.Run (EnableVisualStyles and compatible text false), removing its extra PerMonitorV2 override. Capture actual HWND/thread DPI contexts and ancestry via GA_PARENT, owner separately, plus uint32 style bits and before/after production trace. Prior harness runs are evidence of that harness route, not proof of distributed DPI behavior. Native red/green remains required and default-driver/input/guest/cleanup gates stay closed until actually verified.
