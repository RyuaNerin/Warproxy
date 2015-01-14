using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace Warproxy
{
	internal class ExtStream : IDisposable
	{
		private int				m_buffSize;
		private byte[]			m_buff;
		private bool			m_disposed = false;
		private MemoryStream	m_stream;
		public  bool			m_firstHeader = true;

		public ExtStream(int bufferSize)
		{
			this.m_buffSize	= bufferSize;
			this.m_stream	= new MemoryStream(bufferSize);
			this.m_buff		= new byte[bufferSize];
		}
		~ExtStream()
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
					this.m_stream.Close();
					this.m_stream.Dispose();

					this.m_buff = null;
				}
			}
		}

		public Stream BaseStream
		{
			get { return this.m_stream; }
		}

		public bool FromSocket(Socket socket)
		{
			int remained = socket.Available;
			int received;

			if (remained > 0)
			{
				while (remained > 0)
				{
					received = socket.Receive(this.m_buff, this.m_buffSize, SocketFlags.None);

					this.m_stream.Seek(0, SeekOrigin.End);
					this.m_stream.Write(this.m_buff, 0, received);

					remained -= received;
				}

				return true;
			}

			return false;
		}
		
		public void ToSocket(Socket socket)
		{
			if (this.m_stream.Length > 0)
			{
				int read;
				int sent = 0;

				while (sent < this.m_stream.Length)
				{
					this.m_stream.Position = sent;
					read = (int)(this.m_stream.Length - this.m_stream.Position);
					if (read > this.m_buffSize)
						read = this.m_buffSize;

					if (read > 0)
					{
						read = this.m_stream.Read(this.m_buff, 0, read);

						read = socket.Send(this.m_buff, 0, read, SocketFlags.None);

						sent += read;
					}
				}

				this.m_stream.SetLength(0);
				this.m_stream.Seek(0, SeekOrigin.Begin);
			}
		}

		public void ClearStream(int delLength)
		{
			int length = (int)(this.m_stream.Length - delLength);
			if (length > 0)
			{
				this.m_stream.Position = delLength;

				int read;

				MemoryStream tempStream = new MemoryStream();

				while (this.m_stream.Position < this.m_stream.Length)
				{
					read = (int)(this.m_stream.Length - this.m_stream.Position);
					if (read > this.m_buffSize)
						read = this.m_buffSize;

					this.m_stream.Read(this.m_buff, 0, read);
					tempStream.Write(this.m_buff, 0, read);
				}

				this.m_stream.Close();
				this.m_stream.Dispose();
				this.m_stream = tempStream;
			}
			else
			{
				this.m_stream.SetLength(0);
				this.m_stream.Seek(0, SeekOrigin.Begin);
			}
		}

		public int GetHeaderLength()
		{
			this.m_stream.Position = 0;

			int v = 0x00000000;

			while (this.m_stream.Position < this.m_stream.Length)
			{
				v = v << 8 | this.m_stream.ReadByte();

				if (v == 0x0D0A0D0A)
					return (int)(this.m_stream.Position);
			}

			return -1;
		}

		public string GetHeader(int length)
		{
			byte[] buff = new byte[length];

			this.m_stream.Seek(0, SeekOrigin.Begin);
			this.m_stream.Read(buff, 0, length);

			return Encoding.ASCII.GetString(buff);
		}

		public void InsertData(int position, byte[] inputData)
		{
			int length = (int)(this.m_stream.Length - position);
			this.m_stream.Position = position;

			byte[] buff = new byte[length];
			this.m_stream.Read(buff, 0, length);

			this.m_stream.SetLength(0);
			this.m_stream.Write(inputData, 0, inputData.Length);
			this.m_stream.Write(buff, 0, buff.Length);
		}

		public void SendHeader(Socket socket, int headerLength)
		{
			int lastPosition = 0;

			int		sent;
			int		len;
			int		v = 0;

			bool finded;

			while (lastPosition < headerLength)
			{
				// Find 0x0D0A
				this.m_stream.Position = lastPosition;

				finded = false;
				v = 0;
				while (this.m_stream.Position < headerLength)
				{
					v = (v & 0xFFFFFF) << 8 | this.m_stream.ReadByte();
					if (v == 0x0D0A0D0A)
					{
						finded = true;
						break;
					}
					else if ((v & 0xFFFF0000) == 0x0D0A0000)
					{
						this.m_stream.Position -= 2;
						finded = true;
						break;
					}
				}
				if ((v & 0x0000FFFF) == 0x0D0A)
					finded = true;

				if (finded)
				{
					len = (int)(this.m_stream.Position - lastPosition);

					if (len > this.m_buffSize)
						len = this.m_buffSize;

					this.m_stream.Position = lastPosition;

					if (this.m_firstHeader)
					{
						this.m_buff[0] = 0x0D;
						this.m_buff[1] = 0x0A;
						this.m_stream.Read(this.m_buff, 2, len);
						sent = socket.Send(this.m_buff, 0, len + 2, SocketFlags.None);

						if (sent > 2)
						{
							sent -= 2;

							m_firstHeader = false;
						}
					}
					else
					{
						this.m_stream.Read(this.m_buff, 0, len);
						sent = socket.Send(this.m_buff, 0, len, SocketFlags.None);
					}

					lastPosition += sent;
				}
				else
				{
					break;
				}
			}

			this.ClearStream(headerLength);
		}
	}
}
