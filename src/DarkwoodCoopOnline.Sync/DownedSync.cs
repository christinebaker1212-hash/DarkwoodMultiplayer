using System;
using System.Collections.Generic;
using System.Reflection;
using DarkwoodCoopOnline.Net;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Sync;

public static class DownedSync
{
	public const float BleedOutSeconds = 75f;

	public const float ReviveRange = 220f;

	public const float ReviveHoldSeconds = 3.5f;

	private const float ReviveHealthFraction = 0.4f;

	private const KeyCode ReviveKey = (KeyCode)102;

	private const string ReviveCursorType = "look";

	private const string PrimaryActionRewired = "Primary Action";

	private static readonly Dictionary<LanguageCode, string> ReviveTranslations = new Dictionary<LanguageCode, string>
	{
		{
			(LanguageCode)44,
			"Revive"
		},
		{
			(LanguageCode)45,
			"Revive"
		},
		{
			(LanguageCode)51,
			"Revive"
		},
		{
			(LanguageCode)145,
			"Ożyw"
		},
		{
			(LanguageCode)37,
			"Wiederbeleben"
		},
		{
			(LanguageCode)82,
			"Réanimer"
		},
		{
			(LanguageCode)57,
			"Reanimar"
		},
		{
			(LanguageCode)105,
			"Rianima"
		},
		{
			(LanguageCode)147,
			"Reanimar"
		},
		{
			(LanguageCode)148,
			"Reanimar"
		},
		{
			(LanguageCode)154,
			"Оживить"
		},
		{
			(LanguageCode)189,
			"Оживити"
		},
		{
			(LanguageCode)184,
			"Canlandır"
		},
		{
			(LanguageCode)199,
			"复活"
		},
		{
			(LanguageCode)202,
			"复活"
		},
		{
			(LanguageCode)200,
			"復活"
		},
		{
			(LanguageCode)109,
			"蘇生"
		},
		{
			(LanguageCode)117,
			"소생"
		}
	};

	private static bool _cursorPromptAttached;

	private static bool _layerDiagLogged;

	private static bool _patched;

	private static bool _downed;

	private static float _downedTimer;

	// slot -> downed
	private static readonly Dictionary<int, bool> _peerDowned = new Dictionary<int, bool>();

	// the puppet that currently carries the native "Revive" prompt
	private static RemotePlayerPuppet _promptPuppet;

	private static bool _forceRealDeath;

	private static float _reviveHoldTimer;

	private static bool _holdingRevive;

	private static bool _peerReallyDead;

	private static float _wipeAt = float.MinValue;

	private static float _diagTimer;

	private const float DiagInterval = 1f;

	private static float _broadcastTimer;

	private const float BroadcastInterval = 1f;

	private const float PauseAnimAtSeconds = 2.5f;

	private static bool _animPaused;

	private const int RescuerSortingOrderBoost = 10;

	private static int _sortingOrderBeforeRescuing;

	private static bool _sortingBoosted;

	private const float WatchRealDeathSeconds = 25f;

	private static bool _watchingRealDeath;

	private static float _watchTimer;

	private static float _watchLogTimer;

	private const float WatchLogInterval = 0.5f;

	private static bool _reviveImmersionActive;

	private static bool _reviveAnimEngaged;

	private static AudioObject _reviveHeartbeatAO;

	public static bool PeerReallyDead => _peerReallyDead;

	public static float DownedTimer => _downedTimer;

	public static float ReviveHoldTimer => _reviveHoldTimer;

	public static bool HoldingRevive => _holdingRevive;

	public static bool Downed => _downed;

	// any other player down?
	public static bool PeerDowned
	{
		get
		{
			foreach (KeyValuePair<int, bool> item in _peerDowned)
			{
				if (item.Value)
				{
					return true;
				}
			}
			return false;
		}
	}

	private static readonly Dictionary<int, float> _peerDownedAt = new Dictionary<int, float>();

	// seconds a downed player has left before they bleed out (as seen from here)
	public static float PeerTimeLeft(int slot)
	{
		if (!_peerDownedAt.TryGetValue(slot, out var value))
		{
			return BleedOutSeconds;
		}
		return Mathf.Max(0f, BleedOutSeconds - (Time.unscaledTime - value));
	}

