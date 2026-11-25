using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace DarkwoodCoopOnline.Net;

public static class SaveRelay
{
	private const string UploadUrl = "https://litterbox.catbox.moe/resources/internals/api.php";

	private const string Expiry = "1h";

	private const int TimeoutMs = 90000;

	private const int ChunkSize = 65536;

	private const int ReachabilityTimeoutMs = 5000;

	public static void UploadAsync(byte[] blob, Action<string> onDone, Action<float> onProgress = null)
	{
		Thread thread = new Thread((ThreadStart)delegate
		{
			string obj = null;
			try
			{
				obj = Upload(blob, onProgress);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("[SaveRelay] upload falhou: " + ex.Message);
			}
			onDone(obj);
		});
		thread.IsBackground = true;
		thread.Start();
	}

	public static void DownloadAsync(string url, Action<byte[]> onDone, Action<float> onProgress = null)
	{
		Thread thread = new Thread((ThreadStart)delegate
		{
			byte[] obj = null;
			try
			{
				obj = Download(url, onProgress);
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("[SaveRelay] download falhou: " + ex.Message);
			}
			onDone(obj);
		});
		thread.IsBackground = true;
		thread.Start();
	}

	public static void IsReachableAsync(string url, Action<bool> onDone)
	{
		Thread thread = new Thread((ThreadStart)delegate
		{
			bool obj = false;
			try
			{
				HttpWebRequest obj2 = (HttpWebRequest)WebRequest.Create(url);
				obj2.Method = "HEAD";
				obj2.Timeout = 5000;
				obj2.ReadWriteTimeout = 5000;
				using HttpWebResponse httpWebResponse = (HttpWebResponse)obj2.GetResponse();
				obj = httpWebResponse.StatusCode == HttpStatusCode.OK;
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("[SaveRelay] teste de alcançabilidade HTTP falhou: " + ex.Message);
			}
			onDone(obj);
		});
		thread.IsBackground = true;
		thread.Start();
	}

	private static string Upload(byte[] blob, Action<float> onProgress)
	{
		string text = "----DarkwoodCoop" + Guid.NewGuid().ToString("N");
		HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create("https://litterbox.catbox.moe/resources/internals/api.php");
		httpWebRequest.Method = "POST";
		httpWebRequest.ContentType = "multipart/form-data; boundary=" + text;
		httpWebRequest.Timeout = 90000;
		httpWebRequest.ReadWriteTimeout = 90000;
		byte[] array;
		using (MemoryStream memoryStream = new MemoryStream())
		{
			WriteField(memoryStream, text, "reqtype", "fileupload");
			WriteField(memoryStream, text, "time", "1h");
			string s = "--" + text + "\r\nContent-Disposition: form-data; name=\"fileToUpload\"; filename=\"save.bin\"\r\nContent-Type: application/octet-stream\r\n\r\n";
			byte[] bytes = Encoding.UTF8.GetBytes(s);
			memoryStream.Write(bytes, 0, bytes.Length);
			memoryStream.Write(blob, 0, blob.Length);
			byte[] bytes2 = Encoding.UTF8.GetBytes("\r\n--" + text + "--\r\n");
			memoryStream.Write(bytes2, 0, bytes2.Length);
			array = memoryStream.ToArray();
		}
		httpWebRequest.ContentLength = array.Length;
		using (Stream stream = httpWebRequest.GetRequestStream())
		{
			int num = 0;
			while (num < array.Length)
			{
				int num2 = Math.Min(65536, array.Length - num);
				stream.Write(array, num, num2);
				num += num2;
				onProgress?.Invoke((float)num / (float)array.Length);
			}
		}
		using HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
		using StreamReader streamReader = new StreamReader(httpWebResponse.GetResponseStream());
		string text2 = streamReader.ReadToEnd().Trim();
		return text2.StartsWith("http", StringComparison.OrdinalIgnoreCase) ? text2 : null;
	}

	private static void WriteField(Stream ms, string boundary, string name, string value)
	{
		string s = "--" + boundary + "\r\nContent-Disposition: form-data; name=\"" + name + "\"\r\n\r\n" + value + "\r\n";
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		ms.Write(bytes, 0, bytes.Length);
	}

	private static byte[] Download(string url, Action<float> onProgress)
	{
		HttpWebRequest obj = (HttpWebRequest)WebRequest.Create(url);
		obj.Timeout = 90000;
		obj.ReadWriteTimeout = 90000;
		using HttpWebResponse httpWebResponse = (HttpWebResponse)obj.GetResponse();
		using Stream stream = httpWebResponse.GetResponseStream();
		using MemoryStream memoryStream = new MemoryStream();
		long contentLength = httpWebResponse.ContentLength;
		byte[] array = new byte[65536];
		long num = 0L;
		int num2;
		while ((num2 = stream.Read(array, 0, array.Length)) > 0)
		{
			memoryStream.Write(array, 0, num2);
			num += num2;
			if (contentLength > 0)
			{
				onProgress?.Invoke((float)num / (float)contentLength);
			}
		}
		return memoryStream.ToArray();
	}
}
