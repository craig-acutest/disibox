﻿//
// Copyright (c) 2011, University of Genoa
// All rights reserved.
//
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are met:
//     * Redistributions of source code must retain the above copyright
//       notice, this list of conditions and the following disclaimer.
//     * Redistributions in binary form must reproduce the above copyright
//       notice, this list of conditions and the following disclaimer in the
//       documentation and/or other materials provided with the distribution.
//     * Neither the name of the University of Genoa nor the
//       names of its contributors may be used to endorse or promote products
//       derived from this software without specific prior written permission.
//
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
// ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
// WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
// DISCLAIMED. IN NO EVENT SHALL <COPYRIGHT HOLDER> BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//

using System;
using System.Collections.Generic;
using System.Data.Services.Client;
using System.Linq;
using System.IO;
using System.Threading;
using Disibox.Data.Entities;
using Disibox.Data.Exceptions;
using Disibox.Utils;
using Microsoft.WindowsAzure;
using Microsoft.WindowsAzure.StorageClient;

namespace Disibox.Data
{
    public class DataSource
    {
        private static CloudBlobClient _blobClient;
        private static CloudBlobContainer _filesContainer;
        private static CloudBlobContainer _outputsContainer;

        private static CloudQueue _processingRequests;
        private static CloudQueue _processingCompletions;

        private static CloudTableClient _tableClient;

        private string _loggedUserId;
        private bool _loggedUserIsAdmin;
        private bool _userIsLoggedIn;

        /// <summary>
        /// 
        /// </summary>
        public DataSource()
        {
            var connectionString = Properties.Settings.Default.DataConnectionString;
            var storageAccount = CloudStorageAccount.Parse(connectionString);

            // We do not have to setup everything up every time,
            // that's why we pass "false" as parameter to next three calls.
            InitBlobs(storageAccount, false);
            InitQueues(storageAccount, false);
            InitTables(storageAccount, false);
        }

        public static void Main()
        {
            Setup();
        }

        /*=============================================================================
            Processing queues methods
        =============================================================================*/

        public ProcessingMessage DequeueProcessingRequest()
        {
            // Requirements
            RequireLoggedInUser();
            RequireAdminUser();

            return DequeueProcessingMessage(_processingRequests);
        }

        public void EnqueueProcessingRequest(ProcessingMessage procReq)
        {
            // Requirements
            RequireNotNull(procReq, "procReq");
            RequireLoggedInUser();
            RequireAdminUser();

            EnqueueProcessingMessage(procReq, _processingRequests);
        }

        public ProcessingMessage DequeueProcessingCompletion()
        {
            // Requirements
            RequireLoggedInUser();
            RequireAdminUser();

            return DequeueProcessingMessage(_processingCompletions);
        }

        public void EnqueueProcessingCompletion(ProcessingMessage procCompl)
        {
            // Requirements
            RequireNotNull(procCompl, "procCompl");
            RequireLoggedInUser();
            RequireAdminUser();

            EnqueueProcessingMessage(procCompl, _processingCompletions);
        }

        private static ProcessingMessage DequeueProcessingMessage(CloudQueue procQueue)
        {
            while ((_dequeuedMsg = procQueue.GetMessage()) == null)
                Thread.Sleep(1000);

            //procQueue.BeginGetMessage(AsyncGetMessage, procQueue);
            //ProcQueueHandler.WaitOne();

            var procMsg = ProcessingMessage.FromString(_dequeuedMsg.AsString);
            procQueue.DeleteMessage(_dequeuedMsg);

            return procMsg;
        }

        private static readonly AutoResetEvent ProcQueueHandler = new AutoResetEvent(false);
        private static CloudQueueMessage _dequeuedMsg;

        private static void AsyncGetMessage(IAsyncResult result) 
        {
            var procQueue = (CloudQueue) result.AsyncState;
            _dequeuedMsg = procQueue.EndGetMessage(result);

            ProcQueueHandler.Set();
        }

        private static void EnqueueProcessingMessage(ProcessingMessage procMsg, CloudQueue procQueue)
        {
            var msg = new CloudQueueMessage(procMsg.ToString());

            lock (procQueue)
            {
                procQueue.AddMessage(msg);
                Monitor.PulseAll(procQueue);
            }
        }

