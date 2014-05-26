using System;
using System.Net;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace Warproxy
{
	public static class WarpExtensions
	{
		public static void SetWarp(this WebRequest webRequest, WarpEngine warpEngine)
		{
			warpEngine.SetWarp(webRequest);
		}
		public static void SetWarp(this WebRequest webRequest, WarpEngine warpEngine, IWebProxy proxy)
		{
			warpEngine.SetWarp(webRequest);

			if (proxy != null)
			{
				// warproxy header
				string base64String;

				if (proxy is WebProxy)
				{
					base64String = Helper.FromProxy(proxy as WebProxy);
				}
				else
				{
					WebProxy webProxy = new WebProxy(proxy.GetProxy(webRequest.RequestUri));
					webProxy.Credentials = proxy.Credentials.GetCredential(webRequest.RequestUri, "BASIC");
					base64String = Helper.FromProxy(webProxy);
				}

				webRequest.Headers.Set("warproxy", base64String);
			}
		}
	}
}
