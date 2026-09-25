using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace clarituz_agente_social_midia
{
    // Normalização e serialização de métricas (SDD §3 step 10, §5 MetricasPost).
    public static class MetricasService
    {
        public static MetricasPost Normalizar(string plataforma, string postId, JToken dados)
        {
            switch (plataforma)
            {
                case "Instagram": return NormalizarInstagram(postId, dados);
                case "Facebook": return NormalizarFacebook(postId, dados);
                case "LinkedIn": return NormalizarLinkedIn(postId, dados);
                default: throw new ArgumentException($"Plataforma desconhecida: {plataforma}");
            }
        }

        // IG insights: { data: [ { name, values: [{value}] } ] } ou { data: [ { name, value } ] }
        private static MetricasPost NormalizarInstagram(string postId, JToken dados)
        {
            int Metrica(string nome)
            {
                foreach (var m in dados?["data"] ?? new JArray())
                {
                    if (m["name"]?.ToString() == nome)
                    {
                        var v = m["values"]?[0]?["value"] ?? m["total_value"]?["value"] ?? m["value"];
                        return ToInt(v);
                    }
                }
                return 0;
            }
            return new MetricasPost
            {
                PostId = postId,
                Plataforma = "Instagram",
                Alcance = Metrica("reach"),
                Impressoes = Metrica("impressions"),
                Curtidas = Metrica("likes"),
                Comentarios = Metrica("comments"),
                Salvamentos = Metrica("saved"),
                Cliques = Metrica("total_interactions"),
                ColetadoEm = DateTime.UtcNow
            };
        }

        // FB: objeto combinado { campos: {likes.summary, comments.summary, shares}, insights: {data:[...]} }
        private static MetricasPost NormalizarFacebook(string postId, JToken dados)
        {
            var campos = dados?["campos"];
            int Metrica(string nome)
            {
                foreach (var m in dados?["insights"]?["data"] ?? new JArray())
                {
                    if (m["name"]?.ToString() == nome)
                        return ToInt(m["values"]?[0]?["value"] ?? m["value"]);
                }
                return 0;
            }
            return new MetricasPost
            {
                PostId = postId,
                Plataforma = "Facebook",
                Alcance = Metrica("post_impressions_unique"),
                Impressoes = Metrica("post_impressions"),
                Curtidas = ToInt(campos?["likes"]?["summary"]?["total_count"]),
                Comentarios = ToInt(campos?["comments"]?["summary"]?["total_count"]),
                Salvamentos = 0,
                Cliques = Metrica("post_clicks"),
                ColetadoEm = DateTime.UtcNow
            };
        }

        // LI: { elements: [ { totalShareStatistics: { impressionCount, likeCount, commentCount, clickCount, shareCount, engagement } } ] }
        private static MetricasPost NormalizarLinkedIn(string postId, JToken dados)
        {
            var stats = dados?["elements"]?[0]?["totalShareStatistics"];
            return new MetricasPost
            {
                PostId = postId,
                Plataforma = "LinkedIn",
                Alcance = ToInt(stats?["uniqueImpressionsCount"]),
                Impressoes = ToInt(stats?["impressionCount"]),
                Curtidas = ToInt(stats?["likeCount"]),
                Comentarios = ToInt(stats?["commentCount"]),
                Salvamentos = 0,
                Cliques = ToInt(stats?["clickCount"]),
                ColetadoEm = DateTime.UtcNow
            };
        }

        // Linha JSONL para append no arquivo diário do bucket SM_Metricas.
        public static string Serializar(MetricasPost metricas)
            => JsonConvert.SerializeObject(metricas);

        public static string NomeArquivoDiario(DateTime? data = null)
            => $"metricas-{(data ?? DateTime.UtcNow):yyyy-MM-dd}.jsonl";

        private static int ToInt(JToken token)
        {
            if (token == null) return 0;
            return int.TryParse(token.ToString(), out var v) ? v : 0;
        }
    }
}
