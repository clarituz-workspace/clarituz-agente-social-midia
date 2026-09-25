using System;
using System.Collections.Generic;

namespace clarituz_agente_social_midia
{
    // SDD §5 — Option A: records para dados de transação/imutáveis, class para estado mutável.

    public enum TipoTransacao { GerarConteudo, ModerarComentario, ColetarMetricas }
    public enum Plataforma { Instagram, Facebook }
    public enum Sentimento { Positivo, Neutro, Negativo, Spam, Crise }
    public enum AcaoModeracao { SugerirResposta, SugerirRespostaPrioritaria, SugerirOcultar }
    public enum DecisaoAprovacao { Aprovado, Editado, Rejeitado }

    public record WorkItemData
    {
        public string TipoTransacao { get; init; }   // GerarConteudo | ModerarComentario | ColetarMetricas
        public string Plataforma { get; init; }       // Instagram | Facebook
        public string Nicho { get; init; }
        public string TomDeVoz { get; init; }
        public string Tema { get; init; }             // pauta (GerarConteudo)
        public string MediaUrl { get; init; }
        public string CommentId { get; init; }        // ModerarComentario
        public string CommentTexto { get; init; }
        public string CommentAutor { get; init; }
        public string PostId { get; init; }           // ColetarMetricas — resolvido da URL
        public string PostUrl { get; init; }          // informado pelo humano na aprovação
        public string PostPermalink { get; init; }
    }

    public record ConteudoGerado
    {
        public string Legenda { get; init; }
        public string[] Hashtags { get; init; }
        public string MediaPrompt { get; init; }
        public string LegendaEditada { get; init; }   // preenchido pós-aprovação se editado
        public string PostUrl { get; init; }          // informado pelo humano ao aprovar
    }

    public record SugestaoModeracao
    {
        public string CommentId { get; init; }
        public string Sentimento { get; init; }
        public string RespostaSugerida { get; init; }
        public bool SugereOcultar { get; init; }
        public bool PrioridadeAlta { get; init; }     // Negativo/Crise
    }

    public record MetricasPost
    {
        public string PostId { get; init; }
        public string Plataforma { get; init; }
        public int Alcance { get; init; }
        public int Impressoes { get; init; }
        public int Curtidas { get; init; }
        public int Comentarios { get; init; }
        public int Salvamentos { get; init; }
        public int Cliques { get; init; }
        public DateTime ColetadoEm { get; init; }
    }

    public class ExecucaoContext
    {
        public Dictionary<string, string> Config { get; set; } = new Dictionary<string, string>();
        public int PostsPublicadosHoje { get; set; }
        public List<string> Alertas { get; set; } = new List<string>();
        public int Processados { get; set; }
        public int Falhos { get; set; }
    }
}
