using System;
using System.Net.Http;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace clarituz_agente_social_midia
{
    // Erro tipado para respostas HTTP não-2xx (E1/E2 do SDD §8).
    public class SocialApiException : Exception
    {
        public int StatusCode { get; }
        public int RetryAfterSegundos { get; }
        public bool EhRateLimit => StatusCode == 429;
        public bool EhTemporario => EhRateLimit || StatusCode >= 500 || StatusCode == 0;

        public SocialApiException(int statusCode, string message, int retryAfterSegundos = 0)
            : base(message)
        {
            StatusCode = statusCode;
            RetryAfterSegundos = retryAfterSegundos;
        }
    }

    // Cliente SOMENTE-LEITURA para Meta Graph API e LinkedIn API (SDD §6/§9).
    // Guarda estrutural do invariante zero-write: qualquer método ≠ GET lança exceção.
    public class SocialApiClient : IDisposable
    {
        private const string MetaBase = "https://graph.facebook.com/v21.0";
        private const string LinkedInBase = "https://api.linkedin.com/v2";
        private static readonly TimeSpan[] Backoff = { TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(32) };

        private readonly HttpClient _http;
        private readonly string _metaToken;
        private readonly string _linkedInToken;

        public SocialApiClient(string metaToken, string linkedInToken, int timeoutSegundos = 30)
        {
            _metaToken = metaToken;
            _linkedInToken = linkedInToken;
            _http = new HttpClient { Timeout = TimeSpan.FromSeconds(timeoutSegundos) };
        }

        // Overload para receber credenciais de Orchestrator sem materializar texto em XAML (ST-SEC-009).
        public SocialApiClient(System.Security.SecureString metaToken, System.Security.SecureString linkedInToken, int timeoutSegundos = 30)
            : this(SecureParaTexto(metaToken), SecureParaTexto(linkedInToken), timeoutSegundos) { }

        private static string SecureParaTexto(System.Security.SecureString seguro)
            => seguro == null ? null : new System.Net.NetworkCredential(string.Empty, seguro).Password;

        public void Dispose() => _http?.Dispose();

        // ── Health check (InitAllApplications — fail-fast BR-10/E4) ──

        public async Task<JToken> HealthCheckMetaAsync()
            => await GetMetaAsync("/me");

        public async Task<JToken> HealthCheckLinkedInAsync()
            => await GetLinkedInAsync("/me");

        // ── Comentários (Monitorar_Comentarios — somente leitura) ──

        public async Task<JToken> ObterComentariosMetaAsync(string mediaOuPostId)
            => await GetMetaAsync($"/{mediaOuPostId}/comments?fields=id,text,username,timestamp&limit=100");

        public async Task<JToken> ObterComentariosLinkedInAsync(string shareUrn)
            => await GetLinkedInAsync($"/socialActions/{Uri.EscapeDataString(shareUrn)}/comments");

        // ── Métricas (Coletar_Metricas — somente leitura) ──

        public async Task<JToken> ObterInsightsInstagramAsync(string mediaId)
            => await GetMetaAsync($"/{mediaId}/insights?metric=reach,impressions,saved,likes,comments,total_interactions");

        public async Task<JToken> ObterInsightsFacebookAsync(string postId)
        {
            var campos = await GetMetaAsync($"/{postId}?fields=likes.summary(true),comments.summary(true),shares");
            var insights = await GetMetaAsync($"/{postId}/insights?metric=post_impressions,post_impressions_unique,post_clicks");
            return new JObject
            {
                ["campos"] = campos,
                ["insights"] = insights
            };
        }

        public async Task<JToken> ObterEstatisticasLinkedInAsync(string organizationalEntityUrn)
            => await GetLinkedInAsync($"/organizationalEntityShareStatistics?q=organizationalEntity&organizationalEntity={Uri.EscapeDataString(organizationalEntityUrn)}");

        // ── Resolução de post_id a partir da URL colada pelo humano (B9) ──

        // Instagram: extrai o shortcode de /p/ ou /reel/ e casa com o permalink no feed do ig_user.
        public async Task<JToken> ResolverMediaIdInstagramAsync(string postUrl, string igUserId)
        {
            var shortcode = ExtrairShortcodeInstagram(postUrl);
            if (string.IsNullOrEmpty(shortcode))
                throw new SocialApiException(0, $"URL do Instagram sem shortcode: {postUrl}");

            string proximo = $"/{igUserId}/media?fields=id,permalink&limit=100";
            for (var pagina = 0; pagina < 3 && proximo != null; pagina++)
            {
                var feed = proximo.StartsWith("http")
                    ? await GetRawAsync(proximo, _metaToken)
                    : await GetMetaAsync(proximo);

                foreach (var m in feed["data"] ?? new JArray())
                {
                    var permalink = m["permalink"]?.ToString() ?? "";
                    if (permalink.Contains("/" + shortcode + "/") || permalink.TrimEnd('/').EndsWith("/" + shortcode))
                        return m;
                }
                proximo = feed["paging"]?["next"]?.ToString();
            }
            throw new SocialApiException(404, $"Post não encontrado para o shortcode '{shortcode}' em ig_user {igUserId}");
        }

        // Facebook: extrai o identificador de posts/NNN, pfbid ou story_fbid.
        public string ResolverPostIdFacebook(string postUrl)
        {
            var m = System.Text.RegularExpressions.Regex.Match(postUrl, @"(?:posts/|pfbid|story_fbid=)([\w]+)");
            if (!m.Success)
                throw new SocialApiException(0, $"URL do Facebook sem identificador de post: {postUrl}");
            return m.Groups[1].Value;
        }

        // LinkedIn: extrai urn de activity/ugcPost/share na URL do post.
        public string ResolverUrnLinkedIn(string postUrl)
        {
            var m = System.Text.RegularExpressions.Regex.Match(postUrl, @"(activity|ugcPost|share)[-:](\d+)");
            if (!m.Success)
                throw new SocialApiException(0, $"URL do LinkedIn sem URN de post: {postUrl}");
            var tipo = m.Groups[1].Value;
            return $"urn:li:{(tipo == "activity" ? "activity" : tipo == "share" ? "share" : "ugcPost")}:{m.Groups[2].Value}";
        }

        public static string ExtrairShortcodeInstagram(string url)
        {
            var m = System.Text.RegularExpressions.Regex.Match(url ?? "", @"instagram\.com/(?:p|reel|reels)/([\w\-]+)");
            return m.Success ? m.Groups[1].Value : null;
        }

        // ── Núcleo HTTP — read-only + retry E1/E2 ──

        private Task<JToken> GetMetaAsync(string path)
            => GetRawAsync(MetaBase + path, _metaToken);

        private Task<JToken> GetLinkedInAsync(string path)
            => GetRawAsync(LinkedInBase + path, _linkedInToken);

        private async Task<JToken> GetRawAsync(string url, string bearerToken)
        {
            Exception ultimoErro = null;
            for (var tentativa = 0; tentativa <= Backoff.Length; tentativa++)
            {
                try
                {
                    using (var req = new HttpRequestMessage(HttpMethod.Get, url))
                    {
                        if (!string.IsNullOrEmpty(bearerToken))
                            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", bearerToken);

                        using (var resp = await _http.SendAsync(req).ConfigureAwait(false))
                        {
                            var corpo = await resp.Content.ReadAsStringAsync().ConfigureAwait(false);
                            var status = (int)resp.StatusCode;

                            if (resp.IsSuccessStatusCode)
                                return string.IsNullOrWhiteSpace(corpo) ? new JObject() : JToken.Parse(corpo);

                            var retryAfter = resp.Headers.RetryAfter?.Delta?.TotalSeconds
                                ?? resp.Headers.RetryAfter?.Date?.Subtract(DateTimeOffset.UtcNow).TotalSeconds
                                ?? 0;

                            if ((status == 429 || status >= 500) && tentativa < Backoff.Length)
                            {
                                var espera = retryAfter > 0 ? TimeSpan.FromSeconds(Math.Min(retryAfter, 60)) : Backoff[tentativa];
                                await Task.Delay(espera).ConfigureAwait(false);
                                continue;
                            }
                            throw new SocialApiException(status, $"HTTP {status} em GET {url}: {(corpo?.Length > 300 ? corpo.Substring(0, 300) : corpo)}", (int)retryAfter);
                        }
                    }
                }
                catch (SocialApiException) { throw; }
                catch (Exception ex) when (tentativa < Backoff.Length)
                {
                    ultimoErro = ex; // timeout/rede → backoff exponencial
                    await Task.Delay(Backoff[tentativa]).ConfigureAwait(false);
                }
            }
            throw new SocialApiException(0, $"Falha de transporte em GET {url}: {ultimoErro?.Message}");
        }
    }
}
