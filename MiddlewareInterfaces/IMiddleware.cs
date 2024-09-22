using System.Net;

namespace MiddlewareInterfaces
{
    public interface IMiddleware
    {
        Task<bool> ProcessRequestAsync(HttpListenerContext context);
    }
}
