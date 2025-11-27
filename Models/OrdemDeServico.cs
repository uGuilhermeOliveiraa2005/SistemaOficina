using System;

namespace SistemaOficina.Models
{
    public class OrdemDeServico
    {
        public int Id { get; set; }
        public string NumeroOS { get; set; } = string.Empty;
        public int? ClienteId { get; set; }
        public string? NomeClienteOS { get; set; }
        public string? MarcaEquipamento { get; set; }
        public string? ModeloEquipamento { get; set; }
        public string? NumeroSerie { get; set; }
        public string DefeitoReclamado { get; set; } = string.Empty;
        public string? DefeitosVisiveis { get; set; }
        public string? DefeitoReal { get; set; }
        public string? ServicoExecutado { get; set; }
        public double Valor { get; set; }
        public string? TecnicoResponsavel { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime DataAbertura { get; set; }
        public DateTime? DataFechamento { get; set; }
        public string Aparelho { get; set; } = string.Empty;
        public string? Garantia { get; set; }
        public string? Voltagem { get; set; }
        public string? Cor { get; set; }
        public string? Departamento { get; set; }
        public string? Complemento { get; set; }
        public string? Observacao { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string? NomeClienteCache { get; set; }

        public string? NomeClienteDisplay
        {
            get
            {
                return !string.IsNullOrEmpty(NomeClienteCache) ? NomeClienteCache : NomeClienteOS;
            }
        }

        /// <summary>
        /// Propriedade ajustada para incluir o tipo de aparelho, a marca e o modelo,
        /// fornecendo uma descrição mais completa.
        /// </summary>
        public string EquipamentoDisplay => $"{Aparelho} {MarcaEquipamento} {ModeloEquipamento}".Trim().Replace("  ", " ");
    }
}