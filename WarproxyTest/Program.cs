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
			engine.Start();
			engine.SetProxy(HttpWebRequest.DefaultWebProxy);

 			WebClient wc = new WebClient();
			wc.Proxy = engine.LocalProxy;


			Console.WriteLine("===== START =====");
			Console.WriteLine("Danbooru");
			Console.WriteLine("Recieved Data Length : {0}", wc.DownloadData("http://danbooru.donmai.us/").Length);
			Console.WriteLine("");
			Console.WriteLine("ExHentai");
			Console.WriteLine("Recieved Data Length : {0}", wc.DownloadData("http://exhentai.org").Length);
			Console.WriteLine("=====  END  =====");

			Console.ReadKey();

		}
	}
}
