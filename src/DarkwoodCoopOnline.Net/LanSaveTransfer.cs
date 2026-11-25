using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using DarkwoodCoopOnline.Sync;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DarkwoodCoopOnline.Net;

public static class LanSaveTransfer
{
	private static HttpListener _server;

	private static volatile bool _serverRunning;

	private static volatile bool _hostWorldReady;

	private static bool _patched;

	private static volatile bool _hostInPrologue;

	private static volatile bool _fetching;

	private static volatile byte[] _downloadResult;

	private static string _lastUrl = "";

	private static float _fetchTimer;

	private const float FetchTimeoutSeconds = 75f;

	private static byte[] _pendingBlob;

	private static string _pendingHostIdentity;

	private static bool _pendingConnect;

	private static float _pendingConnectTimer;

	public static string StatusText { get; private set; } = "";


	public static bool IsActive { get; private set; }

	public static bool IsFailed { get; private set; }

	public static string HostPeerStatus { get; private set; } = "";


	public static bool Active => _fetching;

	// The host's world has really finished loading (WorldGenerator.activatePlayer ran)
	public static bool HostWorldReady => _hostWorldReady;

	// a joined world is waiting for its slot or loading
	public static bool JoinInProgress => _pendingConnect;

	private static float _activatedAt = float.MinValue;

	public static void MarkHostWorldNotReady()
	{
		// activatePlayer already ran during this very load (it can come just before worldGenFinished)
		if (Time.unscaledTime - _activatedAt < 15f)
		{
			return;
		}
		_hostWorldReady = false;
	}

	// JOIN: the host's world arrived over the co-op connection
	public static void ApplyAndLoadForJoin(byte[] blob, string hostIdentity)
	{
		ApplyAndLoad(blob, hostIdentity);
	}

	private static void SetStatus(string text)
	{
		StatusText = text;
		IsActive = true;
		IsFailed = false;
	}

	private static void SetFailed(string text)
	{
		StatusText = text;
		IsActive = true;
		IsFailed = true;
	}

	private static void SetDone()
	{
		IsActive = false;
		IsFailed = false;
	}

	public static void SetChecking(string text)
	{
		SetStatus(text);
	}

	public static void SetDoneExternal()
	{
		SetDone();
	}

	public static void ReportConfigError(string text)
	{
		SetFailed(text);
	}

	public static void ClearHostPeerStatus()
	{
		HostPeerStatus = "";
	}

	private static void UpdateHostInPrologue()
	{
		_hostInPrologue = (Object)(object)Player.Instance != (Object)null && Player.Instance.firstPlay;
	}

	public static void EnsurePatched(string modId)
	{
		if (!_patched)
		{
			_patched = true;
			new Harmony(modId).Patch((MethodBase)AccessTools.Method(typeof(WorldGenerator), "activatePlayer", (Type[])null, (Type[])null), (HarmonyMethod)null, new HarmonyMethod(typeof(LanSaveTransfer), "AfterActivatePlayer", (Type[])null), (HarmonyMethod)null, (HarmonyMethod)null, (HarmonyMethod)null);
		}
	}

	private static void AfterActivatePlayer()
	{
		_hostWorldReady = true;
		_activatedAt = Time.unscaledTime;
		Plugin.Log.LogInfo("[LanSaveTransfer] WorldGenerator.activatePlayer() disparou - mundo/prólogo do host realmente prontos agora (/save pode servir).");
		LobbySync.MarkLocalIntroDone();
	}

	public static void StartServer(int port)
	{
		if (_serverRunning)
		{
			return;
		}
		_hostWorldReady = false;
		try
		{
			_server = new HttpListener();
			_server.Prefixes.Add($"http://+:{port}/");
			_server.Start();
			_serverRunning = true;
			Thread thread = new Thread(ServeLoop);
			thread.IsBackground = true;
			thread.Name = "LanSaveServer";
			thread.Start();
			Plugin.Log.LogInfo($"[LanSaveTransfer] servidor local do save ligado na porta {port} (/save).");
		}
		catch (Exception ex)
		{
			Plugin.Log.LogWarning($"[LanSaveTransfer] falha ao iniciar servidor local (porta {port}): {ex.GetType().Name}: {ex.Message} - " + "o cliente não vai conseguir baixar o save automaticamente. Rodar o jogo como Administrador costuma resolver (HttpListener escutando em todas as interfaces exige isso em alguns Windows).");
		}
	}

