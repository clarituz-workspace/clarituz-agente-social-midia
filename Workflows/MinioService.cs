using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Newtonsoft.Json.Linq;

namespace clarituz_agente_social_midia
{
    // Cliente isolado para MinIO/S3 — object store dos dados estruturais da Camila.
    // Substitui o padrão jsonl read-modify-write por objetos imutáveis por chave
    // (posts/{postId}.json, metricas/{data}/{postId}.json): PUT atômico, sem leitura
    // prévia — elimina a race condition entre jobs e a leitura de arquivo inteiro.
    // Ativo apenas quando SM_Config.minio_endpoint está configurado; caso contrário
    // os workflows usam o caminho legado (UiPath Storage Bucket).
    public static class MinioService
    {
        public static bool Configurado(JObject cfg)
            => !string.IsNullOrWhiteSpace(cfg?["minio_endpoint"]?.ToString());

        private static string Bucket(JObject cfg)
        {
            var b = cfg?["minio_bucket"]?.ToString();
            return string.IsNullOrWhiteSpace(b) ? "camila" : b;
        }

        private static AmazonS3Client Cliente(JObject cfg, string accessKey, string secretKey)
            => new AmazonS3Client(accessKey, secretKey, new AmazonS3Config
            {
                ServiceURL = cfg["minio_endpoint"].ToString(),
                ForcePathStyle = true // obrigatório para MinIO (virtual-hosted style é AWS-only por padrão)
            });

        // Garante que o bucket existe (idempotente) — deployment zero-touch.
        private static async Task GarantirBucketAsync(AmazonS3Client s3, JObject cfg)
        {
            var bucket = Bucket(cfg);
            if (await Amazon.S3.Util.AmazonS3Util.DoesS3BucketExistV2Async(s3, bucket).ConfigureAwait(false)) return;
            try { await s3.PutBucketAsync(bucket).ConfigureAwait(false); }
            catch (AmazonS3Exception) { /* corrida de criação ou sem permissão — segue */ }
        }

        // PUT idempotente: mesma chave = mesmo objeto — reescrita é upsert natural.
        public static async Task PutJsonAsync(JObject cfg, string accessKey, string secretKey, string chave, JObject obj)
        {
            using (var s3 = Cliente(cfg, accessKey, secretKey))
            {
                await GarantirBucketAsync(s3, cfg).ConfigureAwait(false);
                await s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = Bucket(cfg),
                    Key = chave,
                    ContentBody = (obj ?? new JObject()).ToString(Newtonsoft.Json.Formatting.None),
                    ContentType = "application/json"
                }).ConfigureAwait(false);
            }
        }

        public static async Task<string> GetTextoAsync(JObject cfg, string accessKey, string secretKey, string chave)
        {
            try
            {
                using (var s3 = Cliente(cfg, accessKey, secretKey))
                {
                    var resp = await s3.GetObjectAsync(Bucket(cfg), chave).ConfigureAwait(false);
                    using (var r = new StreamReader(resp.ResponseStream))
                        return await r.ReadToEndAsync().ConfigureAwait(false);
                }
            }
            catch (AmazonS3Exception ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public static async Task<JObject> GetJsonAsync(JObject cfg, string accessKey, string secretKey, string chave)
        {
            var texto = await GetTextoAsync(cfg, accessKey, secretKey, chave).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(texto)) return null;
            try { return JObject.Parse(texto); } catch { return null; }
        }

        // Lista todas as chaves sob um prefixo (paginação automática — sem limite de 1000).
        public static async Task<List<string>> ListarChavesAsync(JObject cfg, string accessKey, string secretKey, string prefixo)
        {
            var chaves = new List<string>();
            using (var s3 = Cliente(cfg, accessKey, secretKey))
            {
                await GarantirBucketAsync(s3, cfg).ConfigureAwait(false);
                string token = null;
                do
                {
                    var resp = await s3.ListObjectsV2Async(new ListObjectsV2Request
                    {
                        BucketName = Bucket(cfg),
                        Prefix = prefixo,
                        ContinuationToken = token
                    }).ConfigureAwait(false);

                    foreach (var o in resp.S3Objects)
                        chaves.Add(o.Key);
                    token = resp.IsTruncated ? resp.NextContinuationToken : null;
                } while (token != null);
            }
            return chaves;
        }

        // Busca todos os objetos JSON sob um prefixo (list + get por chave).
        public static async Task<List<JObject>> ListarJsonsAsync(JObject cfg, string accessKey, string secretKey, string prefixo)
        {
            var lista = new List<JObject>();
            foreach (var chave in await ListarChavesAsync(cfg, accessKey, secretKey, prefixo).ConfigureAwait(false))
            {
                var obj = await GetJsonAsync(cfg, accessKey, secretKey, chave).ConfigureAwait(false);
                if (obj != null) lista.Add(obj);
            }
            return lista;
        }

        // Conveniência: monta jsonl a partir de lista de objetos — mantém as
        // assinaturas existentes (ResumirDesempenho etc.) sem mudança de formato.
        public static string ParaJsonl(IEnumerable<JObject> objetos)
        {
            var sb = new System.Text.StringBuilder();
            foreach (var o in objetos ?? new List<JObject>())
                sb.AppendLine(o.ToString(Newtonsoft.Json.Formatting.None));
            return sb.ToString();
        }
    }
}
