using System;
using System.Threading.Tasks;
using NetMQ;
using NetMQ.Sockets;
using NUnit.Framework;

namespace Aq.NetMQ.Tests {
    [TestFixture, Parallelizable(ParallelScope.None)]
    public class RequestDispatcherTests : UnitTests {
        [SetUp]
        public void SetUp() {
            PairSocket.CreateSocketPair(
                out this._server, out this._client);

            this._dispatcher = new RequestDispatcher(x => Task.CompletedTask);
            this._dispatcher.Add(this._server);
        }

        [TearDown]
        public void TearDown() {
            this._dispatcher.Dispose();
            this._client.Dispose();
            this._server.Dispose();
        }
        
        [Test]
        public async Task RequestDispatcherShouldSupportNetMQSocketEvents() {
            var tcs = new TaskCompletionSource();

            this.Server.ReceiveReady += (sender, eventArgs) => {
                tcs.TrySetComplete();
            };

            var dispatch = this.Dispatcher.Start();

            try {
                this.Client.SendFrameEmpty();

                await Task.Delay(100);
                Assert.That(tcs.Task.IsCompleted, "tcs.Task.IsCompleted");
            }
            finally {
                dispatch.Complete();
            }

            await Task.Delay(100);
            Assert.That(!dispatch.Completion.IsPending(), "!dispatch.Completion.IsPending()");
            await dispatch.Completion;
        }

        [Test]
        public async Task RequestDispatcherCallRequestHandler() {
            var tcs = new TaskCompletionSource<string>();

            this.Dispatcher.RequestHandler = ctx => {
                Assert.That(tcs.TrySetResult(ctx.Request.First.ConvertToString()));
                return Task.CompletedTask;
            };

            var dispatch = this.Dispatcher.Start();

            try {
                this.Client.SendFrame("Hello");
                await Task.Delay(100);
                Assert.That(tcs.Task.IsCompleted, "tcs.Task.IsCompleted");
                Assert.AreEqual("Hello", tcs.Task.Result, "'Hello' == tcs.Task.Result");
            }
            finally {
                dispatch.Complete();
            }

            await Task.Delay(100);
            Assert.That(!dispatch.Completion.IsPending(), "!dispatch.Completion.IsPending()");
            await dispatch.Completion;
        }

        [Test]
        public async Task IRequestHandlerContextShouldSendData() {
            var tcs = new TaskCompletionSource<string>();

            this.Dispatcher.RequestHandler = async ctx => {
                try { 
                    Assert.That(tcs.TrySetResult(ctx.Request.First.ConvertToString()));
                }
                finally { 
                    await ctx.SendAsync(new NetMQMessage(new [] { new NetMQFrame("Hi!") }));
                }
            };

            var dispatch = this.Dispatcher.Start();

            try {
                this.Client.SendFrame("Hello");
                await Task.Delay(100);
                Assert.That(tcs.Task.IsCompleted, "tcs.Task.IsCompleted");
                Assert.AreEqual("Hello", tcs.Task.Result, "'Hello' == tcs.Task.Result");
                Assert.AreEqual("Hi!", this.Client.ReceiveFrameString(), "'Hi!' == this.Client.ReceiveFrameString()");
            }
            finally {
                dispatch.Complete();
            }

            await Task.Delay(1000);
            Assert.That(!dispatch.Completion.IsPending(), "!dispatch.Completion.IsPending()");
            await dispatch.Completion;
        }

