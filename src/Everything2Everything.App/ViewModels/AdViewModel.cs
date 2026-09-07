using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Everything2Everything.Core.Ads;

namespace Everything2Everything.App.ViewModels;

public class AdViewModel : INotifyPropertyChanged
{
    private readonly IAdService _adService;
    private AdItem? _currentBannerAd;
    private AdItem? _currentLargeAd;
    private bool _isBannerVisible = true;
    private bool _isLargeCardVisible = true;

    public event PropertyChangedEventHandler? PropertyChanged;

    public AdItem? CurrentBannerAd
    {
        get => _currentBannerAd;
        set { _currentBannerAd = value; OnPropertyChanged(); }
    }

    public AdItem? CurrentLargeAd
    {
        get => _currentLargeAd;
        set { _currentLargeAd = value; OnPropertyChanged(); }
    }

    public bool IsBannerVisible
    {
        get => _isBannerVisible;
        set { _isBannerVisible = value; OnPropertyChanged(); }
    }

    public bool IsLargeCardVisible
    {
        get => _isLargeCardVisible;
        set { _isLargeCardVisible = value; OnPropertyChanged(); }
    }

    public ICommand ClickBannerAdCommand { get; }
    public ICommand ClickLargeAdCommand { get; }
    public ICommand DismissBannerCommand { get; }
    public ICommand DismissLargeCardCommand { get; }

    public AdViewModel(IAdService adService)
    {
        _adService = adService ?? throw new ArgumentNullException(nameof(adService));

        CurrentBannerAd = _adService.GetNextBannerAd();
        CurrentLargeAd = _adService.GetNextLargeCardAd();

        ClickBannerAdCommand = new AdCommand(_ =>
        {
            if (CurrentBannerAd != null)
                _adService.OpenAdUrl(CurrentBannerAd);
        });

        ClickLargeAdCommand = new AdCommand(_ =>
        {
            if (CurrentLargeAd != null)
                _adService.OpenAdUrl(CurrentLargeAd);
        });

        DismissBannerCommand = new AdCommand(_ => IsBannerVisible = false);
        DismissLargeCardCommand = new AdCommand(_ => IsLargeCardVisible = false);
    }

    public void RotateBannerAd()
    {
        CurrentBannerAd = _adService.GetNextBannerAd();
    }

    public void RotateLargeCardAd()
    {
        CurrentLargeAd = _adService.GetNextLargeCardAd();
    }

    protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private sealed class AdCommand : ICommand
    {
        private readonly Action<object?> _execute;
        private readonly Func<object?, bool>? _canExecute;

        public AdCommand(Action<object?> execute, Func<object?, bool>? canExecute = null)
        {
            _execute = execute;
            _canExecute = canExecute;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object? parameter) => _execute(parameter);

        public event EventHandler? CanExecuteChanged
        {
            add { }
            remove { }
        }
    }
}
