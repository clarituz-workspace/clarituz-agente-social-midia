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
**Status:** [x] done
**Blocked by:** T2, T3
**Skill prompt:**

> Load uipath-rpa and implement the REFramework glue per §11 rows 1–4 of `clarituz-agente-social-midia-sdd.md`: `Framework/InitAllSettings.xaml` (assets → ExecucaoContext), `Framework/InitAllApplications.xaml` (health checks — fail-fast per BR-10/E4), `Framework/GetTransactionData.xaml` (dequeue `SM_WorkItems` → deserialize to `WorkItemData`), `Framework/SetTransactionStatus.xaml` (Success/Failed + analytics fields per §12). Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] `InitAllSettings.xaml` — `Data/Config.json` (defaults DEV) + assets `SM_Config`/`SM_ContadorDiario`/`SM_UltimoPollComentarios` → `ExecucaoContext` dict; credenciais NÃO ficam no contexto (ST-SEC)
- [x] `InitAllApplications.xaml` — `GetRobotCredential` + `InvokeCode`→`SocialApiClient` (ctor SecureString) no mesmo escopo; health check GET /me Meta + /v2/me LinkedIn; skip com warning se token PENDING; fail-fast `InvalidOperationException` em não-2xx (BR-10/E4)
- [x] `GetTransactionData.xaml` — `ui:GetQueueItem` `SM_WorkItems` → `QueueItem` (UiPath.Core) + SpecificData → out args; log de ref/tipo
- [x] `SetTransactionStatus.xaml` — guard `is QueueItem`, Switch Successful/BusinessException/SystemException → ErrorType Business|Application + Analytics TipoTransacao/Plataforma + contadores Processados/Falhos no contexto
- [x] **Validate:** per-file 0 erros + `uip rpa build` Success — workaround: org analyzer ST-SEC-007/008/009 proíbe SecureString fora do escopo de criação/conversão em XAML → credenciais lidas e consumidas via InvokeCode no mesmo escopo; `Workflows/~ConteudoService.cs` (shadow do designer) esvaziado para não duplicar tipos

## Task T5 — uipath-rpa — Dispatchers (Curadoria, Monitorar_Comentarios, Agendar_Metricas)

**Identity:** `rpa:clarituz-agente-social-midia:dispatchers`
**Status:** [x] done
**Blocked by:** T3, T4
**Skill prompt:**

> Load uipath-rpa and implement the dispatcher workflows per §3 steps 2/7/9 and §11 of `clarituz-agente-social-midia-sdd.md`: `logica/Curadoria_Conteudo.xaml` (LLM pautas → enqueue GerarConteudo, dedup BR-11), `logica/Monitorar_Comentarios.xaml` (poll comments via SocialApiClient → enqueue ModerarComentario, dedup BR-12 + `SM_UltimoPollComentarios`), `logica/Agendar_Metricas.xaml` (enqueue ColetarMetricas for registered posts). These run via scheduled triggers with `in_ModoExecucao`. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] `Curadoria_Conteudo.xaml` — LLM topics → enqueue (unique reference `GerarConteudo-{slug}-{data}`, BR-11)
- [x] `Monitorar_Comentarios.xaml` — poll IG/FB/LI via SocialApiClient → dedup por commentId → enqueue `ModerarComentario-{id}` (BR-12) + `SM_UltimoPollComentarios` atualizado só em sucesso
- [x] `Agendar_Metricas.xaml` — lê `posts-monitorados.jsonl` (bucket `SM_Metricas`) → enqueue `ColetarMetricas-{postId}-{data}` vencidos (BR-13)
- [x] **Validate:** per-file validate 0 erros (4 arquivos: 3 dispatchers + `shared/LLM_Completion.xaml`) + `uip rpa build` → **Success**
- [x] Roteador `Framework/Process.xaml` implementado — dispatcher modes (Curadoria/MonitorarComentarios/AgendarMetricas/Relatorio) + consumer modes (TipoTransacao stub até T6–T8)

**Decisões técnicas T5:**
- `shared/LLM_Completion.xaml` criado como fronteira GenAI: RetryScope ×2 (E5) encapsulando Throw — a chamada real via conexão IS `uipath-uipath-airdk` substitui o Throw quando a conexão for criada na pasta Shared (pendente manual T1).
- `ConteudoService` ganhou `MontarPromptPautas` + `ParsearPautas`; `SocialApiClient` ganhou `ObterMidiasRecentesInstagramAsync`, `ObterFeedFacebookAsync`, `ObterPostsLinkedInAsync` (todos GET-only).
- `ItemInformationCollection` montada via `InvokeCode` + variável `Dictionary<string,object>` — dictionary initializer é rejeitado por expression-tree em C# XAML (CS8074).
- Chamadas a `ConteudoService`/`SocialApiClient` só via `InvokeCode` — o namespace do projeto não resolve em `CSharpValue`/`CSharpReference` (CS0246, mesmo issue do Http_Retry).
- Credenciais Meta/LinkedIn lidas e consumidas no mesmo escopo em `Monitorar_Comentarios` (ST-SEC-007/008/009 — mesmo padrão de T4).
- Duplicata de unique reference (B8) → Log Warn + continua; nunca fatal.
- Zero-write garantido mantido: nenhum POST/DELETE nas plataformas em nenhum dispatcher.

