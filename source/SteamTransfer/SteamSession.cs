// SPDX-License-Identifier: GPL-3.0-or-later
// Direct SteamKit public APIs; no DepotDownloader implementation.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2;
using SteamKit2.Authentication;
using SteamKit2.CDN;

namespace SpiderManInstaller.SteamTransfer
{
    internal sealed class SteamSession
    {
        const uint AppId = 1817070;
        readonly SteamClient client;
        readonly CallbackManager callbacks;
        readonly SteamUser user;
        readonly TransferTrace trace;
        readonly CancellationTokenSource lifetime;
        readonly List<IDisposable> subscriptions = new List<IDisposable>();
        readonly List<HttpClient> clients = new List<HttpClient>();
        readonly List<byte[]> keys = new List<byte[]>();
        readonly TaskCompletionSource<bool> connected = NewCompletion<bool>();
        readonly TaskCompletionSource<EResult> loggedOn = NewCompletion<EResult>();
        readonly TaskCompletionSource<EResult> licenses = NewCompletion<EResult>();
        readonly CancellationTokenRegistration cancelHttp;
        readonly Task pump;
        SteamKit2.CDN.Client cdn;
        Server[] servers;
        int nextServer;
        bool closing;

        static TaskCompletionSource<T> NewCompletion<T>() => new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        public SteamSession(IMachineInfoProvider identity, TransferTrace trace, CancellationToken token)
        {
            this.trace = trace;
            lifetime = CancellationTokenSource.CreateLinkedTokenSource(token);
            client = new SteamClient(SteamConfiguration.Create(b => b.WithProtocolTypes(ProtocolTypes.WebSocket)
                .WithMachineInfoProvider(identity).WithHttpClientFactory(CreateHttpClient)));
            callbacks = new CallbackManager(client);
            user = client.GetHandler<SteamUser>();
            subscriptions.Add(callbacks.Subscribe<SteamClient.ConnectedCallback>(c => { trace.Record(TransferEvent.Connected); connected.TrySetResult(true); }));
            subscriptions.Add(callbacks.Subscribe<SteamUser.LoggedOnCallback>(c => loggedOn.TrySetResult(c.Result)));
            subscriptions.Add(callbacks.Subscribe<SteamApps.LicenseListCallback>(c => licenses.TrySetResult(c.Result)));
            subscriptions.Add(callbacks.Subscribe<SteamClient.DisconnectedCallback>(c =>
            { if (!closing) { trace.Record(TransferEvent.Disconnected); lifetime.Cancel(); } }));
            subscriptions.Add(callbacks.Subscribe<SteamUser.LoggedOffCallback>(c => { if (!closing) lifetime.Cancel(); }));
            cancelHttp = lifetime.Token.Register(() => { lock (clients) foreach (var http in clients) http.Dispose(); });
            pump = Task.Run(async () =>
            {
                try
                {
                    while (!lifetime.IsCancellationRequested)
                    {
                        callbacks.RunCallbacks();
                        await Task.Delay(20, lifetime.Token).ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex) { trace.Record(TransferEvent.Failed, failure: ex); lifetime.Cancel(); }
            });
        }
        HttpClient CreateHttpClient()
        {
            lock (clients)
            {
                lifetime.Token.ThrowIfCancellationRequested();
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(75) };
                clients.Add(http); return http;
            }
        }
        async Task<T> Wait<T>(Task<T> task, int seconds = 60)
        {
            using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
            {
                var delay = Task.Delay(TimeSpan.FromSeconds(seconds), timeout.Token);
                if (await Task.WhenAny(task, delay).ConfigureAwait(false) != task)
                {
                    var ignored = task.ContinueWith(t => { var error = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                    lifetime.Token.ThrowIfCancellationRequested();
                    throw new TimeoutException("Steam did not respond in time. Retry to resume.");
                }
                timeout.Cancel(); return await task.ConfigureAwait(false);
            }
        }
        public async Task Connect()
        {
            client.Connect(); await Wait(connected.Task, 45).ConfigureAwait(false);
        }
        public async Task<QrAuthSession> CreateQr(Action<string> show)
        {
            var auth = await Wait(client.Authentication.BeginAuthSessionViaQRAsync(new AuthSessionDetails
            { DeviceFriendlyName = "UWP Spider Launcher", IsPersistentSession = false })).ConfigureAwait(false);
            auth.ChallengeURLChanged = () => show(auth.ChallengeURL);
            show(auth.ChallengeURL); trace.Record(TransferEvent.QrReady); return auth;
        }
        public async Task Login(Action<string> showQr,SteamLoginCredentials credentials=null,ISteamGuardPrompt prompt=null)
        {
            QrAuthSession qrAuth=null;AuthSession auth=null;AuthSessionDetails details=null;
            try
            {
                await Connect().ConfigureAwait(false);
                using(var approval=CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token))
                {
                    approval.CancelAfter(TimeSpan.FromMinutes(4));
                    if(credentials==null){qrAuth=await CreateQr(showQr).ConfigureAwait(false);auth=qrAuth;}
                    else
                    {
                        if(prompt==null)throw new TransferProblem("Steam Guard verification is unavailable. Try QR sign-in.");
                        details=new AuthSessionDetails{Username=credentials.Username,Password=credentials.TakePassword(),
                            DeviceFriendlyName="UWP Spider Launcher",IsPersistentSession=false,Authenticator=new SteamGuardAuthenticator(prompt,approval.Token)};
                        try{auth=await Wait(client.Authentication.BeginAuthSessionViaCredentialsAsync(details)).ConfigureAwait(false);}
                        finally{details.Password=null;details.Username=null;credentials.Dispose();}
                    }
                    var result=await auth.PollingWaitForResultAsync(approval.Token).ConfigureAwait(false);
                    showQr(null);
                    user.LogOn(new SteamUser.LogOnDetails{Username=result.AccountName,AccessToken=result.RefreshToken,
                        ShouldRememberPassword=false,LoginID=BitConverter.ToUInt32(Guid.NewGuid().ToByteArray(),0),MachineName="UWP Spider Launcher"});
                    result=null; // Session only; no saved password, guard data, or tokens.
                }
                var outcome = await Wait(loggedOn.Task).ConfigureAwait(false);
                trace.Record(TransferEvent.LogonResult, (int)outcome);
                if (outcome != EResult.OK) throw new TransferProblem("Steam sign-in not accepted (" + outcome + ").");
                var licenseResult = await Wait(licenses.Task).ConfigureAwait(false);
                trace.Record(TransferEvent.LicenseResult, (int)licenseResult);
                if (licenseResult != EResult.OK)
                    throw new TransferProblem("Steam could not confirm the account's licenses.");
                trace.Record(TransferEvent.Authenticated);
                var available = await Wait(client.GetHandler<SteamContent>().GetServersForSteamPipe()).ConfigureAwait(false);
                servers = available.Where(s => s.Protocol == Server.ConnectionProtocol.HTTPS && !s.UseAsProxy &&
                    !s.SteamChinaOnly && (s.AllowedAppIds.Length == 0 || s.AllowedAppIds.Contains(AppId)))
                    .OrderByDescending(s => s.PreferredServer).ThenBy(s => s.WeightedLoad).Take(12).ToArray();
                if (servers.Length == 0) throw new TransferProblem("Steam returned no compatible secure download servers.");
                trace.Record(TransferEvent.Servers, servers.Length); cdn = new SteamKit2.CDN.Client(client);
            }
            catch(AuthenticationException ex)
            {throw new TransferProblem("Steam sign-in was not accepted ("+ex.Result+"). Check your account details or Steam Guard and try again.");}
            finally
            {
                if(details!=null){details.Password=null;details.Username=null;}
                credentials?.Dispose();
                if(qrAuth!=null)qrAuth.ChallengeURLChanged=null;
                showQr(null);
            }
        }
        async Task<T> Retry<T>(Func<Server, Task<T>> fetch, CancellationToken token)
        {
            using (var cancelled = token.Register(() => { if (!lifetime.IsCancellationRequested) lifetime.Cancel(); }))
            {
                for (int attempt = 0; ; attempt++)
                {
                    token.ThrowIfCancellationRequested(); lifetime.Token.ThrowIfCancellationRequested();
                    try
                    {
                        int index = (Interlocked.Increment(ref nextServer) & int.MaxValue) % servers.Length;
                        return await fetch(servers[index]).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is HttpRequestException || ex is SteamKitWebRequestException ||
                        ex is IOException || ex is OperationCanceledException)
                    {
                        token.ThrowIfCancellationRequested(); lifetime.Token.ThrowIfCancellationRequested();
                        trace.Record(TransferEvent.Retry, attempt + 1, ex);
                        if (attempt == 3) throw new TransferProblem("Steam content download failed after four attempts. Retry to resume checked chunks.");
                        await Task.Delay(500 * (1 << attempt), lifetime.Token).ConfigureAwait(false);
                    }
                }
            }
        }
        async Task<KeyValue> ReadLanguageProduct()
        {
            var apps=client.GetHandler<SteamApps>();
            var tokens=await apps.PICSGetAccessTokens((uint?)AppId,null);
            ulong access=0;tokens.AppTokens.TryGetValue(AppId,out access);
            var info=await apps.PICSGetProductInfo(new SteamApps.PICSRequest(AppId,access),null);
            if(info.Failed || !info.Complete)throw new TransferProblem("Steam language metadata was incomplete. Retry the download.");
            foreach(var batch in info.Results)
                if(batch.Apps.TryGetValue(AppId,out var product)&&!product.MissingToken)return product.KeyValues;
            throw new TransferProblem("Steam did not grant access to game language metadata.");
        }
        public async Task<List<TransferFile>> ReadPlan(List<ExpectedFile> expected, CancellationToken token, string language="english", bool languageOnly=false)
        {
            var wanted = expected.ToDictionary(f => ImportEngine.SafePath(f.Path), StringComparer.OrdinalIgnoreCase);
            if (wanted.Keys.Any(p => p.StartsWith(".steamkit-transfer", StringComparison.OrdinalIgnoreCase) ||
                p.Equals("import-receipt.txt", StringComparison.OrdinalIgnoreCase))) throw new InvalidDataException("Reserved download metadata path.");
            var planned = new Dictionary<string, TransferFile>(StringComparer.OrdinalIgnoreCase);
            var depots=SteamLanguages.Depots(language,languageOnly);
            KeyValue product=depots.Any(d=>d.Manifest==0)?await Wait(ReadLanguageProduct()).ConfigureAwait(false):null;
            foreach(var selected in depots)
            {
                uint depot=selected.Id;
                ulong manifestId=selected.Manifest!=0?selected.Manifest:SteamLanguages.PublicManifest(product,depot,language);
                var keyResult = await Wait(GetKey(depot)).ConfigureAwait(false);
                trace.Record(TransferEvent.DepotKeyResult, ((long)depot << 32) | (uint)keyResult.Result);
                if (keyResult.Result != EResult.OK || keyResult.DepotKey == null)
                    throw new TransferProblem("Steam denied access to depot " + depot + " (" + keyResult.Result + "). Ownership is required.");
                byte[] key = keyResult.DepotKey; keys.Add(key);
                ulong request = await Wait(client.GetHandler<SteamContent>().GetManifestRequestCode(depot, AppId, manifestId)).ConfigureAwait(false);
                trace.Record(TransferEvent.ManifestAccess, ((long)depot << 1) | (request == 0 ? 0 : 1));
                if (request == 0) throw new TransferProblem("Steam did not grant access to supported depot " + depot + ".");
                var manifest = await Retry(server => cdn.DownloadManifestAsync(depot, manifestId, request, server, key), token).ConfigureAwait(false);
                if (manifest.DepotID != depot || manifest.ManifestGID != manifestId || manifest.FilenamesEncrypted)
                    throw new InvalidDataException("Steam manifest identity could not be verified.");
                foreach (var entry in manifest.Files)
                {
                    string name = ImportEngine.SafePath(entry.FileName);
                    ExpectedFile file;
                    if (!wanted.TryGetValue(name, out file)) continue;
                    if ((entry.Flags & (EDepotFileFlag.Directory | EDepotFileFlag.Symlink)) != 0 ||
                        !string.IsNullOrEmpty(entry.LinkTarget) || entry.TotalSize != (ulong)file.Size || planned.ContainsKey(name))
                        throw new InvalidDataException("Unsupported or duplicate file in manifest: " + name);
                    var plan = new TransferFile { Expected = file };
                    foreach (var raw in entry.Chunks)
                    {
                        if (raw.ChunkID == null || raw.ChunkID.Length != 20 || raw.Checksum == null || raw.Checksum.Length != 4)
                            throw new InvalidDataException("Invalid Steam chunk identity.");
                        var chunk = raw;
                        plan.Chunks.Add(new TransferChunk { Offset = checked((long)chunk.Offset), Length = checked((int)chunk.UncompressedLength),
                            NetworkLength = chunk.CompressedLength, Matches = data => CryptoHelper.AdlerHash(data).SequenceEqual(chunk.Checksum),
                            Fetch = async ct =>
                            {
                                var payload = await Retry(server => cdn.DownloadDepotChunkAsync(depot, chunk, server, key), ct).ConfigureAwait(false);
                                if (!payload.IsProcessed) throw new InvalidDataException("Steam chunk was not verified.");
                                return payload.Data;
                            } });
                    }
                    plan.Validate(); planned.Add(name, plan);
                }
                trace.Record(TransferEvent.Manifest, depot);
            }
            if (planned.Count != wanted.Count) throw new InvalidDataException("Steam manifests do not contain all files for the supported build.");
            return planned.Values.OrderBy(p => Priority(p.Expected.Path)).ThenBy(p => p.Expected.Path, StringComparer.OrdinalIgnoreCase).ToList();
        }
        async Task<SteamApps.DepotKeyCallback> GetKey(uint depot) => await client.GetHandler<SteamApps>().GetDepotDecryptionKey(depot, AppId);
        static int Priority(string name) => name.Equals("Spider-Man.exe", StringComparison.OrdinalIgnoreCase) ? 0 :
            name.Replace('\\', '/').Equals("asset_archive/toc", StringComparison.OrdinalIgnoreCase) ? 1 : 2;
        public async Task Close()
        {
            closing = true; client.Disconnect(); lifetime.Cancel();
            await pump.ConfigureAwait(false);
            cancelHttp.Dispose();
            foreach (var subscription in subscriptions) subscription.Dispose();
            cdn?.Dispose();
            lock (clients) foreach (var http in clients) http.Dispose();
            foreach (var key in keys) Array.Clear(key, 0, key.Length);
            lifetime.Dispose();
        }
    }
}
