using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace DarkwoodCoopOnline.Sync;

public static class DoorDiagnostics
{
	private const BindingFlags AllInst = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

	private static DoorBody _target;

	private static bool _watching;

	private static float _lastLog = -999f;

	private const float LogInterval = 1f / 15f;

	private static Transform _pivot;

	private static Transform _body;

	private static HingeJoint2D _hinge2D;

	private static Rigidbody2D _rb2D;

	private static HingeJoint _hinge3D;

	private static Rigidbody _rb3D;

	private static FieldInfo _rotField;

	public static void CaptureNearest()
	{
		Player instance = Player.Instance;
		if ((Object)(object)instance == (Object)null)
		{
			Plugin.Log.LogInfo("[DoorDiag] Player null");
			return;
		}
		DoorBody val = null;
		float num = float.MaxValue;
		DoorBody[] array = Object.FindObjectsOfType<DoorBody>();
		foreach (DoorBody val2 in array)
		{
			if (!((Object)(object)val2 == (Object)null) && !((Object)(object)val2.door == (Object)null))
			{
				float num2 = Vector3.Distance(((Component)val2.door).transform.position, ((Component)instance).transform.position);
				if (num2 < num)
				{
					num = num2;
					val = val2;
				}
			}
		}
		if ((Object)(object)val == (Object)null)
		{
			Plugin.Log.LogInfo("[DoorDiag] nenhuma DoorBody na cena");
			return;
		}
		_target = val;
		Door door = val.door;
		Plugin.Log.LogInfo("========================= [DoorDiag] CAPTURA =========================");
		Plugin.Log.LogInfo($"[DoorDiag] porta mais próxima a {num:F0} do Player. name='{door.name}'");
		DumpDoorFields(door);
		CacheRefs(val, door);
		DumpHierarchy(val, door);
		DumpPhysics();
		Plugin.Log.LogInfo("[DoorDiag] alvo travado. F12 = começar/parar streaming por frame.");
		Plugin.Log.LogInfo("======================================================================");
	}

	private static void DumpDoorFields(Door door)
	{
		Plugin.Log.LogInfo("[DoorDiag] --- campos do Door ---");
		FieldInfo[] fields = typeof(Door).GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		foreach (FieldInfo fieldInfo in fields)
		{
			object v;
			try
			{
				v = fieldInfo.GetValue(door);
			}
			catch
			{
				v = "<err>";
			}
			Plugin.Log.LogInfo($"[DoorDiag]   {fieldInfo.FieldType.Name,-16} {fieldInfo.Name,-28} = {Fmt(v)}");
		}
	}

