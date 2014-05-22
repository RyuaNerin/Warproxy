using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Text.RegularExpressions;

namespace RyuaNerin.Warp
{
	internal class Warp
	{
		private struct KeyValue
		{
			public KeyValue(string key, string val)
			{
				this.key = key;
				this.val = val;
			}
			public override string ToString()
			{
				return this.ToString(false);
			}
			public string ToString(bool CLCL)
			{
				StringBuilder sb = new StringBuilder();
				sb.Append(this.key);
				sb.Append(": ");
				sb.Append(this.val);
				sb.Append("\r\n");

				if (CLCL)
					sb.Append("\r\n");

				return sb.ToString();
			}

			public string key;
			public string val;
		}

		private enum Status { GetHeader, SendHeader, Connected }

		private const int BufferSize = 4096;	//  4 KB

		// Sockets
		private Socket	_sockLocal = null;
		private Socket	_sockHost = null;
		private Status	_status;
		private int		_port;

		// Buffer
		private byte[]			_LocalBuff	= new byte[Warp.BufferSize];
		private MemoryStream	_Localms	= new MemoryStream();

		private byte[]			_HostBuff	= new byte[Warp.BufferSize];
		private MemoryStream	_Hostms		= new MemoryStream();

		// Header
		private long			_read		= 4;		// For Stream : PO = _read - 4 => Default 4
		private string			_host		= null;
		private string			_firstLine	= null;
		private long			_clength	= 0;		// Content-Length
		private int				_headerl	= 0;		// Sent Header Index
		private IList<KeyValue>	_dicHeaders	= new List<KeyValue>();

		private bool			_isClosed	= false;

		private bool			_endOfRecieve = false;

		public Warp(Socket local)
		{
			this._sockLocal = local;

			this._status = Status.GetHeader;
			this._sockLocal.BeginReceive(this._LocalBuff, 0, Warp.BufferSize, SocketFlags.None, LocalReceive, null);
		}

		private void LocalReceive(IAsyncResult ar)
		{
			if (this._isClosed) return;

			lock (this)
			{
				try
				{
					int read = this._sockLocal.EndReceive(ar);
					if (read > 0)
					{
						this._Localms.Write(this._LocalBuff, 0, read);

						this.Do(true, true);
					}
					else
					{
						if (this.Disconnect())
							return;
					}

					this._sockLocal.BeginReceive(this._LocalBuff, 0, Warp.BufferSize, SocketFlags.None, LocalReceive, null);
				}
				catch
				{ }
			}
		}
		private void LocalSend(IAsyncResult ar)
		{
			if (this._isClosed) return;

			lock (this)
			{
				this._sockLocal.EndSend(ar);

				this.Do(true, false);
			}
		}

		private void HostReceive(IAsyncResult ar)
		{
			if (this._isClosed) return;

			lock (this)
			{
				try
				{
					int read = this._sockHost.EndReceive(ar);
					if (read > 0)
					{
						this._Hostms.Write(this._HostBuff, 0, read);

						this.Do(false, true);
					}
					else
					{
						this._endOfRecieve = true;
						return;
					}

					this._sockHost.BeginReceive(this._HostBuff, 0, Warp.BufferSize, SocketFlags.None, HostReceive, null);
				}
				catch
				{ }
			}
		}
		private void HostSend(IAsyncResult ar)
		{
			if (this._isClosed) return;

			lock (this)
			{
				this._sockHost.EndSend(ar);

				this.Do(false, false);
			}

		}
		private bool Disconnect()
		{
			if (this._Hostms.Length + this._Localms.Length == 0)

			{
// 				this._sockHost.Shutdown(SocketShutdown.Both);
// 				this._sockLocal.Shutdown(SocketShutdown.Both);

// 				this._sockHost.Disconnect(false);
// 				this._sockLocal.Disconnect(false);

//				this._isClosed = true;

				return true;
			}

			return false;
		}

		private void Do(bool isLocal, bool isRecieve)
		{
			if (isLocal)
			{
				// Recived
				// >> Send
				if (isRecieve)
				{
					// Header
					if (this._status == Status.GetHeader && this.HeaderCheck())
					{
						this._status = Status.SendHeader;

						this.BeginSend(this._sockHost, this._firstLine, HostSend);
					}
					else if (this._status == Status.SendHeader)
					{
						if (this._Localms.Length > 0)
							this.BeginSend(this._sockHost, this._Localms, HostSend);
					}
				}
				else
				{
					if (this._status == Status.Connected)
					{
						if (this.IsSocketConnected())
						{
								this.BeginSend(this._sockLocal, this._Hostms, this.LocalSend);
						}
						else
						{
							this._sockHost.Close();
							this._sockLocal.Close();
						}
					}
				}
			}
			else
			{
				if (!isRecieve)
				{
					if (this._status == Status.SendHeader)
					{
						if (this._headerl < this._dicHeaders.Count)
						{
							this.BeginSend(this._sockHost, this._dicHeaders[this._headerl].ToString(this._headerl == this._dicHeaders.Count - 1), this.HostSend);
							this._headerl++;
						}
						else
						{
							this._status = Status.Connected;

							this._sockHost.BeginReceive(this._HostBuff, 0, Warp.BufferSize, SocketFlags.None, HostReceive, null);

							if (this._Localms.Length > 0)
								this.BeginSend(this._sockHost, this._Localms, this.HostSend);
						}
					}
				}
				else
				{
					// Recived
					// >> Send

					if (this._status == Status.Connected)
					{
						this.BeginSend(this._sockLocal, this._Hostms, this.LocalSend);
					}
				}
			}
		}

