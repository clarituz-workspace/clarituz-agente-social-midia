# clarituz-agente-social-midia — Implementation Tasks

**Source SDD:** `clarituz-agente-social-midia-sdd.md`
**SDD scope:** single-product
**Execution autonomy:** autonomous
**Delivery model:** cloud
**Generation date:** 2026-09-25

> Tasks below are derived from the SDD. The SDD remains the architectural source of truth.
> Design invariant (SDD §9): o robô nunca escreve nas plataformas sociais — humano aprova e publica; moderação é sugestão via task.

## Task T1 — uipath-platform — Orchestrator resources (queue, assets, buckets)

**Identity:** `platform:clarituz-agente-social-midia:orchestrator-resources`
**Status:** [~] resources created — pending manual steps below
**Blocked by:** none
**Skill prompt:**

> Load uipath-platform and create the Orchestrator resources required by the process, per §12 and §15 of `clarituz-agente-social-midia-sdd.md`. In folder `Shared` of tenant `DefaultTenant`: create queue `SM_WorkItems` (unique reference YES, auto-retry ×3, analytics fields `TipoTransacao`/`Plataforma`); create assets `SM_Config` (JSON text), `SM_ContadorDiario` (JSON text), `SM_UltimoPollComentarios` (text); create Credential assets `Meta_SystemUserToken` and `LinkedIn_AccessToken` as placeholders for the human to fill (never invent secret values); create storage buckets `SM_Midia` and `SM_Metricas`; verify the IS connection `UiPath GenAI Activities` (`uipath-uipath-airdk`) is enabled in the folder; verify Action Center is available for Form Tasks.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] Create queue `SM_WorkItems` with §12 configuration — created 2026-09-25 (key `8ee55d1c-d8b1-41ca-a895-4e380f92152a`, retries 3, unique reference, retention 90d)
- [x] Create assets: `SM_Config`, `SM_ContadorDiario`, `SM_UltimoPollComentarios` — created with placeholder JSON
- [x] Create credential placeholders `Meta_SystemUserToken`, `LinkedIn_AccessToken` (human fills values) — value `PENDING:PENDING`, human must update in Orchestrator
- [x] Create storage buckets `SM_Midia`, `SM_Metricas` — created in `Shared`
- [ ] Verify GenAI IS connection + Action Center availability in `Shared` — **MANUAL:** existing `UiPath GenAI Activities` connection (`30bc9177-b8e2-42f8-a170-217c624742e0`) lives in the personal workspace folder; create a `uipath-uipath-airdk` connection in `Shared` via Integration Service UI (OAuth, no CLI flag). Action Center is tenant-level — no CLI check; Form Tasks require tenant license.
- [x] **Validate:** `uip or` list confirms queue + assets + buckets exist

## Task T2 — uipath-rpa — Project scaffold (REFramework + Hybrid + persistence)

**Identity:** `rpa:clarituz-agente-social-midia:scaffold`
**Status:** [x] done
**Blocked by:** none
**Skill prompt:**

> Load uipath-rpa and restructure the existing project `clarituz-agente-social-midia` (Portable framework, expressionLanguage CSharp) to the REFramework layout with persistence, per §11 of `clarituz-agente-social-midia-sdd.md`. Install the packages from §14 (pin at build). Declare the input arguments on Main.xaml per §15 (in_ModoExecucao default `ConsumirFila`, in_Plataformas, in_NichoCampanha, in_TomDeVoz, in_JanelaPublicacao, in_LimitePostsDia). Create the folder structure: `Framework/`, `logica/`, `shared/`, `Workflows/`, `Tests/`.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] Install §14 packages — pinned: WebAPI 2.5.2, Persistence 1.13.0, IntegrationService 1.33.0, Testing 25.10.3 (+ System 26.8.2)
- [x] Restructure to REFramework layout — `Main.xaml` agora é StateMachine Init → GetTransaction → Process → End; stubs `Framework/*.xaml` criados; dispatcher modes via sentinel (não-QueueItem) por `in_ModoExecucao`
- [x] Declare Main.xaml input arguments per §15 — `in_Plataformas` como `List<string>` (parser Portable não resolve `string[]`/`x:ArrayOf`); defaults em `project.json` entryPoints; `supportsPersistence: true`
- [x] Create folder skeleton per §11 — `Framework/`, `shared/`, `Workflows/`, `Data/` (+`Config.json` defaults DEV), `Tests/`
- [x] **Validate:** per-file 0 erros (8 arquivos) + `uip rpa build` Success (warnings: Persistence não usado ainda — T6; AH URL da org)

