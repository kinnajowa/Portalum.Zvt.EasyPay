using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;

namespace Portalum.Zvt.EasyPay;

public partial class LicenseWindow : Window
{
    private ILoggerFactory _loggerFactory;
    private ILogger _logger;

    public LicenseWindow(ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<LicenseWindow>();
        InitializeComponent();
        _logger.LogInformation($"{nameof(LicenseWindow)} - Start License selection");
    }
    
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        Application.Current.Shutdown(-5);
    }

    private void ButtonFileChooser_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Multiselect = false;
        dialog.Filter = "License File (*.lic)|*.lic";
        if (dialog.ShowDialog() ?? false)
        {
            this.TextBoxFilePath.Text = dialog.FileName;
        }
    }

    private void ButtonConfirm_OnClick(object sender, RoutedEventArgs e)
    {
        var xmlFile = TextBoxFilePath.Text;
        var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EasyPay");
        var licensePath = Path.Combine(dataPath, "license.lic");
        try
        {
            File.Copy(xmlFile, licensePath);
        }
        catch (Exception ex)
        {
            _logger.LogError($"{nameof(LicenseWindow)} - Can't copy license file: {ex.Message}");
            Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown(-6));
        }
        _logger.LogInformation($"{nameof(LicenseWindow)} - License file copied. Restart Application");
        this.Close();
    }
}