using System;
using System.Collections.Generic;
using System.Linq;
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

        // Linha JSONL para append no arquivo diário do bucket SM_Metricas.
        public static string Serializar(MetricasPost metricas)
            => JsonConvert.SerializeObject(metricas);

        public static string NomeArquivoDiario(DateTime? data = null)
            => $"metricas-{(data ?? DateTime.UtcNow):yyyy-MM-dd}.jsonl";

        // Loop de aprendizado: junta posts-monitorados.jsonl (tema) com metricas-*.jsonl
        // (desempenho) e produz um resumo textual para o prompt do calendário editorial.
        // "" quando não há dados — o prompt simplesmente não ganha bloco de desempenho.
        public static string ResumirDesempenho(string monitoradosJsonl, string metricasJsonl)
        {
            var temas = new Dictionary<string, string>();
            foreach (var linha in Linhas(monitoradosJsonl))
            {
                try
                {
                    var o = JObject.Parse(linha);
                    var pid = o["postId"]?.ToString();
                    if (!string.IsNullOrEmpty(pid)) temas[pid] = o["tema"]?.ToString() ?? "";
                }
                catch { }
            }
            var ultimas = new Dictionary<string, MetricasPost>();
            foreach (var linha in Linhas(metricasJsonl))
            {
                try
                {
                    var m = JsonConvert.DeserializeObject<MetricasPost>(linha);
                    if (m == null || string.IsNullOrEmpty(m.PostId)) continue;
                    if (!ultimas.TryGetValue(m.PostId, out var ant) || m.ColetadoEm >= ant.ColetadoEm)
                        ultimas[m.PostId] = m;
                }
                catch { }
            }
            if (ultimas.Count == 0) return "";

            var posts = ultimas.Values.Select(m => new
            {
                Metricas = m,
                Tema = temas.TryGetValue(m.PostId, out var t) ? t ?? "" : "",
                Taxa = m.Alcance > 0 ? (m.Curtidas + m.Comentarios + m.Salvamentos) / (double)m.Alcance : 0.0
            }).ToList();

            var sb = new System.Text.StringBuilder();
            var melhores = posts.Where(p => p.Tema != "" && p.Taxa > 0)
                .OrderByDescending(p => p.Taxa).Take(5).ToList();
            if (melhores.Count > 0)
                sb.AppendLine("Temas com melhor engajamento recente: " +
                    string.Join("; ", melhores.Select(p =>
                        $"\"{p.Tema}\" ({p.Metricas.Plataforma}, eng. {p.Taxa:P1})")));
            var porPlat = posts.GroupBy(p => p.Metricas.Plataforma ?? "");
            sb.AppendLine("Média de engajamento por plataforma: " +
                string.Join("; ", porPlat.Select(g =>
                    $"{(string.IsNullOrEmpty(g.Key) ? "?" : g.Key)}: {g.Average(p => p.Taxa):P1} ({g.Count()} posts)")));
            return sb.ToString().Trim();
        }

        private static IEnumerable<string> Linhas(string jsonl)
            => (jsonl ?? "").Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0);

        private static int ToInt(JToken token)
        {
            if (token == null) return 0;
            return int.TryParse(token.ToString(), out var v) ? v : 0;
        }
    }
}