## Task T6 — uipath-rpa — GerarConteudo pipeline (generate → validate → approve → register)

**Identity:** `rpa:clarituz-agente-social-midia:gerar-conteudo-pipeline`
**Status:** [x] done
**Blocked by:** T3, T4
**Skill prompt:**

> Load uipath-rpa and implement the GerarConteudo transaction pipeline per §3 steps 3–6 of `clarituz-agente-social-midia-sdd.md`: `logica/Gerar_Conteudo.xaml` (ConteudoService → ConteudoGerado), `logica/Validar_Compliance.xaml` (BR-01..BR-05, BusinessException on violation), `logica/Aprovacao_Humana.xaml` (Create Form Task with publication package + `PostUrl` field + janela/limite informative fields → Wait and Resume — suspend point), `logica/Registrar_Post_Publicado.xaml` (resolve post_id from PostUrl via read-only API lookup → enqueue ColetarMetricas DeferDate +24h; B9 on invalid URL). The robot never publishes. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] `Gerar_Conteudo.xaml` + `Validar_Compliance.xaml` (BR-01..BR-05)
- [x] `Aprovacao_Humana.xaml` — CreateFormTask apenas (pacote publicação + decisao/legenda_editada/url_post_publicado/motivo_rejeicao). **Desvio ST-DBP-024:** WaitForFormTaskAndResume não pode ficar em sub-workflow → movido para `Main.xaml` (timeout 48h → B5 via TryCatch→BusinessRuleException); decisão pós-resume isolada em `logica/Pos_Aprovacao.xaml` (Rejeitado→B1, Editado→revalida compliance, BR-08 PostUrl obrigatória → Registrar_Post_Publicado). Process.xaml retorna `out_TaskPendente`/`out_Conteudo` para o Main.
- [x] `Registrar_Post_Publicado.xaml` — post_id resolution + metrics enqueue
- [x] **Validate:** per-file validate 0 erros + `uip rpa build` Success (warnings: FormActivityLibrary designer, AH URL, Log Message Http_Retry)

## Task T7 — uipath-rpa — Moderar_Comentario (suggestion tasks only)

**Identity:** `rpa:clarituz-agente-social-midia:moderar-comentario`
**Status:** [x] done
**Blocked by:** T3, T4
**Skill prompt:**

> Load uipath-rpa and implement `logica/Moderar_Comentario.xaml` per §3 step 8 and BR-09 of `clarituz-agente-social-midia-sdd.md`: LLM sentiment classification → `SugestaoModeracao` → Form Task to the human with suggested reply (priority High for Negativo/Crise; SugereOcultar for Spam). The robot NEVER replies, hides, or deletes on the platform — human executes. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] Sentiment classification via `ConteudoService.MontarPromptSentimento`/`ParsearSugestao` + `shared/LLM_Completion.xaml`
- [x] Suggestion task creation with priority per §6 mapping — `TaskPriority` é enum literal não-bindável → If/Else com dois `CreateFormTask` (High para Negativo/Crise, Medium demais); Crise também gera LogMessage Error (alerta imediato); task é fire-and-forget (sem wait — nada depende da ação do humano, evita ST-DBP-024); case `ModerarComentario` conectado no `Process.xaml`
- [x] **Validate:** per-file validate 0 erros + `uip rpa build` Success

## Task T8 — uipath-rpa — Coletar_Metricas + Relatorio_Execucao + Process routing

**Identity:** `rpa:clarituz-agente-social-midia:metricas-e-relatorio`
**Status:** [x] done
**Blocked by:** T3, T4
**Skill prompt:**