        [Test]
        public async Task RequestHandlerShouldSupportConcurrency() {
            var endpoint = $"ipc://{Guid.NewGuid()}";

            var tcs1 = new TaskCompletionSource<string>();
            var tcs2 = new TaskCompletionSource<string>();
            var tcs3 = new TaskCompletionSource<string>();

            this.Dispatcher.RequestHandler = async ctx => {
                var prefix = ctx.Request[0];
                var id = ctx.Request[1].ConvertToString();
                var body = ctx.Request[2].ConvertToString();

                switch (id) {
                    case "#1": {
                        tcs1.SetResult(body);
                        var response = await tcs2.Task + await tcs3.Task;
                        await ctx.SendAsync(new NetMQMessage(
                            new [] { prefix, new NetMQFrame(response) }));
                        break;
                    }
                    case "#2": { 
                        tcs2.SetResult(body);
                        var response = await tcs1.Task + await tcs3.Task;
                        await ctx.SendAsync(new NetMQMessage(
                            new [] { prefix, new NetMQFrame(response) }));
                        break;
                    }
                    default:
                        Assert.Fail("id in ('#1', '#2')");
                        break;
                }
            };

            var dispatch = this.Dispatcher.Start();

            try {
                using (var server = new RouterSocket($"@{endpoint}"))
                using (var client1 = new DealerSocket($">{endpoint}"))
                using (var client2 = new DealerSocket($">{endpoint}")) {
                    this.Dispatcher.Add(server);

                    client1.SendFrame("#1", true);
                    client1.SendFrame("[1]");

                    await Task.Delay(100);
                    Assert.That(!tcs1.Task.IsPending(), "!tcs1.Task.IsPending()");
                    Assert.That(tcs2.Task.IsPending(), "tcs2.Task.IsPending()");
                    Assert.AreEqual(tcs1.Task.Result, "[1]");

                    client2.SendFrame("#2", true);
                    client2.SendFrame("[2]");

                    await Task.Delay(100);
                    Assert.That(!tcs1.Task.IsPending(), "!tcs1.Task.IsPending()");
                    Assert.That(!tcs2.Task.IsPending(), "!tcs2.Task.IsPending()");
                    Assert.AreEqual(tcs2.Task.Result, "[2]");

                    tcs3.SetResult("[3]");

                    Assert.AreEqual("[1][3]", client2.ReceiveFrameString(), "'[1][3]' == client1.ReceiveFrameString()");
                    Assert.AreEqual("[2][3]", client1.ReceiveFrameString(), "'[2][3]' == client1.ReceiveFrameString()");
                }
            }
            finally {
                dispatch.Complete();
            }

            await Task.Delay(100);
            Assert.That(!dispatch.Completion.IsPending(), "!dispatch.Completion.IsPending()");
            await dispatch.Completion;
        }

        [Test]
        public async Task RequestHandlerShouldCompleteUponExceptionInHandler() {
            this.Dispatcher.RequestHandler = ctx => {
                throw new ApplicationException();
            };

            var dispatch = this.Dispatcher.Start();

            this.Client.SendFrameEmpty();

            await Task.Delay(100);

            Assert.That(dispatch.Completion.IsFaulted, "dispatch.Completion.IsFaulted");
            var aggregateException = Assert.ThrowsAsync<AggregateException>(
                () => dispatch.Completion, "await dispatch.Completion throws AggregateException");

            Assert.IsInstanceOf<ApplicationException>(
                aggregateException.InnerException,
                "aggregateException.InnerException is ApplicationException");

            Assert.AreEqual(1, aggregateException.InnerExceptions.Count, "1 == aggregateException.InnerExceptions.Count");
        }

        [Test]
        public async Task RequestHandlerShouldCompleteUponExceptionInAsyncHandler() {
            this.Dispatcher.RequestHandler = async ctx => {
                await Task.Yield();
                throw new ApplicationException();
            };

            var dispatch = this.Dispatcher.Start();

            this.Client.SendFrameEmpty();

            await Task.Delay(100);

            Assert.That(dispatch.Completion.IsFaulted, "dispatch.Completion.IsFaulted");
            var aggregateException = Assert.ThrowsAsync<AggregateException>(
                () => dispatch.Completion, "await dispatch.Completion throws AggregateException");

            Assert.IsInstanceOf<ApplicationException>(
                aggregateException.InnerException,
                "aggregateException.InnerException is ApplicationException");

            Assert.AreEqual(1, aggregateException.InnerExceptions.Count, "1 == aggregateException.InnerExceptions.Count");
        }

        private PairSocket Server => this._server;
        private PairSocket Client => this._client;
        private RequestDispatcher Dispatcher => this._dispatcher;

        private PairSocket _server, _client;
        private RequestDispatcher _dispatcher;
    }
}
