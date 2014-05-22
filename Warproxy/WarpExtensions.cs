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
			warpEngine.SetProxy(proxy);
			warpEngine.SetWarp(webRequest);
		}
	}
}
