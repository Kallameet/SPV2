using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using ChatMessage;

namespace ChatServer
{
    class ChatServer
    {
        public string IpAdress { get; private set; }
        public int Port { get; private set; }
        public Dictionary<string, TcpClient> ConnectedClients { get; private set; }

        private const int ReadIntervallInMilliseconds = 500;

        private readonly TcpListener _listener;
        private readonly IFormatter _formatter;
        private bool _isRunning;

        public ChatServer(string ipAdress, int port)
        {
            ConnectedClients = new Dictionary<string, TcpClient>();
            _formatter = new BinaryFormatter();

            IpAdress = ipAdress;
            Port = port;

            IPAddress adress = IPAddress.Parse(ipAdress);
            _listener = new TcpListener(adress, port);
        }

        public void Start()
        {
            _isRunning = true;

            _listener.Start();
            Console.WriteLine("{0} Server started, now listening for clients.", DateTime.Now.ToString("G"));

            while (_isRunning)
            {
                TcpClient newClient = _listener.AcceptTcpClient();

                NetworkStream stream = newClient.GetStream();
                try
                {
                    Message message = (Message)_formatter.Deserialize(stream);

                    if (ConnectedClients.ContainsKey(message.Username))
                    {
                        _formatter.Serialize(stream, new Message(){Type = Message.MessageType.UsernameAlreadyTaken});
                        Console.WriteLine("{0} Username {1} already exists.", message.MessageCreationTime.ToString("G"), message.Username);
                    }
                    else
                    {
                        ConnectedClients.Add(message.Username, newClient);

                        IPEndPoint ipEndPoint = (IPEndPoint)newClient.Client.RemoteEndPoint;

                        Console.WriteLine("{0} New client connected.", message.MessageCreationTime.ToString("G"));
                        Console.WriteLine("{0}:{1} - Username: {2}", ipEndPoint.Address, ipEndPoint.Port, message.Username);
                        Console.WriteLine("Connected clients: {0}", ConnectedClients.Count);

                        ThreadPool.QueueUserWorkItem(DistributeMessage, message);
                        ThreadPool.QueueUserWorkItem(ReadClientMessages, newClient);
                    }
                    
                }
                catch (InvalidCastException e)
                {
                    Console.WriteLine("Client has not connected properly.");
                    Console.WriteLine(e.ToString());
                    newClient.Close();
                }
            }
        }

        public void Stop()
        {
            _isRunning = false;
            foreach (var client in ConnectedClients)
            {
                client.Value.Close();
            }
            _listener.Stop();
        }

        private void DistributeMessage(Object obj)
        {
            try
            {
                Message message = (Message)obj;
                List<string> disconnectedClients = new List<string>();

                foreach (var connectedClient in ConnectedClients)
                {
                    if (connectedClient.Value.Connected)
                    {
                        try
                        {
                            NetworkStream stream = connectedClient.Value.GetStream();
                            _formatter.Serialize(stream, message);
                        }
                        catch (IOException)
                        {
                            //Client closed connection
                            disconnectedClients.Add(connectedClient.Key);
                        }
                    }
                    else
                    {
                        disconnectedClients.Add(connectedClient.Key);
                    }
                }

                foreach (var disconnectedClient in disconnectedClients)
                {
                    ConnectedClients[disconnectedClient].Close();
                    ConnectedClients.Remove(disconnectedClient);
                    Console.WriteLine("Removed disconnected user: {0}", disconnectedClient);
                }
            }

            catch (InvalidCastException e)
            {
                Console.WriteLine("Tried to distribute wrong object to all clients.");
                Console.WriteLine(e.ToString());
            }
        }


        private void ReadClientMessages(Object obj)
        {
            try
            {
                TcpClient client = (TcpClient)obj;

                while (_isRunning && client.Connected)
                {
                    if (client.Connected)
                    {
                        NetworkStream stream = client.GetStream();
                        {
                            try
                            {
                                Message message = (Message)_formatter.Deserialize(stream);
                                ThreadPool.QueueUserWorkItem(DistributeMessage, message);
                            }
                            catch (IOException)
                            {
                                //Client closed connection
                            }
                            catch (SerializationException)
                            {
                                //currently no new message   
                            }
                            catch (InvalidCastException e)
                            {
                                Console.WriteLine("Could not cast received message.");
                                Console.WriteLine(e.ToString());
                            }
                            finally
                            {
                                Thread.Sleep(ReadIntervallInMilliseconds);
                            }
                        }
                    }
                }
            }
            catch (InvalidCastException e)
            {
                Console.WriteLine("Could not cast client.");
                Console.WriteLine(e.ToString());
            }
        }
    }
}
