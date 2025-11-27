using System;

namespace SistemaOficina.Models
{
    public class LancamentoCaixa
    {
        public int Id { get; set; }
        public DateTime Data { get; set; }
        public string Descricao { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public double Valor { get; set; }
        public int? OrdemServicoId { get; set; }
        public int? CompraGastoId { get; set; }

        /// <summary>
        /// Propriedade que faltava: Armazena o ID do acerto ao qual este lançamento (gasto) foi associado.
        /// Se for nulo, significa que o gasto ainda não foi reembolsado em nenhum acerto.
        /// </summary>
        public int? AcertoId { get; set; }

        // Propriedades auxiliares que não existem na tabela, preenchidas por consulta ao banco
        public string? NumeroOSAssociada { get; set; }
        public string? NomeClienteAssociado { get; set; }
        public string? ResponsavelTransacao { get; set; }
    }
}