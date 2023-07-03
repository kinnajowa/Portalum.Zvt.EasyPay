using System.Threading;
using Microsoft.Extensions.Logging;
using Portalum.Zvt.EasyPay.Models;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Portalum.Zvt.Models;
using Serilog.Extensions.Logging;

namespace Portalum.Zvt.EasyPay
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly TransactionConfig _paymentTerminalConfig;

        private readonly ILogger<MainWindow> _logger;
        private readonly ILogger<ZvtClient> _zvtLogger;
        private readonly ILogger<TcpNetworkDeviceCommunication> _tcpLogger;
        private TcpNetworkDeviceCommunication _deviceCommunication;
        private ZvtClient? _zvtClient;
        private readonly CancellationTokenSource _tokenSource;
        private readonly ResultService _resultService;

        public MainWindow(ILogger<MainWindow> logger, ILogger<ZvtClient> zvtLogger, ILogger<TcpNetworkDeviceCommunication> tcpLogger, ConfigurationService configurationService, ResultService resultService)
        {
            _paymentTerminalConfig = configurationService.GetConfiguration();

            _logger = logger;
            _zvtLogger = zvtLogger;
            _tcpLogger = tcpLogger;
            _resultService = resultService;

            InitializeComponent();
            UpdateStatus("Preparing...", StatusType.Information);
            

            switch (_paymentTerminalConfig.Funktion)
            {
                case TransactionType.Payment:
                    LabelTransactionType.Content = "Payment";
                    LabelTransactionDetails.Content = "Amount:";
                    LabelAmount.Content = $"{_paymentTerminalConfig.Betrag:C2}";
                    break;
                case TransactionType.ReversalLastPayment:
                    LabelTransactionType.Content = "Reversal";
                    LabelTransactionDetails.Content = $"Receipt no.: {_paymentTerminalConfig.StornoBelegNr}";
                    LabelAmount.Content = $"{_paymentTerminalConfig.StornoBetrag:C2}";
                    break;
            }

            _tokenSource = new CancellationTokenSource();

            if (configurationService.GetConfiguration().Betrag == 0)
            {
                _logger.LogError("Amount cannot be 0");
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown(-2));
                return;
            }
            
            _ = Task.Run(async () => await InitTransaction(_paymentTerminalConfig));

        }

        private async Task InitTransaction(TransactionConfig paymentTerminalConfig)
        {
            var zvtClientConfig = new ZvtClientConfig
            {
                Encoding = ZvtEncoding.CodePage437,
                Language = Zvt.Language.German,
                Password = int.Parse(_paymentTerminalConfig.Passwort)
            };

            _deviceCommunication = new TcpNetworkDeviceCommunication(
                this._paymentTerminalConfig.IP,
                port: this._paymentTerminalConfig.Port,
                enableKeepAlive: false,
                logger: _tcpLogger);

            this.UpdateStatus("Connect to payment terminal...", StatusType.Information);

            if (!await _deviceCommunication.ConnectAsync(_tokenSource.Token))
            {
                this.UpdateStatus("Cannot connect to payment terminal", StatusType.Error);
                await Task.Delay(3000);

                this._logger.LogError($"{nameof(InitTransaction)} - Cannot connect to {_paymentTerminalConfig.IP}:{_paymentTerminalConfig.Port}");
                Application.Current.Dispatcher.Invoke(() => { Application.Current.Shutdown(-4); });
                return;
            }
            
            
            _zvtClient = new ZvtClient(_deviceCommunication, logger: _zvtLogger, clientConfig: zvtClientConfig);
            _zvtClient.IntermediateStatusInformationReceived += this.IntermediateStatusInformationReceived;
            _zvtClient.StatusInformationReceived += StatusInformationReceived;
            //_zvtClient.ReceiptReceived += ReceiptReceived;


            var res = await _zvtClient.RegistrationAsync(new RegistrationConfig()
            {
                SendIntermediateStatusInformation = true
            }, _tokenSource.Token);

            if (res.State != CommandResponseState.Successful)
            {
                Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown(-5));
            }
            
            _logger.LogInformation($"{nameof(InitTransaction)} - Connection successful");
            UpdateStatus("Connection successful", StatusType.Information);

            switch (paymentTerminalConfig.Funktion)
            {
                case TransactionType.Payment:
                    await this.StartPaymentAsync(paymentTerminalConfig.Betrag);
                    break;
                case TransactionType.ReversalLastPayment:
                    await this.StartReversalAsync(paymentTerminalConfig.StornoBelegNr);
                    break;
                case TransactionType.Credit:
                case TransactionType.RepeatReceiptVendor:
                case TransactionType.RepeatReceiptCustomer:
                case TransactionType.RepeatReceiptEndOfDay:
                case TransactionType.TaxFree:
                case TransactionType.CheckBalanaceAvsCard:
                case TransactionType.Reservation:
                case TransactionType.BookReservation:
                case TransactionType.AbortReservation:
                case TransactionType.Tip:
                case TransactionType.SelectLanguage:
                case TransactionType.ReadCardMagnetic:
                case TransactionType.ReservationPartialAbort:
                case TransactionType.ReadCardChip:
                case TransactionType.Diagnose:
                case TransactionType.EndOfDay:
                case TransactionType.RepeatReceipt:
                default:
                    Application.Current.Dispatcher.Invoke(() => Application.Current.Shutdown(-5));
                    break;
            }
            
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (_zvtClient != null)
            {
                _zvtClient.IntermediateStatusInformationReceived -= this.IntermediateStatusInformationReceived;
                _zvtClient.Dispose();
            }

            _deviceCommunication?.Dispose();
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
                if (_tokenSource.IsCancellationRequested)
                {
                    this._logger.LogInformation($"{nameof(StartReversalAsync)} - Aborted");
                    return;
                }
                _resultService.SetResult(response.State, response.ErrorMessage);
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
                if (_tokenSource.IsCancellationRequested)
                {
                    this._logger.LogInformation($"{nameof(StartPaymentAsync)} - Aborted");
                    return;
                }
                _resultService.SetResult(response.State, response.ErrorMessage);

                
                if (response.State == CommandResponseState.Successful)
                {
                    DisableAbortButtonAsync();
                    
                    this._logger.LogInformation($"{nameof(StartPaymentAsync)} - Successful");

                    this.UpdateStatus("Payment successful", StatusType.Information);
                    await Task.Delay(1000);

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
            UpdateStatus(status, StatusType.Information);
        }
        
        private void ReceiptReceived(ReceiptInfo receipt)
        {
            throw new System.NotImplementedException();
        }

        private void StatusInformationReceived(StatusInformation status)
        {
            _resultService.SetPaymentStatus(status.ReceiptNumber, status.Amount);
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
                    }
                }
                
                UpdateStatus("Transaction Aborted", StatusType.Information);
                _logger.LogInformation("AbortTransaction - transaction aborted");
                
                Thread.Sleep(2000);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Application.Current.Shutdown(-5);
                });
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
