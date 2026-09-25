# Manual Completo — Camila, Agente de Social Mídia

**Versão:** 1.0.8 · **Plataformas:** Instagram e Facebook · **Tecnologia:** UiPath Automation Cloud + IA generativa (texto + imagem + visão) · **Aprendizado:** o calendário semanal prioriza temas que já engajaram

---

## 1. O que a Camila faz

Camila é uma assistente de social media que trabalha de forma autônoma na **preparação** e análise — mas **todas as ações nas redes sociais são feitas por uma pessoa**. Ela nunca publica, responde ou apaga nada sozinha.

**Quatro frentes de trabalho:**

1. **Calendário editorial automático** — toda segunda-feira, a IA planeja a semana: temas por dia, plataforma, formato (feed, reels, carrossel, stories) e objetivo (educar, engajar, converter, prova social, oferta)
2. **Geração de posts** — todo dia, transforma as pautas do dia em pacotes completos: legenda, hashtags, descrição da mídia ideal e melhor horário sugerido
3. **Monitoramento de comentários** — a cada 30 minutos lê comentários novos, classifica o sentimento e sugere respostas prontas
4. **Métricas e relatórios** — acompanha o desempenho dos posts publicados e gera relatórios diários

**Regra de ouro:** tudo que a Camila produz passa por **aprovação humana obrigatória**. O robô está tecnicamente impossibilitado de escrever nas redes sociais — só lê e sugere.

---

## 2. Onde o trabalho acontece — Action Center

O operador usa apenas **uma tela**: o **Action Center** do UiPath Automation Cloud.

- **Acesso pelo computador:** `https://cloud.uipath.com` → login → organização `clarituz` → **Action Center** → aba **Actions/Inbox**
- **Acesso pelo celular:** abrir o mesmo endereço no navegador (a tela é responsiva). Para facilitar: no Chrome ou Safari, use **"Adicionar à tela inicial"** — vira um ícone como se fosse um app
- **Notificações:** o Action Center pode avisar por e-mail quando chegam tarefas novas (configurável no tenant)

### 2.1. Tarefa "Aprovar post"

Chega cada vez que a Camila prepara um conteúdo. A tarefa mostra:

- Plataforma de destino (Instagram ou Facebook)
- Tema da pauta e objetivo
- **Legenda** gerada (editável)
- **Hashtags** sugeridas
- Link da **mídia** aprovada no bucket
- Janela de horário sugerida
- Contador de posts do dia (informativo)

**O que fazer:**
1. Revisar o conteúdo — pode **editar a legenda** no próprio formulário
2. Escolher a decisão: **Aprovar** ou **Rejeitar** (com motivo)
3. Se aprovado: **publicar manualmente** no Instagram/Facebook como faria normalmente
4. **Colar a URL do post publicado** no campo indicado da tarefa — é assim que a Camila encontra o post para acompanhar as métricas
5. Concluir a tarefa

⏱ **Prazo:** tarefas expiram em **48 horas**. Expirada, o item é descartado com segurança.

### 2.2. Tarefa "Moderar comentário"

Chega a cada comentário novo detectado. Mostra:

- Autor e texto do comentário
- **Sentimento classificado**: Positivo, Neutro, Negativo, Crise ou Spam
- **Resposta sugerida** pronta no tom de voz da marca (editável)
- No caso de **Spam**: sugestão de ocultação

**O que fazer:** ir até a rede social e responder/ocultar como decidir — a tarefa é apenas a sugestão da IA com todo o contexto. Comentários **Negativos** e **Crise** chegam com **prioridade alta** no topo da lista.

---

## 3. Agenda automática

Nenhuma ação é necessária para iniciar os processos — os agendadores disparam sozinhos:

| Quando | O que acontece |
|---|---|
| **Segunda-feira 06h30** | IA monta o calendário editorial da semana |
| **Todo dia 06h00** | Agenda as coletas de métricas dos posts monitorados |
| **Todo dia 07h00** | Gera os pacotes de publicação das pautas do dia |
| **A cada 30 minutos** | Lê comentários novos e cria sugestões de resposta |
| **Após publicação** | Métricas coletadas automaticamente (primeira coleta ~24h depois) |

Os relatórios diários e o calendário ficam arquivados na nuvem (pasta Shared → Storage Buckets → `SM_Metricas`) para consulta histórica.

---

## 4. Configuração inicial (uma vez, parte técnica)

> Esta seção é executada pelo responsável técnico na implantação — o operador não precisa dela no dia a dia.

