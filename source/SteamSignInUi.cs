// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using SpiderManInstaller.SteamTransfer;
namespace SpiderManInstaller
{
    public sealed partial class MainPage
    {
        ContentDialog steamAuthDialog;
        sealed class SteamGuardUi : ISteamGuardPrompt
        {
            readonly MainPage page;
            public SteamGuardUi(MainPage page){this.page=page;}
            public Task<string> Code(bool email,bool retry,CancellationToken token)=>page.AskSteamGuardCode(email,retry,token);
            public Task<bool> MobileApproval(CancellationToken token)=>page.AskSteamMobileApproval(token);
        }
        async Task<T> SteamDialog<T>(Func<ContentDialog> create,Func<ContentDialogResult,T> read,Action clear,CancellationToken token)
        {
            var completion=new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            await Dispatcher.RunAsync(CoreDispatcherPriority.Normal,async ()=>
            {
                ContentDialog dialog=null;
                try
                {
                    token.ThrowIfCancellationRequested();dialog=create();steamAuthDialog=dialog;
                    using(token.Register(()=>{var hide=Dispatcher.RunAsync(CoreDispatcherPriority.Normal,()=>dialog.Hide());}))
                    {
                        var result=await dialog.ShowAsync();token.ThrowIfCancellationRequested();
                        completion.TrySetResult(read(result));
                    }
                }
                catch(OperationCanceledException){completion.TrySetCanceled();}
                catch{completion.TrySetException(new IOException("Unable to open the Steam sign-in window. Cancel and try again."));}
                finally{clear();if(ReferenceEquals(steamAuthDialog,dialog))steamAuthDialog=null;}
            });
            return await completion.Task;
        }
        Task<SteamLoginCredentials> AskSteamCredentials(CancellationToken token)
        {
            TextBox account=null;PasswordBox password=null;
            return SteamDialog(()=>
            {
                account=new TextBox{Header="Steam account name",PlaceholderText="Account name, not profile name",MaxLength=128,IsSpellCheckEnabled=false,IsTextPredictionEnabled=false};
                password=new PasswordBox{Header="Password",MaxLength=512,PasswordRevealMode=PasswordRevealMode.Hidden};
                var content=new StackPanel{Spacing=12,MaxWidth=420};content.Children.Add(new TextBlock{Text="Sign in to Steam to download a game you own. Your password is not saved or logged.",TextWrapping=TextWrapping.Wrap});content.Children.Add(account);content.Children.Add(password);
                var dialog=new ContentDialog{Title="Steam sign-in",Content=content,PrimaryButtonText="Sign in",CloseButtonText="Cancel",IsPrimaryButtonEnabled=false};
                account.TextChanged+=(s,e)=>dialog.IsPrimaryButtonEnabled=!String.IsNullOrWhiteSpace(account.Text)&&password.Password.Length>0;
                password.PasswordChanged+=(s,e)=>dialog.IsPrimaryButtonEnabled=!String.IsNullOrWhiteSpace(account.Text)&&password.Password.Length>0;
                dialog.Opened+=(s,e)=>account.Focus(FocusState.Programmatic);return dialog;
            },result=>
            {
                if(result!=ContentDialogResult.Primary)throw new OperationCanceledException();
                var credentials=new SteamLoginCredentials(account.Text,password.Password);account.Text="";password.Password="";return credentials;
            },()=>{if(account!=null)account.Text="";if(password!=null)password.Password="";},token);
        }
        Task<string> AskSteamGuardCode(bool email,bool retry,CancellationToken token)
        {
            PasswordBox code=null;
            return SteamDialog(()=>
            {
                code=new PasswordBox{Header="Steam Guard code",MaxLength=16,PasswordRevealMode=PasswordRevealMode.Hidden};
                var content=new StackPanel{Spacing=12,MaxWidth=420};content.Children.Add(new TextBlock{Text=(retry?"That code was not accepted. ":"")+(email?"Enter the code Steam sent to your email.":"Enter the current code from your Steam authenticator."),TextWrapping=TextWrapping.Wrap});content.Children.Add(code);
                var dialog=new ContentDialog{Title="Steam Guard",Content=content,PrimaryButtonText="Verify code",CloseButtonText="Cancel",IsPrimaryButtonEnabled=false};
                code.PasswordChanged+=(s,e)=>dialog.IsPrimaryButtonEnabled=code.Password.Trim().Length>0;dialog.Opened+=(s,e)=>code.Focus(FocusState.Programmatic);return dialog;
            },result=>
            {
                if(result!=ContentDialogResult.Primary)throw new OperationCanceledException();
                string value=code.Password.Trim();code.Password="";return value;
            },()=>{if(code!=null)code.Password="";},token);
        }
        Task<bool> AskSteamMobileApproval(CancellationToken token)
        {
            return SteamDialog(()=>new ContentDialog{Title="Approve Steam sign-in",Content=new TextBlock{Text="Approve the UWP Spider Launcher sign-in notification in your Steam mobile app, then select Approved. Or choose Use a code to enter an authenticator code instead.",TextWrapping=TextWrapping.Wrap,MaxWidth=420},PrimaryButtonText="Approved",SecondaryButtonText="Use a code",CloseButtonText="Cancel"},result=>
            {
                if(result==ContentDialogResult.None)throw new OperationCanceledException();
                return result==ContentDialogResult.Primary;
            },()=>{},token);
        }
    }
}
