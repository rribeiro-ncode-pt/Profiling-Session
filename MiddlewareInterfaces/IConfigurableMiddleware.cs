using System.Text.Json;

namespace MiddlewareInterfaces
{
    public interface IConfigurableMiddleware : IMiddleware
    {
        void Configure(Dictionary<string, JsonElement> settings);
    }
}
