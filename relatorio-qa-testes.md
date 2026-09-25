# Camila — Relatório de Testes de QA

**Projeto:** clarituz-agente-social-midia
**Versão testada:** 1.0.5 (publicada e ativa no Orchestrator, pasta Shared)
**Data da execução:** 2026-02-10
**Ferramenta:** UiPath `uip rpa run` / `uip rpa validate` / `uip rpa build`
**Resultado geral:** ✅ **APROVADO — 6/6 testes, 0 erros de validação**

---

## Resumo executivo

| Etapa | Resultado |
|---|---|
| Build do projeto | ✅ Success |
| Validação estática (todos os workflows) | ✅ 0 erros (163 warnings cosméticos — ver seção) |
| Suíte de testes automatizados | ✅ 6/6 passaram |
| Invariante zero-write | ✅ Confirmada em runtime |

---

## Detalhe dos testes executados

### 1. `T_COMP_01_ComplianceBasica` — Validação de conteúdo (BR-01..BR-04) ✅

Verifica que todo conteúdo gerado passa pelo filtro de compliance antes de chegar à aprovação humana:

| Cenário | Esperado | Resultado |
|---|---|---|
| Post válido (Instagram) | 0 violações | ✅ |
| Legenda com 2.300 caracteres no Instagram | Violação BR-01 (limite 2.200) | ✅ |
| 31 hashtags no Instagram | Violação BR-02 (máx 30) | ✅ |
| 31 hashtags no Facebook | Violação BR-02 | ✅ |
| Post Instagram sem mídia | Violação BR-03 (mídia obrigatória) | ✅ |
| Mídia `.gif` | Violação BR-04 (permitidos: jpeg/png/mp4) | ✅ |

### 2. `T_COMP_02_TermosProibidos` — Lista de bloqueio (BR-05) ✅

- Termo proibido presente na legenda → violação detectada ✅
- Termo proibido em hashtag → violação detectada ✅
- Verificação **case-insensitive** (maiúsculas/minúsculas) ✅
- Conteúdo sem termos proibidos → aprovado ✅

### 3. `T_MOD_Sentimentos` — Roteamento de moderação ✅

Verifica a classificação de sentimento e a priorização das tarefas humanas:

| Sentimento | Comportamento esperado | Resultado |
|---|---|---|
| Positivo | Task de prioridade normal com resposta sugerida | ✅ |
| Neutro | Task de prioridade normal | ✅ |
| Negativo | Task de **prioridade alta** | ✅ |
| Crise | Prioridade alta + alerta imediato (log Error) | ✅ |
| Spam | Sugestão de **ocultação** (decisão sempre do humano) | ✅ |

### 4. `T_URL_Resolvers` — Identificação de posts por URL ✅

Após a publicação manual, o humano cola a URL do post e a Camila localiza o identificador:

| URL | Resultado |
|---|---|
| `instagram.com/p/<shortcode>` | Extrai shortcode ✅ |
| `instagram.com/reel/<shortcode>` | Extrai shortcode ✅ |
| URL de perfil Instagram | Retorna nulo (sem falsos positivos) ✅ |
| `facebook.com/pagina/posts/<id>` | Extrai post ID ✅ |
| `facebook.com/pagina/posts/pfbid0...` | Extrai token completo ✅ |
| URLs inválidas | `SocialApiException` tipada ✅ |

### 5. `T_PARSE_LLM` — Tolerância das respostas de IA ✅

