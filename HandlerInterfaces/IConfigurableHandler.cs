using System.Text.Json;

namespace HandlerInterfaces
{
    public interface IConfigurableHandler : IRequestHandler
    {
        void Configure(Dictionary<string, JsonElement> settings);
    }
}
