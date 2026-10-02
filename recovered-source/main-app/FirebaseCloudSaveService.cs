using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;

internal sealed class FirebaseCloudSaveService
{
	internal const int MaxCloudArchiveBytes = 4 * 1024 * 1024;
	internal static bool IsCloudArchiveSizeAllowed(long bytes) => bytes > 0 && bytes <= MaxCloudArchiveBytes;
	private const string ProjectId = "true-truck-508712-c8";

	private const string ApiKey = "AIzaSyB86hnK-SQW4aimW03xN7EbB3msZEWOesI";

	private const string GoogleClientId = "703207090384-0jmqu8hs04hhk4e04lffp1bs0r3fosf9.apps.googleusercontent.com";

	private const string AuthDomain = "true-truck-508712-c8.firebaseapp.com";

	private const int ChunkSize = 614400;

	private readonly string stateDir;

	private readonly LegacyJsonSerializer json = new LegacyJsonSerializer();

	private string idToken;

	private string refreshToken;

	private string uid;
	private string googleEmail;

	public bool IsSignedIn => !string.IsNullOrEmpty(idToken) && !string.IsNullOrEmpty(uid);

	public string AccountId => uid ?? string.Empty;
	public string GoogleEmail => googleEmail ?? string.Empty;

	public FirebaseCloudSaveService(string root)
	{
		stateDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Pokemons Play", "Cloud");
		Directory.CreateDirectory(stateDir);
		LoadSession();
	}

	public async Task<bool> SignInAsync()
	{
		string state = Base64Url(RandomBytes(32));
		string verifier = Base64Url(RandomBytes(32));
		string challenge;
		using (SHA256 sHA = SHA256.Create())
		{
			challenge = Base64Url(sHA.ComputeHash(Encoding.ASCII.GetBytes(verifier)));
		}
		int port = GetFreePort();
		string redirect = "http://127.0.0.1:" + port + "/";
		string url = "https://accounts.google.com/o/oauth2/v2/auth?client_id=" + Uri.EscapeDataString(GoogleClientId) + "&redirect_uri=" + Uri.EscapeDataString(redirect) + "&response_type=code&scope=" + Uri.EscapeDataString("openid email profile") + "&state=" + state + "&code_challenge=" + Uri.EscapeDataString(challenge) + "&code_challenge_method=S256&prompt=select_account";
		HttpListener listener = new HttpListener();
		listener.Prefixes.Add(redirect);
		listener.Start();
		try
		{
			Process.Start(new ProcessStartInfo
			{
				FileName = url,
				UseShellExecute = true
			});
			Task<HttpListenerContext> pending = listener.GetContextAsync();
			if (await Task.WhenAny(pending, Task.Delay(TimeSpan.FromMinutes(3.0))) != pending)
			{
				throw new InvalidOperationException("Tempo de login esgotado. Tente novamente.");
			}
			HttpListenerContext context = await pending;
			if (context.Request.QueryString["state"] != state)
			{
				context.Response.StatusCode = 400;
				context.Response.Close();
				throw new InvalidOperationException("Resposta de login invalida. Tente novamente.");
			}
			string code = context.Request.QueryString["code"];
			byte[] response = Encoding.UTF8.GetBytes("<html><body style='font-family:Segoe UI;text-align:center;padding:40px'><h2>Resposta recebida</h2><p>Volte ao Pokemons Play para conferir o resultado do login.</p></body></html>");
			context.Response.ContentType = "text/html; charset=utf-8";
			context.Response.ContentLength64 = response.Length;
			await context.Response.OutputStream.WriteAsync(response, 0, response.Length);
			context.Response.Close();
			if (string.IsNullOrEmpty(code))
			{
				throw new InvalidOperationException("O Google não retornou o código de login.");
			}
			using HttpClient client = new HttpClient();
			FormUrlEncodedContent form = new FormUrlEncodedContent(new Dictionary<string, string>
			{
				{ "code", code },
				{ "client_id", GoogleClientId },
				{ "redirect_uri", redirect },
				{ "grant_type", "authorization_code" },
				{ "code_verifier", verifier }
			});
			string tokenBody = await (await client.PostAsync("https://oauth2.googleapis.com/token", form)).Content.ReadAsStringAsync();
			string googleAccess;
			if (json.DeserializeObject(tokenBody) is Dictionary<string, object> token)
			{
				googleAccess = (token.ContainsKey("access_token") ? token["access_token"].ToString() : null);
			}
			else
			{
				googleAccess = null;
			}
			if (string.IsNullOrEmpty(googleAccess))
			{
				throw new InvalidOperationException("Não foi possível concluir a autenticação Google.");
			}
			string postBody = "access_token=" + Uri.EscapeDataString(googleAccess) + "&providerId=google.com";
			StringContent firebaseForm = new StringContent(json.Serialize(new Dictionary<string, object>
			{
				{ "postBody", postBody },
				{ "requestUri", "https://true-truck-508712-c8.firebaseapp.com" },
				{ "returnSecureToken", true },
				{ "returnIdpCredential", false }
			}), Encoding.UTF8, "application/json");
			string firebaseBody = await (await client.PostAsync("https://identitytoolkit.googleapis.com/v1/accounts:signInWithIdp?key=AIzaSyB86hnK-SQW4aimW03xN7EbB3msZEWOesI", firebaseForm)).Content.ReadAsStringAsync();
			Dictionary<string, object> firebase = json.DeserializeObject(firebaseBody) as Dictionary<string, object>;
			idToken = Read(firebase, "idToken");
			refreshToken = Read(firebase, "refreshToken");
			uid = Read(firebase, "localId");
			googleEmail = Read(firebase, "email");
			if (!IsSignedIn)
			{
				throw new InvalidOperationException("O Firebase não retornou uma sessão válida.");
			}
			SaveSession();
			return true;
		}
		finally
		{
			listener.Stop();
			listener.Close();
		}
	}

