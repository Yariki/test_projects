using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Dropbox.Api;
using Dropbox.Api.Files;

namespace DropboxTest
{
    class Program
    {
        // Add an ApiKey (from https://www.dropbox.com/developers/apps) here
        private const string ApiKey = "jiaz0yjc8015g6t";

        // This loopback host is for demo purpose. If this port is not
        // available on your machine you need to update this URL with an unused port.
        private const string LoopbackHost = "http://127.0.0.1:52475/";

        // URL to receive OAuth 2 redirect from Dropbox server.
        // You also need to register this redirect URL on https://www.dropbox.com/developers/apps.
        private readonly Uri RedirectUri = new Uri(LoopbackHost + "authorize");

        // URL to receive access token from JS.
        private readonly Uri JSRedirectUri = new Uri(LoopbackHost + "token");


        [DllImport("kernel32.dll", ExactSpelling = true)]
        private static extern IntPtr GetConsoleWindow();

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [STAThread]
        static int Main(string[] args)
        {
            var instance = new Program();

            var task = Task.Run(instance.Run);
            task.Wait();

            return task.Result;

        }

        private async Task<int> Run()
        {
            DropboxCertHelper.InitializeCertPinning();

            var accessToken = await this.GetAccessToken();
            if (string.IsNullOrEmpty(accessToken))
            {
                return 1;
            }

            using (var client = new DropboxClient(accessToken))
            {

                var list = await client.Files.ListFolderAsync(String.Empty);

                foreach (var item in list.Entries)
                {
                    Console.WriteLine($"Item: {item} - IsFile: {item.IsFile} - IsFolder: {item.IsFolder}");
                }

                try
                {
                
                    var folderArg = new CreateFolderArg("/temp");
                    var folder = await client.Files.CreateFolderV2Async(folderArg);
                    Console.WriteLine($"Folder created: {folder.Metadata.Name}");

                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }

            return 0;
        }


        private async Task HandleOAuth2Redirect(HttpListener http)
        {
            var context = await http.GetContextAsync();

            // We only care about request to RedirectUri endpoint.
            while (context.Request.Url.AbsolutePath != RedirectUri.AbsolutePath)
            {
                context = await http.GetContextAsync();
            }

            context.Response.ContentType = "text/html";

            // Respond with a page which runs JS and sends URL fragment as query string
            // to TokenRedirectUri.
            using (var file = File.OpenRead("index.html"))
            {
                file.CopyTo(context.Response.OutputStream);
            }

            context.Response.OutputStream.Close();
        }

        private async Task<OAuth2Response> HandleJSRedirect(HttpListener http)
        {
            var context = await http.GetContextAsync();

            // We only care about request to TokenRedirectUri endpoint.
            while (context.Request.Url.AbsolutePath != JSRedirectUri.AbsolutePath)
            {
                context = await http.GetContextAsync();
            }

            var redirectUri = new Uri(context.Request.QueryString["url_with_fragment"]);

            var result = DropboxOAuth2Helper.ParseTokenFragment(redirectUri);

            return result;
        }

        private async Task<string> GetAccessToken()
        {
            var accessToken = string.Empty;
            try
            {
                Console.WriteLine("Waiting for credentials.");
                var state = Guid.NewGuid().ToString("N");
                var authorizeUri =
                    DropboxOAuth2Helper.GetAuthorizeUri(OAuthResponseType.Token, ApiKey, RedirectUri, state: state);
                var http = new HttpListener();
                http.Prefixes.Add(LoopbackHost);

                http.Start();

                System.Diagnostics.Process.Start(authorizeUri.ToString());

                // Handle OAuth redirect and send URL fragment to local server using JS.
                await HandleOAuth2Redirect(http);

                // Handle redirect from JS and process OAuth response.
                var result = await HandleJSRedirect(http);

                if (result.State != state)
                {
                    // The state in the response doesn't match the state in the request.
                    return null;
                }

                Console.WriteLine("and back...");

                // Bring console window to the front.
                SetForegroundWindow(GetConsoleWindow());

                accessToken = result.AccessToken;
                var uid = result.Uid;
                Console.WriteLine("Uid: {0}", uid);

            }
            catch (Exception e)
            {
                Console.WriteLine("Error: {0}", e.Message);
                return null;
            }

            return accessToken;
        }
    }
}