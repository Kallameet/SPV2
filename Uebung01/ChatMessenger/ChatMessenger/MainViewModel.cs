using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Windows;
using System.Windows.Input;
using ChatMessage;
using ChatMessenger.Annotations;

namespace ChatMessenger
{
    public class MainViewModel : INotifyPropertyChanged
    {
        #region Properties

        private String _serverIp;
        public String ServerIp
        {
            get { return _serverIp; }
            set
            {
                _serverIp = value;
                OnPropertyChanged();
            }
        }

        private int _serverPort;
        public int ServerPort
        {
            get { return _serverPort; }
            set
            {
                _serverPort = value;
                OnPropertyChanged();
            }
        }

        private string _username;
        public String Username
        {
            get { return _username; }
            set
            {
                _username = value;
                OnPropertyChanged();
            }
        }

        public ObservableCollection<String> ConnectedUsers { get; set; }

        private string _chatMessages;
        public String ChatMessages
        {
            get
            {
                return _chatMessages;
            }
            set
            {
                _chatMessages = value;
                OnPropertyChanged();
            }
        }

        private string _chatMessageInput;
        public String ChatMessageInput
        {
            get
            {
                return _chatMessageInput;
            }
            set
            {
                _chatMessageInput = value;
                OnPropertyChanged();
            }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get
            {
                return _isConnected;
            }
            set
            {
                _isConnected = value;
                OnPropertyChanged();
                OnPropertyChanged("IsNotConnected");
            }
        }

        public bool IsNotConnected
        {
            get
            {
                return !IsConnected;
            }
        }


        private ICommand _connectCommand;
        public ICommand ConnectCommand
        {
            get
            {
                return _connectCommand ?? (_connectCommand = new CommandHandler(Connect, _canExecute));
            }
        }

        private ICommand _disconnectCommand;
        public ICommand DisconnectCommand
        {
            get
            {
                return _disconnectCommand ?? (_disconnectCommand = new CommandHandler(Disonnect, _canExecute));
            }
        }

        private ICommand _exitCommand;
        public ICommand ExitCommand
        {
            get
            {
                return _exitCommand ?? (_exitCommand = new CommandHandler(Exit, _canExecute));
            }
        }

        private ICommand _sendCommand;
        public ICommand SendCommand
        {
            get
            {
                return _sendCommand ?? (_sendCommand = new CommandHandler(Send, _canExecute));
            }
        }

        #endregion Properties

        private const int ReadIntervallInMilliseconds = 500;
        private const int ConnectTimeoutInSeconds = 5;

        private readonly bool _canExecute;
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private readonly IFormatter _formatter;
        private readonly BackgroundWorker _worker;

        public MainViewModel()
        {
            IsConnected = false;
            ServerIp = "127.0.0.1";
            ServerPort = 12345;

            _canExecute = true;
            _tcpClient = new TcpClient();

            _formatter = new BinaryFormatter();

            _worker = new BackgroundWorker
            {
                WorkerReportsProgress = true,
                WorkerSupportsCancellation = true
            };
            _worker.DoWork += WorkerOnDoWork;
            _worker.ProgressChanged += WorkerOnProgressChanged;

            ConnectedUsers = new ObservableCollection<string>();
        }

        private void Connect()
        {
            IPAddress serverIpAddress;
            if (!IPAddress.TryParse(ServerIp, out serverIpAddress))
            {
                MessageBox.Show(String.Format("\"{0}\" is not a valid IP adress", ServerIp), "Invalid IP adress");
                return;
            }
            if (!(ServerPort > 0))
            {
                MessageBox.Show(String.Format("\"{0}\" is not a valid port", ServerPort), "Invalid port");
                return;
            }
            if (String.IsNullOrWhiteSpace(Username))
            {
                MessageBox.Show(String.Format("\"{0}\" is not a valid username", Username), "Invalid username");
                return;
            }

            try
            {
                IAsyncResult asyncResult = _tcpClient.BeginConnect(serverIpAddress, ServerPort, null, null);
                if (!asyncResult.AsyncWaitHandle.WaitOne(TimeSpan.FromSeconds(ConnectTimeoutInSeconds), false))
                {
                    _tcpClient.Close();
                    throw new TimeoutException();
                }
                _tcpClient.EndConnect(asyncResult);
                _networkStream = _tcpClient.GetStream();

                SendMessage(new Message{ ChatMessage = String.Empty, Type = Message.MessageType.Connect, Username = Username });

                _worker.RunWorkerAsync();
            }
            catch (TimeoutException)
            {
                MessageBox.Show(String.Format("Timeout of {0} seconds has been reached.", ConnectTimeoutInSeconds), "Error in Connect");
            }
            catch (ArgumentNullException e)
            {
                MessageBox.Show(e.ToString(), "Error in Connect");
            }
            catch (SocketException e)
            {
                MessageBox.Show(e.ToString(), "Error in Connect");
            }
        }

