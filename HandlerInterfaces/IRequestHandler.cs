using System.Net;

namespace HandlerInterfaces
{
    public interface IRequestHandler
    {
        Task HandleRequestAsync(HttpListenerRequest request, HttpListenerResponse response);
    }
}
