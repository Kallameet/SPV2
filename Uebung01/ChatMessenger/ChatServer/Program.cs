using System;

namespace ChatServer
{
    class Program
    {
        static void Main(string[] args)
        {
            ChatServer server = new ChatServer("127.0.0.1", 12345);
            try
            {
                server.Start();
            }
            catch (Exception e)
            {
                Console.WriteLine("Unexpected Exception occured.");
                Console.WriteLine(e.ToString());
                Console.ReadKey();
            }
        }
    }
}
