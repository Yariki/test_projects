﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Download;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Util.Store;
using File = Google.Apis.Drive.v3.Data.File;

namespace GoogleDriveTest
{
    internal class Program
    {
        public static void Main(string[] args)
        {
            string[] scopes =
            {
                DriveService.Scope.DriveFile,
                DriveService.Scope.Drive,
                DriveService.Scope.DriveAppdata
            };
            var clientId =
                "612213896674-cpqjc29rj2sog88ihlk2sd9jribrparq.apps.googleusercontent.com"; // From https://console.developers.google.com
            var clientSecret = "yHz-xmR2vudvRUmhG5CY3rv0"; // From https://console.developers.google.com

            // here is where we Request the user to give us access, or use the Refresh Token that was previously stored in %AppData%
            GoogleWebAuthorizationBroker.Folder = "Drive.Sample";
            var credential = GoogleWebAuthorizationBroker.AuthorizeAsync(new ClientSecrets
                {
                    ClientId = clientId,
                    ClientSecret = clientSecret
                },
                scopes,
                "Admin",
                CancellationToken.None,
                new FileDataStore("Daimto.GoogleDrive.Auth.Store")).Result;

            var service = new DriveService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "Drive API Sample"
            });

            
            var filelist = retrieveAllFiles(service,"mimeType = 'application/vnd.google-apps.folder' and name = 'TaskWorkspace'");
            string folderId = string.Empty;
            folderId = !filelist.Any() ? CreateFolderAndReturnId(service, "TaskWorkspace") : filelist.First().Id;

            if (!string.IsNullOrEmpty(folderId))
            {
                //remove
                var removeLIst = retrieveAllFiles(service,$"name = 'WorkspaceManager.vsix.zip'");
                foreach (var fileRemove in removeLIst)
                {
	                service.Files.Delete(fileRemove.Id).Execute();
                }

                // upload
                var fileMetadata = new File()
                {
                    Name = "WorkspaceManager.vsix.zip",
                    Parents = new List<string>() { folderId}
                };
                FilesResource.CreateMediaUpload request;
                using (var stream = new System.IO.FileStream("C:/Users/ADMIN/Downloads/test.txt",
                                        System.IO.FileMode.Open))
                {
                    request = service.Files.Create(
                        fileMetadata, stream,"application/zip");
                    request.Fields = "id";
                    
                    request.Upload();
                }
                var file = request.ResponseBody;
                Console.WriteLine("File ID: " + file.Id);

                var fileResp  = service.Files.Get(file.Id);

                //download


                var memStream = new MemoryStream();
                fileResp.MediaDownloader.ProgressChanged += progress =>
                {
                    switch (progress.Status)
                    {
                        case DownloadStatus.Completed:
                            using (var fileStream = System.IO.File.Create("c:/temp/WorkspaceManager.vsix.zip"))
                            {
                                memStream.WriteTo(fileStream);
                                fileStream.Flush();
                            }

                            
                            break;
                        case DownloadStatus.Failed:
                            if(progress.Exception != null)
                                Console.WriteLine(progress.Exception.Message);
                            break;
                            
                    }
                };
                fileResp.Download(memStream);

            }
            
            
            foreach (var file in filelist) Console.WriteLine( $"{file.Name} - {file.MimeType} - {file.Id}");
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }

        private static string CreateFolderAndReturnId(DriveService service, string name)
        {
            File fileMetadata = new File();
            fileMetadata.Name = "TaskWorkspace";
            fileMetadata.MimeType = "application/vnd.google-apps.folder";

            File file = service.Files.Create(fileMetadata).Execute();
            Console.WriteLine("Folder ID: " + file.Id);
            return file.Id;
        }
        
        private static void UploadFile(DriveService service)
        {
            
        }

        public static List<File> retrieveAllFiles(DriveService service,string searchQuery)
        {
            var result = new List<File>();
            var request = service.Files.List();
            //request.Fields = "id, webContentLink";
            request.Q = searchQuery;

            do
            {
                try
                {
                    var files = request.Execute();

                    result.AddRange(files.Files);
                    request.PageToken = files.NextPageToken;
                }
                catch (Exception e)
                {
                    Console.WriteLine("An error occurred: " + e.Message);
                    request.PageToken = null;
                }
            } while (!string.IsNullOrEmpty(request.PageToken));

            return result;
        }
    }
}