	private static void CacheRefs(DoorBody db, Door door)
	{
		_rotField = typeof(Door).GetField("rot", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		_pivot = ((Component)door).transform;
		_body = door.body;
		FieldInfo field = typeof(Door).GetField("bodyRB", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
		_rb3D = (field != null) ? (field.GetValue(door) as Rigidbody) : null;
		if ((Object)(object)_body != (Object)null)
		{
			if ((Object)(object)_rb3D == (Object)null)
			{
				_rb3D = ((Component)_body).GetComponent<Rigidbody>();
			}
			_hinge3D = ((Component)_body).GetComponent<HingeJoint>();
			_rb2D = ((Component)_body).GetComponent<Rigidbody2D>();
			_hinge2D = ((Component)_body).GetComponent<HingeJoint2D>();
		}
	}

	private static void DumpHierarchy(DoorBody db, Door door)
	{
		Transform transform = ((Component)door).transform;
		Plugin.Log.LogInfo("[DoorDiag] --- hierarquia (raiz='" + ((Object)transform).name + "') ---");
		Plugin.Log.LogInfo("[DoorDiag]   pivô(Door.transform)='" + ((Object)((Component)door).transform).name + "'  body(Door.body)='" + (((Object)(object)door.body != (Object)null) ? ((Object)door.body).name : "null") + "'  DoorBody.transform='" + ((Object)((Component)db).transform).name + "'");
		DumpTree(transform, 0);
	}

	private static void DumpTree(Transform t, int depth)
	{
		List<string> list = new List<string>();
		Component[] components = ((Component)t).GetComponents<Component>();
		foreach (Component val in components)
		{
			if ((Object)(object)val != (Object)null && !(val is Transform))
			{
				list.Add(((object)val).GetType().Name);
			}
		}
		string text = new string(' ', depth * 2);
		TimestampedLogSource log = Plugin.Log;
		string[] obj = new string[8]
		{
			"[DoorDiag]   ",
			text,
			"'",
			((Object)t).name,
			"' [",
			string.Join(",", list.ToArray()),
			"] ",
			null
		};
		Vector3 position = t.position;
		obj[7] = string.Format("wpos={0} lrotZ={1:F1}", position.ToString("F1"), t.localEulerAngles.z);
		log.LogInfo(string.Concat(obj));
		if (depth >= 4)
		{
			return;
		}
		foreach (Transform item in t)
		{
			DumpTree(item, depth + 1);
		}
	}

	private static void DumpPhysics()
	{
		Plugin.Log.LogInfo("[DoorDiag] --- física ---");
		if ((Object)(object)_hinge2D != (Object)null)
		{
			HingeJoint2D hinge2D = _hinge2D;
			Plugin.Log.LogInfo("[DoorDiag]   HingeJoint2D em '" + ((Object)hinge2D).name + "':");
			Plugin.Log.LogInfo($"[DoorDiag]     jointAngle={hinge2D.jointAngle:F2} jointSpeed={hinge2D.jointSpeed:F2} referenceAngle={hinge2D.referenceAngle:F2}");
			TimestampedLogSource log = Plugin.Log;
			object arg = hinge2D.useMotor;
			JointMotor2D motor = hinge2D.motor;
			object arg2 = motor.motorSpeed;
			motor = hinge2D.motor;
			log.LogInfo($"[DoorDiag]     useMotor={arg} motor(speed={arg2:F1},maxTorque={motor.maxMotorTorque:F1})");
			TimestampedLogSource log2 = Plugin.Log;
			object arg3 = hinge2D.useLimits;
			JointAngleLimits2D limits = hinge2D.limits;
			object arg4 = limits.min;
			limits = hinge2D.limits;
			log2.LogInfo($"[DoorDiag]     useLimits={arg3} limits(min={arg4:F1},max={limits.max:F1})");
			Plugin.Log.LogInfo($"[DoorDiag]     anchor={((AnchoredJoint2D)hinge2D).anchor} connectedAnchor={((AnchoredJoint2D)hinge2D).connectedAnchor} autoConfigCA={((AnchoredJoint2D)hinge2D).autoConfigureConnectedAnchor}");
			Plugin.Log.LogInfo(string.Format("[DoorDiag]     connectedBody={0} breakTorque={1:F0}", ((Object)(object)((Joint2D)hinge2D).connectedBody != (Object)null) ? ((Object)((Joint2D)hinge2D).connectedBody).name : "null", ((Joint2D)hinge2D).breakTorque));
		}
		else
		{
			Plugin.Log.LogInfo("[DoorDiag]   HingeJoint2D: (nenhum)");
		}
		if ((Object)(object)_rb2D != (Object)null)
		{
			Rigidbody2D rb2D = _rb2D;
			Plugin.Log.LogInfo($"[DoorDiag]   Rigidbody2D em '{((Object)rb2D).name}': bodyType={rb2D.bodyType} isKinematic={rb2D.isKinematic} " + $"mass={rb2D.mass:F2} gravityScale={rb2D.gravityScale:F1} angularDrag={rb2D.angularDrag:F2} drag={rb2D.drag:F2}");
			TimestampedLogSource log3 = Plugin.Log;
			object[] obj = new object[4] { rb2D.rotation, rb2D.angularVelocity, null, null };
			Vector2 velocity = rb2D.velocity;
			obj[2] = velocity.ToString("F2");
			obj[3] = rb2D.constraints;
			log3.LogInfo(string.Format("[DoorDiag]     rotation={0:F2} angularVelocity={1:F2} velocity={2} constraints={3}", obj));
		}
		else
		{
			Plugin.Log.LogInfo("[DoorDiag]   Rigidbody2D: (nenhum)");
		}
		if ((Object)(object)_hinge3D != (Object)null)
		{
			HingeJoint hinge3D = _hinge3D;
			Plugin.Log.LogInfo($"[DoorDiag]   HingeJoint(3D) em '{((Object)hinge3D).name}': angle={hinge3D.angle:F2} velocity={hinge3D.velocity:F2}");
			Plugin.Log.LogInfo(string.Format("[DoorDiag]     axis={0} anchor={1} connectedAnchor={2} connectedBody={3}", ((Joint)hinge3D).axis, ((Joint)hinge3D).anchor, ((Joint)hinge3D).connectedAnchor, ((Object)(object)((Joint)hinge3D).connectedBody != (Object)null) ? ((Object)((Joint)hinge3D).connectedBody).name : "null"));
			TimestampedLogSource log4 = Plugin.Log;
			object[] obj2 = new object[4] { hinge3D.useLimits, null, null, null };
			JointLimits limits2 = hinge3D.limits;
			obj2[1] = limits2.min;
			limits2 = hinge3D.limits;
			obj2[2] = limits2.max;
			limits2 = hinge3D.limits;
			obj2[3] = limits2.bounciness;
			log4.LogInfo(string.Format("[DoorDiag]     useLimits={0} limits(min={1:F1},max={2:F1},bounce={3:F2})", obj2));
			TimestampedLogSource log5 = Plugin.Log;
			object[] obj3 = new object[4] { hinge3D.useMotor, null, null, null };
			JointMotor motor2 = hinge3D.motor;
			obj3[1] = motor2.targetVelocity;
			motor2 = hinge3D.motor;
			obj3[2] = motor2.force;
			motor2 = hinge3D.motor;
			obj3[3] = motor2.freeSpin;
			log5.LogInfo(string.Format("[DoorDiag]     useMotor={0} motor(targetVel={1:F1},force={2:F1},free={3})", obj3));
			Plugin.Log.LogInfo($"[DoorDiag]     useSpring={hinge3D.useSpring} spring(spring={hinge3D.spring.spring:F1},damper={hinge3D.spring.damper:F1},target={hinge3D.spring.targetPosition:F1})");
		}
		else
		{
			Plugin.Log.LogInfo("[DoorDiag]   HingeJoint(3D): (nenhum)");
		}
		if ((Object)(object)_rb3D != (Object)null)
		{
			Rigidbody rb3D = _rb3D;
			Plugin.Log.LogInfo($"[DoorDiag]   Rigidbody(3D) em '{((Object)rb3D).name}': isKinematic={rb3D.isKinematic} useGravity={rb3D.useGravity} " + $"mass={rb3D.mass:F2} drag={rb3D.drag:F2} angularDrag={rb3D.angularDrag:F2}");
			TimestampedLogSource log6 = Plugin.Log;
			object arg5 = rb3D.constraints;
			object arg6 = rb3D.maxAngularVelocity;
			Vector3 val = rb3D.centerOfMass;
			log6.LogInfo(string.Format("[DoorDiag]     constraints={0} maxAngularVel={1:F1} centerOfMass={2}", arg5, arg6, val.ToString("F2")));
			TimestampedLogSource log7 = Plugin.Log;
			string[] obj4 = new string[6] { "[DoorDiag]     rotationEuler=", null, null, null, null, null };
			Quaternion rotation = rb3D.rotation;
			val = rotation.eulerAngles;
			obj4[1] = val.ToString("F1");
			obj4[2] = " angularVelocity=";
			val = rb3D.angularVelocity;
			obj4[3] = val.ToString("F2");
			obj4[4] = " velocity=";
			val = rb3D.velocity;
			obj4[5] = val.ToString("F2");
			log7.LogInfo(string.Concat(obj4));
		}
		else
		{
			Plugin.Log.LogInfo("[DoorDiag]   Rigidbody(3D): (nenhum)");
		}
	}

	public static void ToggleWatch()
	{
		if ((Object)(object)_target == (Object)null)
		{
			Plugin.Log.LogInfo("[DoorDiag] sem alvo — aperte F11 perto de uma porta primeiro.");
			return;
		}
		_watching = !_watching;
		Plugin.Log.LogInfo($"[DoorDiag] streaming = {_watching}  (porta '{_target.door?.name}')");
		if (_watching)
		{
			Plugin.Log.LogInfo("[DoorDiag] t | rot | opened | bodyLrotZ | hinge3D(angle,vel) | rb3D(eulerZ,angVel,kin)");
		}
	}

	public static void Tick()
	{
		if (!_watching || (Object)(object)_target == (Object)null)
		{
			return;
		}
		Door door = _target.door;
		if ((Object)(object)door == (Object)null)
		{
			_watching = false;
		}
		else if (!(Time.time - _lastLog < 1f / 15f))
		{
			_lastLog = Time.time;
			float num = ((_rotField != null) ? ((float)_rotField.GetValue(door)) : float.NaN);
			object obj;
			Vector3 val;
			if (!((Object)(object)_body != (Object)null))
			{
				obj = "null";
			}
			else
			{
				val = _body.localEulerAngles;
				obj = val.z.ToString("F1");
			}
			string text = (string)obj;
			string text2 = (((Object)(object)_hinge3D != (Object)null) ? $"({_hinge3D.angle:F1},{_hinge3D.velocity:F1})" : "-");
			object obj2;
			if (!((Object)(object)_rb3D != (Object)null))
			{
				obj2 = "-";
			}
			else
			{
				Quaternion rotation = _rb3D.rotation;
				object arg = rotation.eulerAngles.z;
				val = _rb3D.angularVelocity;
				obj2 = $"({arg:F1},{val.magnitude:F2},{_rb3D.isKinematic})";
			}
			string text3 = (string)obj2;
			Plugin.Log.LogInfo($"[DoorWatch] t={Time.time:F2} rot={num:F1} opened={door.opened} " + "bodyLrotZ=" + text + " hinge3D=" + text2 + " rb3D=" + text3);
		}
	}

	private static string Fmt(object v)
	{
		if (v == null)
		{
			return "null";
		}
		if (v is Vector3 val)
		{
			return val.ToString("F2");
		}
		Object val2 = (Object)((v is Object) ? v : null);
		if (val2 != null)
		{
			if (!(val2 != (Object)null))
			{
				return "null";
			}
			return val2.name + "(" + ((object)val2).GetType().Name + ")";
		}
		return v.ToString();
	}

	public static void Reset()
	{
		_target = null;
		_watching = false;
		_pivot = (_body = null);
		_hinge2D = null;
		_rb2D = null;
		_hinge3D = null;
		_rb3D = null;
	}
}
