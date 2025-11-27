using SistemaOficina.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SistemaOficina.ViewModels
{
    /// <summary>
    /// Contém todos os dados necessários para a janela de relatório do caixa.
    /// </summary>
    public class RelatorioCaixaViewModel
    {
        // #################### INÍCIO DA ALTERAÇÃO ####################
        public ConfiguracaoEmpresa Configuracao { get; set; }
        // #################### FIM DA ALTERAÇÃO ####################

        public List<LancamentoCaixa> LancamentosDoPeriodo { get; set; }
        public DateTime DataInicio { get; set; }
        public DateTime DataFim { get; set; }
        public double TotalEntradas { get; set; }
        public double TotalSaidas { get; set; }
        public double SaldoPeriodo => TotalEntradas - TotalSaidas;

        public string PeriodoRelatorio => $"Período de {DataInicio:dd/MM/yyyy} a {DataFim:dd/MM/yyyy}";
        public string DataGeracaoRelatorio => $"Gerado em: {DateTime.Now:dd/MM/yyyy HH:mm:ss}";


        public RelatorioCaixaViewModel()
        {
            LancamentosDoPeriodo = new List<LancamentoCaixa>();
            // #################### INÍCIO DA ALTERAÇÃO ####################
            Configuracao = new ConfiguracaoEmpresa(); // Inicializa para evitar erros de null reference
            // #################### FIM DA ALTERAÇÃO ####################
        }
    }
}