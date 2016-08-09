using System;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Sockets;
using System.Reflection;
using NetMQ;

namespace Aq.NetMQ.Util {
    public static class NetMQSocketExtensions {
        public static Socket GetHandle(this NetMQSocket self) => GetHandleDelegate(self);
        public static PollEvents GetPollEvents(this NetMQSocket self) => GetPollEventsDelegate(self);
        public static int GetSocketOption(this NetMQSocket self, int option) => GetSocketOptionDelegate(self, option);

        public static void AddEventsChangedHandler(
            this NetMQSocket self, EventHandler<NetMQSocketEventArgs> handler)
            => AddEventsChangedHandlerDelegate(self, handler);

        public static void RemoveEventsChangedHandler(
            this NetMQSocket self, EventHandler<NetMQSocketEventArgs> handler)
            => RemoveEventsChangedHandlerDelegate(self, handler);

        public static void InvokeEvents(
            this NetMQSocket self, object sender, PollEvents events)
            => InvokeEventsDelegate(self, sender, events);

        public static PollEvents GetEventsOption(this NetMQSocket self) {
            return (PollEvents) self.GetSocketOption(15 /* ZmqSocketOption.Events */);
        }

        static NetMQSocketExtensions() {
            GetHandleDelegate = CreateGetHandleDelegate();
            GetPollEventsDelegate = CreateGetPollEventsDelegate();
            GetSocketOptionDelegate = CreateGetSocketOptionDelegate();
            InvokeEventsDelegate = CreateInvokeEventsDelegate();
            AddEventsChangedHandlerDelegate = CreateAddEventsChangedHandlerDelegate();
            RemoveEventsChangedHandlerDelegate = CreateRemoveEventsChangedHandlerDelegate();
        }

        private static Func<NetMQSocket, Socket> CreateGetHandleDelegate() {
#if REFLECTION_OLD
            var netMQSocketType = typeof (NetMQSocket);
            var mSocket = netMQSocketType.GetProperty("SocketHandle", BindingFlags.Instance | BindingFlags.NonPublic);
            var mHandle = mSocket.PropertyType.GetProperty("Handle", BindingFlags.Instance | BindingFlags.Public);

#elif REFLECTION_NEW
            var netMQSocketType = typeof (NetMQSocket).GetTypeInfo();
            var mSocket = netMQSocketType.DeclaredProperties.First(x => x.Name == "SocketHandle");
            var mHandle = mSocket.PropertyType.GetTypeInfo().DeclaredProperties.First(x => x.Name == "Handle");

#else
            throw new System.NotSupportedException("Reflection is not supported");
#endif

            var arg0 = Expression.Parameter(typeof (NetMQSocket), "this");
            var lambda = Expression.Lambda(
                Expression.MakeMemberAccess(
                    Expression.MakeMemberAccess(arg0, mSocket),
                    mHandle),
                arg0);

            return (Func<NetMQSocket, Socket>) lambda.Compile();
        }

        private static Func<NetMQSocket, PollEvents> CreateGetPollEventsDelegate() {
            var intType = typeof (int);

#if REFLECTION_OLD
            var netMQSocketType = typeof (NetMQSocket);
            var getPollEventsInfo = netMQSocketType.GetMethod("GetPollEvents", BindingFlags.Instance | BindingFlags.NonPublic);

#elif REFLECTION_NEW
            var netMQSocketType = typeof (NetMQSocket).GetTypeInfo();
            var getPollEventsInfo = netMQSocketType.DeclaredMethods.First(x => x.Name == "GetPollEvents");

#else
            throw new System.NotSupportedException("Reflection is not supported");
#endif

            var arg0 = Expression.Parameter(typeof (NetMQSocket), "this");
            var lambda = Expression.Lambda(
                Expression.Call(
                    arg0,
                    getPollEventsInfo),
                arg0);

            return (Func<NetMQSocket, PollEvents>) lambda.Compile();
        }

        private static Func<NetMQSocket, int, int> CreateGetSocketOptionDelegate() {
            var intType = typeof (int);

#if REFLECTION_OLD
            var netMQSocketType = typeof (NetMQSocket);
            var zmqSocketOptionType = netMQSocketType.Assembly.GetTypes().First(x => x.Name == "ZmqSocketOption");
            var getSocketOptionInfo = netMQSocketType.GetMethod("GetSocketOption", BindingFlags.Instance | BindingFlags.NonPublic);

#elif REFLECTION_NEW
            var netMQSocketType = typeof (NetMQSocket).GetTypeInfo();
            var zmqSocketOptionType = netMQSocketType.Assembly.DefinedTypes.First(x => x.Name == "ZmqSocketOption").AsType();
            var getSocketOptionInfo = netMQSocketType.DeclaredMethods.First(x => x.Name == "GetSocketOption");

#else
            throw new System.NotSupportedException("Reflection is not supported");
#endif

            var arg0 = Expression.Parameter(typeof (NetMQSocket), "this");
            var arg1 = Expression.Parameter(typeof (int), "option");
            var lambda = Expression.Lambda(
                Expression.Convert(
                    Expression.Call(
                        arg0,
                        getSocketOptionInfo,
                        Expression.Convert(
                            arg1, zmqSocketOptionType)),
                    intType),
                arg0,
                arg1);

            return (Func<NetMQSocket, int, int>) lambda.Compile();
        }