	public void SignOut()
	{
		idToken = null;
		refreshToken = null;
		uid = null;
		googleEmail = null;
		string path = Path.Combine(stateDir, "firebase-session.dat");
		if (File.Exists(path))
		{
			File.Delete(path);
		}
	}

	public async Task UploadFolderAsync(string gameId, string folder, GameInfo expectedGame, CloudBankInfo expectedSnapshot)
	{
		if (expectedGame == null) throw new ArgumentNullException(nameof(expectedGame));
		if (expectedSnapshot == null) throw new ArgumentNullException(nameof(expectedSnapshot));
		await UploadFolderAsync(gameId, folder, globalPokemonBank: false, expectedGame: expectedGame, expectedSnapshot: expectedSnapshot);
	}

	public async Task UploadGlobalPokemonBankAsync(string folder)
	{
		CloudBankInfo expectedSnapshot = await ReadGlobalPokemonBankInfoAsync();
		await UploadFolderAsync(PokemonBankCloudArchive.CloudId, folder, globalPokemonBank: true, expectedSnapshot: expectedSnapshot);
	}

	public async Task UploadGlobalPokemonBankArchiveAsync(string folder, string archivePath)
	{
		CloudBankInfo expectedSnapshot = await ReadGlobalPokemonBankInfoAsync();
		await UploadFolderAsync(PokemonBankCloudArchive.CloudId, folder, globalPokemonBank: true, preparedArchive: archivePath, expectedSnapshot: expectedSnapshot);
	}

	public async Task UploadGlobalPokemonBankArchiveAsync(string folder, string archivePath, CloudBankInfo expectedSnapshot)
	{
		if (expectedSnapshot == null) throw new ArgumentNullException(nameof(expectedSnapshot));
		await UploadFolderAsync(PokemonBankCloudArchive.CloudId, folder, globalPokemonBank: true, preparedArchive: archivePath, expectedSnapshot: expectedSnapshot);
	}

