using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace TcpTest
{
    public class Program
    {
        public static int Main(string[] args)
        {
            if(args.Length > 0 && args.Any(a => a == "--debug"))
            {
                var p = Process.GetCurrentProcess();
                Console.WriteLine("Waiting for debugger to be attached.");
                Console.WriteLine($"Process: {p.ProcessName} ID:{p.Id}");
                Console.WriteLine("Press ENTER to continue");
                Console.ReadLine();
                args = args.Where(a => a != "--debug").ToArray();
            }

            var app = new CommandLineApplication();
            app.Name = "tcptest";
            app.FullName = "TCP Test Tool";
            app.Description = "Tool to help test TCP connection behavior";
            app.HelpOption("-h|-?|--help");
            app.VersionOption("-v|--version", typeof(Program).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

            SendCommand.Register(app);
            ListenCommand.Register(app);

            app.Command("help", cmd =>
            {
                cmd.Description = "Get help information for this tool";
                var command = cmd.Argument("<COMMAND>", "The command to get help for.");

                cmd.OnExecute(() =>
                {
                    app.ShowHelp(command.Value);
                    return 0;
                });
            });

            app.OnExecute(() =>
            {
                app.ShowHelp();
                return 0;
            });

            return app.Execute(args);
        }
    }
}