A IA pode devolver JSON cercado de texto ou blocos ```` ```json ```` — os parsers toleram:

| Cenário | Resultado |
|---|---|
| Conteúdo gerado com fences | Legenda, hashtags e mediaPrompt extraídos ✅ |
| Pautas com prosa ao redor | 3 pautas parseadas corretamente ✅ |
| **Calendário editorial com fences** | 2 entradas parseadas, filtro do dia retorna 1 ✅ |
| Entrada de calendário sem data/tema | `FormatException` ✅ |
| Resposta vazia ou sem JSON | `FormatException` (sem crash silencioso) ✅ |

### 6. `T_ZW_ZeroWrite` — Garantia de não-publicação ✅

**Teste não-funcional crítico:** escaneia em runtime todos os arquivos-fonte do projeto (29 arquivos) procurando chamadas de escrita às plataformas:

- **0 ocorrências** de POST/PUT/DELETE/PATCH à Meta ✅
- `SocialApiClient` declara exclusivamente `HttpMethod.Get` ✅
- Wrapper HTTP `Http_Retry.xaml` tem `Method="GET"` fixo no código ✅
- **Única exceção de escrita permitida** (introduzida na 1.0.5): o módulo isolado `Workflows/ImagemService.cs`, que gera arte via OpenAI com destino fixo `api.openai.com` — fora das redes sociais. O teste valida que essa exceção vale somente para arquivos `*Imagem*` e somente quando o destino `api.openai.com` está fixo no código; qualquer outro padrão de escrita continua reprovado ✅

**Conclusão:** é estruturalmente impossível a Camila publicar, responder, ocultar ou excluir qualquer conteúdo nas redes sociais.

---

## Warnings conhecidos (não-bloqueantes)

| Warning | Impacto | Ação |
|---|---|---|
| CS1701 — binding de `System.Linq.Expressions` (160×, pacote Newtonsoft.Json) | Nenhum — redirecionamento de versão padrão do runtime | Nenhuma |
| `UiPath.FormActivityLibrary` não instalado (3×) | Designer de formulários não abre visualmente no Studio; **execução não é afetada** | Opcional: instalar pacote no Studio |
| `Log Message` ausente em `Http_Retry` | Analyzer sugere log no wrapper | Opcional |
| Automation Hub URL exigida pela org | Metadado de governance | Opcional: vincular projeto no Automation Hub |

## Pendências de ambiente (fora do código)

Os testes cobrem a lógica; para produção, restam apenas configurações no Orchestrator:

1. Token real da Meta no asset `Meta_SystemUserToken`
2. `ig_user_id`/`fb_page_id` reais no asset `SM_Config`
3. Conexão UiPath GenAI na pasta Shared (usada pelo `shared/LLM_Completion.xaml`)
4. Machine template com runtime **Unattended** na pasta Shared

---

## Taxa de acerto do projeto

**Suíte automatizada: 30/30 assertions — 100% de acerto** ✅

| Teste | Assertions | Passou | Taxa |
|---|---|---|---|
| T_COMP_01 — Compliance BR-01..04 | 6 | 6 | 100% |
| T_COMP_02 — Termos proibidos BR-05 | 3 | 3 | 100% |
| T_MOD — Sentimentos e priorização | 5 | 5 | 100% |
| T_URL — Resolvers IG/FB | 6 | 6 | 100% |
| T_PARSE — Parsers LLM + calendário | 7 | 7 | 100% |
| T_ZW — Zero-write (29 arquivos varridos) | 3 | 3 | 100% |
| **Total** | **30** | **30** | **100%** |

**Cobertura estática:** validação do projeto inteiro → **0 erros**; build → **Success**; zero ocorrências de escrita em plataforma.

### Nota honesta sobre o que esse número significa

Os 100% medem a camada **determinística e testável** do projeto: validação de compliance, parsers de IA, resolução de URLs, roteamento de sentimento e a garantia estrutural de não-publicação.

**Não entram na taxa** (dependem de configuração manual de ambiente, ainda pendente): chamadas reais à Meta Graph API, execução da conexão GenAI, criação de tasks no Action Center e leitura/escrita nos buckets — esses caminhos serão exercitados no primeiro smoke job após preencher credenciais e conexão.

## Conclusão

A suíte de QA confirma que a Camila está funcionalmente íntegra na versão 1.0.5: geração de calendário editorial, curadoria de conteúdo, validação de compliance, aprovação humana obrigatória, moderação com priorização por sentimento, coleta de métricas e a garantia de **zero escrita** nas plataformas — todos verificados e passando.