        /*=============================================================================
            File and output handling methods
        =============================================================================*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="fileContent"></param>
        /// <param name="overwrite">If it is true then the file on the cloud will be overwritten with <paramref name="fileName"/></param>
        /// <returns></returns>
        /// <exception cref="FileAlreadyExistingException">If the current user already have a file with the name <paramref name="fileName"/></exception>
        public string AddFile(string fileName, Stream fileContent, bool overwrite = false)
        {
            if (overwrite)
                return AddFile(fileName, fileContent);

            var fileToAdd = _loggedUserId + "/" + fileName;
            var filesOfUser = GetFileMetadata();

            foreach (var fileAndMime in filesOfUser)
            {
                string tempFileName = fileAndMime.Filename;
                if (!_loggedUserIsAdmin)
                    tempFileName = _loggedUserId + "/" + tempFileName;

                if (tempFileName.Equals(fileToAdd))
                    throw new FileAlreadyExistingException();
            }

            return AddFile(fileName, fileContent);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileName"></param>
        /// <param name="fileContent"></param>
        /// <exception cref="ArgumentNullException">Both parameters should not be null.</exception>
        /// <exception cref="LoggedInUserRequiredException">A user must be logged in to use this method.</exception>
        public string AddFile(string fileName, Stream fileContent)
        {
            // Requirements
            RequireNotNull(fileName, "fileName");
            RequireNotNull(fileContent, "fileContent");
            RequireLoggedInUser();

            var cloudFileName = GenerateFileName(_loggedUserId, fileName);
            var fileContentType = Common.GetContentType(fileName);
            return UploadBlob(cloudFileName, fileContentType, fileContent);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileUri"></param>
        /// <returns>True if file has been really deleted, false otherwise.</returns>
        /// <exception cref="DeletingNotOwnedFileException">If a common user is trying to delete another user's file.</exception>
        public bool DeleteFile(string fileUri)
        {
            // Requirements
            RequireNotNull(fileUri, "fileUri");
            RequireLoggedInUser();
            
            // Administrators can delete every file.
            if (_loggedUserIsAdmin)
                return DeleteBlob(fileUri, _filesContainer);

            var prefix = _filesContainer.Name + "/" + _loggedUserId;

            if (fileUri.IndexOf(prefix) == -1 )
                throw new DeletingNotOwnedFileException();

            return DeleteBlob(fileUri, _filesContainer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="fileUri"></param>
        /// <returns></returns>
        public Stream GetFile(string fileUri)
        {
            // Requirements
            // TODO RequireLoggedInUser();

            return DownloadBlob(fileUri, _filesContainer);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="LoggedInUserRequiredException"></exception>
        /// <returns></returns>
        public IList<FileMetadata> GetFileMetadata()
        {
            // Requirements
            RequireLoggedInUser();

            var blobs = ListBlobs(_filesContainer);
            var prefix = _filesContainer.Name + "/";
            var prefixLength = prefix.Length;

            if (_loggedUserIsAdmin)
                prefixLength--;

            if (!_loggedUserIsAdmin)
                return (from blob in blobs
                        select (CloudBlob) blob into file
                        let uri = file.Uri.ToString()
                        let size = Common.ConvertBytesToKilobytes(file.Properties.Length)
                        let controlUserFiles = prefix + "" + _loggedUserId
                        let prefixStart = uri.IndexOf(controlUserFiles)
                        let fileName = uri.Substring(prefixStart + prefixLength + _loggedUserId.Length + 1)
                        where uri.IndexOf(controlUserFiles) != -1
                        select new FileMetadata(fileName, Common.GetContentType(fileName), uri, size)).ToList();

            return (from blob in blobs
                    select (CloudBlob) blob into file
                    let uri = file.Uri.ToString()
                    let size = Common.ConvertBytesToKilobytes(file.Properties.Length)
                    let prefixStart = uri.IndexOf(prefix)
                    let fileName = uri.Substring(prefixStart + prefixLength + 1)
                    select new FileMetadata(fileName, Common.GetContentType(fileName), uri, size)).ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="LoggedInUserRequiredException"></exception>
        /// <returns></returns>
        public IList<string> GetFileNames()
        {
            // Requirements
            RequireLoggedInUser();

            var blobs = ListBlobs(_filesContainer);
            var names = new List<string>();

            var prefix = _filesContainer.Name + "/" + _loggedUserId;
            var prefixLength = prefix.Length;

            foreach (var blob in blobs)
            {
                var uri = blob.Uri.ToString();
                var prefixStart = uri.IndexOf(prefix);
                var fileName = uri.Substring(prefixStart + prefixLength + 1);
                names.Add(fileName);
            }

            return names;
        }

        private static string GenerateFileName(string userId, string fileName)
        {
            return _filesContainer.Name + "/" + userId + "/" + fileName;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="ArgumentNullException"></exception>
        /// <param name="toolName"></param>
        /// <param name="outputContentType"></param>
        /// <param name="outputContent"></param>
        /// <returns></returns>
        public string AddOutput(string toolName, string outputContentType, Stream outputContent)
        {
            // Requirements
            RequireNotNull(toolName, "toolName");
            RequireNotNull(outputContentType, "outputContentType");
            RequireNotNull(outputContent, "outputContent");

            var outputName = GenerateOutputName(toolName);
            return UploadBlob(outputName, outputContentType, outputContent);
        }

        public bool DeleteOutput(string outputUri)
        {
            return DeleteBlob(outputUri, _outputsContainer);
        }

        public Stream GetOutput(string outputUri)
        {
            // Requirements
            // TODO RequireLoggedInUser();

            return DownloadBlob(outputUri, _outputsContainer);
        }

        private static string GenerateOutputName(string toolName)
        {
            return _outputsContainer.Name + "/" + toolName + Guid.NewGuid();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="blobUri"></param>
        /// <param name="blobContainer"></param>
        /// <returns></returns>
        private static Stream DownloadBlob(string blobUri, CloudBlobContainer blobContainer)
        {
            var blob = blobContainer.GetBlobReference(blobUri);
            return blob.OpenRead();
        }

        /// <summary>
        /// Uploads given stream to blob storage.
        /// </summary>
        /// <param name="blobName"></param>
        /// <param name="blobContentType"></param>
        /// <param name="blobContent"></param>
        /// <returns></returns>
        private static string UploadBlob(string blobName, string blobContentType, Stream blobContent)
        {
            blobContent.Seek(0, SeekOrigin.Begin);
            var blob = _blobClient.GetBlockBlobReference(blobName);
            blob.Properties.ContentType = blobContentType;
            blob.UploadFromStream(blobContent);
            return blob.Uri.ToString();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="blobUri"></param>
        /// <param name="blobContaner"></param>
        /// <returns></returns>
        private static bool DeleteBlob(string blobUri, CloudBlobContainer blobContaner)
        {
            var blob = blobContaner.GetBlobReference(blobUri);
            return blob.DeleteIfExists();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="blobContainer"></param>
        /// <returns></returns>
        private static IEnumerable<IListBlobItem> ListBlobs(CloudBlobContainer blobContainer)
        {
            var options = new BlobRequestOptions {UseFlatBlobListing = true};
            return  blobContainer.ListBlobs(options);
        }

        /*=============================================================================
            User handling methods
        =============================================================================*/

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userEmail"></param>
        /// <param name="userPwd"></param>
        /// <param name="userIsAdmin"></param>
        /// <exception cref="AdminUserRequiredException">Only administrators can use this method.</exception>
        /// <exception cref="ArgumentNullException"></exception>
        /// <exception cref="LoggedInUserRequiredException">A user must be logged in to use this method.</exception>
        public void AddUser(string userEmail, string userPwd, bool userIsAdmin)
        {
            // Requirements
            RequireNotNull(userEmail, "userEmail");
            RequireNotNull(userPwd, "userPwd");
            RequireLoggedInUser();
            RequireAdminUser();

            var userId = GenerateUserId(userIsAdmin);
            var user = new User(userId, userEmail, userPwd, userIsAdmin);
            
            var ctx = _tableClient.GetDataServiceContext();
            ctx.AddObject(user.PartitionKey, user);
            ctx.SaveChanges();
        }

