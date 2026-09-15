using System.Text.Json;

namespace System.Web.Script.Serialization
{
    internal sealed class JavaScriptSerializer
    {
        private static readonly JsonSerializerOptions Options = new JsonSerializerOptions
        {
            IncludeFields = true,
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public int MaxJsonLength { get; set; }

        public T Deserialize<T>(string input)
        {
            return JsonSerializer.Deserialize<T>(input, Options);
        }

        public string Serialize(object input)
        {
            return JsonSerializer.Serialize(input, Options);
        }
    }
}
