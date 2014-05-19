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
		public const int DefaultPort = 54635;

		private IList<WarpThread> m_warps = new List<WarpThread>();

		internal	WebProxy	m_localProxy;
		internal	IWebProxy	m_Proxy;
		private		bool		m_disposed				= false;
		private		bool		m_isStarted				= false;
		private		int			m_maxQueuedConnections	= 20;
		private		int			m_port					= WarpEngine.DefaultPort;
		internal	int			m_bufferSize			= 16 * 1024;	// 16 KiB
		internal	int			m_timeOut				= 30 * 1000;	// 30 Seconds
		private		Socket		m_socket;

		public WarpEngine()
		{
			this.m_isStarted	= false;
			this.m_localProxy	= new WebProxy("127.0.0.1", this.m_port);
		}
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
					if (this.m_socket != null && this.m_socket.Connected)
						this.m_socket.Close();

					for (int i = 0; i < this.m_warps.Count; ++i)
						this.m_warps[i].Dispose();

					this.m_warps.Clear();
				}
			}
		}

		private void SetProxyPort()
		{
			UriBuilder uriBuilder = new UriBuilder(this.m_localProxy.Address);
			uriBuilder.Port = this.m_port;

			this.m_localProxy.Address = uriBuilder.Uri;
		}

		public IWebProxy LocalProxy
		{
			get { return this.m_localProxy; }
		}

		public int NowQueuedConnections
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
				if (this.m_isStarted)
					throw new Exception("Socket is not closed");

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
				if (this.m_isStarted)
					throw new Exception("Socket is not closed");

				if (value < 0)
					throw new ArgumentOutOfRangeException("BufferSize is must 0 or more");

				this.m_bufferSize = value;
			}
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
		
		public void Start(int port)
		{
			this.Port = port;
			this.Start();
		}
		public void Start()
		{
			this.m_isStarted = true;

			this.m_socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			this.m_socket.ReceiveTimeout	= this.m_timeOut;
			this.m_socket.SendTimeout		= this.m_timeOut;
			this.m_socket.ReceiveBufferSize	= this.m_bufferSize;
			this.m_socket.SendBufferSize	= this.m_bufferSize;
			
			this.m_socket.Bind(new IPEndPoint(IPAddress.Any, this.m_port));

			this.m_socket.Listen(this.m_maxQueuedConnections);
			this.m_socket.BeginAccept(BeginAcceptCallback, this.m_socket);
		}

		public void Stop()
		{
			this.m_socket.Close();

			this.m_isStarted = false;
		}

		//////////////////////////////////////////////////////////////////////////

		private void BeginAcceptCallback(IAsyncResult ar)
		{
			Socket listener = (Socket)ar.AsyncState;

			try
			{
				Socket client = listener.EndAccept(ar);
				client.ReceiveBufferSize = this.m_bufferSize;
				client.SendBufferSize = this.m_bufferSize;

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
