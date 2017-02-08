using System;
using System.Threading;

namespace TcpTest
{
    public static class CommandHelper
    {
        public static int Error(string message)
        {
            Console.Error.WriteLine("error: " + message);
            return 1;
        }

        public static CancellationToken CreateCtrlCToken()
        {
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, args) =>
            {
                args.Cancel = true;
                cts.Cancel();
            };
            return cts.Token;
        }
    }
}