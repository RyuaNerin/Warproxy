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
		private WarpEngine		m_warpEngine;
		private bool			m_disposed = false;
		private Thread			m_thread;
		private MemoryStream	m_buffStream;
		private byte[]			m_buffArray;
		private int				m_bufferSize;
		private Socket			m_client;
		private Socket			m_server;

		public WarpThread(WarpEngine warpEngine, Socket socketThread)
		{
			this.m_client		= socketThread;
			this.m_warpEngine	= warpEngine;

			this.m_bufferSize	= this.m_warpEngine.m_bufferSize;

			this.m_buffStream	= new MemoryStream	(this.m_bufferSize);
			this.m_buffArray	= new byte			[this.m_bufferSize];

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
						this.m_buffStream.Dispose();

						if (this.m_client != null && this.m_client.Connected)
							this.m_client.Close();

						if (this.m_server != null && this.m_server.Connected)
							this.m_server.Close();

						this.m_thread.Abort();
					}
					catch
					{ }
				}
			}
		}

		private static readonly byte[] arrConnect = Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n");
		private void ProgressThread()
		{
			bool isConnect;
			IPAddress ipAddress;
			int port;

			try
			{
				if (!this.m_client.Poll(this.m_warpEngine.TimeOut, SelectMode.SelectRead))
					return;

				// Receive Header
				int headerLength;
				do 
				{
					this.SocketRead(this.m_client);
					headerLength = this.GetHeaderLength();
				}
				while (headerLength == -1);

				string header = this.GetHeader(headerLength);

				if (this.ParseHeader(ref header, out isConnect, out ipAddress, out port))
				{
					this.m_server = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

					this.m_server.NoDelay			= true;
					this.m_server.ReceiveBufferSize	= this.m_bufferSize;
					this.m_server.SendBufferSize	= this.m_bufferSize;

					this.m_server.Connect(ipAddress, port);

					if (this.m_server.Connected)
					{
						if (isConnect)
							this.InsertData(headerLength, WarpThread.arrConnect);
						else
							this.InsertData(headerLength, Encoding.ASCII.GetBytes(header));

						headerLength = this.GetHeaderLength();

						this.SendHeader(this.m_server, headerLength);
						this.ClearStream(headerLength);

						while (this.m_client.Connected && this.m_server.Connected)
						{
							if (this.m_client.Poll(1000, SelectMode.SelectRead))
							{
								this.SocketRead(this.m_client);
								this.SocketWrite(this.m_server);
								this.ClearStream();
							}

							if (this.m_server.Poll(1000, SelectMode.SelectRead))
							{
								this.SocketRead(this.m_server);
								this.SocketWrite(this.m_client);
								this.ClearStream();
							}
							
							Thread.Sleep(10);
						}

						this.m_server.Close();
						this.m_client.Close();
					}
				}
			}
			catch
			{ }

			this.m_warpEngine.DeleteWarpThread(this);
		}

		private void SocketRead(Socket socket)
		{
			int totalReceived = 0;
			int length = socket.Available;
			int received;

			while (totalReceived < length)
			{
				received = socket.Receive(this.m_buffArray, this.m_bufferSize, SocketFlags.None);

				this.m_buffStream.Write(m_buffArray, 0, received);

				totalReceived += received;
			}
		}
		private void SocketWrite(Socket socket)
		{
			int read;
			int sent = 0;
			byte[] buff = new byte[this.m_bufferSize];

			while (sent < this.m_buffStream.Length)
			{
				this.m_buffStream.Position = sent;
				read = (int)(this.m_buffStream.Length - this.m_buffStream.Position);
				if (read > this.m_bufferSize)
					read = this.m_bufferSize;

				if (read > 0)
				{
					read = this.m_buffStream.Read(buff, 0, read);

					read = socket.Send(buff, 0, read, SocketFlags.None);

					sent += read;
					sent += read;
				}
			}
		}
		private void ClearStream()
		{
			this.ClearStream((int)this.m_buffStream.Length);
		}
		private void ClearStream(int delLength)
		{
			int length = (int)(this.m_buffStream.Length - delLength);
			if (length > 0)
			{
				this.m_buffStream.Position = delLength;

				byte[] buff = new byte[length];
				this.m_buffStream.Read(buff, 0, length);

				this.m_buffStream.SetLength(0);
				this.m_buffStream.Seek(0, SeekOrigin.Begin);
				this.m_buffStream.Write(buff, 0, length);
			}
			else
			{
				this.m_buffStream.SetLength(0);
				this.m_buffStream.Seek(0, SeekOrigin.Begin);
			}
		}

		//////////////////////////////////////////////////////////////////////////

		public void SendHeader(Socket socket, int headerLength)
		{
			int lastPosition = 0;

			int sent;
			byte[] buff;

			bool finded;
			int v4 = 0;

			while (lastPosition < headerLength)
			{
				// Find 0x0D0A
				this.m_buffStream.Position = lastPosition;

				finded = false;
				v4 = 0;
				while (this.m_buffStream.Position < headerLength)
				{
					v4 = (v4 & 0xFFFFFF) << 8 | this.m_buffStream.ReadByte();
					if ((v4 & 0xFFFF) == 0x0D0A || v4 == 0x0D0A0D0A)
					{
						finded = true;
						break;
					}
				}

				if (finded)
				{
					buff = new byte[this.m_buffStream.Position - lastPosition];
					this.m_buffStream.Position = lastPosition;
					this.m_buffStream.Read(buff, 0, buff.Length);

					sent = socket.Send(buff, 0, buff.Length, SocketFlags.None);

					lastPosition += sent;
				}
				else
				{
					break;
				}
			}
		}

		private int GetHeaderLength()
		{
			this.m_buffStream.Position = 0;

			int v = 0x00000000;

			while (this.m_buffStream.Position < this.m_buffStream.Length)
			{
				v = (v & 0x00FFFFFF) << 8 | this.m_buffStream.ReadByte();

				if (v == 0x0D0A0D0A)
					return (int)(this.m_buffStream.Position);
			}

			return -1;
		}
		private string GetHeader(int length)
		{
			byte[] buff = new byte[length];

			this.m_buffStream.Seek(0, SeekOrigin.Begin);
			this.m_buffStream.Read(buff, 0, length);

			return Encoding.ASCII.GetString(buff);
		}
		private void InsertData(int position, byte[] inputData)
		{
			int length = (int)(this.m_buffStream.Length - position);
			this.m_buffStream.Position = position;

			byte[] buff = new byte[length];
			this.m_buffStream.Read(buff, 0, length);

			this.m_buffStream.SetLength(0);
			this.m_buffStream.Write(inputData, 0, inputData.Length);
			this.m_buffStream.Write(buff, 0, buff.Length);
		}

		//////////////////////////////////////////////////////////////////////////
		private static Regex regRequest		= new Regex("([^ ]+) ([^ ]+) HTTP", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static Regex regHost		= new Regex("Host: (.+)\r\n", RegexOptions.IgnoreCase | RegexOptions.Compiled);
		private static Regex regIsIP		= new Regex("(([0-9]|[0-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])\\.){3}([0-9]|[0-9][0-9]|1[0-9][0-9]|2[0-4][0-9]|25[0-5])", RegexOptions.Compiled);
		private bool ParseHeader(ref string header, out bool isConnect, out IPAddress ipAdress, out int port)
		{
			isConnect = false;
			ipAdress = null;
			port = 0;

			string host = null;
			string requestUriString;
			Uri requestUri;

			Match match;

			// Get HostName & Port
			match = regRequest.Match(header);
			try
			{
				string scheme = match.Groups[1].Value;

				requestUriString = match.Groups[2].Value;
				UriBuilder uriBuilder = new UriBuilder(requestUriString);

				host = uriBuilder.Host;
				port = uriBuilder.Port;

				// CONNECT
				if (scheme == "CONNECT")
				{
					isConnect = true;
				}
				else
				{
					isConnect = false;

					match = regHost.Match(header);

					if (match.Success)
					{
						if (uriBuilder.Host == null)
						{
							uriBuilder.Host = match.Groups[1].Value;
							host = uriBuilder.Host;
						}
					}
					else
					{
						// Not contains Host Header
						return false;
					}
				}

				requestUri = uriBuilder.Uri;
				
				header = header.Replace(requestUriString, uriBuilder.Path);
			}
			catch
			{
				return false;
			}

			// Check Proxy
			if (this.m_warpEngine.m_Proxy != null && !this.m_warpEngine.m_Proxy.IsBypassed(requestUri))
			{
				Uri proxyUri = this.m_warpEngine.m_Proxy.GetProxy(requestUri);

				host = proxyUri.Host;
				port = proxyUri.Port;
			}

			if (regIsIP.IsMatch(host))
				ipAdress = IPAddress.Parse(host);
			else
				ipAdress = Dns.GetHostAddresses(host)[0];

			return true;
		}
	}
}
