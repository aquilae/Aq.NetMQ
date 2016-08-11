using System.Threading.Tasks;

namespace Aq.NetMQ {
    public delegate Task RequestHandler(IRequestHandlerContext context);
}