        private static Action<NetMQSocket, object, PollEvents> CreateInvokeEventsDelegate() {
#if REFLECTION_OLD
            var netMQSocketType = typeof (NetMQSocket);
            var invokeEventsInfo = netMQSocketType.GetMethod("InvokeEvents", BindingFlags.Instance | BindingFlags.NonPublic);

#elif REFLECTION_NEW
            var netMQSocketType = typeof (NetMQSocket).GetTypeInfo();
            var invokeEventsInfo = netMQSocketType.DeclaredMethods.First(x => x.Name == "InvokeEvents");

#else
            throw new System.NotSupportedException("Reflection is not supported");
#endif

            var arg0 = Expression.Parameter(typeof (NetMQSocket), "this");
            var arg1 = Expression.Parameter(typeof (object), "sender");
            var arg2 = Expression.Parameter(typeof (PollEvents), "events");
            var lambda = Expression.Lambda(
                Expression.Call(
                    arg0,
                    invokeEventsInfo,
                    arg1,
                    arg2),
                arg0,
                arg1,
                arg2);

            return (Action<NetMQSocket, object, PollEvents>) lambda.Compile();
        }

        private static Action<NetMQSocket, EventHandler<NetMQSocketEventArgs>> CreateAddEventsChangedHandlerDelegate() {
#if REFLECTION_OLD
            var netMQSocketType = typeof (NetMQSocket);
            var eventsChangedInfo = netMQSocketType.GetEvent("EventsChanged", BindingFlags.Instance | BindingFlags.NonPublic);

#elif REFLECTION_NEW
            var netMQSocketType = typeof (NetMQSocket).GetTypeInfo();
            var eventsChangedInfo = netMQSocketType.DeclaredEvents.First(x => x.Name == "EventsChanged");

#else
            throw new System.NotSupportedException("Reflection is not supported");
#endif

            var arg0 = Expression.Parameter(typeof (NetMQSocket), "this");
            var arg1 = Expression.Parameter(typeof (EventHandler<NetMQSocketEventArgs>), "handle");
            var lambda = Expression.Lambda(
                Expression.Call(
                    arg0,
                    eventsChangedInfo.AddMethod,
                    arg1),
                arg0,
                arg1);

            return (Action<NetMQSocket, EventHandler<NetMQSocketEventArgs>>) lambda.Compile();
        }

        private static Action<NetMQSocket, EventHandler<NetMQSocketEventArgs>> CreateRemoveEventsChangedHandlerDelegate() {
#if REFLECTION_OLD
            var netMQSocketType = typeof (NetMQSocket);
            var eventsChangedInfo = netMQSocketType.GetEvent("EventsChanged", BindingFlags.Instance | BindingFlags.NonPublic);

#elif REFLECTION_NEW
            var netMQSocketType = typeof (NetMQSocket).GetTypeInfo();
            var eventsChangedInfo = netMQSocketType.DeclaredEvents.First(x => x.Name == "EventsChanged");

#else
            throw new System.NotSupportedException("Reflection is not supported");
#endif

            var arg0 = Expression.Parameter(typeof (NetMQSocket), "this");
            var arg1 = Expression.Parameter(typeof (EventHandler<NetMQSocketEventArgs>), "handle");
            var lambda = Expression.Lambda(
                Expression.Call(
                    arg0,
                    eventsChangedInfo.RemoveMethod,
                    arg1),
                arg0,
                arg1);

            return (Action<NetMQSocket, EventHandler<NetMQSocketEventArgs>>) lambda.Compile();
        }

        private static Func<NetMQSocket, Socket> GetHandleDelegate { get; }
        private static Func<NetMQSocket, PollEvents> GetPollEventsDelegate { get; }
        private static Func<NetMQSocket, int, int> GetSocketOptionDelegate { get; }
        private static Action<NetMQSocket, object, PollEvents> InvokeEventsDelegate { get; }
        private static Action<NetMQSocket, EventHandler<NetMQSocketEventArgs>> AddEventsChangedHandlerDelegate { get; }
        private static Action<NetMQSocket, EventHandler<NetMQSocketEventArgs>> RemoveEventsChangedHandlerDelegate { get; }
    }
}