        void WorkerOnProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            Message message = e.UserState as Message;

            if (message != null)
            {
                switch (message.Type)
                {
                    case Message.MessageType.Connect:
                        {
                            if (message.Username.Equals(Username))
                            {
                                ChatMessages += String.Format("({1}) Welcome, {0}!\n", Username, message.MessageCreationTime.ToString("G"));
                                IsConnected = true;
                            }
                            else
                            {
                                ConnectedUsers.Add(message.Username);
                                ChatMessages += String.Format("({1}) {0} has joined the server.\n", message.Username, message.MessageCreationTime.ToString("G"));
                            }
                            break;
                        }
                    case Message.MessageType.Disconnect:
                        {
                            if (!message.Username.Equals(Username))
                            {
                                ConnectedUsers.Remove(message.Username);
                                ChatMessages += String.Format("({1}) {0} has left the server.\n", message.Username, message.MessageCreationTime.ToString("G"));
                            }
                            break;
                        }
                    case Message.MessageType.ChatMessage:
                        {
                            if (message.Username != Username && !ConnectedUsers.Contains(message.Username))
                            {
                                ConnectedUsers.Add(message.Username);
                            }

                            ChatMessages += String.Format("({1}) {2}: {0} \n", message.ChatMessage, message.MessageCreationTime.ToString("G"), message.Username);
                            break;
                        }
                    case Message.MessageType.UsernameAlreadyTaken:
                        {
                            ChatMessages += String.Format("({1}) Username {0} is already taken!\n", Username, message.MessageCreationTime.ToString("G"));

                            _networkStream.Close();
                            _tcpClient.Close();
                            _tcpClient = new TcpClient();
                            _worker.CancelAsync();

                            break;
                        }
                    default:
                        //do nothing
                        break;
                }
            }
        }

        private void WorkerOnDoWork(object sender, DoWorkEventArgs doWorkEventArgs)
        {
            while (!(_worker.CancellationPending) && _tcpClient.Connected)
            {
                if (_tcpClient.Connected)
                {
                    try
                    {
                        Message message = (Message)_formatter.Deserialize(_networkStream);

                        _worker.ReportProgress(0, message);
                    }
                    catch (InvalidCastException e)
                    {
                        MessageBox.Show(e.ToString(), "Error in WorkerOnDoWork");
                    }
                }
                else
                {
                    MessageBox.Show("The connection to the server has been lost. Client is no longer connected.", "Lost connection to server");
                    IsConnected = false;
                    _networkStream.Close();
                    _tcpClient.Close();
                    _tcpClient = new TcpClient();
                    _worker.CancelAsync();
                }
                Thread.Sleep(ReadIntervallInMilliseconds);
            }
        }

        private void Disonnect()
        {
            if (_tcpClient.Connected)
            {
                try
                {
                    _formatter.Serialize(_networkStream, new Message() { ChatMessage = Username, Type = Message.MessageType.Disconnect, Username = Username });
                }
                catch (IOException)
                {
                    //Client closed connection
                }

                _networkStream.Close();
                _tcpClient.Close();
            }

            IsConnected = false;
            _tcpClient = new TcpClient();

            ConnectedUsers.Clear();
            ChatMessages = String.Empty;

            _worker.CancelAsync();
        }

        private void Exit()
        {
            Application.Current.Shutdown();
        }

        private void Send()
        {
            if (String.IsNullOrEmpty(ChatMessageInput))
            {
                MessageBox.Show("Message cannot be empty", "Invalid chat message");
                return;
            }

            SendMessage(new Message() { ChatMessage = ChatMessageInput, Type = Message.MessageType.ChatMessage, Username = Username });
            ChatMessageInput = String.Empty;
        }

        private void SendMessage(Message message)
        {
            if (_tcpClient.Connected)
            {
                _formatter.Serialize(_networkStream, message);
            }
            else
            {
                MessageBox.Show("The connection to the server has been lost. Client is no longer connected.", "Lost connection to server");
                IsConnected = false;
                _networkStream.Close();
                _tcpClient.Close();
                _tcpClient = new TcpClient();
                _worker.CancelAsync();
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        [NotifyPropertyChangedInvocator]
        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            var handler = PropertyChanged;
            if (handler != null) handler(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
