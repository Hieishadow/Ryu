using Ryujinx.Common.Logging.Formatters;
using System;

namespace Ryujinx.Common.Logging.Targets
{
    public class ConsoleLogTarget : ILogTarget
    {
        private readonly DefaultLogFormatter _formatter;
        private readonly string _name;
        string ILogTarget.Name { get => _name; }

        public ConsoleLogTarget(string name)
        {
            _formatter = new DefaultLogFormatter();
            _name = name;
        }

        public void Log(object sender, LogEventArgs args)
        {
            // PATCH ANDROID - sem ForegroundColor
            try { Console.WriteLine(_formatter.Format(args)); } catch {}
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
        }
    }
}
