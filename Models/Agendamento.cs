using System;
using System.ComponentModel;

namespace SistemaOficina.Models
{
    public class Agendamento : INotifyPropertyChanged
    {
        public int Id { get; set; }
        public int? ClienteId { get; set; }
        public string? NomeClienteAvulso { get; set; }
        public string Equipamento { get; set; }
        public DateTime DataAgendamento { get; set; }
        public string? Endereco { get; set; }
        public string? Numero { get; set; }
        public string? Bairro { get; set; }
        public string? Cidade { get; set; }

        // #################### INÍCIO DA ALTERAÇÃO ####################
        public string? UF { get; set; }
        public string? CEP { get; set; }
        // #################### FIM DA ALTERAÇÃO ####################

        private string _status;
        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                }
            }
        }

        public string? NomeClienteCache { get; set; }
        public string NomeClienteDisplay => ClienteId.HasValue ? NomeClienteCache : NomeClienteAvulso;

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}