using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FamilyTransfer.Models
{
    public class SectionItem : INotifyPropertyChanged
    {
        private bool _isSelected;
        private string _name;
        private string _part;

        public string Name
        {
            get => _name;
            set { _name = value; OnPropertyChanged(); }
        }

        public string Part
        {
            get => _part;
            set { _part = value; OnPropertyChanged(); }
        }

        public string FullName => $"{Name} - {Part}";


        public bool IsSelected
        {
            get => _isSelected;
            set { _isSelected = value; OnPropertyChanged(); }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}