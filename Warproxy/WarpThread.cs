using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.IO;
using System.Text.RegularExpressions;
using System.Net;
using System.Security;

namespace Warproxy
{
	internal class WarpThread : IDisposable
	{
		private WarpEngine	m_warpEngine;
		private bool		m_disposed = false;
		private Thread		m_thread;

		private Socket		m_client;
		private ExtStream	m_stmClient;

		private Socket		m_server;
		private ExtStream	m_stmServer;

		public WarpThread(WarpEngine warpEngine, Socket socketThread)
		{
			this.m_warpEngine	= warpEngine;

			this.m_client		= socketThread;
			this.m_stmClient	= new ExtStream(this.m_warpEngine.BufferSize);

			this.m_stmServer	= new ExtStream(this.m_warpEngine.BufferSize);

			this.m_thread = new Thread(ProgressThread);
			this.m_thread.Start();
		}
		~WarpThread()
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
					try
					{
						if (this.m_client != null && this.m_client.Connected)
							this.m_client.Close();

						if (this.m_server != null && this.m_server.Connected)
							this.m_server.Close();

						if (this.m_thread.IsAlive)
							this.m_thread.Abort();

						this.m_stmClient.Dispose();
						this.m_stmServer.Dispose();
					}
					catch
					{ }
				}
			}
		}

		private static readonly byte[] arrConnect = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
		private void ProgressThread()
		{
			int			port;
			bool		isConnect;
			IPAddress[]	ipAddresses4;
			IPAddress[]	ipAddresses6;

			bool		isNotNull = false;
			string		header;
			int			headerLength;
			int			headerLengthFixed;

			bool		getResponseHeader = true;

			bool		receivedData;

			// For Keep-Alive
			bool		keepAlive	= false;
			int			timeOut		= 5 * 10000000; // 1 Second = 1000 0000 Ticks
			int			max			= 100;

			long		endTick		= DateTime.Now.Ticks + timeOut;

			try
			{
				if (!this.m_client.Poll(this.m_warpEngine.TimeOut, SelectMode.SelectRead))
					return;

				// isNotNull	!isNotNull	Connected
				// ---			---			---
				// false		true					=> true
				// true			false		true		=> true

				while (this.m_client.Connected && (!isNotNull || this.m_server.Connected))
				{
					receivedData = false;

					//////////////////////////////////////////////////////////////////////////

					if (this.m_client.Poll(1000, SelectMode.SelectRead))
					{
						receivedData = receivedData | this.m_stmClient.FromSocket(this.m_client);

						headerLength = this.m_stmClient.GetHeaderLength();

						if (headerLength > 0)
						{
							header = this.m_stmClient.GetHeader(headerLength);

							getResponseHeader = true;

							if (this.ParseRequestHeader(ref header, out isConnect, out ipAddresses4, out ipAddresses6, out port, out headerLengthFixed))
							{
								if (isNotNull && (keepAlive && max == 0))
									this.m_server.Close();

								if (ipAddresses4.Length > 0)
									this.ConnectionSocket(AddressFamily.InterNetwork, ipAddresses4, port);

								if (ipAddresses4.Length == 0 | !this.m_server.Connected)
									this.ConnectionSocket(AddressFamily.InterNetworkV6, ipAddresses6, port);

								if (this.m_server.Connected)
								{
									isNotNull = true;

									if (isConnect)
										this.m_stmClient.InsertData(headerLength, WarpThread.arrConnect);
									else
										this.m_stmClient.InsertData(headerLength, Encoding.ASCII.GetBytes(header));

									this.m_stmClient.SendHeader(this.m_server, headerLengthFixed);
								}
								else
								{
									break;
								}
							}
						}

						if (isNotNull && this.m_server.Connected)
							this.m_stmClient.ToSocket(this.m_server);
					}

					//////////////////////////////////////////////////////////////////////////

					if (isNotNull && this.m_server.Poll(1000, SelectMode.SelectRead))
					{
						receivedData = receivedData | this.m_stmServer.FromSocket(this.m_server);

						if (getResponseHeader)
						{
							headerLength = this.m_stmServer.GetHeaderLength();

							if (headerLength > 0)
							{
								header = this.m_stmServer.GetHeader(headerLength);

								if (this.ParseResponseHeader(header, out keepAlive, out timeOut, out max))
									getResponseHeader = false;
							}
						}

						if (!getResponseHeader)
							this.m_stmServer.ToSocket(this.m_client);
					}

					//////////////////////////////////////////////////////////////////////////

					if (!receivedData)
					{
						Thread.Sleep(50);

						if (DateTime.Now.Ticks >= endTick)
							break;
					}
					else
					{
						endTick = DateTime.Now.Ticks + timeOut;
					}
				}
			}
			catch
			{ }

			if (this.m_client.Connected)
				this.m_client.Close();

			if (this.m_server != null && this.m_server.Connected)
				this.m_server.Close();

			this.m_warpEngine.DeleteWarpThread(this);
		}

		private void ConnectionSocket(AddressFamily addressFamily, IPAddress[] ipAddresses, int port)
		{
			this.m_server = new Socket(addressFamily, SocketType.Stream, ProtocolType.Tcp);
			this.m_server.ReceiveTimeout	= this.m_warpEngine.TimeOut;
			this.m_server.SendTimeout		= this.m_warpEngine.TimeOut;
			this.m_server.ReceiveBufferSize	= this.m_warpEngine.BufferSize;
			this.m_server.SendBufferSize	= this.m_warpEngine.BufferSize;
			this.m_server.NoDelay			= true;
			this.m_server.Connect(ipAddresses, port);
		}

		//////////////////////////////////////////////////////////////////////////

		private void GetAddresses(string host, ref IPAddress[] ipAddresses4, ref IPAddress[] ipAddresses6)
		{
			List<IPAddress> lstIPv4 = new List<IPAddress>();
			List<IPAddress> lstIPv6 = new List<IPAddress>();

			foreach (IPAddress ip in Dns.GetHostAddresses(host))
				if (ip.AddressFamily == AddressFamily.InterNetwork)
					lstIPv4.Add(ip);
				else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
					lstIPv6.Add(ip);

			ipAddresses4 = lstIPv4.ToArray();
			ipAddresses6 = lstIPv6.ToArray();
		}

		private static Regex	regRequest	= new Regex("([^ ]+) ([^ ]+) HTTP",			RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static Regex	regHeader	= new Regex("([^:\r\n]+): ([^\r\n]+)\r\n",	RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private bool ParseRequestHeader(
			ref string header,
			out bool isConnect,
			out IPAddress[] ipAddresses4,
			out IPAddress[] ipAddresses6,
			out int port,
			out int headerLength)
		{
			isConnect		= false;
			ipAddresses4				= null;
			ipAddresses6				= null;
			port			= 0;
			headerLength	= 0;

			string host = null;

			UriBuilder uriBuilder;

			Match m;

			//////////////////////////////////////////////////////////////////////////
			// Edit Request Line
			m = regRequest.Match(header);
			if (!m.Success)
				return false;

			string requestUriString = m.Groups[2].Value;

			uriBuilder = new UriBuilder(requestUriString);

			isConnect = (m.Groups[1].Value == "CONNECT");

			// SSL
			if (isConnect)
			{
				GetAddresses(uriBuilder.Host, ref ipAddresses4, ref ipAddresses6);
				port = uriBuilder.Port;

				return true;
			}

			header = header.Replace(requestUriString, String.Format("{0}{1}", uriBuilder.Path, uriBuilder.Query));

			//////////////////////////////////////////////////////////////////////////
			// Check Headers
			string key, val;
			bool foundHost = false;

			WebProxy	webProxy		= null;
			bool		containsProxy	= false;

			m = regHeader.Match(header);
			while (m.Success)
			{
				key = m.Groups[1].Value.ToLower();
				val = m.Groups[2].Value;

				switch (key)
				{
					case "host":
						foundHost = true;
						host = val;
						break;

					case "warproxy":
						header = header.Replace(m.Groups[0].Value, "");
						try
						{
							webProxy		= Helper.ToProxy(val);
							containsProxy	= true;
						}
						catch
						{
							containsProxy	= false;
						}
						break;

					// Remove Header
					case "proxy-connection":
						header = header.Replace(m.Groups[0].Value, "");
						break;
				}

				m = m.NextMatch();
			}

			if (!foundHost)
				return false;

			if (uriBuilder.Scheme == null)
				uriBuilder.Scheme = "http";

			if (uriBuilder.Host == null)
				uriBuilder.Host = host;

			Uri requestUri = uriBuilder.Uri;

			//////////////////////////////////////////////////////////////////////////
			// Proxy
			if (containsProxy)
			{
				// Other Proxy
				if (!webProxy.IsBypassed(requestUri))
					requestUri = webProxy.GetProxy(requestUri);
			}
			else
			{
				// Default Proxy
				if (this.m_warpEngine.Proxy != null && !this.m_warpEngine.Proxy.IsBypassed(requestUri))
					requestUri = this.m_warpEngine.Proxy.GetProxy(requestUri);
			}

			//////////////////////////////////////////////////////////////////////////

			this.GetAddresses(requestUri.Host, ref ipAddresses4, ref ipAddresses6);
			port = requestUri.Port;

			headerLength = Encoding.ASCII.GetByteCount(header);

			return true;
		}

		private static Regex	regResponse		= new Regex("HTTP/([^ ]+)",					RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static Regex	regConnection	= new Regex("connection: ([^\r\n]+)\r\n",	RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static Regex	regKeepAlive	= new Regex("keep-alive: ([^\r\n]+)\r\n",	RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static Regex	regTimeout		= new Regex("timeout=(\\d+)",				RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static Regex	regMax			= new Regex("max=(\\d+)",					RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private bool ParseResponseHeader(string header, out bool keepAlive, out int timeOut, out int max)
		{
			keepAlive	= false;
			timeOut		= 5 * 10000000;
			max			= 0;

			Match m;

			m = regResponse.Match(header);
			if (!m.Success)
				return false;

			keepAlive = m.Groups[1].Value == "1.1";

			if (keepAlive)
			{
				m = regConnection.Match(header);

				if (m.Success && m.Groups[1].Value == "keep-alive")
				{
					m = regKeepAlive.Match(header);
					if (m.Success)
					{
						try
						{
							timeOut = int.Parse(regTimeout.Match(m.Groups[1].Value).Groups[1].Value);
							max = int.Parse(regMax.Match(m.Groups[1].Value).Groups[1].Value);
						}
						catch
						{
							timeOut	= 5;
							max		= 0;
						}
					}

					timeOut = timeOut * 10000000;	// Seconds To Ticks
				}
			}

			return true;
		}
	}
}
