using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace clarituz_agente_social_midia
{
    // Integração Power BI — streaming dataset (push URL).
    // Único ponto de escrita HTTP do projeto; destino exclusivo: api.powerbi.com.
    // Não escreve em nenhuma plataforma social — invariante zero-write preservada.
    public static class PowerBiService
    {
        private const string HostPermitido = "api.powerbi.com";

        // Converte MetricasPost na linha do streaming dataset.
        // Os nomes devem bater EXATAMENTE com os campos definidos no dataset do Power BI.
        public static JObject MontarLinha(MetricasPost m)
        {
            return new JObject
            {
                ["post_id"] = m.PostId ?? "",
                ["plataforma"] = m.Plataforma ?? "",
                ["alcance"] = m.Alcance,
                ["impressoes"] = m.Impressoes,
                ["curtidas"] = m.Curtidas,
                ["comentarios"] = m.Comentarios,
                ["salvamentos"] = m.Salvamentos,
                ["cliques"] = m.Cliques,
                ["coletado_em"] = m.ColetadoEm.ToString("yyyy-MM-ddTHH:mm:ssZ")
            };
        }

        // POST de uma linha no streaming dataset. Falha sobe para o caller tratar (warn).
        public static async Task PushLinhaAsync(string pushUrl, JObject linha)
        {
            if (string.IsNullOrWhiteSpace(pushUrl)) return;
            var uri = new Uri(pushUrl);
            if (!string.Equals(uri.Host, HostPermitido, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Push URL de métricas não é api.powerbi.com — recusado");

            var corpo = "[" + linha.ToString(Newtonsoft.Json.Formatting.None) + "]";
            using (var http = new HttpClient())
            using (var req = new HttpRequestMessage(HttpMethod.Post, uri))
            {
                req.Content = new StringContent(corpo, Encoding.UTF8, "application/json");
                var resp = await http.SendAsync(req).ConfigureAwait(false);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                    throw new SocialApiException((int)resp.StatusCode,
                        $"Power BI push falhou: {(int)resp.StatusCode} — {body?.Substring(0, Math.Min(body?.Length ?? 0, 200))}");
                }
            }
        }
    }
}