> Load uipath-rpa and implement per §3 steps 9–11 of `clarituz-agente-social-midia-sdd.md`: `logica/Coletar_Metricas.xaml` (insights via SocialApiClient → MetricasService → Storage Bucket `SM_Metricas`, upsert by postId+data), `logica/Relatorio_Execucao.xaml` (consolidated run report), and wire `Framework/Process.xaml` routing by `TipoTransacao` + `in_ModoExecucao` dispatcher modes per §6. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] `Coletar_Metricas.xaml` — credenciais no escopo → insights read-only IG/FB/LI → `MetricasService.Normalizar` → upsert por postId em `metricas-yyyy-MM-dd.jsonl` no `SM_Metricas` (BR-13); novo método `SocialApiClient.ObterEstatisticasPostLinkedInAsync` (stats por share URN)
- [x] `Relatorio_Execucao.xaml` — consolida contadores/alertas do ExecucaoContext → LogMessage + `relatorios/relatorio-*.txt` no bucket (falha de gravação vira warn, não quebra a run)
- [x] `Process.xaml` — roteamento completo: dispatchers (Curadoria/MonitorarComentarios/AgendarMetricas/Relatorio) + consumers (GerarConteudo/ModerarComentario/ColetarMetricas); todos os stubs removidos
- [x] **Validate:** per-file validate 0 erros + `uip rpa build` Success

## Task T9 — uipath-rpa — Testing (MANDATORY)

**Identity:** `rpa:clarituz-agente-social-midia:testing`
**Status:** [x] completed
**Blocked by:** T4, T5, T6, T7, T8
**Skill prompt:**

> Load uipath-rpa and run its testing workflow end-to-end per §17 of `clarituz-agente-social-midia-sdd.md`: happy path, exception cases (B1–B9), system errors (E1–E6), and the zero-write non-functional test (mock platforms; no POST/DELETE to Meta/LinkedIn anywhere). Register test cases in `project.json` `fileInfoCollection`. Use the skill's testing references for commands and practices.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] Test cases per §17 Requirements Traceability — 6 arquivos em `Tests/` registrados no `project.json`: `T_COMP_01_ComplianceBasica` (BR-01..BR-04, 6 cenários), `T_COMP_02_TermosProibidos` (BR-05, case-insensitive, legenda+hashtag), `T_MOD_Sentimentos` (Positivo/Neutro→normal, Negativo/Crise→prioritária, Spam→sugere ocultar — BR-09), `T_URL_Resolvers` (IG /p|reel/ shortcode, FB posts/NNN+pfbid, LI activity→urn, URLs inválidas→SocialApiException — BR-14/B9), `T_PARSE_LLM` (fences ```json, prosa ao redor, resposta sem JSON→FormatException — E5)
- [x] Zero-write guarantee test — `T_ZW_ZeroWrite` escaneia todos os `Workflows/*.cs` + XAMLs de `logica|shared|Framework|Main` em runtime: 25 arquivos, 0 ocorrências de POST/PUT/DELETE/PATCH a plataformas; `SocialApiClient` declara só `HttpMethod.Get`
- [x] **Validate:** 6/6 testes executados verdes via `uip rpa run` + build Success. Ajustes: `Language="CSharp"` obrigatório em todo `InvokeCode` (sem ele o runner standalone compila como VB — aplicado em lote no projeto); expectativa do resolver FB corrigida (`pfbid0...` retorna o token completo); `logica/Aprovacao_Humana.xaml` reescrito após Studio resserializar versão híbrida stale (Wait+args antigos); variáveis de teste renomeadas p/ padrão do projeto

## Task T10 — uipath-platform — Pack, publish, triggers

**Identity:** `platform:clarituz-agente-social-midia:deploy`
**Status:** [x] completed
**Blocked by:** T9
**Skill prompt:**

> Load uipath-platform (or `uip rpa pack` + `uip or packages upload`) and publish the package to Orchestrator folder `Shared`, create the process, and configure triggers per §16 of `clarituz-agente-social-midia-sdd.md`: queue trigger on `SM_WorkItems` (max 1 concurrent), scheduled triggers for `Curadoria` (daily), `MonitorarComentarios` (every 15–30 min), `AgendarMetricas` (daily) with `in_ModoExecucao` arguments. Delivery model: cloud.
> Use values, mappings, and structure exactly as documented in the SDD at clarituz-agente-social-midia-sdd.md. Do not infer or guess.

- [x] `uip rpa pack` + upload package — `clarituz-agente-social-midia.1.0.0.nupkg` publicado no feed do tenant
- [x] Create process in `Shared` — release `729fd7ab-7ecf-46cb-a90a-1a67c3467748`; `processes resources` → todas as dependências (fila, assets, buckets) com `ValidationResult=Success`
- [x] Configure queue + time triggers with mode arguments — `SM_WorkItems_ConsumirFila` (queue, threshold 1, 1 item/job, max 1 job — evita race no contador diário) + `SM_Curadoria_Diaria` (07:00), `SM_MonitorarComentarios_30min` (cada 30min, dentro da faixa 15–30 do SDD), `SM_AgendarMetricas_Diaria` (06:00) — todos TZ `E. South America Standard Time` (API rejeita IANA), `--input-arguments` com `in_ModoExecucao` correto, runtime Unattended, enabled
- [x] **Validate:** 4 triggers visíveis/enabled em `Shared` via `triggers list`. Smoke job pendente — depende de credenciais Meta/LinkedIn + conexão GenAI configuradas (manual T1) e de runtimes na pasta