### 4.1. Pré-requisitos na Meta

- Conta Instagram **Business ou Creator** vinculada a uma página do Facebook
- Meta Business Account (`business.facebook.com`)
- App Meta criado em `developers.facebook.com` com produto **Instagram Graph API**

### 4.2. Token da Meta (`Meta_SystemUserToken`)

**Opção A — token definitivo (recomendado, não expira):**

1. `business.facebook.com` → Business Settings → **Usuários → Usuários do sistema** → criar usuário (Admin)
2. **Gerar novo token** → selecionar o app → marcar **"nunca expirar"**
3. Escopos necessários (todos de **leitura**): `instagram_basic`, `instagram_manage_comments`, `instagram_manage_insights`, `pages_read_engagement`, `pages_show_list`, `business_management`
4. Em **Adicionar ativos**: conceder acesso à conta IG, à página FB e ao app

**Opção B — token de teste (~2h de validade):** Graph API Explorer → `developers.facebook.com/tools/explorer` → selecionar o app → Generate Access Token com os mesmos escopos.

**Onde colocar:** Orchestrator → pasta **Shared** → Assets → `Meta_SystemUserToken` → campo **Password**.

### 4.3. IDs das contas (`SM_Config`)

Descoberta via Graph API Explorer:

```
GET /me/accounts                                          → fb_page_id (id da página)
GET /{fb_page_id}?fields=instagram_business_account       → ig_user_id
```

Editar o asset `SM_Config` (pasta Shared):

```json
{
  "ig_user_id": "1784xxxxxxxxxxxxx",
  "fb_page_id": "10xxxxxxxxxxxxx",
  "llm_model": "gpt-4o",
  "calendario_editorial": {},
  "termos_proibidos": ["termo1", "termo2"],
  "janelas_sugeridas": "09:00-12:00;18:00-21:00",
  "limite_posts_dia": 3,
  "dias_calendario": 7
}
```

### 4.4. Demais itens (já configurados na implantação)

- **Conexão GenAI:** Integration Service → pasta Shared → UiPath GenAI Activities — status `Enabled` ✅
- **Runtime:** pasta Shared usa robô **Serverless** (nuvem UiPath) — nenhuma máquina local necessária ✅
- **Mídias:** imagens/vídeos aprovados vão no bucket `SM_Midia` (jpeg, jpg, png, mp4)

### 4.5. Geração de imagem por IA (opcional)

A Camila pode **gerar a arte do post automaticamente**: o LLM produz o briefing visual (`mediaPrompt`) e a OpenAI Images transforma em imagem real, que sobe para o bucket `SM_Midia` (pasta `geradas/`) e aparece pronta na tarefa de aprovação.

**Como ativar (uma vez):**

1. Crie uma API key na OpenAI: `platform.openai.com` → API keys → `sk-...` (requer conta com billing)
2. No Orchestrator: pasta **Shared** → Assets → `OpenAI_ApiKey` → Edit → campo **Password** = a chave
3. No asset `SM_Config`, ligue a feature:
   ```json
   "gerar_imagem": true,
   "imagem_modelo": "gpt-image-1",
   "imagem_tamanho": "1024x1024",
   "imagem_qualidade": "high",
   "imagem_qa": true
   ```

**Detalhes:**

- **Modelos suportados:** `gpt-image-1` (padrão — melhor qualidade da OpenAI), `dall-e-3` (~US$0.04/imagem), `dall-e-2` (mais barato). Troque em `imagem_modelo`.
- **Qualidade:** `imagem_qualidade` controla o parâmetro `quality` da API — `gpt-image-1` usa `high` (padrão)/`medium`/`low`; `dall-e-3` mapeia para `hd`/`standard`.
- **Tamanhos:** `1024x1024` (feed quadrado), `1792x1024` (horizontal), `1024x1792` (stories/reels).
- **Quando NÃO gera:** se o item já trouxer uma `MediaUrl` (mídia fornecida manualmente tem prioridade) ou se `gerar_imagem=false`.
- **Formato automático:** se a pauta do calendário for `reels` ou `stories`, a arte sai vertical **1024×1792** (9:16); `feed`/`carrossel` usam o `imagem_tamanho` configurado.
- **QA visual (`imagem_qa`, padrão `true`):** após gerar a arte, um modelo de visão (gpt-4o-mini) "olha" a imagem e verifica se bate com o briefing, se há texto ilegível, artefatos deformados, marca d'água ou violação dos `termos_proibidos`. Se reprovar, a Camila **regenera uma vez automaticamente** antes de enviar para aprovação. O veredito aparece no log e no campo `NotaImagemQA` do conteúdo — o humano continua sendo a decisão final.
- **Se falhar** (sem key, sem crédito, timeout): o post segue o fluxo normal e o operador anexa a mídia manualmente na aprovação — o `mediaPrompt` continua visível como briefing.
- Custo: cada imagem gera uma cobrança na conta OpenAI — monitore o billing.

