using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Warproxy;
using System.Net;

namespace WarproxyTest
{
	class Program
	{
		static void Main(string[] args)
		{
			WarpEngine engine = new WarpEngine();
			engine.MaxQueuedConnections = 5;
			engine.Start();
			engine.SetProxy(HttpWebRequest.DefaultWebProxy);


 			using (WebClient wc = new WebClient())
			{
				wc.Proxy = engine.LocalProxy;

				Console.WriteLine("===== START =====");

				for (int i = 0; i < 20; ++i)
					Console.WriteLine("Recieved Data Length : {0:00} {1}", i, wc.DownloadData("http://danbooru.donmai.us/").Length);

				Console.WriteLine("=====  END  =====");
			}

// 			Console.ReadKey();
// 			Console.ReadKey();
// 			
// 			engine.Stop();
// 			engine.Dispose();

		}
	}
}
