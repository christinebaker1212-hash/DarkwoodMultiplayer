using DarkwoodCoopOnline.Net;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class PlayerSync
{
	public static float PeerHealth { get; private set; } = -1f;


	public static byte[] BuildLocalState()
	{
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_001c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0049: Unknown result type (might be due to invalid IL or missing references)
		//IL_004e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0052: Unknown result type (might be due to invalid IL or missing references)
		//IL_0143: Unknown result type (might be due to invalid IL or missing references)
		//IL_01bf: Unknown result type (might be due to invalid IL or missing references)
		//IL_01c4: Unknown result type (might be due to invalid IL or missing references)
		//IL_021e: Unknown result type (might be due to invalid IL or missing references)
		//IL_0223: Unknown result type (might be due to invalid IL or missing references)
		//IL_026c: Unknown result type (might be due to invalid IL or missing references)
		//IL_0293: Unknown result type (might be due to invalid IL or missing references)
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			return null;
		}
		Vector3 position = ((Component)instance).transform.position;
		float v = 0f;
		string v2 = "Idle";
		string v3 = "";
		tk2dSpriteAnimator torsoAnimator = instance.torsoAnimator;
		if ((Object)(object)torsoAnimator != (Object)null)
		{
			Quaternion rotation = ((Component)torsoAnimator).transform.rotation;
			v = rotation.eulerAngles.y;
			tk2dSpriteAnimationClip currentClip = torsoAnimator.CurrentClip;
			if (currentClip != null && !string.IsNullOrEmpty(currentClip.name))
			{
				v2 = currentClip.name;
			}
			if ((Object)(object)torsoAnimator.Library != (Object)null)
			{
				v3 = ((Object)torsoAnimator.Library).name;
			}
		}
		string v4 = "";
		if (!InvItemClass.isNull(instance.currentItem) && (Object)(object)instance.currentItem.baseClass != (Object)null && (instance.currentItem.baseClass.lightEmitter != (Object)null || instance.currentItem.baseClass.isFlashlight))
		{
			v4 = instance.currentItem.type;
		}
		float v5 = 0f;
		float v6 = 0f;
		Transform val = ((Component)instance).transform.Find("Flashlight");
		if ((Object)(object)val != (Object)null)
		{
			Light2D component = ((Component)val).GetComponent<Light2D>();
			if ((Object)(object)component != (Object)null)
			{
				v5 = component.LightRadius;
				v6 = component.LightColor.r;
			}
		}
		bool v7 = (Object)(object)instance.lightDot != (Object)null && instance.lightDot.lightsPlayer;
		float v8 = (((Object)(object)instance.lightDot != (Object)null) ? instance.lightDot.LightRadius : 0f);
		int v9 = (((Object)(object)torsoAnimator != (Object)null) ? torsoAnimator.CurrentFrame : 0);
		bool v10 = instance.attacking || instance.specialAttacking;
		string v11 = "";
		Quaternion q = Quaternion.identity;
		bool v12 = false;
		bool v13 = false;
		Transform val2 = ((Component)instance).transform.Find("PlayerLegs");
		tk2dSpriteAnimator val3 = (((Object)(object)val2 != (Object)null) ? ((Component)val2).GetComponent<tk2dSpriteAnimator>() : null);
		Renderer val4 = null;
		if ((Object)(object)val3 != (Object)null)
		{
			tk2dSpriteAnimationClip currentClip2 = val3.CurrentClip;
			if (currentClip2 != null)
			{
				v11 = currentClip2.name;
			}
			q = ((Component)val3).transform.rotation;
			val4 = ((Component)val3).GetComponent<Renderer>();
			v12 = (Object)(object)val4 != (Object)null && val4.enabled;
			v13 = val3.Playing;
		}
		bool v14 = (Object)(object)torsoAnimator == (Object)null || !torsoAnimator.Paused;
		return PacketWriter.Begin(PacketType.PlayerState).Vec2XZ(position).Float(v)
			.Str(v2)
			.Bool(v10)
			.Str(v3)
			.Str(v11)
			.Quat(q)
			.Bool(v12)
			.Bool(v13)
			.Bool(v14)
			.Str(v4)
			.Int(v9)
			.Float(((CharBase)instance).health)
			.Bool(v7)
			.Float(v8)
			.Float(v5)
			.Float(v6)
			.Finish();
	}

	public static void ApplyRemoteState(PacketReader r, RemotePlayerPuppet puppet)
	{
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0035: Unknown result type (might be due to invalid IL or missing references)
		//IL_003a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0062: Unknown result type (might be due to invalid IL or missing references)
		//IL_0067: Unknown result type (might be due to invalid IL or missing references)
		//IL_00bd: Unknown result type (might be due to invalid IL or missing references)
		//IL_00c6: Unknown result type (might be due to invalid IL or missing references)
		if (!((Object)(object)puppet == (Object)null))
		{
			float y = (((Object)(object)Player.Instance != (Object)null) ? ((Component)Player.Instance).transform.position.y : 0f);
			Vector3 pos = r.Vec2XZ(y);
			float aimAngle = r.Float();
			string clip = r.Str();
			bool attacking = r.Bool();
			string lib = r.Str();
			string legsClip = r.Str();
			Quaternion legsRot = r.Quat();
			bool legsVisible = r.Bool();
			bool legsPlaying = r.Bool();
			bool torsoPlaying = r.Bool();
			string lightItemType = r.Str();
			int torsoFrame = r.Int();
			PeerHealth = r.Float();
			bool lanternOn = r.Bool();
			float lanternRadius = r.Float();
			float flashlightRadius = r.Float();
			float flashlightBrightness = r.Float();
			puppet.ApplyState(pos, aimAngle, clip, attacking, lib, legsClip, legsRot, legsVisible, legsPlaying, torsoPlaying, lightItemType, torsoFrame, lanternOn, lanternRadius, flashlightRadius, flashlightBrightness);
		}
	}

	public static void Reset()
	{
		PeerHealth = -1f;
	}
}
