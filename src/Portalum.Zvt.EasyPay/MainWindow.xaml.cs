using System.Threading;
using Microsoft.Extensions.Logging;
using Portalum.Zvt.EasyPay.Models;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace Portalum.Zvt.EasyPay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly PaymentTerminalConfig _paymentTerminalConfig;

        private readonly ILogger<MainWindow> _logger;
        private TcpNetworkDeviceCommunication _deviceCommunication;
        private ZvtClient? _zvtClient;
        private readonly CancellationTokenSource _tokenSource;

        public MainWindow(
            ILoggerFactory loggerFactory,
            PaymentTerminalConfig paymentTerminalConfig)
        {
            this._loggerFactory = loggerFactory;
            this._paymentTerminalConfig = paymentTerminalConfig;

            this._logger = loggerFactory.CreateLogger<MainWindow>();

            this.InitializeComponent();
            this.UpdateStatus("Preparing...", StatusType.Information);
            this.LabelAmount.Content = $"{paymentTerminalConfig.Amount:C2}";

            switch (paymentTerminalConfig.TransactionType)
            {
                case TransactionType.Payment:
                    this.LabelTransactionType.Content = "Payment";
                    this.LabelTransactionDetails.Content = "Amount:";
                    break;
                case TransactionType.Reversal:
                    this.LabelTransactionType.Content = "Reversal";
                    this.LabelTransactionDetails.Content = $"Receipt no.: {paymentTerminalConfig.ReceiptNumber}";
                    break;
                default:
                    break;
            }

            _tokenSource = new CancellationTokenSource();

            _ = Task.Run(async () => await InitTransaction(paymentTerminalConfig));

        }

        private async Task InitTransaction(PaymentTerminalConfig paymentTerminalConfig)
        {
            var zvtClientConfig = new ZvtClientConfig
            {
                Encoding = ZvtEncoding.CodePage437,
                Language = Zvt.Language.German,
                Password = _paymentTerminalConfig.Password
            };

            var deviceCommunicationLogger = this._loggerFactory.CreateLogger<TcpNetworkDeviceCommunication>();
            var zvtClientLogger = this._loggerFactory.CreateLogger<ZvtClient>();

            _deviceCommunication = new TcpNetworkDeviceCommunication(
                this._paymentTerminalConfig.IpAddress,
                port: this._paymentTerminalConfig.Port,
                enableKeepAlive: false,
                logger: deviceCommunicationLogger);

            this.UpdateStatus("Connect to payment terminal...", StatusType.Information);

            if (!await _deviceCommunication.ConnectAsync(_tokenSource.Token))
            {
                this.UpdateStatus("Cannot connect to payment terminal", StatusType.Error);
                await Task.Delay(3000);

                this._logger.LogError($"{nameof(StartPaymentAsync)} - Cannot connect to {this._paymentTerminalConfig.IpAddress}:{this._paymentTerminalConfig.Port}");
                Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(-4); });
                return;
            }
            
            _zvtClient = new ZvtClient(_deviceCommunication, logger: zvtClientLogger, clientConfig: zvtClientConfig);
            _zvtClient.IntermediateStatusInformationReceived += this.IntermediateStatusInformationReceived;


            var task = () => { Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown(-5)); };

            switch (paymentTerminalConfig.TransactionType)
            {
                case TransactionType.Payment:
                    task = async () => await this.StartPaymentAsync(paymentTerminalConfig.Amount);
                    break;
                case TransactionType.Reversal:
                    task = async () =>
                        await this.StartReversalAsync(paymentTerminalConfig.ReceiptNumber);
                    break;
            }
            
            _ = Task.Run(task);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_zvtClient != null)
            {
                _zvtClient.IntermediateStatusInformationReceived -= this.IntermediateStatusInformationReceived;
                _zvtClient.Dispose();
            }

            _deviceCommunication.Dispose();
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

        private async Task StartReversalAsync(int receiptNo)
        {
            this._logger.LogInformation($"{nameof(StartReversalAsync)} - Start");
            
            try
            {
                var response = await _zvtClient.ReversalAsync(receiptNo, _tokenSource.Token);
                if (response.State == CommandResponseState.Successful)
                {
                    DisableAbortButtonAsync();
                    
                    this._logger.LogInformation($"{nameof(StartReversalAsync)} - Successful");

                    this.UpdateStatus("Reversal successful", StatusType.Information);
                    await Task.Delay(1000);

                    Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(0); });
                    return;
                }

                this._logger.LogInformation($"{nameof(StartReversalAsync)} - Not successful");

                this.UpdateStatus("Reversal not successful", StatusType.Error);
                await Task.Delay(1000);

                Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(-1); });
            }
            finally
            {
            }
        }

        private async Task StartPaymentAsync(decimal amount)
        {
            this._logger.LogInformation($"{nameof(StartPaymentAsync)} - Start");

            try
            {

                var response = await _zvtClient.PaymentAsync(amount, _tokenSource.Token);
                this._logger.LogInformation($"{nameof(StartPaymentAsync)} - Aborted");
                if (_tokenSource.IsCancellationRequested) return;
                
                if (response.State == CommandResponseState.Successful)
                {
                    DisableAbortButtonAsync();
                    
                    this._logger.LogInformation($"{nameof(StartPaymentAsync)} - Successful");

                    this.UpdateStatus("Payment successful", StatusType.Information);
                    await Task.Delay(1000000);

                    Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(0); });
                    return;
                }

                this._logger.LogInformation($"{nameof(StartPaymentAsync)} - Not successful");

                this.UpdateStatus("Payment not successful", StatusType.Error);
                await Task.Delay(1000);

                Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(-1); });
            } finally {}
        }

        private void IntermediateStatusInformationReceived(string status)
        {
            this.UpdateStatus(status, StatusType.Information);
        }

        private void ButtonBase_OnClick(object sender, RoutedEventArgs e)
        {
            DisableAbortButton();
            
            Task.Run(async () =>
            { 
                _logger.LogInformation($"AbortTransaction - Abort current transaction");
                if (_zvtClient != null)
                {
                    var res = await _zvtClient.AbortAsync();
                    if (res.State == CommandResponseState.Successful)
                    {
                        _tokenSource.Cancel();
                        UpdateStatus("Transaction Aborted", StatusType.Information);
                        _logger.LogInformation("AbortTransaction - transaction aborted");
                    }
                }
                else
                {
                    UpdateStatus("Could not abort transaction. Try manually.", StatusType.Error);
                    _logger.LogError("AbortTransaction - could not abort transaction");
                }
                Thread.Sleep(2000);
                Application.Current.Dispatcher.Invoke(Application.Current.Shutdown);
            });
        }

        private void DisableAbortButtonAsync()
        {
            Application.Current.Dispatcher.Invoke(DisableAbortButton);
        }
        private void DisableAbortButton()
        {
            ButtonAbortTransaction.IsEnabled = false;
            ButtonAbortTransaction.Background = Brushes.LightGray;
        }
        
    }
}
