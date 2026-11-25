using System;
using System.IO;
using BepInEx;

namespace DarkwoodCoopOnline.Sync;

public static class StableIdentity
{
	private const string LocalGuidFileName = "DarkwoodCoopOnline_LocalIdentity.txt";

	private static string _cached;

	public static string Resolve()
	{
		if (_cached != null)
		{
			return _cached;
		}
		string text = TryReadGoldbergSteamId();
		_cached = ((!string.IsNullOrEmpty(text)) ? ("steam:" + text) : ("local:" + GetOrCreatePersistedGuid()));
		Plugin.Log.LogInfo("[StableIdentity] identidade local resolvida: '" + _cached + "' " + ((text != null) ? "(via steam_settings do Nucleus Co-op/Goldberg)." : "(GUID local persistido - sem Goldberg detectado)."));
		return _cached;
	}

	private static string TryReadGoldbergSteamId()
	{
		try
		{
			string path = Path.Combine(Paths.GameRootPath, "steam_settings", "configs.user.ini");
			if (!File.Exists(path))
			{
				return null;
			}
			bool flag = false;
			string[] array = File.ReadAllLines(path);
			for (int i = 0; i < array.Length; i++)
			{
				string text = array[i].Trim();
				if (text.Length == 0 || text.StartsWith(";") || text.StartsWith("#"))
				{
					continue;
				}
				if (text.StartsWith("[") && text.EndsWith("]"))
				{
					flag = text.Equals("[user::general]", StringComparison.OrdinalIgnoreCase);
				}
				else if (flag)
				{
					int num = text.IndexOf('=');
					if (num >= 0 && text.Substring(0, num).Trim().Equals("account_steamid", StringComparison.OrdinalIgnoreCase))
					{
						string text2 = text.Substring(num + 1).Trim();
						return string.IsNullOrEmpty(text2) ? null : text2;
					}
				}
			}
			return null;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[StableIdentity] falha lendo configs.user.ini do Goldberg: " + ex.GetType().Name + ": " + ex.Message + " - caindo pro GUID local.");
			return null;
		}
	}

	private static string GetOrCreatePersistedGuid()
	{
		string text = Path.Combine(Paths.PluginPath, "DarkwoodCoopOnline_LocalIdentity.txt");
		try
		{
			if (File.Exists(text))
			{
				string text2 = File.ReadAllText(text).Trim();
				if (!string.IsNullOrEmpty(text2))
				{
					return text2;
				}
			}
			string text3 = Guid.NewGuid().ToString("N");
			File.WriteAllText(text, text3);
			return text3;
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[StableIdentity] falha lendo/gravando " + text + ": " + ex.GetType().Name + ": " + ex.Message + " - usando GUID só desta sessão (não vai persistir entre reinícios do jogo).");
			return Guid.NewGuid().ToString("N");
		}
	}
}
