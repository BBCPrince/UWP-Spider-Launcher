// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Threading;
using System.Threading.Tasks;
using SteamKit2.Authentication;
namespace SpiderManInstaller.SteamTransfer
{
    // Session-only input. Never serialize this object or include it in diagnostics.
    internal sealed class SteamLoginCredentials : IDisposable
    {
        public string Username { get; private set; }
        string password;
        public SteamLoginCredentials(string username,string password)
        {
            if(String.IsNullOrWhiteSpace(username)||username.Trim().Length>128||String.IsNullOrEmpty(password)||password.Length>512)
                throw new TransferProblem("Enter your Steam account name and password.");
            Username=username.Trim();this.password=password;
        }
        public string TakePassword(){var value=password;password=null;return value;}
        public void Dispose(){Username=null;password=null;}
    }
    internal interface ISteamGuardPrompt
    {
        Task<string> Code(bool email,bool retry,CancellationToken token);
        Task<bool> MobileApproval(CancellationToken token);
    }
    internal sealed class SteamGuardAuthenticator : IAuthenticator
    {
        readonly ISteamGuardPrompt prompt;readonly CancellationToken token;
        public SteamGuardAuthenticator(ISteamGuardPrompt prompt,CancellationToken token){this.prompt=prompt;this.token=token;}
        public async Task<string> GetDeviceCodeAsync(bool previousCodeWasIncorrect)
        {token.ThrowIfCancellationRequested();var code=await prompt.Code(false,previousCodeWasIncorrect,token).ConfigureAwait(false);token.ThrowIfCancellationRequested();return code;}
        public async Task<string> GetEmailCodeAsync(string email,bool previousCodeWasIncorrect)
        {token.ThrowIfCancellationRequested();var code=await prompt.Code(true,previousCodeWasIncorrect,token).ConfigureAwait(false);token.ThrowIfCancellationRequested();return code;}
        public async Task<bool> AcceptDeviceConfirmationAsync()
        {token.ThrowIfCancellationRequested();var accepted=await prompt.MobileApproval(token).ConfigureAwait(false);token.ThrowIfCancellationRequested();return accepted;}
    }
}
