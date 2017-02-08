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

        public static void RegisterCtrlCTrigger(CancellationTokenSource cts)
        {
            Console.CancelKeyPress += (sender, args) =>
            {
                args.Cancel = true;
                cts.Cancel();
            };
        }

        public static CancellationToken CreateCtrlCToken()
        {
            var cts = new CancellationTokenSource();
            RegisterCtrlCTrigger(cts);
            return cts.Token;
        }
    }
}