---

## 5. Personalização do comportamento

| Parâmetro | Onde | Efeito |
|---|---|---|
| `limite_posts_dia` | `SM_Config` | Máximo de posts sugeridos por dia (padrão 3) |
| `janelas_sugeridas` | `SM_Config` | Horários recomendados exibidos na aprovação |
| `termos_proibidos` | `SM_Config` | Palavras que bloqueiam o conteúdo antes da aprovação |
| `dias_calendario` | `SM_Config` | Horizonte do calendário editorial (padrão 7 dias) |
| `llm_model` | `SM_Config` | Modelo de IA usado para texto (padrão gpt-4o) |
| `gerar_imagem` | `SM_Config` | Liga/desliga geração automática de arte (`true` = gera com gpt-image-1) |
| `imagem_modelo` | `SM_Config` | Modelo OpenAI de imagem: `gpt-image-1` (padrão, alta qualidade), `dall-e-3`, `dall-e-2` |
| `imagem_tamanho` | `SM_Config` | Resolução: `1024x1024` (feed), `1792x1024`, `1024x1792` (stories) |
| `imagem_qualidade` | `SM_Config` | `high` (padrão)/`medium`/`low` no gpt-image-1; `hd`/`standard` no dall-e-3 |
| `imagem_qa` | `SM_Config` | `true` (padrão) = visão avalia a arte e regenera 1× se reprovada; `false` = sobe direto |
| Nicho / tom de voz | Argumento do processo | Define o contexto da campanha; pode variar por execução |

---

## 6. Garantias de segurança

- **Zero escrita nas redes:** verificado por teste automatizado que varre o código — não existe nenhuma chamada de publicação, resposta ou exclusão
- **Credenciais criptografadas** no Orchestrator; nunca em logs ou no código
- **Aprovação obrigatória:** não existe caminho no robô que publique sem um humano
- **Auditoria completa:** cada item processado, aprovado, rejeitado ou com falha fica registrado
- **Falhas isoladas:** erro em um item não derruba os demais; falhas temporárias de rede têm retry automático

---

## 7. Qualidade comprovada

Suíte de testes automatizados executada na versão 1.0.8: **30/30 verificações — 100% de acerto** (detalhes em `relatorio-qa-testes.md`), incluindo compliance de conteúdo, classificação de sentimento, resolução de URLs, tolerância das respostas da IA e a garantia zero-escrita.

---

## 8. Resolução de problemas

| Sintoma | Provável causa | O que fazer |
|---|---|---|
| Tarefas não chegam no Action Center | Token Meta ausente/vencido, ou `ig_user_id` incorreto | Conferir assets `Meta_SystemUserToken` e `SM_Config`; ver logs do job no Orchestrator |
| Job falha no health check | Token inválido ou sem escopos | Regenerar token com os escopos de leitura listados em 4.2 |
| Pautas não seguem o calendário | Calendário ainda não gerado | Normal — na ausência dele, a Camila gera pautas sob demanda; na próxima segunda o calendário é montado |
| Tarefa expirou | Não respondida em 48h | Normal — item descartado; nova pauta será gerada no ciclo seguinte |
| Métricas zeradas para um post | URL colada incorreta na aprovação | A URL precisa ser o link público do post (ex: `instagram.com/p/...`) |

**Onde ver logs:** Orchestrator → pasta Shared → Jobs → clique no job → aba Logs. Cada execução registra o que foi feito e, em caso de erro, a causa exata.

---

## 9. Glossário rápido

| Termo | Significado |
|---|---|
| **Action Center** | Tela onde humanos aprovam/executam o que o robô sugere |
| **Pauta** | Tema de post planejado (do calendário editorial) |
| **Pacote de publicação** | Legenda + hashtags + mídia + horário sugerido, prontos para revisão |
| **Zero-write** | Garantia técnica de que o robô nunca escreve nas redes |
| **Serverless** | Execução na nuvem UiPath — sem servidor/computador dedicado |
