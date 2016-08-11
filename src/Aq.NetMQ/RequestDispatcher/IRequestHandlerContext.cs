using System.Threading;
using System.Threading.Tasks;
using NetMQ;

namespace Aq.NetMQ {
    public interface IRequestHandlerContext {
        RequestDispatcher Dispatcher { get; }

        NetMQMessage Request { get; }

        CancellationToken Cancellation { get; }

        Task<bool> TrySendAsync(NetMQMessage response);
        Task<bool> TrySendAsync(NetMQMessage response, CancellationToken cancellation);

        Task SendAsync(NetMQMessage response);
        Task SendAsync(NetMQMessage response, CancellationToken cancellation);
    }
}
