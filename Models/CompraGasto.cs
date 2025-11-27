using System;

namespace SistemaOficina.Models
{
    public class CompraGasto
    {
        public int Id { get; set; }
        public DateTime Data { get; set; }
        public string Descricao { get; set; }
        public string Tipo { get; set; } // 'Compra', 'Gasto', 'ReceitaExtra'
        public double Valor { get; set; }
        public string Responsavel { get; set; }

        // #################### INÍCIO DA ALTERAÇÃO ####################
        // Nova propriedade para armazenar o número da OS associada.
        // Não será uma coluna na tabela ComprasGastos, apenas um campo para carregar dados da consulta.
        [System.ComponentModel.DataAnnotations.Schema.NotMapped]
        public string NumeroOSAssociada { get; set; }
        // #################### FIM DA ALTERAÇÃO ####################
    }
}