	private static void ServeLoop()
	{
		while (_serverRunning)
		{
			HttpListenerContext context;
			try
			{
				context = _server.GetContext();
			}
			catch
			{
				break;
			}
			try
			{
				if (context.Request.Url.AbsolutePath == "/status")
				{
					byte[] bytes = Encoding.UTF8.GetBytes(_hostInPrologue ? "prologue" : "chapter");
					context.Response.ContentType = "text/plain";
					context.Response.ContentLength64 = bytes.Length;
					context.Response.OutputStream.Write(bytes, 0, bytes.Length);
				}
				else if (context.Request.Url.AbsolutePath == "/save")
				{
					HostPeerStatus = $"Player connecting from {context.Request.RemoteEndPoint?.Address} - downloading world save...";
					int num = 0;
					while (!_hostWorldReady && num < 60000)
					{
						Thread.Sleep(250);
						num += 250;
					}
					if (!_hostWorldReady)
					{
						Plugin.Log.LogWarning($"[LanSaveTransfer] mundo do host ainda não sinalizou pronto depois de {60}s - servindo mesmo assim (não travar o cliente pra sempre).");
					}
					byte[] buffer = WorldSync.SerializeWorld();
					byte[] bytes2 = Encoding.UTF8.GetBytes(StableIdentity.Resolve());
					using MemoryStream memoryStream = new MemoryStream();
					using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
					binaryWriter.Write(bytes2.Length);
					binaryWriter.Write(bytes2);
					binaryWriter.Write(buffer);
					byte[] array = memoryStream.ToArray();
					context.Response.ContentType = "application/octet-stream";
					context.Response.ContentLength64 = array.Length;
					context.Response.OutputStream.Write(array, 0, array.Length);
					Plugin.Log.LogInfo($"[LanSaveTransfer] save servido ({array.Length} bytes, mundo pronto={_hostWorldReady}) pra {context.Request.RemoteEndPoint}.");
				}
				else
				{
					context.Response.StatusCode = 404;
				}
			}
			catch (Exception ex)
			{
				Plugin.Log.LogWarning("[LanSaveTransfer] erro servindo pedido: " + ex.Message);
			}
			finally
			{
				try
				{
					context.Response.OutputStream.Close();
				}
				catch
				{
				}
			}
		}
	}

	public static void StopServer()
	{
		if (_serverRunning)
		{
			_serverRunning = false;
			try
			{
				_server?.Stop();
				_server?.Close();
			}
			catch
			{
			}
			_server = null;
			Plugin.Log.LogInfo("[LanSaveTransfer] servidor local do save desligado.");
		}
	}

	public static void Tick()
	{
		UpdateHostInPrologue();
		TickPendingConnect();
		if (!_fetching)
		{
			return;
		}
		if (_downloadResult == null)
		{
			_fetchTimer += Time.unscaledDeltaTime;
			if (_fetchTimer > 75f)
			{
				_fetching = false;
				SetFailed($"JOIN FAILED: no response from {_lastUrl} after {75f:F0}s - check the host's IP/port, and that the HOST is on the main menu with LAN mode selected.");
				Plugin.Log.LogError($"[LanSaveTransfer] timeout ({75f:F0}s) esperando resposta de {_lastUrl} - host inalcançável, IP/porta errados, ou firewall bloqueando.");
			}
			return;
		}
		byte[] downloadResult = _downloadResult;
		_fetching = false;
		_downloadResult = null;
		if (downloadResult.Length == 0)
		{
			SetFailed("JOIN FAILED: could not download the world save from the host (0 bytes). Check the host's IP/port and that its LAN server is running.");
			Plugin.Log.LogError("[LanSaveTransfer] download falhou (0 bytes) - confirme que o HOST está no menu principal com o servidor ligado (mesmo IP/porta configurados dos 2 lados, e que o firewall não está bloqueando).");
			return;
		}
		SetStatus($"save downloaded ({downloadResult.Length} bytes) - applying...");
		string @string;
		byte[] blob;
		try
		{
			using MemoryStream memoryStream = new MemoryStream(downloadResult);
			using BinaryReader binaryReader = new BinaryReader(memoryStream);
			int count = binaryReader.ReadInt32();
			@string = Encoding.UTF8.GetString(binaryReader.ReadBytes(count));
			blob = binaryReader.ReadBytes((int)(memoryStream.Length - memoryStream.Position));
		}
		catch (Exception ex)
		{
			SetFailed("JOIN FAILED: malformed response from host (couldn't read its identity header).");
			Plugin.Log.LogError("[LanSaveTransfer] falha lendo identidade embutida no payload: " + ex.GetType().Name + ": " + ex.Message);
			return;
		}
		ApplyAndLoad(blob, @string);
	}

