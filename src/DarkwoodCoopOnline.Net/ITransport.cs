namespace DarkwoodCoopOnline.Net;

public interface ITransport
{
	bool Connected { get; }

	string LocalIdentity { get; }

	string PeerIdentity { get; }

	void Start(NetRole role);

	void Send(byte[] data, bool reliable);

	bool TryReceive(out byte[] data);

	void Pump();

	void Stop();
}
