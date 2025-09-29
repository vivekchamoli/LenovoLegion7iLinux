using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ReactiveUI;

namespace LenovoLegionToolkit.Avalonia.ViewModels
{
    public abstract class ViewModelBase : ReactiveObject, INotifyPropertyChanged
    {
        private bool _isLoading;
        private string _title = string.Empty;
        private string _statusMessage = string.Empty;
        private bool _hasError;
        private string _errorMessage = string.Empty;

        public bool IsLoading
        {
            get => _isLoading;
            set => this.RaiseAndSetIfChanged(ref _isLoading, value);
        }

        public string Title
        {
            get => _title;
            set => this.RaiseAndSetIfChanged(ref _title, value);
        }

        public string StatusMessage
        {
            get => _statusMessage;
            set => this.RaiseAndSetIfChanged(ref _statusMessage, value);
        }

        public bool HasError
        {
            get => _hasError;
            set => this.RaiseAndSetIfChanged(ref _hasError, value);
        }

        public string ErrorMessage
        {
            get => _errorMessage;
            set => this.RaiseAndSetIfChanged(ref _errorMessage, value);
        }

        protected virtual void SetError(string message)
        {
            ErrorMessage = message;
            HasError = !string.IsNullOrEmpty(message);
        }

        protected virtual void ClearError()
        {
            ErrorMessage = string.Empty;
            HasError = false;
        }

        protected virtual void SetStatus(string message)
        {
            StatusMessage = message;
        }

        public new event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        public virtual void Initialize()
        {
        }

        public virtual void Cleanup()
        {
        }
    }
}