        /// <summary>
        /// Deletes user corresponding to given email address.
        /// </summary>
        /// <param name="userEmail">The email address of the user that should be deleted.</param>
        /// <exception cref="CannotDeleteUserException"></exception>
        /// <exception cref="UserNotExistingException"></exception>
        /// <exception cref="AdminUserRequiredException"></exception>
        /// <exception cref="LoggedInUserRequiredException"></exception>
        public void DeleteUser(string userEmail)
        {
            // Requirements
            RequireNotNull(userEmail, "userEmail");
            RequireLoggedInUser();
            RequireAdminUser();

            if (userEmail == Properties.Settings.Default.DefaultAdminEmail)
                throw new CannotDeleteUserException();

            var ctx = _tableClient.GetDataServiceContext();
            
            // Added a call to ToList() to avoid an error on Count() call.
            var q = GetTable<User>(ctx, User.UserPartitionKey).Where(u => u.Email == userEmail).ToList();
            if (q.Count() == 0)
                throw new UserNotExistingException();
            var user = q.First();
            
            ctx.DeleteObject(user);
            ctx.SaveChanges();
        }

        /// <summary>
        /// Completely clears the storage and sets it up to the initial state.
        /// </summary>
        public static void Clear()
        {
            _filesContainer.Delete();
            _processingRequests.Delete();
            _tableClient.DeleteTableIfExist(Entry.EntryPartitionKey);
            _tableClient.DeleteTableIfExist(User.UserPartitionKey);

            Setup();
        }

