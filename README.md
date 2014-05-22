# Warproxy 1.0.0
* HTTP transparent local proxy
* Written by C# .Net Framework 3.5
* Support TCP Only.
* [Based Warp (wirtten in python)](https://github.com/devunt/warp)


# LICENSE
* MIT LICENSE (Include in [LICENSE file](/LICENSE))
* *EXEMPTION CLAUSE*
 * *All caused by the usage of WARP is the responsibility of the user.*
 * *Code contributors WARP is not responsible for the use.*

# CLASS

## WarpEngine (IDisposable)
* Function

|Return Type|Name|
|:---:|---|
|void|Dispose()
|void|Start()
|void|Start(int)
|void|Stop()
|void|SetWarp(WebRequest)
|void|SetProxy(IWebProxy)


* Property

|Return Type|Name|ReadOnly|
|:---:|---|---|
|int|MowQueuedConnections|O|
|int|MaxQueuedConnectinos||
|int|BufferSize||
|int|Port||
|int|TimeOut|||

## WarpExtensions (static)
* Function

|Return Type|Name|
|:---:|---|
|void|SetWarp(this WebRequest, WarpEngine)|
|void|SetWarp(this WebRequest, WarpEngine, IWebProxy)|


* Usage
 * `WebRequest.SetWarp(engine);`
 * `WebRequest.SetWarp(engine, proxy);`

# EXAMPLE
```cs
WarpEngine engine = new WarpEngine();
engine.Start();
WebRequest req = WebRequest.Create("url");

// #1
engine.SetProxy(HttpWebRequest.DefaultWebProxy);
req.Proxy = engine.LocalProxy;

// #2
// engine.SetProxy(HttpWebRequest.DefaultWebProxy);
// engine.SetWarp(req);

// #3
// engine.SetProxy(HttpWebRequest.DefaultWebProxy);
// req.SetWarp(engine);

// #4
// req.SetWarp(engine, HttpWebRequest.DefaultWebProxy);
```