	private async Task UploadFolderAsync(string gameId, string folder, bool globalPokemonBank, GameInfo expectedGame = null, string preparedArchive = null, CloudBankInfo expectedSnapshot = null)
	{
		if (!IsSignedIn)
		{
			throw new InvalidOperationException("Faça login com Google antes de sincronizar.");
		}
		ValidateGameId(gameId);
		if (!globalPokemonBank && expectedGame == null)
			throw new InvalidOperationException("Selecione o jogo antes de enviar um backup de save.");
		if (expectedSnapshot == null)
			throw new InvalidOperationException("Consulte a cópia atual da nuvem antes de enviar um backup.");
		CheckEmulators();
		await RefreshAsync();
		CheckEmulators();
		if (!globalPokemonBank && Directory.GetFiles(folder, "*", SearchOption.AllDirectories).Length == 0)
		{
			throw new InvalidOperationException("Não há saves locais para enviar.");
		}
		Dictionary<string, object> previous = null;
		try
		{
			previous = await GetDocumentAsync(gameId);
		}
		catch (FileNotFoundException)
		{
		}
		if (expectedSnapshot != null)
		{
			CloudBankInfo currentSnapshot = previous == null
				? new CloudBankInfo(false, 0, 0, null)
				: globalPokemonBank ? ReadCloudBankInfo(previous) : ReadCloudSaveInfo(previous);
			if (!IsSameCloudSnapshot(expectedSnapshot, currentSnapshot))
				throw new InvalidOperationException("A cópia na nuvem mudou depois da confirmação. Atualize as informações e confirme novamente antes de enviar.");
		}
		bool ownsArchive = string.IsNullOrEmpty(preparedArchive);
		string archive = ownsArchive ? Path.Combine(Path.GetTempPath(), "pokemons-play-" + Guid.NewGuid().ToString("N") + ".zip") : preparedArchive;
		try
		{
			CheckEmulators();
			if (ownsArchive)
			{
				if (globalPokemonBank) PokemonBankCloudArchive.CreateVerifiedArchive(folder, archive);
				else
				{
					SaveBackupService.CreateVerifiedArchive(folder, archive);
					SaveBackupService.ValidateArchiveForGame(archive, expectedGame);
				}
			}
			else if (globalPokemonBank) PokemonBankCloudArchive.ValidateArchive(archive);
			else SaveBackupService.ValidateArchiveForGame(archive, expectedGame);
			CheckEmulators();
			if (!IsCloudArchiveSizeAllowed(new FileInfo(archive).Length))
			{
				throw new InvalidOperationException(globalPokemonBank
					? "O banco compactado excede o limite atual de 4 MB da nuvem. Remova arquivos duplicados ou envie uma coleção menor."
					: "O backup excede o limite de 4 MB por jogo nesta versão.");
			}
			byte[] bytes = File.ReadAllBytes(archive);
			int count = (bytes.Length + 614400 - 1) / 614400;
			List<Dictionary<string, object>> writes = new List<Dictionary<string, object>>();
			for (int i = 0; i < count; i++)
			{
				int num = Math.Min(614400, bytes.Length - i * 614400);
				byte[] array = new byte[num];
				Buffer.BlockCopy(bytes, i * 614400, array, 0, num);
				writes.Add(FirestoreWrite(gameId + "/chunks/" + i.ToString("D5"), new Dictionary<string, object>
				{
					{
						"data",
						Convert.ToBase64String(array)
					},
					{ "index", i },
					{
						"updatedAt",
						DateTime.UtcNow.ToString("O")
					}
				}));
			}
			writes.Add(FirestoreWrite(gameId, new Dictionary<string, object>
			{
				{ "chunkCount", count },
				{ "size", bytes.Length },
				{
					"sha256",
					Hash(bytes)
				},
				{
					"updatedAt",
					DateTime.UtcNow.ToString("O")
				},
				{ "schema", 1 }
			}));
			if (globalPokemonBank)
				((Dictionary<string, object>)((Dictionary<string, object>)writes[writes.Count - 1]["update"])["fields"]).Add("pokemonCount", FirestoreValue(PokemonBankCloudArchive.CountPokemonInArchive(archive)));
			writes[writes.Count - 1]["currentDocument"] = ((previous == null) ? new Dictionary<string, object> { { "exists", false } } : new Dictionary<string, object> {
			{
				"updateTime",
				Read(previous, "updateTime")
			} });
			await CommitAsync(writes);
		}
		finally
		{
			if (ownsArchive && File.Exists(archive))
			{
				File.Delete(archive);
			}
		}
	}

