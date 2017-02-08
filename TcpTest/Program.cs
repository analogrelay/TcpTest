using Microsoft.Extensions.CommandLineUtils;
using System;
using System.Reflection;

namespace TcpTest
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var app = new CommandLineApplication();
            app.Name = "TCP Test Tool";
            app.Description = "Tool to help test TCP connection behavior";
            app.HelpOption("-h|-?|--help");
            app.VersionOption("-v|--version", typeof(Program).GetTypeInfo().Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion);

            SendCommand.Register(app);
        }
    }
}