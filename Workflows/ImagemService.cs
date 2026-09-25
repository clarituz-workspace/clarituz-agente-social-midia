using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace clarituz_agente_social_midia
{
    // Integração OpenAI Images — gera a arte do post a partir do mediaPrompt do LLM.
    // Único destino permitido: api.openai.com. Não escreve em nenhuma plataforma social.
    public static class ImagemService
    {
        private const string HostPermitido = "api.openai.com";
        private const string EndpointGeracao = "https://api.openai.com/v1/images/generations";
        private const string EndpointChat = "https://api.openai.com/v1/chat/completions";
        private const string ModeloVisao = "gpt-4o-mini";

        // QA visual: um modelo com visão pontua a arte gerada em rubrica estruturada
        // (0–10 por critério); o veredito é computado aqui — o modelo não decide sozinho.
        // Retorna { "aprovada": bool, "motivo": "...", "aderencia": n, "legibilidade": n,
        //           "artefatos": n, "seguranca": n }.
        public static async Task<JObject> AvaliarImagemAsync(
            System.Security.SecureString apiKey,
            byte[] imagemBytes,
            string briefing,
            string termosProibidos)
        {
            var regras =
                "Avalie a imagem como QA de social media, dando nota de 0 a 10 em cada criterio: " +
                "aderencia (a imagem corresponde ao briefing?), " +
                "legibilidade (texto na imagem esta legivel e sem erros?), " +
                "artefatos (10 = sem defeitos; 0 = rostos/maos/bordas quebrados), " +
                "seguranca (10 = limpa; reduza por marca d'agua, logo de marca famosa, ou violacao destes termos: " +
                (termosProibidos ?? "nenhum") + "). " +
                "Briefing: " + briefing + ". " +
                "Responda APENAS JSON: {\"aderencia\": 0-10, \"legibilidade\": 0-10, \"artefatos\": 0-10, \"seguranca\": 0-10, \"motivo\": \"frase curta\"}";

            var corpo = new JObject
            {
                ["model"] = ModeloVisao,
                ["response_format"] = new JObject { ["type"] = "json_object" },
                ["messages"] = new JArray
                {
                    new JObject
                    {
                        ["role"] = "user",
                        ["content"] = new JArray
                        {
                            new JObject { ["type"] = "text", ["text"] = regras },
                            new JObject
                            {
                                ["type"] = "image_url",
                                ["image_url"] = new JObject
                                {
                                    ["url"] = "data:image/png;base64," + Convert.ToBase64String(imagemBytes)
                                }
                            }
                        }
                    }
                }
            };

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) })
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", SecureParaTexto(apiKey));
                using (var req = new HttpRequestMessage(HttpMethod.Post, new Uri(EndpointChat)))
                {
                    req.Content = new StringContent(
                        corpo.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                    var resp = await http.SendAsync(req).ConfigureAwait(false);
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                        throw new SocialApiException((int)resp.StatusCode,
                            $"OpenAI visao falhou: {(int)resp.StatusCode} — {body?.Substring(0, Math.Min(body?.Length ?? 0, 300))}");

                    var texto = JObject.Parse(body)?["choices"]?[0]?["message"]?["content"]?.ToString();
                    var notas = JObject.Parse(texto);

                    // Veredito derivado da rubrica — critério objetivo, não delegado ao modelo.
                    var aprovada =
                        Nota(notas, "aderencia") >= 6 &&
                        Nota(notas, "legibilidade") >= 6 &&
                        Nota(notas, "artefatos") >= 6 &&
                        Nota(notas, "seguranca") >= 8;
                    notas["aprovada"] = aprovada;
                    return notas;
                }
            }
        }

        private static int Nota(JObject notas, string criterio)
        {
            int n;
            return notas?[criterio] != null && int.TryParse(notas[criterio].ToString(), out n)
                ? Math.Max(0, Math.Min(10, n)) : 0;
        }

        // Gera uma imagem e retorna os bytes PNG.
        // modelos suportados: gpt-image-1 (default, alta qualidade), dall-e-3, dall-e-2.
        public static async Task<byte[]> GerarImagemAsync(
            System.Security.SecureString apiKey,
            string prompt,
            string modelo,
            string tamanho,
            string qualidade)
        {
            if (string.IsNullOrWhiteSpace(prompt))
                throw new ArgumentException("mediaPrompt vazio — nada a gerar");

            var model = string.IsNullOrWhiteSpace(modelo) ? "gpt-image-1" : modelo;
            var corpo = new JObject
            {
                ["model"] = model,
                ["prompt"] = prompt,
                ["n"] = 1,
                ["size"] = string.IsNullOrWhiteSpace(tamanho) ? "1024x1024" : tamanho
            };

            if (model.StartsWith("dall-e"))
            {
                // dall-e retorna URL por padrão; pedir b64 para uniformizar. gpt-image-1 já devolve b64.
                corpo["response_format"] = "b64_json";
                // dall-e-3 aceita quality "standard"|"hd"
                if (model == "dall-e-3")
                    corpo["quality"] = qualidade == "standard" ? "standard" : "hd";
            }
            else if (model.StartsWith("gpt-image"))
            {
                // gpt-image aceita quality "low"|"medium"|"high"|"auto"
                corpo["quality"] = qualidade == "low" || qualidade == "medium" || qualidade == "auto"
                    ? qualidade : "high";
            }

            using (var http = new HttpClient { Timeout = TimeSpan.FromSeconds(120) })
            {
                http.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", SecureParaTexto(apiKey));

                using (var req = new HttpRequestMessage(HttpMethod.Post, new Uri(EndpointGeracao)))
                {
                    req.Content = new StringContent(
                        corpo.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json");
                    var resp = await http.SendAsync(req).ConfigureAwait(false);
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    if (!resp.IsSuccessStatusCode)
                        throw new SocialApiException((int)resp.StatusCode,
                            $"OpenAI images falhou: {(int)resp.StatusCode} — {body?.Substring(0, Math.Min(body?.Length ?? 0, 300))}");

                    var dados = JObject.Parse(body)?["data"];
                    var item = dados != null && dados.HasValues ? dados[0] : null;
                    var b64 = item?["b64_json"]?.ToString();
                    if (!string.IsNullOrEmpty(b64))
                        return Convert.FromBase64String(b64);

                    var url = item?["url"]?.ToString();
                    if (!string.IsNullOrEmpty(url))
                    {
                        var uri = new Uri(url);
                        if (!uri.Host.EndsWith("openai.com", StringComparison.OrdinalIgnoreCase)
                            && !uri.Host.EndsWith("oaidalleapiprodscus.blob.core.windows.net", StringComparison.OrdinalIgnoreCase))
                            throw new InvalidOperationException("URL de imagem fora dos hosts esperados — recusado");
                        return await http.GetByteArrayAsync(uri).ConfigureAwait(false);
                    }
                    throw new FormatException("Resposta da OpenAI sem imagem (nem b64_json nem url)");
                }
            }
        }

        private static string SecureParaTexto(System.Security.SecureString seguro)
            => seguro == null ? null : new System.Net.NetworkCredential(string.Empty, seguro).Password;
    }
}
