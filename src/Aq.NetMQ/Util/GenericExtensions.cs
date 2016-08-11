using NetMQ;

namespace Aq.NetMQ.Util {
    public static class GenericExtensions {
        public static bool HasFlagFast(this PollEvents self, PollEvents flag) {
            return 0 < ((int) self & (int) flag);
        }
    }
}