	public async Task DownloadFolderAsync(string gameId, string folder, GameInfo expectedGame)
	{
		if (expectedGame == null) throw new ArgumentNullException(nameof(expectedGame));
		CloudBankInfo expectedSnapshot = await ReadCloudSaveInfoAsync(gameId);
		await DownloadFolderAsync(gameId, folder, expectedGame, expectedSnapshot);
	}

	public async Task DownloadFolderAsync(string gameId, string folder, GameInfo expectedGame, CloudBankInfo expectedSnapshot)
	{
		if (expectedGame == null) throw new ArgumentNullException(nameof(expectedGame));
		if (expectedSnapshot == null) throw new ArgumentNullException(nameof(expectedSnapshot));
		await DownloadFolderAsync(gameId, folder, globalPokemonBank: false, expectedSnapshot: expectedSnapshot, expectedGame: expectedGame);
	}

	public async Task DownloadGlobalPokemonBankAsync(string folder)
	{
		CloudBankInfo expectedSnapshot = await ReadGlobalPokemonBankInfoAsync();
		await DownloadFolderAsync(PokemonBankCloudArchive.CloudId, folder, globalPokemonBank: true, expectedSnapshot);
	}

	public async Task DownloadGlobalPokemonBankAsync(string folder, CloudBankInfo expectedSnapshot)
	{
		await DownloadFolderAsync(PokemonBankCloudArchive.CloudId, folder, globalPokemonBank: true, expectedSnapshot);
	}

	private async Task DownloadFolderAsync(string gameId, string folder, bool globalPokemonBank, CloudBankInfo expectedSnapshot = null, GameInfo expectedGame = null)
	{
		if (!IsSignedIn)
		{
			throw new InvalidOperationException("Faça login com Google antes de baixar.");
		}
		ValidateGameId(gameId);
		CheckEmulators();
		await RefreshAsync();
		Dictionary<string, object> manifest = await GetDocumentAsync(gameId);
		CloudBankInfo manifestSnapshot = globalPokemonBank ? ReadCloudBankInfo(manifest) : ReadCloudSaveInfo(manifest);
		if (expectedSnapshot != null && !IsSameCloudSnapshot(expectedSnapshot, manifestSnapshot))
		{
			throw new InvalidOperationException("A cópia mudou depois da confirmação. Atualize as informações da nuvem e confira novamente antes de restaurar.");
		}
		int count = ReadIntField(manifest, "chunkCount");
		if (count <= 0)
		{
			throw new InvalidOperationException("Nenhum save na nuvem para este jogo.");
		}
		if (count > 7)
		{
			throw new InvalidOperationException("Backup maior que o limite permitido.");
		}
		using MemoryStream archive = new MemoryStream();
		for (int i = 0; i < count; i++)
		{
			string encoded = ReadStringField(await GetDocumentAsync(gameId + "/chunks/" + i.ToString("D5")), "data");
			if (string.IsNullOrEmpty(encoded))
			{
				throw new InvalidOperationException("Parte de save incompleta na nuvem.");
			}
			byte[] bytes = Convert.FromBase64String(encoded);
			await archive.WriteAsync(bytes, 0, bytes.Length);
		}
		byte[] payload = archive.ToArray();
		if (payload.Length != ReadIntField(manifest, "size") || Hash(payload) != ReadStringField(manifest, "sha256"))
		{
			throw new InvalidOperationException("Backup incompleto ou alterado. O save local foi preservado.");
		}
		Dictionary<string, object> finalManifest = await GetDocumentAsync(gameId);
		CloudBankInfo finalSnapshot = globalPokemonBank ? ReadCloudBankInfo(finalManifest) : ReadCloudSaveInfo(finalManifest);
		if (expectedSnapshot != null && !IsSameCloudSnapshot(expectedSnapshot, finalSnapshot))
			throw new InvalidOperationException("Outro computador atualizou o save enquanto ele era baixado. O save local foi preservado.");
		if (ReadStringField(finalManifest, "sha256") != Hash(payload))
		{
			throw new InvalidOperationException("Outro computador atualizou o save. Baixe novamente.");
		}
		if (globalPokemonBank && expectedSnapshot != null && expectedSnapshot.PokemonCount > 0)
			ValidateCloudBankPokemonCount(expectedSnapshot.PokemonCount, PokemonBankCloudArchive.CountPokemonInArchiveBytes(payload));
		if (globalPokemonBank) PokemonBankCloudArchive.Restore(payload, folder);
		else RestoreArchive(payload, folder, expectedGame);
	}

