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
            ["Facebook"] = 63206,
            ["LinkedIn"] = 3000
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
- Hashtags relevantes ao nicho (sem # na palavra, retorne só os termos)
- Um prompt de mídia descrevendo a imagem/vídeo ideal

Responda APENAS com JSON válido, sem markdown, no formato:
{{""legenda"": ""..."", ""hashtags"": [""..."", ""...""], ""mediaPrompt"": ""...""}}";
        }

        public static ConteudoGerado ParsearConteudoGerado(string respostaLlm)
        {
            var json = JObject.Parse(ExtrairJson(respostaLlm));
            return new ConteudoGerado
            {
                Legenda = json["legenda"]?.ToString() ?? "",
                Hashtags = json["hashtags"]?.ToObject<string[]>() ?? Array.Empty<string>(),
                MediaPrompt = json["mediaPrompt"]?.ToString() ?? "",
                LegendaEditada = null,
                PostUrl = null
            };
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
