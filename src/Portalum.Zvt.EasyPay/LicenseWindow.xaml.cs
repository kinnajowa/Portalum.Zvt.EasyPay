using System;
using System.IO;
using System.Windows;
using Microsoft.Extensions.FileSystemGlobbing.Internal.PathSegments;
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
    }
    
    private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
    {
        Application.Current.Shutdown(-5);
    }

    private void ButtonFileChooser_OnClick(object sender, RoutedEventArgs e)
    {
        OpenFileDialog dialog = new OpenFileDialog();
        dialog.Multiselect = false;
        dialog.Filter = "License File (*.xml)|*.xml";
        if (dialog.ShowDialog() ?? false)
        {
            this.TextBoxFilePath.Text = dialog.FileName;
        } 
    }

    private void ButtonConfirm_OnClick(object sender, RoutedEventArgs e)
    {
        var xmlFile = TextBoxFilePath.Text;
        var dataPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData), "EasyPay");
        var licensePath = Path.Combine(dataPath, "license.xml");
        File.Copy(xmlFile, licensePath);
        this.Close();
    }
}