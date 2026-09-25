# Camila — Agente de Social Mídia (clarituz-agente-social-midia)

Robô UiPath (REFramework + C# coded workflows) para gestão de Instagram e Facebook via **Meta Graph API**, com geração de conteúdo por LLM, aprovação humana no Action Center e coleta de métricas.

> **Invariante de segurança — zero-write:** o robô **nunca** publica posts, responde comentários, oculta ou deleta nada nas plataformas. `SocialApiClient` só declara `HttpMethod.Get`. Todo conteúdo é **sugerido**; o humano revisa no Action Center, publica manualmente e informa a URL do post.

## Arquitetura

```
Main.xaml (StateMachine REFramework, suspend/resume)
└── Init → GetTransaction → Process → End
     └── Framework/Process.xaml — roteador
          ├── Modo dispatcher (execução única, via in_ModoExecucao):
          │    ├── CalendarioEditorial → logica/Calendario_Editorial.xaml (LLM gera calendário semanal → bucket)
          │    ├── Curadoria          → logica/Curadoria_Conteudo.xaml   (pautas do dia do calendário, ou ad-hoc → enqueue GerarConteudo)
          │    ├── MonitorarComentarios → logica/Monitorar_Comentarios.xaml (polling read-only IG/FB → enqueue ModerarComentario)
          │    ├── AgendarMetricas    → logica/Agendar_Metricas.xaml     (posts vencidos → enqueue ColetarMetricas)
          │    └── Relatorio          → logica/Relatorio_Execucao.xaml   (contadores → log + bucket)
          └── Modo ConsumirFila (queue trigger, por TipoTransacao):
               ├── GerarConteudo      → Gerar_Conteudo → Gerar_Imagem (opcional, OpenAI → SM_Midia)
               │                         → Validar_Compliance (BR-01..05) → Aprovacao_Humana (cria task)
               │                         ↓ retorna ao Main
               │                      WaitForFormTaskAndResume (48h) → Pos_Aprovacao → Registrar_Post_Publicado
               │                         (humano publica, cola URL → resolve post_id → enqueue ColetarMetricas +24h)
               ├── ModerarComentario  → logica/Moderar_Comentario.xaml  (sentimento LLM → task com resposta sugerida)
               └── ColetarMetricas    → logica/Coletar_Metricas.xaml    (insights → upsert JSONL no bucket)
```

ST-DBP-024: `WaitForFormTaskAndResume` existe **apenas** em `Main.xaml`; workflows filhos só criam tasks.

## Camadas codificadas (`Workflows/`)

| Arquivo | Função |
|---|---|
| `Models.cs` | DTOs: `WorkItemData`, `ConteudoGerado`, `SugestaoModeracao`, `MetricasPost`, `ExecucaoContext` + enums |
| `SocialApiClient.cs` | Cliente Meta **GET-only** — comentários, mídias, insights, resolução de post_id por URL; retry 2s→8s→32s honrando `Retry-After`; `SocialApiException` tipada |
| `ConteudoService.cs` | Prompts LLM + parsers tolerantes a ```json fences + `ValidarCompliance` (BR-01..05) |
| `MetricasService.cs` | Normalização IG/FB → `MetricasPost`; arquivo diário `metricas-YYYY-MM-dd.jsonl` |
| `ImagemService.cs` | Geração de arte via OpenAI Images — único ponto de escrita HTTP externa, destino fixo `api.openai.com`; desligado se `gerar_imagem=false`; falha não bloqueia (humano anexa mídia na aprovação) |

## Regras de negócio implementadas

| Regra | Onde |
|---|---|
| BR-01 limite de legenda (IG 2200, FB 63206) | `ConteudoService.ValidarCompliance` |
| BR-02 máx 30 hashtags | idem |
| BR-03 mídia IG obrigatória no bucket `SM_Midia` | `Validar_Compliance.xaml` (lista bucket) + `ValidarCompliance` |
| BR-04 extensões jpeg/jpg/png/mp4 | `ValidarCompliance` |
| BR-05 termos proibidos (config `termos_proibidos`) | `ValidarCompliance` |
| BR-08 URL do post obrigatória após aprovação | `Pos_Aprovacao.xaml` |
| BR-09 moderação sempre executada por humano | `Moderar_Comentario.xaml` (só cria task) |
| B5 timeout de aprovação 48h | `WaitForFormTaskAndResume` no Main |
| B9 URL inválida → BusinessRuleException | `Registrar_Post_Publicado.xaml` |
| BR-13 métricas upsert por postId/dia | `Coletar_Metricas.xaml` |
| Dedup por unique reference | dispatchers (`{Tipo}-{id}-{data}`) |
| Sentimento: Negativo/Crise → task High; Spam → sugere ocultar | `ParsearSugestao` + 2× `CreateFormTask` |

## Recursos no Orchestrator (pasta `Shared`)

| Tipo | Nome | Uso |
|---|---|---|
| Fila | `SM_WorkItems` | itens `GerarConteudo` / `ModerarComentario` / `ColetarMetricas` |
| Asset Credential | `Meta_SystemUserToken` | token de System User da Meta (**escopos**: `instagram_basic`, `instagram_manage_comments`, `instagram_manage_insights`, `pages_read_engagement`, `pages_show_list`, `business_management`) |
| Asset Credential | `OpenAI_ApiKey` | chave `sk-...` da OpenAI para geração de imagem (só usada se `gerar_imagem=true`) |
| Asset Text | `SM_Config` | `{"ig_user_id":"","fb_page_id":"","llm_model":"gpt-4o","calendario_editorial":{},"termos_proibidos":[],"janelas_sugeridas":"09:00-12:00;18:00-21:00","limite_posts_dia":3,"dias_calendario":7,"gerar_imagem":false,"imagem_modelo":"dall-e-3","imagem_tamanho":"1024x1024"}` |
| Asset Text | `SM_ContadorDiario` | contador de posts/dia (reset por data UTC) |
| Asset Text | `SM_UltimoPollComentarios` | `{"instagram":"","facebook":""}` — watermark do polling |
| Bucket | `SM_Midia` | mídias aprovadas para publicação |
| Bucket | `SM_Metricas` | `metricas-*.jsonl`, `posts-monitorados.jsonl`, `relatorios/`, `calendario/calendario-editorial.json` |

## Deploy (T10 — já feito)

- Pacote **1.0.5** publicado; processo `clarituz-agente-social-midia` na pasta `Shared` (release `729fd7ab-7ecf-46cb-a90a-1a67c3467748`)
- Triggers (TZ `E. South America Standard Time`, Unattended):
  - `SM_WorkItems_ConsumirFila` — queue trigger, threshold 1, máx 1 job
  - `SM_Calendario_Semanal` — `0 30 6 ? * MON` (segunda 06:30) → `in_ModoExecucao=CalendarioEditorial`
  - `SM_Curadoria_Diaria` — `0 0 7 * * ?` → `in_ModoExecucao=Curadoria`
  - `SM_MonitorarComentarios_30min` — `0 0/30 * * * ?` → `in_ModoExecucao=MonitorarComentarios`
  - `SM_AgendarMetricas_Diaria` — `0 0 6 * * ?` → `in_ModoExecucao=AgendarMetricas`

## Checklist para colocar em produção

1. **Meta System User token** — Business Settings → Usuários do sistema → gerar token "nunca expira" com os escopos acima; conceder acesso à conta IG + página FB + app. Colar em `Meta_SystemUserToken` (campo Password).
2. **`SM_Config`** — preencher `ig_user_id` (via `GET /{page_id}?fields=instagram_business_account`) e `fb_page_id` se usar FB.
3. **Conexão GenAI** — Integration Service → pasta Shared → UiPath GenAI Activities → Add connection. Hoje `shared/LLM_Completion.xaml` falha explicitamente sem ela (ponto único a substituir pela chamada IS).
4. **Runtime** — machine template com runtime Unattended atribuído à pasta `Shared`.
5. Opcional: `UiPath.FormActivityLibrary` (designer de forms), Automation Hub URL da org.

## Testes (`Tests/`)

6 test cases XAML (`VerifyExpression`), executáveis via `uip rpa run --file-path Tests/<nome>.xaml`:

- `T_COMP_01` — BR-01..BR-04 (6 cenários)
- `T_COMP_02` — BR-05 termos proibidos
- `T_MOD_Sentimentos` — roteamento Positivo/Neutro/Negativo/Crise/Spam
- `T_URL_Resolvers` — extração de post_id de URLs IG/FB + inválidas → `SocialApiException`
- `T_PARSE_LLM` — parsers toleram fences/prosa; resposta sem JSON → `FormatException`
- `T_ZW_ZeroWrite` — escaneia os fontes em runtime: 0 chamadas POST/PUT/DELETE/PATCH às plataformas

## Notas de manutenção

- `InvokeCode` sempre com `Language="CSharp"` — sem o atributo o runner compila como VB.
- Arquivos `~<nome>.xaml`/`~<nome>.cs` são shadow copies do Studio — fechar a aba no Studio antes de buildar; se o build quebrar com tipos duplicados/args stale, é isso.
- Não reintroduzir endpoints de escrita: o invariante é validado por teste (`T_ZW`) e por estrutura (`SocialApiClient` só tem GET; `Http_Retry.xaml` tem `Method="GET"` hardcoded).
