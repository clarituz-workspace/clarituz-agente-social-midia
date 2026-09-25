using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace clarituz_agente_social_midia
{
    public static class ConteudoService
    {
        public static readonly Dictionary<string, int> LimiteLegenda = new Dictionary<string, int>
        {
            ["Instagram"] = 2200,
            ["Facebook"] = 63206
        };

        public static string MontarPromptGeracao(WorkItemData item, string janelaSugerida)
        {
            var limite = LimiteLegenda.TryGetValue(item.Plataforma ?? "", out var l) ? l : 2200;
            return
$@"Você é Camila, social media especialista em campanhas estratégicas de marketing de alta conversão.

Gere um post para {item.Plataforma} com:
- Nicho da campanha: {item.Nicho}
- Tom de voz: {item.TomDeVoz}
- Tema/pauta: {item.Tema}
- Janela de publicação sugerida (informativa): {janelaSugerida}
- Legenda com no máximo {limite} caracteres (limite da plataforma)
- Uma legenda ALTERNATIVA (variante B) com abordagem diferente — ex.: se a A for educativa, a B pode ser mais direta/emocional; mesma regra de limite
- Hashtags relevantes ao nicho (sem # na palavra, retorne só os termos)
- Um prompt de mídia descrevendo a imagem/vídeo ideal

Responda APENAS com JSON válido, sem markdown, no formato:
{{""legenda"": ""..."", ""legendaAlternativa"": ""..."", ""hashtags"": [""..."", ""...""], ""mediaPrompt"": ""...""}}";
        }

        public static ConteudoGerado ParsearConteudoGerado(string respostaLlm)
        {
            var json = JObject.Parse(ExtrairJson(respostaLlm));
            return new ConteudoGerado
            {
                Legenda = json["legenda"]?.ToString() ?? "",
                LegendaAlternativa = json["legendaAlternativa"]?.ToString() ?? "",
                Hashtags = json["hashtags"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                MediaPrompt = json["mediaPrompt"]?.ToString() ?? "",
                LegendaEditada = null,
                PostUrl = null
            };
        }

        public static string MontarPromptPautas(string nicho, string tomDeVoz, int quantidade)
        {
            return
$@"Você é Camila, social media especialista em campanhas estratégicas de alta conversão.

Sugira {quantidade} pautas de posts para o nicho ""{nicho}"" com tom de voz ""{tomDeVoz}"".
Cada pauta deve ser específica, acionável e orientada a conversão (não temas genéricos).

Responda APENAS com JSON válido, sem markdown, no formato:
[{{""tema"": ""..."", ""mediaUrl"": null}}]";
        }

        public static JArray ParsearPautas(string respostaLlm)
        {
            if (string.IsNullOrWhiteSpace(respostaLlm))
                throw new FormatException("Resposta LLM vazia");
            var inicio = respostaLlm.IndexOf('[');
            var fim = respostaLlm.LastIndexOf(']');
            if (inicio < 0 || fim <= inicio)
                throw new FormatException("Resposta LLM não contém array JSON de pautas");
            return JArray.Parse(respostaLlm.Substring(inicio, fim - inicio + 1));
        }

        // desempenho: resumo de MetricasService.ResumirDesempenho (loop de aprendizado);
        // null/vazio = primeira semana, sem histórico.
        public static string MontarPromptCalendario(string nicho, string tomDeVoz, string[] plataformas, int dias,
            string desempenho = null)
        {
            var blocoDesempenho = string.IsNullOrWhiteSpace(desempenho) ? "" :
$@"
Desempenho recente dos posts já publicados (priorize temas/plataformas que engajam mais):
{desempenho}
";
            return
$@"Você é Camila, social media especialista em campanhas estratégicas de alta conversão.

Monte um calendário editorial para os próximos {dias} dias, começando hoje ({DateTime.UtcNow:yyyy-MM-dd}), para o nicho ""{nicho}"" com tom de voz ""{tomDeVoz}"".
Plataformas: {string.Join(", ", plataformas ?? new[] { "Instagram" })}.
{blocoDesempenho}
Regras:
- Distribua os posts ao longo dos dias — no máximo 1 post por dia por plataforma
- Alterne objetivos: educar, engajar, converter, prova social, oferta
- Alterne formatos quando fizer sentido: feed, reels, carrossel, stories
- Cada tema deve ser específico e acionável, orientado a conversão — nada genérico

Responda APENAS com JSON válido, sem markdown, no formato:
[{{""data"": ""yyyy-MM-dd"", ""tema"": ""..."", ""plataforma"": ""Instagram"", ""formato"": ""feed"", ""objetivo"": ""...""}}]";
        }

        // Parseia o calendário editorial gerado pela LLM. Cada entrada precisa de data + tema.
        public static JArray ParsearCalendario(string respostaLlm)
        {
            var arr = ParsearPautas(respostaLlm);
            foreach (var e in arr)
            {
                if (e["data"] == null || string.IsNullOrWhiteSpace(e["data"].ToString())
                    || e["tema"] == null || string.IsNullOrWhiteSpace(e["tema"].ToString()))
                    throw new FormatException("Entrada de calendário sem data/tema");
            }
            return arr;
        }

        // Entradas do calendário cuja data == hoje (UTC). Retorna array vazio se nada programado.
        public static JArray EntradasDoDia(JArray calendario, DateTime hojeUtc)
        {
            var hoje = hojeUtc.ToString("yyyy-MM-dd");
            var resultado = new JArray();
            if (calendario == null) return resultado;
            foreach (var e in calendario)
            {
                var data = e["data"] == null ? "" : e["data"].ToString();
                if (data.StartsWith(hoje)) resultado.Add(e);
            }
            return resultado;
        }

        // Validação de compliance BR-01..BR-05 — retorna lista de violações (vazia = aprovado).
        public static List<string> ValidarCompliance(string plataforma, string legenda, string[] hashtags,
            string mediaUrl, bool mediaExisteNoBucket, IEnumerable<string> termosProibidos)
        {
            var violacoes = new List<string>();
            var limite = LimiteLegenda.TryGetValue(plataforma ?? "", out var l) ? l : 2200;

            // BR-01 — limite de legenda
            if ((legenda ?? "").Length > limite)
                violacoes.Add($"BR-01: legenda com {(legenda ?? "").Length} chars excede limite {limite} ({plataforma})");

            // BR-02 — máximo de hashtags
            var maxHashtags = 30;
            var nHashtags = (hashtags ?? Array.Empty<string>()).Length;
            if (nHashtags > maxHashtags)
                violacoes.Add($"BR-02: {nHashtags} hashtags excede máximo {maxHashtags} ({plataforma})");

            // BR-03 — mídia obrigatória no Instagram
            if (plataforma == "Instagram" && (string.IsNullOrWhiteSpace(mediaUrl) || !mediaExisteNoBucket))
                violacoes.Add("BR-03: post de feed Instagram exige MediaUrl válida no bucket SM_Midia");

            // BR-04 — formato de mídia
            if (!string.IsNullOrWhiteSpace(mediaUrl))
            {
                var ext = mediaUrl.Contains('.')
                    ? mediaUrl.Substring(mediaUrl.LastIndexOf('.') + 1).ToLowerInvariant()
                    : "";
                if (ext != "jpeg" && ext != "jpg" && ext != "png" && ext != "mp4")
                    violacoes.Add($"BR-04: extensão de mídia '{ext}' não suportada (permitidas: jpeg, png, mp4)");
            }

            // BR-05 — termos proibidos
            if (termosProibidos != null)
            {
                var texto = ((legenda ?? "") + " " + string.Join(" ", hashtags ?? Array.Empty<string>())).ToLowerInvariant();
                foreach (var termo in termosProibidos)
                {
                    if (!string.IsNullOrWhiteSpace(termo) && texto.Contains(termo.ToLowerInvariant()))
                        violacoes.Add($"BR-05: termo proibido '{termo}' encontrado no conteúdo");
                }
            }

            return violacoes;
        }

        public static string MontarPromptSentimento(WorkItemData item)
        {
            return
$@"Você é Camila, social media. Classifique o comentário abaixo e sugira uma resposta.

Comentário de {item.CommentAutor} na plataforma {item.Plataforma}:
\""{item.CommentTexto}\""

Regras:
- sentimento: Positivo | Neutro | Negativo | Spam | Crise
- Crise = ameaça legal, denúncia grave, conteúdo que exige escalonamento imediato
- respostaSugerida: resposta curta no tom de voz {item.TomDeVoz}, em PT-BR; vazia se Spam
- O robô NUNCA publica a resposta — um humano decide e executa

Responda APENAS com JSON válido, sem markdown:
{{""sentimento"": ""..."", ""respostaSugerida"": ""...""}}";
        }

        // Mapeamento §6: Negativo/Crise → prioridade alta; Spam → sugere ocultar.
        public static SugestaoModeracao ParsearSugestao(string respostaLlm, string commentId)
        {
            var json = JObject.Parse(ExtrairJson(respostaLlm));
            var sentimento = json["sentimento"]?.ToString() ?? "Neutro";
            var eNegativoOuCrise = sentimento == "Negativo" || sentimento == "Crise";
            return new SugestaoModeracao
            {
                CommentId = commentId,
                Sentimento = sentimento,
                RespostaSugerida = json["respostaSugerida"]?.ToString() ?? "",
                SugereOcultar = sentimento == "Spam",
                PrioridadeAlta = eNegativoOuCrise
            };
        }

        // Remove fences ```json ... ``` e texto fora do objeto.
        private static string ExtrairJson(string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                throw new FormatException("Resposta LLM vazia");
            var inicio = texto.IndexOf('{');
            var fim = texto.LastIndexOf('}');
            if (inicio < 0 || fim <= inicio)
                throw new FormatException("Resposta LLM não contém JSON");
            return texto.Substring(inicio, fim - inicio + 1);
        }
    }
}
