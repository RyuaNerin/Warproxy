using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Collections;
using System.IO;
using System.Threading;

namespace Warproxy
{
	public class WarpEngine : IDisposable
	{
		public const int		DefaultPort		= 54635;
		public const int		DefaultBuffer	= 16 * 1024;	// 16 KiB
		public const int		DefaultTimeOut	= 30 * 1000;	// 30 Seconds

		private IList<WarpThread> m_warps = new List<WarpThread>();

		private	WebProxy	m_localProxy;
		private	IWebProxy	m_Proxy;
		private	Socket		m_socketv4;
		private	Socket		m_socketv6;

		private	bool		m_disposed				= false;
		private	bool		m_isStarted				= false;

		private	int			m_maxQueuedConnections	= 20;
		private	int			m_port;
		private	int			m_bufferSize;
		private	int			m_timeOut;

#region Constructor
		public WarpEngine()
			: this(WarpEngine.DefaultPort)
		{
		}
		public WarpEngine(int port)
		{
			this.m_port			= port;
			this.m_timeOut		= WarpEngine.DefaultTimeOut;
			this.m_bufferSize	= WarpEngine.DefaultBuffer;

			this.m_localProxy = new WebProxy("127.0.0.1", this.m_port);
		}
#endregion

#region Destructor
		~WarpEngine()
		{
			Dispose(false);
		}
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}
		protected void Dispose(bool disposing)
		{
			if (!this.m_disposed)
			{
				this.m_disposed = true;

				if (disposing)
				{
					if (this.m_socketv4 != null && this.m_socketv4.Connected)
						this.m_socketv4.Close();

					if (this.m_socketv6 != null && this.m_socketv6.Connected)
						this.m_socketv6.Close();

					for (int i = 0; i < this.m_warps.Count; ++i)
						this.m_warps[i].Dispose();

					this.m_warps.Clear();
				}
			}
		}
#endregion

#region Properties
		public IWebProxy LocalProxy
		{
			get { return this.m_localProxy; }
		}

		internal IWebProxy Proxy
		{
			get { return this.m_Proxy; }
		}

		public int ConnectionCount
		{
			get { lock (this.m_warps) return this.m_warps.Count; }
		}

		public int MaxQueuedConnections
		{
			get { return this.m_maxQueuedConnections; }
			set
			{
				if (this.m_isStarted)
					throw new Exception("Socket is not closed");

				if (value < 0)
					throw new ArgumentOutOfRangeException("Max queued connections is must 0 or more");

				this.m_maxQueuedConnections = value;
			}
		}

		public int Port
		{
			get { return this.m_port; }
			set
			{
				if (this.m_isStarted)
					throw new Exception("Socket is not closed");

				if (value < 1 || 65535 < value)
					throw new ArgumentOutOfRangeException("Port is must 1 ~ 65535");

				this.m_port = value;
				this.SetProxyPort();
			}
		}

		public int TimeOut
		{
			get { return this.m_timeOut; }
			set
			{
				if (value < 0)
					throw new ArgumentOutOfRangeException("TimeOut is must 0 or more");
				
				this.m_timeOut = value;
			}
		}

		public int BufferSize
		{
			get { return this.m_bufferSize; }
			set
			{
				if (value < 0)
					throw new ArgumentOutOfRangeException("BufferSize is must 0 or more");

				this.m_bufferSize = value;
			}
		}
#endregion

		private void SetProxyPort()
		{
			UriBuilder uriBuilder = new UriBuilder(this.m_localProxy.Address);
			uriBuilder.Port = this.m_port;

			this.m_localProxy.Address = uriBuilder.Uri;
		}

		public void SetWarp(WebRequest webRequest)
		{
			webRequest.Proxy = this.m_Proxy;
		}

		public void SetProxy(IWebProxy proxy)
		{
			this.m_localProxy.Credentials = proxy.Credentials;
			this.m_Proxy = proxy;
		}
		
		public void Start()
		{
			if (this.m_isStarted)
				throw new Exception("Socket is not closed");

			this.m_isStarted = true;

			this.SetSocket(this.m_socketv4, AddressFamily.InterNetwork, IPAddress.Any);
			this.SetSocket(this.m_socketv6, AddressFamily.InterNetworkV6 | AddressFamily.InterNetworkV6, IPAddress.IPv6Any);
		}

		private void SetSocket(Socket socket, AddressFamily addressFamily, IPAddress ipAdress)
		{
			socket = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);
			socket.ReceiveTimeout		= this.m_timeOut;
			socket.SendTimeout			= this.m_timeOut;
			socket.ReceiveBufferSize	= this.m_bufferSize;
			socket.SendBufferSize		= this.m_bufferSize;

			socket.Bind(new IPEndPoint(ipAdress, this.m_port));
			socket.Listen(this.m_maxQueuedConnections);
			socket.BeginAccept(BeginAcceptCallback, socket);
		}

		public void Stop()
		{
			if (!this.m_isStarted)
				throw new Exception("Socket is not started");

			try
			{
				this.m_socketv4.Close();
			}
			catch
			{ }

			try
			{
				this.m_socketv6.Close();
			}
			catch
			{ }

			this.m_isStarted = false;
		}

		//////////////////////////////////////////////////////////////////////////

		private void BeginAcceptCallback(IAsyncResult ar)
		{
			Socket listener = (Socket)ar.AsyncState;

			try
			{
				Socket client = listener.EndAccept(ar);
				client.ReceiveTimeout		= this.m_timeOut;
				client.SendTimeout			= this.m_timeOut;
				client.ReceiveBufferSize	= this.m_bufferSize;
				client.SendBufferSize		= this.m_bufferSize;

				lock (this.m_warps)
					this.m_warps.Add(new WarpThread(this, client));

				listener.BeginAccept(BeginAcceptCallback, listener);
			}
			catch { }
		}

		internal void DeleteWarpThread(WarpThread warp)
		{
			try
			{
				lock (this.m_warps)
					this.m_warps.Remove(warp);

				warp.Dispose();
			}
			catch
			{ }
		}
	}
}