## Change log — pós-T10

- **[x] Remoção do módulo LinkedIn (pedido do usuário)** — escopo agora é Meta-only (Instagram + Facebook). Removidos: métodos LinkedIn do `SocialApiClient` (ctor virou single-token), `NormalizarLinkedIn`, enum/limite/branch de hashtags LinkedIn, `linkedin_org_urn` de `Data/Config.json` + asset `SM_Config`, health check/credencial/branches LI nos 4 workflows que usavam API, asserts LI dos testes. Assets atualizados; pacote **1.0.1** publicado e processo `729fd7ab` atualizado via `update-version`. Asset `LinkedIn_AccessToken` ficou órfão (remoção manual opcional). Testes re-executados verdes; build Success.
- **[x] Calendário editorial automático (pedido do usuário)** — novo dispatcher `CalendarioEditorial`: `logica/Calendario_Editorial.xaml` chama LLM (`ConteudoService.MontarPromptCalendario`/`ParsearCalendario`, N dias via config `dias_calendario`, default 7) e grava `calendario/calendario-editorial.json` no bucket `SM_Metricas`. `Curadoria_Conteudo` lê o calendário, filtra entradas do dia (`EntradasDoDia`) e respeita `plataforma` por entrada; se ausente/vazio, fallback para geração ad-hoc de pautas. Rota adicionada no `Process.xaml`; trigger `SM_Calendario_Semanal` (`0 30 6 ? * MON`). Pacote **1.0.3** publicado, processo atualizado. Cobertura de teste adicionada a `T_PARSE_LLM` (parse + filtro do dia + entrada inválida → FormatException) — verde.
- **[x] Reversão do Power BI (pedido do usuário)** — `PowerBiService.cs` removido, push retirado do `Coletar_Metricas`, `powerbi_push_url` removido do `Config.json`/`SM_Config`, docs revertidos; processo repontado para **1.0.3** (pacote 1.0.4 ficou no feed sem uso). Commit `c61af99`.
- **[x] Geração de imagem por IA (pedido do usuário)** — `Workflows/ImagemService.cs` (OpenAI Images, POST fixo `api.openai.com`, b64_json ou url, suporta dall-e-3/dall-e-2/gpt-image-1); novo `logica/Gerar_Imagem.xaml` inserido como etapa 1.5 no pipeline GerarConteudo do `Process.xaml`: lê `gerar_imagem`/`imagem_modelo`/`imagem_tamanho` do config, gera bytes → `UploadStorageFile` em `SM_Midia/geradas/` → `SpecData["MediaUrl"]` preenchida → compliance BR-03 passa com a arte anexada. Não gera se o item já trouxer MediaUrl (mídia manual tem prioridade); falha vira warn não-bloqueante. Credential `OpenAI_ApiKey` criada (PENDING) + chaves no `SM_Config`. `T_ZW_ZeroWrite` agora usa tabela de exceções por arquivo→host (somente `*Imagem*`→`api.openai.com`). Pacote **1.0.5** publicado, processo atualizado.
- **[x] Imagem em alta qualidade (pedido do usuário)** — default `imagem_modelo` → `gpt-image-1` e novo config `imagem_qualidade` (default `high`): `ImagemService.GerarImagemAsync` agora envia `quality` (`high`/`medium`/`low`/`auto` no gpt-image-1; `hd`/`standard` no dall-e-3; omitido no dall-e-2). `Config.json`/`SM_Config` atualizados. Pacote **1.0.6** publicado, processo atualizado.
- **[x] QA visual da arte gerada (pedido do usuário — "cérebro visual")** — `ImagemService.AvaliarImagemAsync`: POST `api.openai.com/v1/chat/completions` com `gpt-4o-mini` + visão (imagem como data-URI base64), `response_format=json_object` retornando `{"aprovada":bool,"motivo":str}`; avalia aderência ao briefing, texto ilegível, artefatos, marca d'água e `termos_proibidos`. `Gerar_Imagem.xaml`: reprovou → regenera 1×; veredito vai ao log e ao `conteudo["NotaImagemQA"]`. Config `imagem_qa` (default `true`) no `Config.json`/`SM_Config`. Mesma exceção do zero-write (host já era `api.openai.com` — 28 arquivos, 0 hits). Pacote **1.0.7** publicado, processo atualizado.
