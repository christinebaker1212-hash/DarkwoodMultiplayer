using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class SyncKeyUtil
{
	public static string PosKey(Vector3 p)
	{
		return $"{Mathf.RoundToInt(p.x)}_{Mathf.RoundToInt(p.z)}";
	}

	public static string Make(string name, Vector3 p)
	{
		return name + "@" + PosKey(p);
	}

	public static bool TryParsePos(string key, out Vector3 pos)
	{
		pos = Vector3.zero;
		if (string.IsNullOrEmpty(key))
		{
			return false;
		}
		int num = key.LastIndexOf('@');
		if (num < 0 || num + 1 >= key.Length)
		{
			return false;
		}
		string text = key.Substring(num + 1);
		int num2 = text.IndexOf('_', 1);
		if (num2 <= 0 || num2 + 1 >= text.Length)
		{
			return false;
		}
		if (!int.TryParse(text.Substring(0, num2), out var result))
		{
			return false;
		}
		if (!int.TryParse(text.Substring(num2 + 1), out var result2))
		{
			return false;
		}
		pos = new Vector3((float)result, 0f, (float)result2);
		return true;
	}
}
