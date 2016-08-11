# `Aq.NetMQ`

## Supplemental library for NetMQ project

### Usage

#### `Aq.NetMQ.AsyncPoller`

```csharp
class Program {
  static async Task MainAsync() {
    PairSocket server, client;
    PairSocket.CreateSocketPair(out server, out client);
  
    using (server)
    using (client)
    using (var dispatcher = new RequestDispatcher(RequestHandler)) {
      server.Bind("ipc://example");
      dispatcher.Add(server);
      
      var dispatch = dispatcher.Start();
      
      client.Connect("ipc://example");
      
      client.SendFrame("Ping");
      Debug.Assert("Pong" == client.ReceiveFrameString());
      
      client.SendFrame("Error"); // will cause dispatch.Completion to fail
      
      dispatch.Complete();
      await dispatch.Completion; // will throw NotImplementedException
    }
  }
  
  static async Task RequestHandler(IRequestHandlerContext context) {
    if ("Ping" == context.Request.First.ConvertToString()) {
      var response = new NetMQMessage();
      response.Append("Pong");
      await context.SendAsync(response);
    }
    else {
      throw new NotImplementedException();
    }
  }
}
```

### API

#### `Aq.NetMQ.AsyncPoller`

##### Asynchronous poller for `NetMQSocket` with `Dataflow`-like API

- `IRunningRequestDispatcher AsyncPoller.IsRunning { get; }`

- `IRunningRequestDispatcher AsyncPoller.Running { get; }`  
  When running, returns current `IRunningReuqestDispatcher` instance

- `void AsyncPoller.Add(NetMQSocket socket)`  
  Adds `socket` to poller on next iteration. If poll is in progress, effective starting from next iteration.

- `void AsyncPoller.Remove(NetMQSocket socket)`  
  Removes `socket` from poller. If poll is in progress, effective starting from next iteration.

- `IRunningAsyncPoller AsyncPoller.Start()`  
  Starts the poller

#### `Aq.NetMQ.IRunningAsyncPoller`

##### Represents running state of `AsyncPoller`

- `Task IRunningAsyncPoller.Completion { get; }`  
  The task holding poll result

- `void IRunningAsyncPoller.Complete()`  
  Signals the poller to spin, if necessary, and stop.

#### `Aq.NetMQ.RequestDispatcher`

##### Asynchronous request dispatcher for NetMQ sockets

- `delegate Task RequestHandler(IRequestHandlerContext context)`  
  Delegate used to handle incoming requests

- `RequestDispatcher(RequestHandler requestHandler)`  
  Creates new instance of `RequestDispatcher` that passes incoming requests to `requestHandler` when running

- `RequestHandler RequestDispatcher.RequestHandler { get; set; }`  
  Gets or sets handler to be used for incoming requests

- `void RequestDispatcher.Add(NetMQSocket socket)`  
  Adds `socket` to dispatcher's poller. See `AsyncPoller.Add()`

- `void RequestDispatcher.Remove(NetMQSocket socket)`  
  Removes `socket` from dispatcher's poller. See `AsyncPoller.Remove()`

- `IRunningRequestDispatcher RequestDispatcher.Start()`  
  Starts the dispatcher


#### `Aq.NetMQ.IRunningRequestDispatcher`

##### Represents running state of `AsyncPoller`

- `Task IRunningRequestDispatcher.Completion { get; }`
  The task holding dispatch result

- `void IRunningRequestDispatcher.Complete()`  
  Dispatcher will stop accepting new requests, wait for pending handlers to finish, then exit. See also `AsyncPoller.Complete()`

#### `Aq.NetMQ.IRequestHandlerContext`

- `RequestDispatcher IRequestHandlerContext.Dispatcher { get; }`  
  Dispatcher calling the handler

- `NetMQMessage IRequestHandlerContext.Request { get; }`  
  Incoming request buffered frames

- `CancellationToken IRequestHandlerContext.Cancellation { get; }`  
  Cancellation token associated with current dispatch operation. Being canceled upon `RequestDispatcher.Complete()` call

- `Task<bool> IRequestHandlerContext.TrySendAsync(NetMQMessage response)`  
  Attempts to send `response` on next poller iteration. Returns `true` upon successful send, `false` if `NetMQSocket.Send()` would block

- `Task<bool> IRequestHandlerContext.TrySendAsync(NetMQMessage response, CancellationToken cancellation)`  
  Attempts to send `response` on next poller iteration. Returns `true` upon successful send, `false` if `NetMQSocket.Send()` would block. Throws `TaskCanceledException` if `cancellation` being canceled before next poller iteration.

- `Task IRequestHandlerContext.SendAsync(NetMQMessage response)`  
  Waits for socket to unblock and sends `response`

- `Task IRequestHandlerContext.SendAsync(NetMQMessage response, CancellationToken cancellation)`  
  Waits for socket to unblock and sends `response`. Throws `TaskCanceledException` if `cancellation` being canceled before send succeeds
