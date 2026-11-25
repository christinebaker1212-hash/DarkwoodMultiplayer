using System.Collections.Generic;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class NightEventDiagnostics
{
	private class Info
	{
		public bool knownStarted;

		public bool knownStartedToday;
	}

	private static readonly Dictionary<CustomEvent, Info> _events = new Dictionary<CustomEvent, Info>();

	private static readonly Dictionary<GameEvents, bool> _gameEvents = new Dictionary<GameEvents, bool>();

	private static bool _streaming;

	private static string _lastScenario = "";

	private static string _lastLocation = "";

	public static void ToggleStream()
	{
		_streaming = !_streaming;
		if ((Object)(object)Singleton<NightScenarios>.Instance != (Object)null)
		{
			Singleton<NightScenarios>.Instance.logEvents = _streaming;
		}
		Plugin.Log.LogInfo($"[NightDiag] streaming = {_streaming} (NightScenarios.logEvents junto)");
		if (_streaming)
		{
			_events.Clear();
		}
	}

	public static void Capture()
	{
		NightScenarios instance = Singleton<NightScenarios>.Instance;
		string arg = (((Object)(object)instance != (Object)null && (Object)(object)instance.currentScenario != (Object)null) ? ((Object)instance.currentScenario).name : "null");
		Plugin.Log.LogInfo("========================= [NightDiag] CAPTURA =========================");
		TimestampedLogSource log = Plugin.Log;
		string text = $"[NightDiag] currentScenario='{arg}' scenarioId={(((Object)(object)instance != (Object)null) ? instance.scenarioId : (-1))} ";
		object arg2 = Singleton<Controller>.Instance?.day;
		Controller instance2 = Singleton<Controller>.Instance;
		log.LogInfo(text + $"day={arg2} time={((instance2 != null) ? new int?(instance2.CurrentTime) : null)}");
		CustomEvent[] array = Object.FindObjectsOfType<CustomEvent>();
		Plugin.Log.LogInfo($"[NightDiag] {array.Length} CustomEvent na cena:");
		CustomEvent[] array2 = array;
		foreach (CustomEvent val in array2)
		{
			if (!((Object)(object)val == (Object)null))
			{
				DumpEvent(val, "  ");
			}
		}
		Plugin.Log.LogInfo("========================================================================");
	}

	private static void DumpEvent(CustomEvent ce, string indent)
	{
		RandomEvent theEvent = ce.theEvent;
		string text = ((theEvent != null) ? ((object)theEvent.type).ToString() : "null");
		string text2 = ((theEvent != null) ? theEvent.Value : "");
		bool flag = theEvent?.startedToday ?? false;
		Plugin.Log.LogInfo($"[NightDiag] {indent}'{((Object)((Component)ce).gameObject).name}' started={ce.started} type={text} " + $"value='{text2}' startedToday={flag} priority={ce.priority} freq={ce.frequency} " + "categories=[" + string.Join(",", ce.categories.ConvertAll((CustomEventCategory c) => ((object)c).ToString()).ToArray()) + "]");
	}

	public static void Tick()
	{
		//IL_01c7: Unknown result type (might be due to invalid IL or missing references)
		//IL_0217: Unknown result type (might be due to invalid IL or missing references)
		if (!_streaming)
		{
			return;
		}
		NightScenarios instance = Singleton<NightScenarios>.Instance;
		string text = (((Object)(object)instance != (Object)null && (Object)(object)instance.currentScenario != (Object)null) ? ((Object)instance.currentScenario).name : "null");
		if (text != _lastScenario)
		{
			Plugin.Log.LogInfo("[NightWatch] cenário mudou: '" + _lastScenario + "' -> '" + text + "' " + $"day={Singleton<Controller>.Instance?.day}");
			_lastScenario = text;
		}
		Player instance2 = Player.Instance;
		string text2 = (((Object)(object)instance2 != (Object)null && (Object)(object)instance2.whereAmI != (Object)null && (Object)(object)instance2.whereAmI.bigLocation != (Object)null) ? ((Object)((Component)instance2.whereAmI.bigLocation).gameObject).name : "null");
		if (text2 != _lastLocation)
		{
			Plugin.Log.LogInfo("[NightWatch] location do jogador mudou: '" + _lastLocation + "' -> '" + text2 + "'");
			_lastLocation = text2;
		}
		GameEvents[] array = Object.FindObjectsOfType<GameEvents>();
		foreach (GameEvents val in array)
		{
			if ((Object)(object)val == (Object)null)
			{
				continue;
			}
			if (!_gameEvents.TryGetValue(val, out var value))
			{
				_gameEvents[val] = val.fired;
				if (val.fired)
				{
					Plugin.Log.LogInfo("[NightWatch] *** GameEvents ACHADO JÁ DISPARADO: '" + ((Object)((Component)val).gameObject).name + "' " + $"pos={((Component)val).transform.position:F0} location={text2} ***");
				}
			}
			else
			{
				if (val.fired && !value)
				{
					Plugin.Log.LogInfo("[NightWatch] *** GameEvents DISPAROU: '" + ((Object)((Component)val).gameObject).name + "' " + $"pos={((Component)val).transform.position:F0} location={text2} ***");
				}
				_gameEvents[val] = val.fired;
			}
		}
		CustomEvent[] array2 = Object.FindObjectsOfType<CustomEvent>();
		foreach (CustomEvent val2 in array2)
		{
			if ((Object)(object)val2 == (Object)null)
			{
				continue;
			}
			if (!_events.TryGetValue(val2, out var value2))
			{
				value2 = new Info
				{
					knownStarted = val2.started,
					knownStartedToday = (val2.theEvent != null && val2.theEvent.startedToday)
				};
				_events[val2] = value2;
				if (val2.started)
				{
					Plugin.Log.LogInfo("[NightWatch] *** ACHADO JÁ DISPARADO (novo na cena) ***");
					DumpEvent(val2, "");
				}
			}
			else
			{
				bool knownStartedToday = val2.theEvent != null && val2.theEvent.startedToday;
				if (val2.started && !value2.knownStarted)
				{
					Plugin.Log.LogInfo("[NightWatch] *** DISPAROU *** ");
					DumpEvent(val2, "");
				}
				value2.knownStarted = val2.started;
				value2.knownStartedToday = knownStartedToday;
			}
		}
	}

	public static void Reset()
	{
		_events.Clear();
		_gameEvents.Clear();
		_streaming = false;
		_lastScenario = "";
		_lastLocation = "";
	}
}
