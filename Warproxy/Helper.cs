using System;
using System.IO;
using System.Net;
using System.Runtime.Serialization.Formatters.Binary;

namespace Warproxy
{
	internal static class Helper
	{
		public static string FromProxy(WebProxy	webProxy)
		{
			string base64String;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				BinaryFormatter	formatter = new BinaryFormatter();
				formatter.Serialize(memoryStream, webProxy);

				base64String = Convert.ToBase64String(memoryStream.ToArray(), Base64FormattingOptions.None);

				memoryStream.Close();
				memoryStream.Dispose();
			}

			return base64String;
		}

		public static WebProxy ToProxy(string webProxyString)
		{
			WebProxy webProxy;
			using (MemoryStream memoryStream = new MemoryStream())
			{
				byte[] buff = Convert.FromBase64String(webProxyString);
				memoryStream.Write(buff, 0, buff.Length);

				BinaryFormatter	formatter = new BinaryFormatter();
				webProxy = (WebProxy)formatter.Deserialize(memoryStream);

				memoryStream.Close();
				memoryStream.Dispose();
			}

			return webProxy;
		}
	}
}