## Task T3 — uipath-rpa — Coded foundation (Models, services, HTTP retry)

**Identity:** `rpa:clarituz-agente-social-midia:coded-foundation`
**Status:** [x] done
**Blocked by:** T2
**Skill prompt:**

> Load uipath-rpa and implement the coded foundation in `Workflows/` per §5 and §11 of `clarituz-agente-social-midia-sdd.md`: `Models.cs` (records/enums exactly as §5), `SocialApiClient.cs` (Meta Graph + LinkedIn **read-only** client: comments, insights, media lookup — never POST/DELETE to the platforms), `ConteudoService.cs` (prompt building + LLM response parsing), `MetricasService.cs` (metrics normalization/serialization), plus `shared/Http_Retry.xaml` (timeout, exponential backoff, honors `Retry-After`). Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] `Workflows/Models.cs` — records/enums §5 completos + `SocialApiException`/`ExecucaoContext`
- [x] `Workflows/SocialApiClient.cs` — somente GET (read-only estrutural): comentários Meta/LinkedIn, insights IG/FB/LI, resolução de post_id a partir de URL humana (shortcode IG, post FB, URN LI), health checks, retry interno 2s→8s→32s honrando `Retry-After`
- [x] `Workflows/ConteudoService.cs`, `Workflows/MetricasService.cs` — prompts LLM + parse JSON; normalização de métricas IG/FB/LI → `MetricasPost` + serialização JSONL diária
- [x] `shared/Http_Retry.xaml` — wrapper `NetHttpRequest` moderno (GET fixo = zero-write estrutural): ExponentialBackoff 2s→8s→32s (multiplier 4), `PreferRetryAfterValue`, retry em 429/5xx, timeout 30s
- [x] **Validate:** per-file 0 erros nos 5 arquivos + `uip rpa build` Success (warnings: FormActivityLibrary.Contracts resolução; AH URL da org; Log Message ausente em Http_Retry)

## Task T4 — uipath-rpa — Framework workflows (Init, GetTransaction, SetStatus)

**Identity:** `rpa:clarituz-agente-social-midia:framework`
**Status:** [ ] pending
**Blocked by:** T2, T3
**Skill prompt:**

> Load uipath-rpa and implement the REFramework glue per §11 rows 1–4 of `clarituz-agente-social-midia-sdd.md`: `Framework/InitAllSettings.xaml` (assets → ExecucaoContext), `Framework/InitAllApplications.xaml` (health checks — fail-fast per BR-10/E4), `Framework/GetTransactionData.xaml` (dequeue `SM_WorkItems` → deserialize to `WorkItemData`), `Framework/SetTransactionStatus.xaml` (Success/Failed + analytics fields per §12). Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [ ] `InitAllSettings.xaml` — assets + SM_Config parse → ExecucaoContext
- [ ] `InitAllApplications.xaml` — health check Meta/LinkedIn/GenAI (BR-10)
- [ ] `GetTransactionData.xaml` — dequeue + deserialize (B-fail on missing fields)
- [ ] `SetTransactionStatus.xaml` — Output/Analytics fields per §12
- [ ] **Validate:** per-file validate + project build clean

## Task T5 — uipath-rpa — Dispatchers (Curadoria, Monitorar_Comentarios, Agendar_Metricas)

**Identity:** `rpa:clarituz-agente-social-midia:dispatchers`
**Status:** [ ] pending
**Blocked by:** T3, T4
**Skill prompt:**

> Load uipath-rpa and implement the dispatcher workflows per §3 steps 2/7/9 and §11 of `clarituz-agente-social-midia-sdd.md`: `logica/Curadoria_Conteudo.xaml` (LLM pautas → enqueue GerarConteudo, dedup BR-11), `logica/Monitorar_Comentarios.xaml` (poll comments via SocialApiClient → enqueue ModerarComentario, dedup BR-12 + `SM_UltimoPollComentarios`), `logica/Agendar_Metricas.xaml` (enqueue ColetarMetricas for registered posts). These run via scheduled triggers with `in_ModoExecucao`. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [ ] `Curadoria_Conteudo.xaml` — LLM topics → enqueue (unique reference)
- [ ] `Monitorar_Comentarios.xaml` — poll → dedup → enqueue
- [ ] `Agendar_Metricas.xaml` — enqueue pending metric collections
- [ ] **Validate:** per-file validate + project build clean

## Task T6 — uipath-rpa — GerarConteudo pipeline (generate → validate → approve → register)

**Identity:** `rpa:clarituz-agente-social-midia:gerar-conteudo-pipeline`
**Status:** [ ] pending
**Blocked by:** T3, T4
**Skill prompt:**

