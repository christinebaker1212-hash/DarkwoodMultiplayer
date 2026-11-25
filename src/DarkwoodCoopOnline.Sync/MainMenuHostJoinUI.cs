using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using DarkwoodCoopOnline.Net;
using Steamworks;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class MainMenuHostJoinUI
{
	private const uint DarkwoodAppId = 274520u;

	private static bool _pendingHost;

	private static bool _showJoinList;

	private static readonly List<(ulong id64, string name)> _joinable = new List<(ulong, string)>();

	private static string _joinFeedback = "";

	private static GUIStyle _boxStyle;

	private static GUIStyle _titleStyle;

	private static GUIStyle _hintStyle;

	public static void Tick()
	{
		if (_pendingHost)
		{
			if (Core.mainMenu && IsTopLevelMenuOpen())
			{
				_pendingHost = false;
			}
			else if ((Object)(object)Player.Instance != (Object)null && Core.worldGenFinished())
			{
				_pendingHost = false;
				Plugin.Instance?.StartHostSession();
				Plugin.Log.LogInfo("[MainMenuHostJoinUI] mundo carregado — iniciando HOST automaticamente (botão do menu).");
			}
		}
	}

	private static bool IsTopLevelMenuOpen()
	{
		MainMenu instance = Singleton<MainMenu>.Instance;
		if ((Object)(object)instance != (Object)null && (Object)(object)instance.Menu0 != (Object)null)
		{
			return instance.Menu0.activeSelf;
		}
		return false;
	}

	public static void TriggerHost()
	{
		_pendingHost = true;
		_showJoinList = false;
		Singleton<MainMenu>.Instance.displayProfilesMenu();
	}

	public static void TriggerJoin()
	{
		if (Plugin.TransportModeIsLan)
		{
			Plugin.Instance?.TryBeginLanJoin();
			return;
		}
		if (!SteamAvailability.Available)
		{
			CoopMenuList.Show("JOIN", new List<CoopMenuList.Entry>
			{
				new CoopMenuList.Entry
				{
					Label = "(Steam not available in this build)",
					KeepOpen = true,
					OnPick = delegate
					{
					}
				},
				new CoopMenuList.Entry
				{
					Label = "Use LAN mode instead (see ~ panel)",
					KeepOpen = true,
					OnPick = delegate
					{
					}
				}
			}, null);
			Plugin.Log.LogWarning("[MainMenuHostJoinUI] JOIN por amigo da Steam indisponível nesta build (" + SteamAvailability.UnavailableReason + ") - use o modo LAN com o IP do host.");
			return;
		}
		RefreshJoinable();
		List<CoopMenuList.Entry> list = new List<CoopMenuList.Entry>();
		foreach (var item in _joinable)
		{
			var (id64, name) = item;
			list.Add(new CoopMenuList.Entry
			{
				Label = name,
				OnPick = delegate
				{
					PickSteamFriend(id64, name);
				}
			});
		}
		if (_joinable.Count == 0)
		{
			list.Add(new CoopMenuList.Entry
			{
				Label = "(no friends hosting right now)",
				KeepOpen = true,
				OnPick = delegate
				{
				}
			});
		}
		list.Add(new CoopMenuList.Entry
		{
			Label = "Steam friends list...",
			KeepOpen = true,
			OnPick = OpenSteamFriendsOverlay
		});
		CoopMenuList.Show("JOIN", list, null);
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void PickSteamFriend(ulong id64, string name)
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		CSteamID val = new CSteamID(id64);
		SteamFriends.RequestFriendRichPresence(val);
		if (!PrologueJoinSync.TryJoinFriendSteam(val))
		{
			Plugin.Log.LogWarning("[MainMenuHostJoinUI] '" + name + "' ainda não está hospedando (ou a info da Steam não chegou) - tente de novo em 1-2s.");
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void OpenSteamFriendsOverlay()
	{
		if (SteamManager.Initialized)
		{
			SteamFriends.ActivateGameOverlay("friends");
		}
	}

	public static void Draw()
	{
		//IL_0023: Unknown result type (might be due to invalid IL or missing references)
		//IL_002a: Expected O, but got Unknown
		//IL_0042: Unknown result type (might be due to invalid IL or missing references)
		//IL_0053: Unknown result type (might be due to invalid IL or missing references)
		//IL_0058: Unknown result type (might be due to invalid IL or missing references)
		//IL_006a: Expected O, but got Unknown
		//IL_0074: Unknown result type (might be due to invalid IL or missing references)
		//IL_0079: Unknown result type (might be due to invalid IL or missing references)
		//IL_0081: Unknown result type (might be due to invalid IL or missing references)
		//IL_0088: Unknown result type (might be due to invalid IL or missing references)
		//IL_0094: Expected O, but got Unknown
		//IL_00ad: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c1: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		//IL_00ce: Unknown result type (might be due to invalid IL or missing references)
		//IL_00d5: Unknown result type (might be due to invalid IL or missing references)
		//IL_00e1: Expected O, but got Unknown
		//IL_00fa: Unknown result type (might be due to invalid IL or missing references)
		//IL_0168: Unknown result type (might be due to invalid IL or missing references)
		//IL_018a: Unknown result type (might be due to invalid IL or missing references)
		//IL_01b8: Unknown result type (might be due to invalid IL or missing references)
		//IL_01e8: Unknown result type (might be due to invalid IL or missing references)
		if (Core.mainMenu && IsTopLevelMenuOpen() && !MainMenuNativeButtons.Created)
		{
			if (_boxStyle == null)
			{
				Texture2D val = new Texture2D(1, 1);
				val.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
				val.Apply();
				GUIStyle val2 = new GUIStyle();
				val2.normal.background = val;
				_boxStyle = val2;
				_titleStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 14,
					fontStyle = (FontStyle)1,
					alignment = (TextAnchor)4
				};
				_titleStyle.normal.textColor = new Color(0.9f, 0.9f, 0.85f);
				_hintStyle = new GUIStyle(GUI.skin.label)
				{
					fontSize = 11,
					alignment = (TextAnchor)3,
					wordWrap = true
				};
				_hintStyle.normal.textColor = new Color(0.75f, 0.75f, 0.7f);
			}
			float num = 220f;
			float num2 = (float)Screen.width - num - 16f;
			float num3 = 16f;
			float num4 = (_showJoinList ? (100f + (float)_joinable.Count * 26f + 34f + ((!string.IsNullOrEmpty(_joinFeedback)) ? 34f : 0f)) : 96f);
			GUI.Box(new Rect(num2, num3, num, num4), GUIContent.none, _boxStyle);
			GUI.Label(new Rect(num2, num3 + 4f, num, 20f), "Darkwood Co-op Online", _titleStyle);
			if (GUI.Button(new Rect(num2 + 10f, num3 + 28f, num - 20f, 26f), "HOST"))
			{
				TriggerHost();
			}
			if (GUI.Button(new Rect(num2 + 10f, num3 + 60f, num - 20f, 26f), "JOIN"))
			{
				TriggerJoin();
			}
			if (_showJoinList && SteamAvailability.Available)
			{
				DrawJoinListSteam(num2, num3, num);
			}
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void DrawJoinListSteam(float x, float y, float w)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_00f5: Unknown result type (might be due to invalid IL or missing references)
		//IL_007e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0133: Unknown result type (might be due to invalid IL or missing references)
		//IL_008c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0091: Unknown result type (might be due to invalid IL or missing references)
		float num = y + 96f;
		if (_joinable.Count == 0)
		{
			GUI.Label(new Rect(x + 10f, num, w - 20f, 30f), "No friends playing Darkwood right now.", _hintStyle);
			num += 30f;
		}
		else
		{
			foreach (var (num2, text) in _joinable)
			{
				if (GUI.Button(new Rect(x + 10f, num, w - 20f, 22f), text))
				{
					CSteamID val = new CSteamID(num2);
					SteamFriends.RequestFriendRichPresence(val);
					_joinFeedback = (PrologueJoinSync.TryJoinFriendSteam(val) ? "" : ("'" + text + "' isn't hosting yet (or the data hasn't arrived) - try again in 1-2s."));
				}
				num += 26f;
			}
		}
		if (GUI.Button(new Rect(x + 10f, num + 4f, w - 20f, 26f), "Open Steam friends list"))
		{
			OpenSteamFriendsOverlay();
		}
		if (!string.IsNullOrEmpty(_joinFeedback))
		{
			num += 34f;
			GUI.Label(new Rect(x + 10f, num, w - 20f, 34f), _joinFeedback, _hintStyle);
		}
	}

	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void RefreshJoinable()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_002f: Unknown result type (might be due to invalid IL or missing references)
		//IL_0030: Unknown result type (might be due to invalid IL or missing references)
		//IL_0041: Unknown result type (might be due to invalid IL or missing references)
		//IL_0057: Unknown result type (might be due to invalid IL or missing references)
		//IL_005d: Unknown result type (might be due to invalid IL or missing references)
		//IL_006d: Unknown result type (might be due to invalid IL or missing references)
		_joinable.Clear();
		_joinFeedback = "";
		if (!SteamManager.Initialized)
		{
			return;
		}
		try
		{
			int friendCount = SteamFriends.GetFriendCount((EFriendFlags)4);
			FriendGameInfo_t val = default(FriendGameInfo_t);
			for (int i = 0; i < friendCount; i++)
			{
				CSteamID friendByIndex = SteamFriends.GetFriendByIndex(i, (EFriendFlags)4);
				if (SteamFriends.GetFriendGamePlayed(friendByIndex, ref val) && val.m_gameID.AppID().m_AppId == 274520)
				{
					_joinable.Add((friendByIndex.m_SteamID, SteamFriends.GetFriendPersonaName(friendByIndex)));
					SteamFriends.RequestFriendRichPresence(friendByIndex);
				}
			}
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning("[MainMenuHostJoinUI] falha lendo lista de amigos: " + ex.Message);
			_joinable.Clear();
		}
	}

	public static void Reset()
	{
		_pendingHost = false;
		_showJoinList = false;
		_joinable.Clear();
		_joinFeedback = "";
	}
}
