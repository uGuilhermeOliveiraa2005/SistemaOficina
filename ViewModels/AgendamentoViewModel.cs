using SistemaOficina.Models;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SistemaOficina.ViewModels
{
    public class AgendamentoViewModel : INotifyPropertyChanged
    {
        public Agendamento Agendamento { get; }

        private string _tempoRestante;
        public string TempoRestante
        {
            get => _tempoRestante;
            set
            {
                if (_tempoRestante != value)
                {
                    _tempoRestante = value;
                    OnPropertyChanged();
                }
            }
        }

        // Propriedades delegadas para fácil acesso no XAML
        public string NomeClienteDisplay => Agendamento.NomeClienteDisplay;
        public string Equipamento => Agendamento.Equipamento;
        public DateTime DataAgendamento => Agendamento.DataAgendamento;

        public AgendamentoViewModel(Agendamento agendamento)
        {
            Agendamento = agendamento;
            _tempoRestante = "Calculando...";
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}