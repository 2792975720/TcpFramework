using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TcpFramework
{
    public sealed class TcpConnectionPool
    {
        private readonly Dictionary<string, TcpService> _connections = new Dictionary<string, TcpService>();
        private readonly object _lock = new object();

        public IReadOnlyDictionary<string, TcpService> Connections => _connections;

        public async Task<TcpService> StartConnectionAsync(string key, TcpServiceOptions options)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentNullException(nameof(key));
            if (options == null) throw new ArgumentNullException(nameof(options));

            TcpService service;
            bool existed;
            lock (_lock)
            {
                existed = _connections.TryGetValue(key, out service);
                if (existed)
                {
                    service.UpdateOptions(options);
                }
                else
                {
                    service = new TcpService();
                    service.UpdateOptions(options);
                    _connections[key] = service;
                }
            }

            Log.Write(LogLevel.Info, "ConnectionPool starting connection.",
                Log.Fields(("poolKey", key), ("host", options.Host), ("port", options.Port), ("existing", existed)));
            await service.StartAsync(options).ConfigureAwait(false);
            Log.Write(LogLevel.Info, "ConnectionPool started connection.",
                Log.Fields(("poolKey", key), ("host", options.Host), ("port", options.Port)));
            return service;
        }

        public bool TryGet(string key, out TcpService service)
        {
            lock (_lock) return _connections.TryGetValue(key, out service);
        }

        public void Close(string key)
        {
            TcpService service = null;
            lock (_lock)
            {
                if (_connections.TryGetValue(key, out service))
                    _connections.Remove(key);
            }

            service?.Close();
            Log.Write(LogLevel.Info, "ConnectionPool closed connection.",
                Log.Fields(("poolKey", key), ("found", service != null)));
        }

        public void CloseAll()
        {
            List<TcpService> services;
            lock (_lock)
            {
                services = new List<TcpService>(_connections.Values);
                _connections.Clear();
            }

            foreach (var service in services)
                service.Close();

            Log.Write(LogLevel.Info, "ConnectionPool closed all connections.",
                Log.Fields(("count", services.Count)));
        }
    }
}
