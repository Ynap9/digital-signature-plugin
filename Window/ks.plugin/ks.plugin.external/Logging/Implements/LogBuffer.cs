using ks.plugin.external.Logging.Interfaces;

namespace ks.plugin.external.Logging.Implements
{
    /// <summary>Memory only: logs are never written to disk.</summary>
    public class LogBuffer : ILogBuffer
    {
        private const int MaxLines = 1000;

        private readonly object _lock = new();
        private readonly Queue<string> _lines = new();

        public event Action<string>? LineAdded;

        public void Add(string line)
        {
            lock (_lock)
            {
                _lines.Enqueue(line);
                while (_lines.Count > MaxLines)
                {
                    _lines.Dequeue();
                }
            }

            LineAdded?.Invoke(line);
        }

        public IReadOnlyList<string> GetLines()
        {
            lock (_lock)
            {
                return _lines.ToList();
            }
        }
    }
}
