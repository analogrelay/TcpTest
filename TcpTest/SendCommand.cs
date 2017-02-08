using Microsoft.Extensions.CommandLineUtils;
using System.Threading.Tasks;
using System.Net.Sockets;
using System;
using System.Text;
using System.Net;

namespace TcpTest
{
    public class SendCommand
    {
        public static readonly string DefaultMessage = "Ping\r\n";

        public static void Register(CommandLineApplication app)
        {
            app.Command("send", cmd =>
            {
                cmd.Description = "Connects to the specified endpoint, and sends the same message repeatedly";

                var messageOption = cmd.Option("-m|--message <MESSAGE>", "The message to send to the other endpoint", CommandOptionType.SingleValue);
                var intervalOption = cmd.Option("-i|--interval <INTERVAL>", "The interval at which to send the message in seconds", CommandOptionType.SingleValue);
                var endpointArgument = cmd.Argument("<ENDPOINT>", "The endpoint to connect to, specified as <host or IP>:<port>");

                cmd.OnExecute(async () =>
                {
                    if (string.IsNullOrEmpty(endpointArgument.Value))
                    {
                        return CommandHelper.Error("Missing required argument <ENDPOINT>");
                    }
                    var interval = TimeSpan.FromSeconds(1);
                    if (intervalOption.HasValue())
                    {
                        if (!Int32.TryParse(intervalOption.Value(), out int seconds))
                        {
                            return CommandHelper.Error("Interval must be an integer");
                        }
                        interval = TimeSpan.FromSeconds(seconds);
                    }
                    var splat = endpointArgument.Value.Split(':');
                    if (splat.Length != 2)
                    {
                        return CommandHelper.Error("Invalid endpoint, expected format <host or IP>:<port>. Only IPv4 addresses or DNS names are supported");
                    }
                    if (!Int32.TryParse(splat[1], out int port))
                    {
                        return CommandHelper.Error("Invalid port number: " + splat[1]);
                    }

                    return await ExecuteAsync(messageOption.Value(), splat[0], port, interval);
                });
            });
        }

        private static async Task<int> ExecuteAsync(string message, string host, int port, TimeSpan interval)
        {
            message = message ?? DefaultMessage;
            var payload = Encoding.UTF8.GetBytes(message + Environment.NewLine);
            using (var socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                Console.Error.WriteLine($"Connecting to {host}:{port}");
                await socket.ConnectAsync(host, port);
                Console.Error.WriteLine($"Connection to {host}:{port} established!");

                try
                {
                    var cancellationToken = CommandHelper.CreateCtrlCToken();

                    // Send until terminated
                    while (!cancellationToken.IsCancellationRequested)
                    {
                        await socket.SendAsync(new ArraySegment<byte>(payload), SocketFlags.None);

                        await Task.Delay(interval);
                    }
                }
                catch (OperationCanceledException)
                {
                    Console.Error.WriteLine("Cancellation requested.");
                }
            }

            return 0;
        }
    }
}