	internal static void ValidateCloudBankPokemonCount(int expectedCount, int actualCount)
	{
		if (expectedCount > 0 && expectedCount != actualCount)
			throw new InvalidDataException($"A cópia da nuvem informa {expectedCount} Pokémon, mas o ZIP contém {actualCount}. O Banco local foi preservado.");
	}

	internal static bool IsSameCloudBankSnapshot(CloudBankInfo expected, CloudBankInfo current)
		=> IsSameCloudSnapshot(expected, current);

	internal static bool IsSameCloudSnapshot(CloudBankInfo expected, CloudBankInfo current)
	{
		if (expected == null || current == null)
			return false;
		if (!expected.Exists || !current.Exists)
			return expected.Exists == current.Exists;
		if (!string.IsNullOrEmpty(expected.Revision) && !string.IsNullOrEmpty(current.Revision))
			return string.Equals(expected.Revision, current.Revision, StringComparison.Ordinal);
		return expected.PokemonCount == current.PokemonCount
			&& expected.CompressedBytes == current.CompressedBytes
			&& expected.UpdatedAt == current.UpdatedAt;
	}

	private CloudBankInfo ReadCloudBankInfo(Dictionary<string, object> manifest)
	{
		string updated = ReadStringField(manifest, "updatedAt");
		DateTimeOffset? date = DateTimeOffset.TryParse(updated, out DateTimeOffset parsed) ? parsed : null;
		return new CloudBankInfo(true, ReadIntField(manifest, "pokemonCount"), ReadIntField(manifest, "size"), date, Read(manifest, "updateTime"));
	}

	private CloudBankInfo ReadCloudSaveInfo(Dictionary<string, object> manifest)
	{
		string updated = ReadStringField(manifest, "updatedAt");
		DateTimeOffset? date = DateTimeOffset.TryParse(updated, out DateTimeOffset parsed) ? parsed : null;
		return new CloudBankInfo(true, 0, ReadIntField(manifest, "size"), date, Read(manifest, "updateTime"));
	}

	public async Task<CloudBankInfo> ReadCloudSaveInfoAsync(string gameId)
	{
		if (!IsSignedIn) throw new InvalidOperationException("Faça login com Google para consultar a cópia na nuvem.");
		ValidateGameId(gameId);
		await RefreshAsync();
		try { return ReadCloudSaveInfo(await GetDocumentAsync(gameId)); }
		catch (FileNotFoundException) { return new CloudBankInfo(false, 0, 0, null); }
	}

	public async Task<CloudBankInfo> ReadGlobalPokemonBankInfoAsync()
	{
		if (!IsSignedIn) throw new InvalidOperationException("Faça login com Google para consultar a cópia da conta.");
		await RefreshAsync();
		try
		{
			Dictionary<string, object> manifest = await GetDocumentAsync(PokemonBankCloudArchive.CloudId);
			return ReadCloudBankInfo(manifest);
		}
		catch (FileNotFoundException) { return new CloudBankInfo(false, 0, 0, null); }
	}

	private Dictionary<string, object> FirestoreWrite(string relative, Dictionary<string, object> fields)
	{
		Dictionary<string, object> dictionary = new Dictionary<string, object>();
		foreach (KeyValuePair<string, object> field in fields)
		{
			dictionary[field.Key] = FirestoreValue(field.Value);
		}
		Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
		dictionary2.Add("update", new Dictionary<string, object>
		{
			{
				"name",
				FirestoreUserDocumentName(uid, relative)
			},
			{ "fields", dictionary }
		});
		return dictionary2;
	}