		#region Header
		private static byte[] CrLfCrLF = { 0x0D, 0x0A, 0x0D, 0x0A };
		private bool HeaderCheck()
		{
			bool b = false;

			this._Localms.Position = this._read - 4;

			this._read = this._Localms.Length;

			int p = 0;

			while (this._Localms.Position < this._Localms.Length)
			{
				if (this._Localms.ReadByte() == CrLfCrLF[p])
				{
					p++;
					if (p == 4)
					{
						this.HeaderParse();
						b = true;
						break;
					}
				}
				else
				{
					p = 0;
				}
			}

			this._Localms.Seek(0, SeekOrigin.End);

			return b;
		}

		private static Regex regGetIP = new Regex("([0-9]+\\.[0-9]+\\.[0-9]+\\.[0-9]+)", RegexOptions.Compiled);
		private static Regex regURI = new Regex("^[^ ]+ ([^ ]+)", RegexOptions.Compiled);
		private void HeaderParse()
		{
			int length = (int)this._Localms.Position;
			int wlength = (int)(this._Localms.Length - this._Localms.Position);

			byte[] buff = new byte[length];
			this._Localms.Seek(0, SeekOrigin.Begin);
			this._Localms.Read(buff, 0, length);
			string[] header = Encoding.ASCII.GetString(buff).Replace("\r", "").Split('\n');

			if (wlength > 0)
			{
				buff = new byte[wlength];
				this._Localms.Read(buff, 0, wlength);

				this._Localms.SetLength(0);

				this._Localms.Write(buff, 0, wlength);
			}
			else
			{
				this._Localms.SetLength(0);
			}

			//////////////////////////////////////////////////////////////////////////
			// Parse Header

			this._firstLine = header[0];
			this._port = this._firstLine.IndexOf("https://") >= 0 ? 443 : 80;

			int ind;
			string key, val;
			bool containsProxy = false;
			string warpProxyHeader = null;
			for (int i = 1; i < header.Length; ++i)
			{
				ind = header[i].IndexOf(':');
				if (ind < 0)
					continue;

				key = header[i].Substring(0, ind).ToLower().Trim();
				val = header[i].Substring(ind + 1).Trim();

				switch (key)
				{
					case "content-length":
						this._clength = long.Parse(val);
						break;

					case "host":
						if (this._host == null)
							this._host = val;
						break;

					case "warp-proxy":
						containsProxy = true;
						warpProxyHeader = val;
						continue;

// 					case "connection":
// 					case "proxy-connection":
// 						continue;
				}

				this._dicHeaders.Add(new KeyValue(key, val));
			}

			//////////////////////////////////////////////////////////////////////////
			// Set Request Uri

			Uri requestUri = null; // use for proxy

			Match mRequestURI = regURI.Match(this._firstLine);
			string uri = mRequestURI.Groups[1].Value;
			if (uri.StartsWith("https://") || uri.StartsWith("http://"))
			{
				requestUri = new Uri(uri);

				this._firstLine = this._firstLine.Replace(requestUri.AbsolutePath, "");
			}
			else
			{
				if (containsProxy) requestUri = new Uri(String.Format("http://{0}/{1}", this._host, uri));
			}

			//////////////////////////////////////////////////////////////////////////
			// Proxy
			if (containsProxy)
			{
				try
				{
					WebProxy webProxy = Helper.FromSerializedProxy(warpProxyHeader) as WebProxy;
					if (webProxy != null)
					{
						if (webProxy.IsBypassed(requestUri))
						{
							Uri uriProxy = webProxy.GetProxy(requestUri);
							this._host = uriProxy.Host;
							this._port = uriProxy.Port;
						}
					}
				}
				catch
				{
					// Don't use proxy if An error has occurred.
				}
			}

			//////////////////////////////////////////////////////////////////////////
			// Socket

			this._sockHost = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

			Match mIPAddress = regGetIP.Match(this._host);
			if (mIPAddress.Success)
				this._sockHost.Connect(IPAddress.Parse(mIPAddress.Groups[0].Value), this._port);

			else
				this._sockHost.Connect(Dns.GetHostAddresses(this._host)[0], this._port);

			this._sockHost.SendBufferSize = 4096;
			this._sockHost.NoDelay = true;
		}
		#endregion

		private void BeginSend(Socket socket, string str, AsyncCallback callback)
		{
			byte[] buff = Encoding.ASCII.GetBytes(str);
			socket.BeginSend(buff, 0, buff.Length, SocketFlags.None, callback, null);
		}
		private void BeginSend(Socket socket, Stream stream, AsyncCallback callback)
		{
			if (stream.Length > 0)
			{
				stream.Seek(0, SeekOrigin.Begin);

				int lengthRead = (int)stream.Length;
				if (lengthRead > BufferSize)
					lengthRead = BufferSize;

				int lengthSave = (int)(stream.Length - lengthRead);
				if (lengthSave < 0)
					lengthSave = 0;

				byte[] buffSend = new byte[lengthRead];
				byte[] buffSave = new byte[lengthSave];

				stream.Seek(0, SeekOrigin.Begin);
				stream.Read(buffSend, 0, lengthRead);
				stream.Read(buffSave, 0, lengthSave);

				stream.SetLength(0);
				stream.Seek(0, SeekOrigin.Begin);
				stream.Write(buffSave, 0, lengthSave);

				socket.BeginSend(buffSend, 0, lengthSave, SocketFlags.None, callback, null);
			}
		}

		private bool IsSocketConnected()
		{
			return !(this._sockHost.Poll(1, SelectMode.SelectRead) && this._sockHost.Available == 0);
		}
	}
}
