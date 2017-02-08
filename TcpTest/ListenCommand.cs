using Microsoft.Extensions.CommandLineUtils;
using System.Threading.Tasks;
using System.Net.Sockets;
using System;
using System.Text;
using System.Net;

namespace TcpTest
{
    public class ListenCommand
    {
        public static void Register(CommandLineApplication app)
        {
            app.Command("listen", cmd =>
            {
                cmd.Description = "Accepts connections on the specified port, and dumps output from the first connection it received";

                var portArgument = cmd.Argument("<PORT>", "The port number on which to listen");

                cmd.OnExecute(async () =>
                {
                    if (string.IsNullOrEmpty(portArgument.Value))
                    {
                        return CommandHelper.Error("Missing required argument <PORT>");
                    }

                    if(!Int32.TryParse(portArgument.Value, out int port))
                    {
                        return CommandHelper.Error("Port number must be an integer");
                    }

                    return await ExecuteAsync(port);
                });
            });
        }

        private static async Task<int> ExecuteAsync(int port)
        {
            byte[] buffer = new byte[1024];
            using (var socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(IPAddress.Any, port));
                socket.Listen(10);
                Console.WriteLine($"Socket is listening on port {port}");
                var client = await socket.AcceptAsync();
                Console.WriteLine($"Received a connection from: {client.RemoteEndPoint}");

                try
                {
                    var cancellationToken = CommandHelper.CreateCtrlCToken();
                    using (cancellationToken.Register(() => client.Dispose()))
                    {
                        // Send until terminated
                        while (!cancellationToken.IsCancellationRequested)
                        {
                            var length = await client.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);

                            Console.Write(Encoding.UTF8.GetString(buffer, 0, length));
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("Cancellation requested.");
                }
                catch (ObjectDisposedException)
                {
                    Console.WriteLine("Cancellation requested.");
                }
            }

            return 0;
        }
    }
}