using System.IO;
using UnityEngine;

namespace DarkwoodCoopOnline.Net;

public sealed class PacketWriter
{
	private readonly MemoryStream _ms;

	private readonly BinaryWriter _bw;

	private PacketWriter(PacketType type)
	{
		_ms = new MemoryStream();
		_bw = new BinaryWriter(_ms);
		_bw.Write((byte)type);
	}

	public static PacketWriter Begin(PacketType type)
	{
		return new PacketWriter(type);
	}

	public PacketWriter Byte(byte v)
	{
		_bw.Write(v);
		return this;
	}

	public PacketWriter Bool(bool v)
	{
		_bw.Write(v);
		return this;
	}

	public PacketWriter Int(int v)
	{
		_bw.Write(v);
		return this;
	}

	public PacketWriter UShort(ushort v)
	{
		_bw.Write(v);
		return this;
	}

	public PacketWriter Float(float v)
	{
		_bw.Write(v);
		return this;
	}

	public PacketWriter Str(string v)
	{
		_bw.Write(v ?? string.Empty);
		return this;
	}

	public PacketWriter Bytes(byte[] v)
	{
		_bw.Write((ushort)v.Length);
		_bw.Write(v);
		return this;
	}

	public PacketWriter Vec2XZ(Vector3 p)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		_bw.Write(p.x);
		_bw.Write(p.z);
		return this;
	}

	public PacketWriter Vec3(Vector3 p)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		_bw.Write(p.x);
		_bw.Write(p.y);
		_bw.Write(p.z);
		return this;
	}

	public PacketWriter Quat(Quaternion q)
	{
		//IL_0006: Unknown result type (might be due to invalid IL or missing references)
		//IL_0017: Unknown result type (might be due to invalid IL or missing references)
		//IL_0028: Unknown result type (might be due to invalid IL or missing references)
		//IL_0039: Unknown result type (might be due to invalid IL or missing references)
		_bw.Write(q.x);
		_bw.Write(q.y);
		_bw.Write(q.z);
		_bw.Write(q.w);
		return this;
	}

	public byte[] Finish()
	{
		_bw.Flush();
		return _ms.ToArray();
	}
}