	private async Task CommitAsync(List<Dictionary<string, object>> writes)
	{
		using HttpClient client = new HttpClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
		for (int offset = 0; offset < writes.Count; offset += 400)
		{
			List<Dictionary<string, object>> part = writes.GetRange(offset, Math.Min(400, writes.Count - offset));
			string body = json.Serialize(new Dictionary<string, object> { { "writes", part } });
			HttpResponseMessage response = await client.PostAsync("https://firestore.googleapis.com/v1/projects/true-truck-508712-c8/databases/(default)/documents:commit", new StringContent(body, Encoding.UTF8, "application/json"));
			if (!response.IsSuccessStatusCode)
			{
				throw new InvalidOperationException("O Firebase recusou o save: " + (int)response.StatusCode);
			}
		}
	}

	private async Task<Dictionary<string, object>> GetDocumentAsync(string relative)
	{
		using HttpClient client = new HttpClient();
		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", idToken);
		string url = "https://firestore.googleapis.com/v1/" + FirestoreUserDocumentName(uid, relative).Substring(("projects/" + ProjectId + "/databases/(default)/documents/").Length);
		HttpResponseMessage response = await client.GetAsync(url);
		if (response.StatusCode == HttpStatusCode.NotFound)
		{
			throw new FileNotFoundException("Nenhum save disponível na nuvem para este jogo.");
		}
		if (!response.IsSuccessStatusCode)
		{
			throw new InvalidOperationException("Falha ao acessar a nuvem (HTTP " + (int)response.StatusCode + "). Tente novamente.");
		}
		return json.DeserializeObject(await response.Content.ReadAsStringAsync()) as Dictionary<string, object>;
	}

	private int ReadIntField(Dictionary<string, object> document, string name)
	{
		Dictionary<string, object> dictionary = ((document == null) ? null : (document["fields"] as Dictionary<string, object>));
		Dictionary<string, object> dictionary2 = ((dictionary == null || !dictionary.ContainsKey(name)) ? null : (dictionary[name] as Dictionary<string, object>));
		int result;
		return (dictionary2 != null && int.TryParse(dictionary2.ContainsKey("integerValue") ? dictionary2["integerValue"].ToString() : "0", out result)) ? result : 0;
	}

	private string ReadStringField(Dictionary<string, object> document, string name)
	{
		Dictionary<string, object> dictionary = ((document == null) ? null : (document["fields"] as Dictionary<string, object>));
		Dictionary<string, object> dictionary2 = ((dictionary == null || !dictionary.ContainsKey(name)) ? null : (dictionary[name] as Dictionary<string, object>));
		return (dictionary2 != null && dictionary2.ContainsKey("stringValue")) ? dictionary2["stringValue"].ToString() : null;
	}

	private static Dictionary<string, object> FirestoreValue(object value)
	{
		if (value is int)
		{
			Dictionary<string, object> dictionary = new Dictionary<string, object>();
			dictionary.Add("integerValue", value.ToString());
			return dictionary;
		}
		Dictionary<string, object> dictionary2 = new Dictionary<string, object>();
		dictionary2.Add("stringValue", value.ToString());
		return dictionary2;
	}

	private void SaveSession()
	{
		byte[] bytes = Encoding.UTF8.GetBytes(uid + "\n" + idToken + "\n" + refreshToken + "\n" + googleEmail);
		PersistProtectedSession(Path.Combine(stateDir, "firebase-session.dat"), ProtectedData.Protect(bytes, null, DataProtectionScope.CurrentUser));
	}

	internal static void PersistProtectedSession(string path, byte[] protectedBytes)
		=> PersistProtectedSession(path, protectedBytes, (temporary, destination) => File.Move(temporary, destination, overwrite: true));

	internal static void PersistProtectedSession(string path, byte[] protectedBytes, Action<string, string> replaceFile)
	{
		if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("Caminho de sessão inválido.", nameof(path));
		if (protectedBytes == null) throw new ArgumentNullException(nameof(protectedBytes));
		if (replaceFile == null) throw new ArgumentNullException(nameof(replaceFile));
		string temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
		try
		{
			File.WriteAllBytes(temporary, protectedBytes);
			replaceFile(temporary, path);
		}
		finally
		{
			if (File.Exists(temporary))
			{
				try { File.Delete(temporary); }
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
			}
		}
	}