	public static bool IsPeerDowned(int slot)
	{
		if (_peerDowned.TryGetValue(slot, out var value))
		{
			return value;
		}
		return false;
	}

	public static bool IsPeerReallyDead(int slot)
	{
		return _peerReallyDead;
	}

	// Someone still standing who could come and revive me?
	private static bool AnyoneCanRevive()
	{
		foreach (RemotePlayerPuppet puppet in CoopPeers.Puppets)
		{
			if ((Object)(object)puppet != (Object)null && puppet.HasState && !IsPeerDowned(puppet.Slot))
			{
				return true;
			}
		}
		return false;
	}

	public static void OnPeerLeft(int slot)
	{
		_peerDowned.Remove(slot);
		_peerDownedAt.Remove(slot);
		if ((Object)(object)_promptPuppet != (Object)null && _promptPuppet.Slot == slot)
		{
			_promptPuppet = null;
			_cursorPromptAttached = false;
		}
		// the one who could revive me left and nobody else can: bleed out normally (Tick handles it)
	}

	private static string GetLocalizedReviveText()
	{
		LanguageCode key;
		try
		{
			key = Language.CurrentLanguage();
		}
		catch
		{
			return "Revive";
		}
		if (!ReviveTranslations.TryGetValue(key, out var value))
		{
			return "Revive";
		}
		return value;
	}

	private static bool PrimaryActionHeld()
	{
		InputScript instance = Singleton<InputScript>.Instance;
		if ((Object)(object)instance == (Object)null || instance.rewiredPlayer == null)
		{
			return false;
		}
		return instance.rewiredPlayer.GetButton("Primary Action");
	}

	private static bool ControllerReviveHeld()
	{
		try
		{
			KeyCode keyCode = CoopConfig.ReviveButton?.Value ?? ((KeyCode)330);
			return (int)keyCode != 0 && Input.GetKey(keyCode);
		}
		catch
		{
			return false;
		}
	}

