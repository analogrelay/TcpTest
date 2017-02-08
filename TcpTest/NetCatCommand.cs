using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Threading;

namespace TcpTest
{
    public static class NetCatCommand
    {
        public static void Register(CommandLineApplication app)
        {
            app.Command("nc", cmd =>
            {
                cmd.Description = "Behaves similarly to the 'netcat' or 'nc' application";

                var listenOption = cmd.Option("-l", "Used to specify that nc should listen for an incoming connection rather than initiate a connection to a remote host.", CommandOptionType.NoValue);
                var messageOption = cmd.Option("-m <MESSAGE>", "Specifies a message that will be sent at regular intervals after establishing the connection", CommandOptionType.SingleValue);
                var intervalOption = cmd.Option("-i <INTERVAL>", "Specifies an interval, in seconds, to send the message provided by -m at. Ignored if -m is not present, defaults to 1 second", CommandOptionType.SingleValue);
                var endpointArgument = cmd.Argument("<ENDPOINT>", "Used to specify the endpoint to connect to or listen at. Expected format is [<hostname or IP>:]<port>, where the host name (and separating ':') is optional.");

                cmd.OnExecute(async () =>
                {
                    if (string.IsNullOrEmpty(endpointArgument.Value))
                    {
                        return CommandHelper.Error("Missing required argument <ENDPOINT>");
                    }

                    string host = null;
                    var endpoint = endpointArgument.Value;
                    if (endpoint.Contains(":"))
                    {
                        var splat = endpointArgument.Value.Split(':');
                        if (splat.Length != 2)
                        {
                            return CommandHelper.Error("Invalid endpoint, expected format <host or IP>:<port>. Only IPv4 addresses or DNS names are supported");
                        }
                        host = splat[0];
                        endpoint = splat[1];
                    }

                    if (!Int32.TryParse(endpoint, out int port))
                    {
                        return CommandHelper.Error("Invalid port number: " + endpoint);
                    }

                    TimeSpan interval = TimeSpan.FromSeconds(1);
                    if (!string.IsNullOrEmpty(intervalOption.Value()) && Int32.TryParse(intervalOption.Value(), out int intervalSeconds))
                    {
                        interval = TimeSpan.FromSeconds(intervalSeconds);
                    }

                    return await ExecuteAsync(listenOption.HasValue(), host, port, messageOption.Value(), interval);
                });
            });
        }

        private static async Task<int> ExecuteAsync(bool listen, string host, int port, string message, TimeSpan interval)
        {
            try
            {
                if (listen)
                {
                    return await ExecuteListenAsync(host, port, message, interval);
                }
                else
                {
                    return await ExecuteConnectAsync(host, port, message, interval);
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException ex)
            {
                return CommandHelper.Error($"Socket failed due to {ex.SocketErrorCode}");
            }
            return 0;
        }

        private static async Task<int> ExecuteConnectAsync(string host, int port, string message, TimeSpan interval)
        {
            using (var socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                Console.Error.WriteLine($"** Connecting to {host}:{port} **");
                await socket.ConnectAsync(host, port);
                Console.Error.WriteLine($"** Connection to {host}:{port} established! **");
                Console.Title = $"[TcpTest] Connected to {host}:{port}";

                await ExecuteNetCatAsync(socket, message, interval);
            }
            Console.Error.WriteLine("** Connection Closed **");

            return 0;
        }

        private static async Task<int> ExecuteListenAsync(string host, int port, string message, TimeSpan interval)
        {
            var address = IPAddress.Any;
            if (!string.IsNullOrEmpty(host) && !IPAddress.TryParse(host, out address))
            {
                var addresses = await Dns.GetHostAddressesAsync(host);
                if (addresses.Length == 0)
                {
                    return CommandHelper.Error("Unable to resolve DNS name: " + host);
                }
                address = addresses[0];
            }

            using (var socket = new Socket(SocketType.Stream, ProtocolType.Tcp))
            {
                socket.Bind(new IPEndPoint(address, port));
                socket.Listen(10);
                Console.Error.WriteLine($"** Socket is listening on port {port} **");
                var client = await socket.AcceptAsync();
                Console.Error.WriteLine($"** Received a connection from: {client.RemoteEndPoint} **");
                Console.Title = $"[TcpTest] Listening to {client.RemoteEndPoint}";

                await ExecuteNetCatAsync(client, message, interval);
            }
            Console.Error.WriteLine("** Listener Closed **");

            return 0;
        }

        private static async Task ExecuteNetCatAsync(Socket socket, string message, TimeSpan interval)
        {
            var cts = new CancellationTokenSource();
            CommandHelper.RegisterCtrlCTrigger(cts);
            var sendPump = string.IsNullOrEmpty(message) ?
                RunStandardInput(socket, Console.In, cts.Token) :
                RunMessagePumpAsync(socket, message, interval, cts.Token);
            var recvPump = RunStandardOutput(socket, Console.Out, cts.Token);

            await Task.WhenAny(sendPump, recvPump);
            Console.Error.WriteLine("** Shutdown requested **");
            cts.Cancel();
            await Task.WhenAll(sendPump, recvPump);
        }

        private static async Task RunMessagePumpAsync(Socket socket, string message, TimeSpan interval, CancellationToken cancellationToken)
        {
            Console.Error.WriteLine($"** Transmitting the provided message every {interval.TotalSeconds:0} seconds **");
            var buffer = Encoding.UTF8.GetBytes(message + Environment.NewLine);
            while (!cancellationToken.IsCancellationRequested)
            {
                await socket.SendAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                Console.WriteLine($"S [{DateTime.Now.ToString("O")}]: {message}");
                await Task.Delay(interval);
            }
        }

        private static async Task RunStandardOutput(Socket socket, TextWriter stdout, CancellationToken cancellationToken)
        {
            Console.Error.WriteLine("** Ready to receive messages on the socket **");
            var buffer = new byte[1024];
            using (cancellationToken.Register(() => socket.Dispose()))
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var len = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None);
                    var content = Encoding.UTF8.GetString(buffer, 0, len);
                    await stdout.WriteAsync($"R [{DateTime.Now.ToString("O")}]: {content}");
                }
            }
        }

        private static async Task RunStandardInput(Socket socket, TextReader stdin, CancellationToken cancellationToken)
        {
            Console.Error.WriteLine("** Ready to receive input on stdin **");
            var buffer = new byte[1024];
            while (!cancellationToken.IsCancellationRequested)
            {
                var line = await stdin.ReadLineAsync();
                line += Environment.NewLine; // Restore the newline that ReadLine stripped
                var len = Encoding.UTF8.GetBytes(line, 0, line.Length, buffer, 0);
                await socket.SendAsync(new ArraySegment<byte>(buffer, 0, len), SocketFlags.None);
                Console.WriteLine("S: " + line);
            }
        }
    }
}
