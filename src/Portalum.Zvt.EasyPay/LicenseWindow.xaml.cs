using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Portalum.Zvt.EasyPay.Models;
using Portalum.Zvt.EasyPay.Services;

namespace Portalum.Zvt.EasyPay;

public partial class LicenseWindow
{
    private readonly ILogger<LicenseWindow> _logger;
    private readonly LicenseService _licenseService;

    public LicenseWindow(ILogger<LicenseWindow> logger, LicenseService licenseService)
    {
        _licenseService = licenseService;
        _logger = logger;
        InitializeComponent();
        _logger.LogInformation($"{nameof(LicenseWindow)} - Start License selection");
        UpdateStatus("Select valid license", StatusType.Information);
    }
    
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        Application.Current.Shutdown(-5);
    }
    
    private void UpdateStatus(string status, StatusType statusType)
    {
        this.LabelStatus.Dispatcher.Invoke(() =>
        {
            var brushForeground = Brushes.White;
            var brushBackground = Brushes.Transparent;

            if (statusType == StatusType.Error)
            {
                brushForeground = new SolidColorBrush(Color.FromRgb(255, 21, 21));
                brushBackground = Brushes.White;
            }

            this.LabelStatus.Foreground = brushForeground;
            this.LabelStatus.Background = brushBackground;


            this.LabelStatus.Content = status;
        });
    }


    private void ButtonFileChooser_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Multiselect = false,
            Filter = "License File (*.lic)|*.lic"
        };
        if (dialog.ShowDialog() ?? false)
        {
            this.TextBoxFilePath.Text = dialog.FileName;
        }
    }

    private void ButtonConfirm_OnClick(object sender, RoutedEventArgs e)
    {
        var xmlFile = TextBoxFilePath.Text;
        if (string.IsNullOrEmpty(xmlFile))
        {
            UpdateStatus("No file specified", StatusType.Error);
            return;
        }
        
        try
        {
            _licenseService.SubmitLicense(xmlFile);
            _logger.LogInformation($"{nameof(LicenseWindow)} - License file copied.");
            if (_licenseService.LicenseValid()) Close();
            else
            {
                UpdateStatus("License not valid", StatusType.Error);
            }
        }
        catch (Exception ex)
        {
            UpdateStatus(ex.Message, StatusType.Error);
        }
    }
}