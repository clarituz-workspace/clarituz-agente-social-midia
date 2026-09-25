# Camila — Relatório de Funcionamento

**Cargo:** Social Media — especialista em campanhas estratégicas de marketing de alta conversão para diversos nichos
**Plataformas:** Instagram e Facebook (via API oficial da Meta)
**Tecnologia:** UiPath Automation Cloud + Inteligência Artificial generativa (LLM)

---

## O que a Camila faz

A Camila é uma assistente automatizada que trabalha em três frentes do social media, **sempre sob supervisão humana**:

### 1. Geração de conteúdo (todo dia, 07h)

- **A própria Camila monta o calendário editorial**: toda segunda-feira às 06h30, a IA planeja os posts da semana inteira — distribuídos por dia e plataforma, alternando formatos (feed, reels, carrossel, stories) e objetivos (educar, engajar, converter, prova social, oferta)
- Todo dia, ela pega **as pautas programadas para hoje** no calendário e produz o **pacote completo de publicação**: legenda otimizada, hashtags, sugestão de mídia e janela de horário recomendada
- Se o calendário não existir ou o dia estiver livre, ela gera pautas sob demanda — o processo nunca fica parado
- Antes de chegar ao humano, todo conteúdo passa por **validação automática de compliance**: tamanho da legenda, limite de hashtags, mídia presente, formato de arquivo válido e lista de **termos proibidos** (palavras que a marca não quer associadas)

### 2. Aprovação humana obrigatória — o robô nunca publica

Ponto central do design: **a Camila não publica nada automaticamente**.

- Cada post vira uma **tarefa de aprovação** que aparece no Action Center (pode ser acessado pelo navegador ou celular)
- A tarefa mostra o pacote completo: plataforma, tema, legenda, hashtags, link da mídia e o horário sugerido
- O responsável pode **aprovar, editar a legenda ou rejeitar** (com motivo)
- Se ninguém responder em **48 horas**, a tarefa expira e o item é descartado com segurança
- Após aprovar, **o próprio humano publica** no Instagram/Facebook e cola a URL do post na tarefa — a Camila usa essa URL apenas para localizar o post e acompanhar as métricas

### 3. Monitoramento de comentários (a cada 30 minutos)

- Lê **automaticamente** os comentários novos em posts do Instagram e Facebook
- A IA classifica o **sentimento** de cada comentário: Positivo, Neutro, Negativo, Crise ou Spam
- Gera uma **resposta sugerida** no tom de voz configurado e cria uma tarefa para o humano
- Comentários **Negativos ou de Crise** vão com **prioridade alta**; **Spam** vem com sugestão de ocultação
- Novamente: **quem responde, oculta ou ignora é sempre o humano** — a Camila só sugere

### 4. Métricas e relatórios (diário, 06h) + Power BI em tempo real

- Após cada publicação, a Camila agenda coletas automáticas de desempenho
- Consolida curtidas, comentários, compartilhamentos, alcance e impressões em relatórios diários
- Tudo fica arquivado em armazenamento na nuvem UiPath, disponível para consulta histórica
- **Integração com Power BI em tempo real:** no momento em que cada métrica é coletada, a Camila envia a linha direto para um dashboard do Power BI — os números aparecem no painel segundos depois da coleta, sem esperar o relatório do dia seguinte
- **Painel sempre vivo:** alcance, impressões, curtidas, comentários, salvamentos, cliques e data da coleta por post/plataforma
- **À prova de falhas:** se o Power BI estiver fora do ar, a métrica continua gravada normalmente no armazenamento da UiPath — nenhum dado se perde e o relatório diário não é afetado

---

## Garantias de segurança

| Garantia | Como é implementada |
|---|---|
| O robô **nunca escreve** nas redes sociais | Verificado por teste automatizado que escaneia todo o código — zero chamadas de publicação, resposta ou exclusão |
| Credenciais protegidas | Token da Meta fica criptografado no Orchestrator; nunca aparece em logs ou no código |
| Nada publica sem humano | Arquitetura exige aprovação no Action Center — é impossível o fluxo publicar sozinho |
| Falhas não acumulam | Itens com erro de negócio são descartados com registro; falhas técnicas têm retry automático controlado |
| Auditoria completa | Cada item processado, aprovado, rejeitado ou falho fica registrado na fila e em relatórios diários |

---

## Como funciona na prática (dia a dia)

```
Segunda 06h30 ─ IA monta o calendário editorial da semana (temas por dia e plataforma)

07h00 ─ Camila pega as pautas de hoje no calendário
        └─→ Para cada pauta: gera legenda + hashtags → valida → cria tarefa de aprovação

A cada 30min ─ Lê comentários novos no IG/FB
        └─→ Classifica sentimento → cria tarefa com resposta sugerida (prioridade se negativo)

Humano ─ Abre o Action Center → revisa → aprova/edita/rejeita → publica manualmente → cola a URL

+24h ─ Camila coleta as métricas do post publicado
        └─→ envia a linha na hora para o dashboard do Power BI (tempo real)

06h00 ─ Relatório diário consolidado: o que foi gerado, aprovado, publicado e os resultados
```

## Como o cliente acessa e opera

A operação diária acontece em **um único lugar: o Action Center** do UiPath Automation Cloud (`cloud.uipath.com` → organização → Action Center → Actions). O acesso funciona de **qualquer navegador, inclusive no celular** — basta abrir o site, fazer login e salvar o atalho na tela inicial (Chrome/Safari → "Adicionar à tela inicial"). O tenant também pode notificar por e-mail quando chegam tarefas novas.

**O que aparece para o operador:**

| Tarefa | Quando chega | O que fazer |
|---|---|---|
| **Aprovar post** | Após cada conteúdo gerado | Revisar legenda, hashtags e mídia → editar se quiser → aprovar → publicar no Instagram/Facebook → colar a URL do post na tarefa |
| **Moderar comentário** | A cada comentário novo (30min) | Ler comentário + resposta sugerida → responder na rede social (ou ocultar se spam) → concluir a tarefa |

Comentários **Negativos/Crise** chegam com prioridade alta. Nenhuma ação técnica é exigida — os agendadores disparam sozinhos; o operador só revisa, decide e executa na rede social. Relatórios diários ficam arquivados em nuvem (bucket `SM_Metricas`) para consulta.

## Limites configuráveis

- **Horizonte do calendário editorial** (padrão: 7 dias) — ajustável por configuração
- **Limite de posts por dia** (padrão: 3) — a tarefa de aprovação informa o contador atual
- **Janelas de publicação sugeridas** (padrão: 09h–12h e 18h–21h)
- **Termos proibidos** — lista editável, verificação sem distinção de maiúsculas
- **Tom de voz e nicho** — passados por execução, permitem campanhas diferentes por cliente/produto

## Qualidade assegurada

- **6 testes automatizados** cobrindo validação de conteúdo, classificação de sentimento, resolução de URLs, tolerância de erros da IA e a garantia zero-escrita — todos executados e aprovados antes da publicação
- Execução monitorada por **agendadores dedicados** na nuvem, com alertas em caso de falha