	private void LoadSession()
	{
		string path = Path.Combine(stateDir, "firebase-session.dat");
		if (!File.Exists(path))
		{
			return;
		}
		try
		{
			string[] array = Encoding.UTF8.GetString(ProtectedData.Unprotect(File.ReadAllBytes(path), null, DataProtectionScope.CurrentUser)).Split('\n');
			if (array.Length >= 3)
			{
				uid = array[0];
				idToken = array[1];
				refreshToken = array[2];
				googleEmail = array.Length > 3 ? array[3] : string.Empty;
			}
		}
		catch (CryptographicException)
		{
		}
		catch (IOException)
		{
		}
		catch (UnauthorizedAccessException)
		{
		}
	}

	private async Task RefreshAsync()
	{
		using HttpClient client = new HttpClient();
		HttpResponseMessage result = await client.PostAsync("https://securetoken.googleapis.com/v1/token?key=AIzaSyB86hnK-SQW4aimW03xN7EbB3msZEWOesI", new FormUrlEncodedContent(new Dictionary<string, string>
		{
			{ "grant_type", "refresh_token" },
			{ "refresh_token", refreshToken }
		}));
		if (!result.IsSuccessStatusCode)
		{
			throw new InvalidOperationException("Sessão expirada ou indisponível. Entre com Google novamente.");
		}
		Dictionary<string, object> dictionary = json.DeserializeObject(await result.Content.ReadAsStringAsync()) as Dictionary<string, object>;
		Dictionary<string, object> data = dictionary;
		if (Read(data, "user_id") != uid)
		{
			throw new InvalidOperationException("A conta retornada não corresponde à sessão.");
		}
		idToken = Read(data, "id_token");
		refreshToken = Read(data, "refresh_token");
		SaveSession();
	}

	private static void ValidateGameId(string value)
	{
		if (string.IsNullOrWhiteSpace(value) || value.IndexOfAny(new char[2] { '/', '\\' }) >= 0 || value == "." || value == "..")
		{
			throw new InvalidOperationException("Jogo inválido.");
		}
	}