	private static void ApplyAndLoad(byte[] blob, string hostIdentity)
	{
		int mappedSlotForHost = WorldSync.GetMappedSlotForHost(hostIdentity);
		string text = WorldSync.PeekWorldId(blob);
		string mappedWorldIdForHost = WorldSync.GetMappedWorldIdForHost(hostIdentity);
		bool flag = text == null || mappedWorldIdForHost == null || text == mappedWorldIdForHost;
		if (mappedSlotForHost >= 1 && flag)
		{
			WriteAndLoad(blob, hostIdentity, -1);
			return;
		}
		if (mappedSlotForHost >= 1 && !flag)
		{
			Plugin.Log.LogInfo("[LanSaveTransfer] host '" + hostIdentity + "' mandou um mundo diferente do que já tínhamos mapeado - pedindo pro jogador escolher o slot de novo.");
		}
		_pendingBlob = blob;
		_pendingHostIdentity = hostIdentity;
		Plugin.Log.LogInfo("[LanSaveTransfer] host novo ('" + hostIdentity + "') - pedindo pro jogador escolher o slot.");
		SaveSlotPickerUI.Show(WorldSync.GetSlotSummaries(), OnSlotChosen);
	}

	private static void OnSlotChosen(int slotId)
	{
		byte[] pendingBlob = _pendingBlob;
		string pendingHostIdentity = _pendingHostIdentity;
		_pendingBlob = null;
		_pendingHostIdentity = null;
		WriteAndLoad(pendingBlob, pendingHostIdentity, slotId);
	}

	private static void WriteAndLoad(byte[] blob, string hostIdentity, int explicitSlot)
	{
		SetStatus($"save complete ({blob.Length} bytes) - writing to disk...");
		Plugin.Log.LogInfo($"[LanSaveTransfer] save completo ({blob.Length} bytes) - escrevendo e carregando.");
		int slot = WorldSync.DeserializeWorld(blob, hostIdentity, explicitSlot);
		if (slot < 0)
		{
			SetFailed("JOIN FAILED: error writing the save to disk - check the log for '[World][M2.2] falha'.");
			Plugin.Log.LogError("[LanSaveTransfer] falha escrevendo o save (ver log '[World][M2.2] falha').");
			return;
		}
		SaveManager instance = Singleton<SaveManager>.Instance;
		UI instance2 = Singleton<UI>.Instance;
		if ((Object)(object)instance == (Object)null || (Object)(object)instance2 == (Object)null)
		{
			SetFailed("JOIN FAILED: SaveManager/UI not available - the menu wasn't ready yet. Try again.");
			Plugin.Log.LogError("[LanSaveTransfer] SaveManager/UI indisponível (menu ainda não estava pronto?) - não deu pra carregar sozinho.");
			return;
		}
		MainMenu.SaveState val = instance.loadGameProfiles();
		if (val != null && val.profiles != null)
		{
			Core.profiles = val.profiles;
		}
		GameProfile val2 = Core.profiles?.Find((GameProfile p) => p != null && p.id == slot);
		if (val2 == null)
		{
			SetFailed($"JOIN FAILED: profile #{slot} was written but didn't show up in the list.");
			Plugin.Log.LogError($"[LanSaveTransfer] perfil #{slot} foi escrito mas não apareceu na lista - abortando.");
			return;
		}
		Core.currentProfile = val2;
		instance.updateFilePaths();
		_pendingConnect = true;
		_pendingConnectTimer = 0f;
		SetStatus($"save written to profile #{slot} - loading the world...");
		((MonoBehaviour)instance2).StartCoroutine(instance2.initLoadGame());
		Plugin.Log.LogInfo($"[LanSaveTransfer] initLoadGame() disparado pro perfil #{slot} - esperando o mundo carregar pra conectar a sessão.");
	}

	private static void TickPendingConnect()
	{
		if (!_pendingConnect)
		{
			return;
		}
		_pendingConnectTimer += Time.unscaledDeltaTime;
		if ((Object)(object)Player.Instance != (Object)null && Core.worldGenFinished())
		{
			_pendingConnect = false;
			Plugin.Log.LogInfo("[LanSaveTransfer] mundo carregado - conectando a sessão LAN automaticamente.");
			if (JoinFlow.OnWorldLoaded())
			{
				SetDone();
				return;
			}
			SetFailed("JOIN FAILED: the world loaded, but the network session could not start. Check LAN.Port/PeerIP in the connection window ('0'), or try F8 manually.");
			Plugin.Log.LogError("[LanSaveTransfer] mundo carregou mas StartClientSessionLan() falhou (sessão continua null) - ver logs de [Plugin]/BuildTransport acima pra causa exata.");
		}
		else if (_pendingConnectTimer > 120f)
		{
			_pendingConnect = false;
			SetFailed("JOIN FAILED: the world did not finish loading in 2 minutes.");
			Plugin.Log.LogError("[LanSaveTransfer] mundo não carregou em 2min - conecte manualmente (F8) se o save já estiver certo.");
		}
	}

	public static void Reset()
	{
		_fetching = false;
		_downloadResult = null;
		_pendingBlob = null;
		_pendingHostIdentity = null;
		_pendingConnect = false;
		_pendingConnectTimer = 0f;
		StatusText = "";
		IsActive = false;
		IsFailed = false;
		HostPeerStatus = "";
	}
}
