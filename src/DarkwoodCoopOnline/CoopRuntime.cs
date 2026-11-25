namespace DarkwoodCoopOnline;

public static class CoopRuntime
{
	public static CoopSession Session { get; private set; }

	public static bool SessionActive
	{
		get
		{
			if (!DebugCheats.VanillaMode && Session != null)
			{
				return Session.Linked;
			}
			return false;
		}
	}

	public static bool RealSessionActive
	{
		get
		{
			if (Session != null)
			{
				return Session.Linked;
			}
			return false;
		}
	}

	public static bool PeerConnected => SessionActive;

	public static void SetSession(CoopSession session)
	{
		Session = session;
	}
}