> Load uipath-rpa and implement the GerarConteudo transaction pipeline per §3 steps 3–6 of `clarituz-agente-social-midia-sdd.md`: `logica/Gerar_Conteudo.xaml` (ConteudoService → ConteudoGerado), `logica/Validar_Compliance.xaml` (BR-01..BR-05, BusinessException on violation), `logica/Aprovacao_Humana.xaml` (Create Form Task with publication package + `PostUrl` field + janela/limite informative fields → Wait and Resume — suspend point), `logica/Registrar_Post_Publicado.xaml` (resolve post_id from PostUrl via read-only API lookup → enqueue ColetarMetricas DeferDate +24h; B9 on invalid URL). The robot never publishes. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [ ] `Gerar_Conteudo.xaml` + `Validar_Compliance.xaml` (BR-01..BR-05)
- [ ] `Aprovacao_Humana.xaml` — Form Task + suspend/resume + PostUrl
- [ ] `Registrar_Post_Publicado.xaml` — post_id resolution + metrics enqueue
- [ ] **Validate:** per-file validate + project build clean

## Task T7 — uipath-rpa — Moderar_Comentario (suggestion tasks only)

**Identity:** `rpa:clarituz-agente-social-midia:moderar-comentario`
**Status:** [ ] pending
**Blocked by:** T3, T4
**Skill prompt:**

> Load uipath-rpa and implement `logica/Moderar_Comentario.xaml` per §3 step 8 and BR-09 of `clarituz-agente-social-midia-sdd.md`: LLM sentiment classification → `SugestaoModeracao` → Form Task to the human with suggested reply (priority High for Negativo/Crise; SugereOcultar for Spam). The robot NEVER replies, hides, or deletes on the platform — human executes. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [ ] Sentiment classification via ConteudoService.cs
- [ ] Suggestion task creation with priority per §6 mapping
- [ ] **Validate:** per-file validate + project build clean

## Task T8 — uipath-rpa — Coletar_Metricas + Relatorio_Execucao + Process routing

**Identity:** `rpa:clarituz-agente-social-midia:metricas-e-relatorio`
**Status:** [ ] pending
**Blocked by:** T3, T4
**Skill prompt:**

> Load uipath-rpa and implement per §3 steps 9–11 of `clarituz-agente-social-midia-sdd.md`: `logica/Coletar_Metricas.xaml` (insights via SocialApiClient → MetricasService → Storage Bucket `SM_Metricas`, upsert by postId+data), `logica/Relatorio_Execucao.xaml` (consolidated run report), and wire `Framework/Process.xaml` routing by `TipoTransacao` + `in_ModoExecucao` dispatcher modes per §6. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [ ] `Coletar_Metricas.xaml` — insights → normalize → bucket
- [ ] `Relatorio_Execucao.xaml` — counters + alerts
- [ ] `Process.xaml` — TipoTransacao router + dispatcher modes
- [ ] **Validate:** per-file validate + project build clean

## Task T9 — uipath-rpa — Testing (MANDATORY)

**Identity:** `rpa:clarituz-agente-social-midia:testing`
**Status:** [ ] pending
**Blocked by:** T4, T5, T6, T7, T8
**Skill prompt:**

> Load uipath-rpa and run its testing workflow end-to-end per §17 of `clarituz-agente-social-midia-sdd.md`: happy path, exception cases (B1–B9), system errors (E1–E6), and the zero-write non-functional test (mock platforms; no POST/DELETE to Meta/LinkedIn anywhere). Register test cases in `project.json` `fileInfoCollection`. Use the skill's testing references for commands and practices.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [ ] Test cases per §17 Requirements Traceability
- [ ] Zero-write guarantee test
- [ ] **Validate:** all tests pass; record results

## Task T10 — uipath-platform — Pack, publish, triggers

**Identity:** `platform:clarituz-agente-social-midia:deploy`
**Status:** [ ] pending
**Blocked by:** T9
**Skill prompt:**

> Load uipath-platform (or `uip rpa pack` + `uip or packages upload`) and publish the package to Orchestrator folder `Shared`, create the process, and configure triggers per §16 of `clarituz-agente-social-midia-sdd.md`: queue trigger on `SM_WorkItems` (max 1 concurrent), scheduled triggers for `Curadoria` (daily), `MonitorarComentarios` (every 15–30 min), `AgendarMetricas` (daily) with `in_ModoExecucao` arguments. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [ ] `uip rpa pack` + upload package
- [ ] Create process in `Shared`
- [ ] Configure queue + time triggers with mode arguments
- [ ] **Validate:** process + triggers visible; smoke job runs
