namespace ks.plugin.external.Logging.Interfaces
{
    public interface ILogBuffer
    {
        event Action<string>? LineAdded;

        void Add(string line);

        IReadOnlyList<string> GetLines();
    }
}
