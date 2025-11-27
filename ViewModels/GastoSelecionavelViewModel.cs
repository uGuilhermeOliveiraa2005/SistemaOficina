using SistemaOficina.Models;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SistemaOficina.ViewModels
{
    public class GastoSelecionavelViewModel : INotifyPropertyChanged
    {
        public LancamentoCaixa Lancamento { get; }

        private bool _isSelected;
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                _isSelected = value;
                OnPropertyChanged();
            }
        }

        public GastoSelecionavelViewModel(LancamentoCaixa lancamento)
        {
            Lancamento = lancamento;
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}