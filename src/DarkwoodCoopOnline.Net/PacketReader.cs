using System.IO;
using UnityEngine;

namespace DarkwoodCoopOnline.Net;

public sealed class PacketReader
{
	private readonly BinaryReader _br;

	public PacketType Type { get; }

	public PacketReader(byte[] data)
	{
		_br = new BinaryReader(new MemoryStream(data));
		Type = (PacketType)_br.ReadByte();
	}

	public byte Byte()
	{
		return _br.ReadByte();
	}

	public bool Bool()
	{
		return _br.ReadBoolean();
	}

	public int Int()
	{
		return _br.ReadInt32();
	}

	public ushort UShort()
	{
		return _br.ReadUInt16();
	}

	public float Float()
	{
		return _br.ReadSingle();
	}

	public string Str()
	{
		return _br.ReadString();
	}

	public byte[] Bytes()
	{
		int count = _br.ReadUInt16();
		return _br.ReadBytes(count);
	}

	public Vector3 Vec2XZ(float y)
	{
		return new Vector3(_br.ReadSingle(), y, _br.ReadSingle());
	}

	public Vector3 Vec3()
	{
		return new Vector3(_br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle());
	}

	public Quaternion Quat()
	{
		return new Quaternion(_br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle(), _br.ReadSingle());
	}
}
