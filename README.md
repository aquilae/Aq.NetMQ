# `Aq.NetMQ`

## Supplemental library for NetMQ project

### `Aq.NetMQ.AsyncPoller`

#### Asynchronous poller for `NetMQSocket` with `Dataflow`-like API

- `void AsyncPoller.Add(NetMQSocket socket)`  
  Adds `socket` to poller on next iteration. If poll is in progress, effective starting from next iteration.

- `void AsyncPoller.Remove(NetMQSocket socket)`  
  Removes `socket` from poller. If poll is in progress, effective starting from next iteration.

- `IRunningAsyncPoller AsyncPoller.Start()`  
  Starts the poller

- `Task IRunningAsyncPoller.Completion { get; }`  
  The task holding poll result

- `void IRunningAsyncPoller.Complete()`  
  Signals the poller to spin, if necessary, and stop.
