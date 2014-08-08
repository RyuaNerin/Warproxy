# Warproxy 1.1.2
* HTTP transparent local proxy library
* Written by C# .Net Framework 3.5
* [Based Warp (wirtten in python)](https://github.com/devunt/warp)


# SUPPORT
* Support TCP only.
* Support IPv4 & IPv6.
* Support keep-alive connection (HTTP 1.1).
* Support other proxy.


# [LICENSE](/LICENSE)
* MIT LICENSE
* *EXEMPTION CLAUSE*
 * *All caused by the usage of WARP is the responsibility of the user.*
 * *Code contributors WARP is not responsible for the use.*


# CLASS
## WarpEngine (IDisposable)
* Constructor
 * `new WarpEngine()`
 * `new WarpEngine(Port)`


* Function
 * `void Dispose()`
 * `void Start()`
 * `void Stop()`
 * `void SetWarp(WebRequest)`


* Property
 * `int ConnectionCount (ReadOnly)`
 * `int MaxQueuedConnections`
 * `int BufferSize`
 * `int Port`
 * `int TimeOut`


## WarpExtensions (static)
* Function
 * `void SetWarp(this WebRequest, WarpEngine)`
 * `void SetWarp(this WebRequest, WarpEngine, IWebProxy)`


* Usage
 * `(WebRequest).SetWarp(engine)`
 * `(WebRequest).SetWarp(engine, IWebProxy)`


* Example
 ```
 WebRequest req1 = WebRequest.Create("http://www.google.com/");
 WebRequest req2 = WebRequest.Create("http://www.google.com/");
 
 engine.SetProxy(HttpWebRequest.DefaultWebProxy);
 
 // req1 use DefaultWebProxy
 req1.SetWarp(engine);
 
 // req2 use NewWebProxy. not DefaultWebProxy
 req2.SetWarp(engine, NewWebProxy);
 ```