using NetMQ;

namespace Aq.NetMQ {
    public delegate bool PollHandler(
        IRunningAsyncPoller poller,
        NetMQSocket socket, object state,
        bool gotRead, bool gotWrite, bool gotError);
}