	private static bool IsNativelySelected(RemotePlayerPuppet peerPuppet)
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance != (Object)null && (Object)(object)peerPuppet != (Object)null)
		{
			return (Object)(object)instance.selectedObject == (Object)(object)((Component)peerPuppet).transform;
		}
		return false;
	}

	private static void EnsureRevivePrompt(RemotePlayerPuppet peerPuppet)
	{
		if (_cursorPromptAttached && (Object)(object)_promptPuppet != (Object)(object)peerPuppet)
		{
			ClearRevivePrompt(_promptPuppet);
		}
		if (!_cursorPromptAttached && !((Object)(object)peerPuppet == (Object)null))
		{
			CustomCursorAction val = ((Component)peerPuppet).GetComponent<CustomCursorAction>();
			if ((Object)(object)val == (Object)null)
			{
				val = ((Component)peerPuppet).gameObject.AddComponent<CustomCursorAction>();
			}
			val.cursorType = "look";
			val.actionText = GetLocalizedReviveText();
			if ((Object)(object)((Component)peerPuppet).GetComponent<Selectable>() == (Object)null)
			{
				((Component)peerPuppet).gameObject.AddComponent<Selectable>();
			}
			_cursorPromptAttached = true;
			_promptPuppet = peerPuppet;
			Plugin.Log.LogInfo("[Downed] prompt nativo (CustomCursorAction+Selectable) anexado no boneco do peer.");
			LogSelectionDiagnostics(peerPuppet);
		}
	}

	private static void LogSelectionDiagnostics(RemotePlayerPuppet peerPuppet)
	{
		if (_layerDiagLogged)
		{
			return;
		}
		_layerDiagLogged = true;
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)peerPuppet == (Object)null)
		{
			return;
		}
		int selectableLayerMask = instance.selectableLayerMask;
		int layer = ((Component)peerPuppet).gameObject.layer;
		List<string> list = new List<string>();
		for (int i = 0; i < 32; i++)
		{
			if ((selectableLayerMask & (1 << i)) != 0)
			{
				string text = LayerMask.LayerToName(i);
				list.Add(string.Format("{0}:{1}", i, string.IsNullOrEmpty(text) ? "(sem nome)" : text));
			}
		}
		bool flag = (selectableLayerMask & (1 << layer)) != 0;
		Plugin.Log.LogInfo($"[Downed][Diag][Layer] selectableLayerMask={selectableLayerMask} (bin={Convert.ToString(selectableLayerMask, 2)}) " + $"reach={instance.reach:F0} " + $"puppetLayer={layer}({LayerMask.LayerToName(layer)}) puppetLayerIncluído={flag} " + "layersAceitas=[" + string.Join(", ", list) + "]");
	}

	private static void ClearRevivePrompt(RemotePlayerPuppet peerPuppet)
	{
		if (!_cursorPromptAttached)
		{
			return;
		}
		_cursorPromptAttached = false;
		peerPuppet = _promptPuppet ?? peerPuppet;
		_promptPuppet = null;
		if (!((Object)(object)peerPuppet == (Object)null))
		{
			CustomCursorAction component = ((Component)peerPuppet).GetComponent<CustomCursorAction>();
			if ((Object)(object)component != (Object)null)
			{
				Object.Destroy((Object)(object)component);
			}
			Selectable component2 = ((Component)peerPuppet).GetComponent<Selectable>();
			if ((Object)(object)component2 != (Object)null)
			{
				Object.Destroy((Object)(object)component2);
			}
		}
	}

	private static void ReleaseAttackers(Player p)
	{
		for (int num = p.charactersAttackingMe.Count - 1; num >= 0; num--)
		{
			Character val = p.charactersAttackingMe[num];
			if ((Object)(object)val != (Object)null)
			{
				val.stopAttacking(((Component)p).transform);
			}
		}
	}

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(Player), "die", (Type[])null, (Type[])null), new HarmonyMethod(typeof(DownedSync), "DiePrefix", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static bool DiePrefix(Player __instance)
	{
		if (_forceRealDeath || !CoopRuntime.SessionActive)
		{
			return true;
		}
		if (!CoopRuntime.PeerConnected || CoopPeers.Count == 0)
		{
			StartWatchingRealDeath(__instance);
			return true;
		}
		if (!AnyoneCanRevive())
		{
			Plugin.Log.LogInfo("[Downed] everyone else is down too - nobody left to revive: party wipe.");
			_peerReallyDead = true;
		_wipeAt = Time.unscaledTime;
			foreach (RemotePlayerPuppet puppet in CoopPeers.Puppets)
			{
				puppet?.SetReallyDead();
			}
			CoopRuntime.Session?.SendReliable(PacketWriter.Begin(PacketType.ForceDeath).Finish());
			StartWatchingRealDeath(__instance);
			return true;
		}
		EnterDowned(__instance);
		return false;
	}

	private static void EnterDowned(Player p)
	{
		if (!_downed)
		{
			_downed = true;
			_downedTimer = 0f;
			_animPaused = false;
			ClearRescuerBoost(p);
			p.fakeDeathAni();
			((CharBase)p).invulnerable = true;
			((CharBase)p).invisible = true;
			ReleaseAttackers(p);
			p.setHealth(1f);
			Plugin.Log.LogInfo("[Downed] caí — incapacitado, aguardando reanimação ou sangramento.");
			CoopRuntime.Session?.SendReliable(PacketWriter.Begin(PacketType.DownedState).Bool(v: true).Finish());
			DumpState(p, "EnterDowned (logo após fakeDeathAni)");
		}
	}

	private static void ApplyRescuerBoost(Player p)
	{
		if (!_sortingBoosted && !((Object)(object)p.torsoAnimator == (Object)null) && !((Object)(object)p.torsoAnimator.Sprite == (Object)null))
		{
			_sortingOrderBeforeRescuing = p.torsoAnimator.Sprite.SortingOrder;
			p.torsoAnimator.Sprite.SortingOrder = _sortingOrderBeforeRescuing + 10;
			_sortingBoosted = true;
		}
	}

	private static void ClearRescuerBoost(Player p)
	{
		if (_sortingBoosted)
		{
			if ((Object)(object)p.torsoAnimator != (Object)null && (Object)(object)p.torsoAnimator.Sprite != (Object)null)
			{
				p.torsoAnimator.Sprite.SortingOrder = _sortingOrderBeforeRescuing;
			}
			_sortingBoosted = false;
		}
	}

	private static void DumpState(Player p, string tag)
	{
		string text = (((Object)(object)p.torsoAnimator != (Object)null && (Object)(object)p.torsoAnimator.Library != (Object)null) ? ((Object)p.torsoAnimator.Library).name : "(null)");
		string text2 = (((Object)(object)p.torsoAnimator != (Object)null && p.torsoAnimator.CurrentClip != null) ? p.torsoAnimator.CurrentClip.name : "(null)");
		Plugin.Log.LogInfo("[Downed][Diag] " + tag + ": lib='" + text + "' clip='" + text2 + "' " + $"fakingDeathAni={p.fakingDeathAni} performingAction={p.performingAction} endingSleep={p.endingSleep} " + $"forbidInputs={Core.forbidInputs} alive={((CharBase)p).alive} dying={p.dying} invulnerable={((CharBase)p).invulnerable} " + $"health={((CharBase)p).health:F1}");
	}

	private static void StartWatchingRealDeath(Player p)
	{
		_watchingRealDeath = true;
		_watchTimer = 0f;
		_watchLogTimer = 0f;
		DumpState(p, "die() chamado — estado JUSTO ANTES de p.die()");
	}

	public static void Tick(float dt)
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		// after a party wipe everyone respawns: stop treating the others as dead once that's over
		if (_peerReallyDead && !_watchingRealDeath && Time.unscaledTime - _wipeAt > 30f && ((CharBase)instance).alive)
		{
			_peerReallyDead = false;
			_peerDowned.Clear();
		}
		if (_watchingRealDeath)
		{
			_watchTimer += dt;
			_watchLogTimer += dt;
			if (_watchLogTimer >= 0.5f)
			{
				_watchLogTimer = 0f;
				DumpState(instance, $"watch pós-die() +{_watchTimer:F1}s");
			}
			if (!Core.forbidInputs || _watchTimer >= 25f)
			{
				Plugin.Log.LogInfo((!Core.forbidInputs) ? $"[Downed][Diag] onDeath() completou de verdade (forbidInputs=false) em {_watchTimer:F1}s — transição OK." : $"[Downed][Diag] {25f:F0}s depois do die() ainda com forbidInputs=true — onDeath() TRAVOU no meio da corrotina.");
				if (instance.endingSleep)
				{
					Plugin.Log.LogWarning("[Downed][Diag] jogador ainda preso no estado 'Sleep' pós-morte - forçando saída manualmente.");
					instance.stopAllPerformingActionAnims();
				}
				if (Core.forbidInputs)
				{
					if ((Object)(object)instance.torsoAnimator != (Object)null)
					{
						instance.torsoAnimator.Resume();
					}
					if ((Object)(object)instance.Rigidbody != (Object)null)
					{
						instance.Rigidbody.constraints = ((CharBase)instance).normalConstraints;
					}
					instance.skippedDay = false;
					if ((Object)(object)Singleton<UI>.Instance != (Object)null)
					{
						Singleton<UI>.Instance.tweenBlackScreenTop(new Color(0f, 0f, 0f, 0f), 1f);
					}
					Core.forbidInputs = false;
					Plugin.Log.LogWarning("[Downed][Diag] contramedida geral aplicada - forcei o fim da corrotina de respawn manualmente (Resume+constraints+fade+forbidInputs=false).");
				}
				_watchingRealDeath = false;
			}
		}
		_broadcastTimer += dt;
		if (_broadcastTimer >= 1f)
		{
			_broadcastTimer = 0f;
			CoopRuntime.Session?.SendReliable(PacketWriter.Begin(PacketType.DownedState).Bool(_downed).Finish());
		}
		if (_downed)
		{
			_downedTimer += dt;
			ReleaseAttackers(instance);
			if (!_animPaused && _downedTimer >= 2.5f && (Object)(object)instance.torsoAnimator != (Object)null)
			{
				_animPaused = true;
				instance.torsoAnimator.Pause();
				Plugin.Log.LogInfo($"[Downed] pausando 'DeathFake' por tempo ({2.5f}s) — frame {instance.torsoAnimator.CurrentFrame}.");
			}
			_diagTimer += dt;
			if (_diagTimer >= 1f)
			{
				_diagTimer = 0f;
				DumpState(instance, $"caído há {_downedTimer:F0}s");
			}
			if (_downedTimer >= 75f)
			{
				Plugin.Log.LogInfo("[Downed] sangramento esgotado — morrendo de vez.");
				ForceRealDeath(instance);
			}
			return;
		}
		RemotePlayerPuppet peerPuppet = CoopPeers.Nearest(((Component)instance).transform.position, (RemotePlayerPuppet pp) => IsPeerDowned(pp.Slot));
		if ((Object)(object)peerPuppet == (Object)null)
		{
			_reviveHoldTimer = 0f;
			_holdingRevive = false;
			ClearRescuerBoost(instance);
			ClearRevivePrompt(peerPuppet);
			EndReviveImmersion(instance);
			return;
		}
		ApplyRescuerBoost(instance);
		EnsureRevivePrompt(peerPuppet);
		float num = Vector3.Distance(((Component)instance).transform.position, ((Component)peerPuppet).transform.position);
		// keyboard F, the controller revive button (A / Cross by default), or the game's own interact action;
		// on a controller there's no cursor to aim at them, so being in range is enough
		bool key = Input.GetKey((KeyCode)102) || ControllerReviveHeld();
		bool flag = IsNativelySelected(peerPuppet);
		bool flag2 = PrimaryActionHeld();
		_diagTimer += dt;
		if (_diagTimer >= 1f)
		{
			_diagTimer = 0f;
			Plugin.Log.LogInfo($"[Downed][Diag] peerDowned=true dist={num:F0} (range={220f}) " + $"segurandoF={key} nativeSelected={flag} primaryActionHeld={flag2} holdTimer={_reviveHoldTimer:F1}");
		}
		if (num <= 220f && (key || flag2))
		{
			_holdingRevive = true;
			if (!_reviveImmersionActive)
			{
				BeginReviveImmersion(instance);
			}
			if ((Object)(object)_reviveHeartbeatAO != (Object)null)
			{
				_reviveHeartbeatAO.volume = Mathf.Lerp(0.15f, 1f, Mathf.Clamp01(_reviveHoldTimer / 3.5f));
			}
			_reviveHoldTimer += dt;
			if (_reviveHoldTimer >= 3.5f)
			{
				_reviveHoldTimer = 0f;
				_holdingRevive = false;
				EndReviveImmersion(instance);
				Plugin.Log.LogInfo($"[Downed] revived '{peerPuppet.PeerName}' (slot {peerPuppet.Slot}).");
				CoopRuntime.Session?.SendTo(peerPuppet.Slot, PacketWriter.Begin(PacketType.Revive).Finish());
				_peerDowned[peerPuppet.Slot] = false;
			}
		}
		else
		{
			_reviveHoldTimer = Mathf.Max(0f, _reviveHoldTimer - dt);
			_holdingRevive = false;
			if (_reviveImmersionActive)
			{
				EndReviveImmersion(instance);
			}
		}
	}

	private static void BeginReviveImmersion(Player p)
	{
		_reviveImmersionActive = true;
		if (!p.performingAction)
		{
			p.disarmingTrap = true;
			p.gettingInventory = true;
			p.performingAction = true;
			_reviveAnimEngaged = true;
		}
		_reviveHeartbeatAO = AudioController.Play("player_low_health", ((Component)p).transform);
		if ((Object)(object)_reviveHeartbeatAO != (Object)null)
		{
			_reviveHeartbeatAO.volume = 0.15f;
		}
	}

	private static void EndReviveImmersion(Player p)
	{
		_reviveImmersionActive = false;
		if (_reviveAnimEngaged)
		{
			_reviveAnimEngaged = false;
			p.gotInventory = false;
			p.hidingInventory = true;
			p.disarmingTrap = false;
		}
		if ((Object)(object)_reviveHeartbeatAO != (Object)null)
		{
			_reviveHeartbeatAO.Stop(0.5f);
			_reviveHeartbeatAO = null;
		}
	}

	private static void ForceRealDeath(Player p)
	{
		_forceRealDeath = true;
		((CharBase)p).invulnerable = false;
		if ((Object)(object)p.torsoAnimator != (Object)null)
		{
			p.torsoAnimator.Resume();
		}
		p.stopAllPerformingActionAnims();
		StartWatchingRealDeath(p);
		p.die();
		_forceRealDeath = false;
		_downed = false;
		_downedTimer = 0f;
		((CharBase)p).invisible = false;
		CoopRuntime.Session?.SendReliable(PacketWriter.Begin(PacketType.DownedState).Bool(v: false).Finish());
		foreach (RemotePlayerPuppet puppet in CoopPeers.Puppets)
		{
			puppet?.SetReallyDead();
		}
		_peerReallyDead = true;
		_wipeAt = Time.unscaledTime;
		CoopRuntime.Session?.SendReliable(PacketWriter.Begin(PacketType.ForceDeath).Finish());
	}

	public static void ApplyForceDeath()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return;
		}
		if (_downed)
		{
			if ((Object)(object)instance.torsoAnimator != (Object)null)
			{
				instance.torsoAnimator.Resume();
			}
			instance.stopAllPerformingActionAnims();
			((CharBase)instance).invulnerable = false;
		}
		else
		{
			ClearRescuerBoost(instance);
		}
		_forceRealDeath = true;
		StartWatchingRealDeath(instance);
		instance.die();
		_forceRealDeath = false;
		_downed = false;
		_downedTimer = 0f;
		((CharBase)instance).invisible = false;
		foreach (RemotePlayerPuppet puppet in CoopPeers.Puppets)
		{
			puppet?.SetReallyDead();
		}
		_peerReallyDead = true;
		_wipeAt = Time.unscaledTime;
		_peerDowned.Clear();
		Plugin.Log.LogInfo("[Downed] party wipe - dying with everyone.");
	}

	public static void ApplyDownedState(PacketReader r, int from)
	{
		bool flag = r.Bool();
		if (flag != IsPeerDowned(from))
		{
			Plugin.Log.LogInfo($"[Downed] '{CoopPeers.NameOf(from)}' {(flag ? "is down" : "is back up")}.");
			if (flag)
			{
				CoopNotice.Show(CoopPeers.NameOf(from) + " is down! Hold F next to them to revive");
			}
		}
		if (flag && !IsPeerDowned(from))
		{
			_peerDownedAt[from] = Time.unscaledTime;
		}
		_peerDowned[from] = flag;
	}

	public static void ApplyRevive()
	{
		Player instance = Player.Instance;
		if (!((Object)(object)instance == (Object)null) && _downed)
		{
			_downed = false;
			_downedTimer = 0f;
			((CharBase)instance).invisible = false;
			instance.stopAllPerformingActionAnims();
			((CharBase)instance).invulnerable = false;
			instance.setHealth(((CharBase)instance).maxHealth * 0.4f);
			if ((Object)(object)instance.torsoAnimator != (Object)null)
			{
				instance.torsoAnimator.Resume();
			}
			Plugin.Log.LogInfo("[Downed] fui reanimado.");
		}
	}

	public static void Reset()
	{
		_downed = false;
		_downedTimer = 0f;
		_peerDowned.Clear();
		_promptPuppet = null;
		_reviveHoldTimer = 0f;
		_diagTimer = 0f;
		_broadcastTimer = 0f;
		_animPaused = false;
		_holdingRevive = false;
		_sortingBoosted = false;
		_cursorPromptAttached = false;
		_peerReallyDead = false;
		_watchingRealDeath = false;
		if ((Object)(object)Player.Instance != (Object)null)
		{
			EndReviveImmersion(Player.Instance);
			return;
		}
		_reviveImmersionActive = false;
		_reviveAnimEngaged = false;
		AudioObject reviveHeartbeatAO = _reviveHeartbeatAO;
		if (reviveHeartbeatAO != null)
		{
			reviveHeartbeatAO.Stop(0.1f);
		}
		_reviveHeartbeatAO = null;
	}
}