	internal static string FirestoreUserDocumentName(string userId, string relative)
	{
		if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(relative))
			throw new InvalidOperationException("Conta ou caminho de backup inválido.");
		string[] parts = relative.Split('/');
		ValidateGameId(parts[0]);
		if (!(parts.Length == 1 || (parts.Length == 3 && parts[1] == "chunks" && parts[2].Length == 5 && parts[2].All(char.IsDigit))))
			throw new InvalidOperationException("Conta ou caminho de backup inválido.");
		return "projects/" + ProjectId + "/databases/(default)/documents/users/" + Uri.EscapeDataString(userId) + "/saves/" + relative;
	}

	private static void CheckEmulators()
	{
		string[] array = new string[3] { "melonDS", "visualboyadvance-m", "azahar" };
		foreach (string processName in array)
		{
			Process[] processes = Process.GetProcessesByName(processName);
			try
			{
				foreach (Process process in processes)
				{
					if (!process.HasExited)
						throw new InvalidOperationException("Feche o emulador antes de sincronizar os saves.");
				}
			}
			finally
			{
				foreach (Process process in processes)
					process.Dispose();
			}
		}
	}

	internal static string Hash(byte[] bytes)
	{
		using SHA256 sHA = SHA256.Create();
		return BitConverter.ToString(sHA.ComputeHash(bytes)).Replace("-", "");
	}

	internal static void RestoreArchive(byte[] bytes, string folder, GameInfo expectedGame)
	{
		if (expectedGame == null) throw new ArgumentNullException(nameof(expectedGame));
		string text = Path.GetFullPath(folder).TrimEnd('\\', '/');
		string text2 = text + ".restore-" + Guid.NewGuid().ToString("N");
		string text3 = text + ".backup-" + Guid.NewGuid().ToString("N");
		try
		{
			using (MemoryStream stream = new MemoryStream(bytes))
				SaveBackupService.ExtractArchive(stream, text2);
			SaveBackupService.ValidateForGame(text2, expectedGame);

			CheckEmulators();
			bool hasCurrentSave = Directory.Exists(text);
			if (hasCurrentSave)
			{
				string savesRoot = Path.GetDirectoryName(text);
				if (string.IsNullOrWhiteSpace(savesRoot))
					throw new InvalidOperationException("Não foi possível localizar a pasta de saves para criar o backup automático.");
				string automaticBackups = Path.Combine(savesRoot, "Backups", "Automaticos");
				SaveBackupService.CreateIfChanged(text, automaticBackups, Path.GetFileName(text));
				CheckEmulators();
				Directory.Move(text, text3);
			}

			try
			{
				Directory.Move(text2, text);
			}
			catch
			{
				if (hasCurrentSave && !Directory.Exists(text))
					Directory.Move(text3, text);
				throw;
			}

			if (hasCurrentSave && Directory.Exists(text3))
			{
				try { Directory.Delete(text3, recursive: true); }
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
			}
		}
		finally
		{
			if (Directory.Exists(text2))
			{
				try { Directory.Delete(text2, recursive: true); }
				catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
			}
		}
	}

	private static string Read(Dictionary<string, object> map, string key)
	{
		return (map != null && map.ContainsKey(key) && map[key] != null) ? map[key].ToString() : null;
	}

	private static byte[] RandomBytes(int count)
	{
		byte[] array = new byte[count];
		using (RandomNumberGenerator randomNumberGenerator = RandomNumberGenerator.Create())
		{
			randomNumberGenerator.GetBytes(array);
		}
		return array;
	}

	private static string Base64Url(byte[] data)
	{
		return Convert.ToBase64String(data).TrimEnd('=').Replace('+', '-')
			.Replace('/', '_');
	}

	private static int GetFreePort()
	{
		TcpListener tcpListener = new TcpListener(IPAddress.Loopback, 0);
		tcpListener.Start();
		int port = ((IPEndPoint)tcpListener.LocalEndpoint).Port;
		tcpListener.Stop();
		return port;
	}
}

internal sealed class CloudBankInfo
{
	public bool Exists { get; }
	public int PokemonCount { get; }
	public int CompressedBytes { get; }
	public DateTimeOffset? UpdatedAt { get; }
	public string Revision { get; }
	public CloudBankInfo(bool exists, int pokemonCount, int compressedBytes, DateTimeOffset? updatedAt)
		: this(exists, pokemonCount, compressedBytes, updatedAt, null)
	{
	}
	public CloudBankInfo(bool exists, int pokemonCount, int compressedBytes, DateTimeOffset? updatedAt, string revision)
	{
		Exists = exists; PokemonCount = pokemonCount; CompressedBytes = compressedBytes; UpdatedAt = updatedAt; Revision = revision;
	}
}

// Keeps the recovered Firebase service's dictionary-based JSON contract on modern .NET.
internal sealed class LegacyJsonSerializer
{
	public string Serialize(object value) => JsonSerializer.Serialize(value);

	public object DeserializeObject(string json)
	{
		using JsonDocument document = JsonDocument.Parse(json);
		return ConvertElement(document.RootElement);
	}

	private static object ConvertElement(JsonElement element)
	{
		switch (element.ValueKind)
		{
			case JsonValueKind.Object:
				Dictionary<string, object> map = new Dictionary<string, object>();
				foreach (JsonProperty property in element.EnumerateObject()) map[property.Name] = ConvertElement(property.Value);
				return map;
			case JsonValueKind.Array:
				List<object> list = new List<object>();
				foreach (JsonElement item in element.EnumerateArray()) list.Add(ConvertElement(item));
				return list;
			case JsonValueKind.String:
				return element.GetString();
			case JsonValueKind.Number:
				return element.TryGetInt64(out long integer) ? integer : element.GetDouble();
			case JsonValueKind.True:
				return true;
			case JsonValueKind.False:
				return false;
			default:
				return null;
		}
	}
}
