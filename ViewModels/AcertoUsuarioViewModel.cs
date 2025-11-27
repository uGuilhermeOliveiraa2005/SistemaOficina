using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SistemaOficina.ViewModels
{
    public class AcertoUsuarioViewModel : INotifyPropertyChanged
    {
        public string Username { get; set; }

        private double _expensesToReimburse;
        public double ExpensesToReimburse
        {
            get => _expensesToReimburse;
            set { _expensesToReimburse = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalToReceive)); }
        }

        private double _percentage;
        public double Percentage
        {
            get => _percentage;
            set { _percentage = value; OnPropertyChanged(); }
        }

        private double _profitShare;
        public double ProfitShare
        {
            get => _profitShare;
            set { _profitShare = value; OnPropertyChanged(); OnPropertyChanged(nameof(TotalToReceive)); }
        }

        public double TotalToReceive => ExpensesToReimburse + ProfitShare;

        public event PropertyChangedEventHandler PropertyChanged;
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}