        /// <summary>
        /// Fetches and returns all administrators emails.
        /// </summary>
        /// <returns>All administrators emails.</returns>
        /// <exception cref="LoggedInUserRequiredException">A user must be logged in to use this method.</exception>
        /// <exception cref="AdminUserRequiredException">Only administrators can use this method.</exception>
        public IList<string> GetAdminUsersEmails()
        {
            // Requirements
            RequireLoggedInUser();
            RequireAdminUser();

            var ctx = _tableClient.GetDataServiceContext();
            var adminUsers = GetTable<User>(ctx, User.UserPartitionKey).Where(u => u.IsAdmin).ToList();
            return adminUsers.Select(u => u.Email).ToList();
        }

        /// <summary>
        /// Fetches and returns all common users emails.
        /// </summary>
        /// <returns>All common users emails.</returns>
        /// <exception cref="LoggedInUserRequiredException">A user must be logged in to use this method.</exception>
        /// <exception cref="AdminUserRequiredException">Only administrators can use this method.</exception>
        public IList<string> GetCommonUsersEmails()
        {
            // Requirements
            RequireLoggedInUser();
            RequireAdminUser();

            var ctx = _tableClient.GetDataServiceContext();
            var commonUsers = GetTable<User>(ctx, User.UserPartitionKey).Where(u => !u.IsAdmin).ToList();
            return commonUsers.Select(u => u.Email).ToList();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userEmail"></param>
        /// <param name="userPwd"></param>
        /// <exception cref="UserNotExistingException"></exception>
        public void Login(string userEmail, string userPwd)
        {
            var ctx = _tableClient.GetDataServiceContext();

            var hashedPwd = Hash.ComputeMD5(userPwd);
            var predicate = new Func<User, bool>(u => u.Email == userEmail && u.HashedPassword == hashedPwd);
            var q = GetTable<User>(ctx, User.UserPartitionKey).Where(predicate);
            if (q.Count() != 1)
                throw new UserNotExistingException();
            var user = q.First();

            lock (this)
            {
                _userIsLoggedIn = true;
                _loggedUserId = user.RowKey;
                _loggedUserIsAdmin = user.IsAdmin;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        public void Logout()
        {
            lock (this)
            {
                _userIsLoggedIn = false;
            }
        }

        private static string GenerateUserId(bool userIsAdmin)
        {
            var ctx = _tableClient.GetDataServiceContext();

            var q = GetTable<Entry>(ctx, Entry.EntryPartitionKey).Where(e => e.RowKey == "NextUserId");
            var nextUserIdEntry = q.First();
            var nextUserId = int.Parse(nextUserIdEntry.Value);

            var firstIdChar = (userIsAdmin) ? 'a' : 'u';
            var userId = string.Format("{0}{1}", firstIdChar, nextUserId.ToString("D16"));

            nextUserId += 1;
            nextUserIdEntry.Value = nextUserId.ToString();
            
            // Next method must be called in order to save the update.
            ctx.UpdateObject(nextUserIdEntry);
            ctx.SaveChanges();

            return userId;
        }

        private static IQueryable<T> GetTable<T>(DataServiceContext ctx, string tableName) where T : TableServiceEntity
        {
            return ctx.CreateQuery<T>(tableName).Where(e => e.PartitionKey == tableName);
        }

        private static void InitBlobs(CloudStorageAccount storageAccount, bool doInitialSetup)
        {
            _blobClient = storageAccount.CreateCloudBlobClient();
            
            var filesBlobName = Properties.Settings.Default.FilesBlobName;
            _filesContainer = _blobClient.GetContainerReference(filesBlobName);

            var outputsBlobName = Properties.Settings.Default.OutputsBlobName;
            _outputsContainer = _blobClient.GetContainerReference(outputsBlobName);

            // Next instructions are dedicated to initial setup.
            if (!doInitialSetup) return;

            _filesContainer.CreateIfNotExist();
            _outputsContainer.CreateIfNotExist();

            var permissions = _filesContainer.GetPermissions();
            permissions.PublicAccess = BlobContainerPublicAccessType.Container;
            _filesContainer.SetPermissions(permissions);
            _outputsContainer.SetPermissions(permissions);
        }

        private static void InitQueues(CloudStorageAccount storageAccount, bool doInitialSetup)
        {
            var queueClient = storageAccount.CreateCloudQueueClient();
            
            var processingRequestsName = Properties.Settings.Default.ProcessingRequestsName;
            _processingRequests = queueClient.GetQueueReference(processingRequestsName);

            var processingCompletionsName = Properties.Settings.Default.ProcessingCompletionsName;
            _processingCompletions = queueClient.GetQueueReference(processingCompletionsName);

            // Next instructions are dedicated to initial setup.)
            if (!doInitialSetup) return;

            _processingRequests.CreateIfNotExist();
            _processingCompletions.CreateIfNotExist();
        }

        private static void InitTables(CloudStorageAccount storageAccount, bool doInitialSetup)
        {
            _tableClient = new CloudTableClient(storageAccount.TableEndpoint.AbsoluteUri, storageAccount.Credentials);
            _tableClient.RetryPolicy = RetryPolicies.Retry(3, TimeSpan.FromSeconds(1));

            // Next instructions are dedicated to initial setup.
            if (!doInitialSetup) return;

            InitEntriesTable();
            InitUsersTable();
        }

        private static void InitEntriesTable()
        {
            _tableClient.CreateTableIfNotExist(Entry.EntryPartitionKey);

            var ctx = _tableClient.GetDataServiceContext();

            var q = GetTable<Entry>(ctx, Entry.EntryPartitionKey).Where(e => e.RowKey == "NextUserId");
            if (Enumerable.Any(q)) return;

            var nextUserIdEntry = new Entry("NextUserId", 0.ToString());
            ctx.AddObject(Entry.EntryPartitionKey, nextUserIdEntry);
            ctx.SaveChanges();
        }

        private static void InitUsersTable()
        {
            _tableClient.CreateTableIfNotExist(User.UserPartitionKey);

            var ctx = _tableClient.GetDataServiceContext();

            var q = GetTable<User>(ctx, User.UserPartitionKey).Where(u => u.RowKey == "a0");
            if (Enumerable.Any(q)) return;

            var defaultAdminEmail = Properties.Settings.Default.DefaultAdminEmail;
            var defaultAdminPwd = Properties.Settings.Default.DefaultAdminPwd;
            var defaultAdminUser = new User("a0", defaultAdminEmail, defaultAdminPwd, true);

            ctx.AddObject(User.UserPartitionKey, defaultAdminUser);
            ctx.SaveChanges();
        }

        private static void Setup()
        {
            var connectionString = Properties.Settings.Default.DataConnectionString;
            var storageAccount = CloudStorageAccount.Parse(connectionString);

            InitBlobs(storageAccount, true);
            InitQueues(storageAccount, true);
            InitTables(storageAccount, true);
        }

        /*=============================================================================
            Requirement checking methods
        =============================================================================*/

        /// <summary>
        /// Checks if currently logged in user is administrator;
        /// if he's not, an appropriate exception is thrown.
        /// </summary>
        /// <exception cref="AdminUserRequiredException">If logged in user is not administrator.</exception>
        private void RequireAdminUser()
        {
            if (_loggedUserIsAdmin) return;
            throw new AdminUserRequiredException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="LoggedInUserRequiredException"></exception>
        private void RequireLoggedInUser()
        {
            if (_userIsLoggedIn) return;
            throw new LoggedInUserRequiredException();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="obj"></param>
        /// <param name="paramName"></param>
        /// <exception cref="ArgumentNullException"></exception>
        private static void RequireNotNull(object obj, string paramName)
        {
            if (obj != null) return;
            throw new ArgumentNullException(paramName);
        }
    }
}