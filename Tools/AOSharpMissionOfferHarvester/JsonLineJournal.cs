using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace AORebirth.MissionEvidence
{
    internal sealed class JsonLineJournal : IDisposable
    {
        private readonly FileStream _stream;
        private readonly JavaScriptSerializer _serializer = new JavaScriptSerializer { MaxJsonLength = int.MaxValue };

        internal string Path { get; private set; }

        internal JsonLineJournal(string path)
        {
            Path = path;
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(path));
            _stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
        }

        internal void Append(string eventType, string sessionId, string requestId, IDictionary<string, object> payload)
        {
            var record = new Dictionary<string, object>
            {
                ["event_type"] = eventType,
                ["schema_version"] = 1,
                ["session_id"] = sessionId,
                ["request_id"] = requestId,
                ["timestamp_utc"] = DateTime.UtcNow.ToString("o"),
                ["payload"] = payload
            };
            byte[] bytes = Encoding.UTF8.GetBytes(_serializer.Serialize(record) + "\n");
            _stream.Write(bytes, 0, bytes.Length);
            _stream.Flush(true);
        }

        public void Dispose()
        {
            _stream.Flush(true);
            _stream.Dispose();
        }
    }
}
