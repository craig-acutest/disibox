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
// DISCLAIMED. IN NO EVENT SHALL UNIVERSITY OF GENOA BE LIABLE FOR ANY
// DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
// (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
// LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
// ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
// SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using Disibox.Data.Client;
using Disibox.Data.Entities;
using Disibox.Data.Setup;
using Disibox.Gui.Utils;
using Disibox.Processing.Tests.Properties;
using Disibox.Utils;
using NUnit.Framework;

namespace Disibox.Processing.Tests
{
    [TestFixture]
    public class ProcessingTests
    {
        private static String DefaultAdminEmail
        {
            get { return Data.Setup.Properties.Settings.Default.DefaultAdminEmail; }
        }

        private static String DefaultAdminPwd
        {
            get { return Data.Setup.Properties.Settings.Default.DefaultAdminPwd; }
        }

        private const String CommonUserEmail = "a_common@test.pino";
        private static readonly String CommonUserPwd = new string('a', CommonUserEmail.Length) + "_pwd";

        private static readonly string ServerString = Settings.Default.DefaultProcessingServer;
        private static readonly int ServerPort = Settings.Default.DefaultProcessingServerPort;

        private ClientDataSource DataSource { get; set; }

        private TcpClient _server;
        private StreamReader _reader;
        private StreamWriter _writer;

        private Stream _processedFileStream;
        private Stream _fileToUpload;

        [SetUp]
        protected void SetUp()
        {
            CloudStorageSetup.ResetStorage();
            DataSource = new ClientDataSource();
            DataSource.Login(DefaultAdminEmail, DefaultAdminPwd);

            _server = new TcpClient();

            /* estabilishing tcp connection with dispatcher */
            _server.Connect(IPAddress.Parse(ServerString), ServerPort);
            _reader = new StreamReader(_server.GetStream());
            _writer = new StreamWriter(_server.GetStream()) {AutoFlush = true};
        }

        [TearDown]
        protected void TearDown()
        {
            DataSource.Logout();
            DataSource = null;

            try
            {
                _reader.Close();
                _writer.Close();
                _server.Close();
            }
            catch
            {
            }

            _server = null;
            _reader = null;
            _writer = null;

            try
            {
                _processedFileStream.Close();
                _fileToUpload.Close();
            }
            catch
            {
            }

            CloudStorageSetup.ResetStorage();
        }

        [Test]
        public void UploadTextFileAndProcessMd5AsAdminUser()
        {
            UploadFileAndProcessMd5("textfile.txt", Shared.ObjectToStream(Resources.Textfile));
        }

        [Test]
        public void UploadJpgImageFileAndProcessMd5AsAdminUser()
        {
            UploadFileAndProcessMd5("image.jpg", Shared.ObjectToStream(Resources.JpgImage));
        }

        [Test]
        public void UploadTextFileAndProcessMd5AsCommonUser()
        {
            UploadFileAndProcessMd5("textfile.txt", Shared.ObjectToStream(Resources.Textfile), true);
        }

        [Test]
        public void UploadJpgImageFileAndProcessMd5AsCommonUser()
        {
            UploadFileAndProcessMd5("image.jpg", Shared.ObjectToStream(Resources.JpgImage), true);
        }

        [Test]
        public void UploadAndInvertBmpImageAsAdminUser()
        {
            var image = Shared.ObjectToStream(Resources.BmpImage);
            var invertedImage = Shared.ObjectToStream(Resources.InvertedBmpImage);
            UploadAndInvertImage("image.bmp", image, invertedImage, UserType.AdminUser);
        }

        [Test]
        public void UploadAndInvertJpgImageAsAdminUser()
        {
            var image = Shared.ObjectToStream(Resources.JpgImage);
            var invertedImage = Shared.ObjectToStream(Resources.InvertedJpgImage);
            UploadAndInvertImage("image.jpg", image, invertedImage, UserType.AdminUser);
        }

        [Test]
        public void UploadAndInvertPngImageAsAdminUser()
        {
            var image = Shared.ObjectToStream(Resources.PngImage);
            var invertedImage = Shared.ObjectToStream(Resources.InvertedPngImage);
            UploadAndInvertImage("image.png", image, invertedImage, UserType.AdminUser);
        }

        [Test]
        public void UploadAndInvertBmpImageAsCommonUser()
        {
            var image = Shared.ObjectToStream(Resources.BmpImage);
            var invertedImage = Shared.ObjectToStream(Resources.InvertedBmpImage);
            UploadAndInvertImage("image.bmp", image, invertedImage, UserType.CommonUser);
        }

        [Test]
        public void UploadAndInvertJpgImageAsCommonUser()
        {
            var image = Shared.ObjectToStream(Resources.JpgImage);
            var invertedImage = Shared.ObjectToStream(Resources.InvertedJpgImage);
            UploadAndInvertImage("image.jpg", image, invertedImage, UserType.CommonUser);
        }

        [Test]
        public void UploadAndInvertPngImageAsCommonUser()
        {
            var image = Shared.ObjectToStream(Resources.PngImage);
            var invertedImage = Shared.ObjectToStream(Resources.InvertedPngImage);
            UploadAndInvertImage("image.png", image, invertedImage, UserType.CommonUser);
        }

        private void UploadFileAndProcessMd5(string fileName, Stream fileContent, bool commonUser = false)
        {
            _fileToUpload = fileContent;
            IList<ProcessingToolInformation> processingToolInformations = new List<ProcessingToolInformation>();

            #region preparing_environment

            if (commonUser)
            {
                DataSource.AddUser(CommonUserEmail, CommonUserPwd, UserType.CommonUser);
                DataSource.Logout();
                DataSource.Login(CommonUserEmail, CommonUserPwd);
            }

            /* uploading the file to process */
            DataSource.AddFile(fileName, _fileToUpload, true);

            /* retriving the uri of the file just uploaded */
            var fileMetadata = DataSource.GetFileMetadata().Where(fm => fm.Name.Equals(fileName)).First();

            /* authenticating */
            var email = (commonUser) ? CommonUserEmail : DefaultAdminEmail;
            var pwd = (commonUser) ? CommonUserPwd : DefaultAdminPwd;
            _writer.WriteLine(email);
            _writer.WriteLine(pwd);

            _writer.WriteLine(fileMetadata.ContentType);
            _writer.WriteLine(fileMetadata.Uri);

            var answer = _reader.ReadLine();
            if (answer == null || answer.Equals("KO"))
                throw new Exception();

            /* useless but have to do this */
            var numberOfProcessingTools = Int32.Parse(_reader.ReadLine());

            for (var i = 0; i < numberOfProcessingTools; ++i)
            {
                var info = _reader.ReadLine();
                processingToolInformations.Add(ProcessingToolInformation.FromString(info));
            }

            #endregion

            var operationToApply = "MD5 calculator";
            var uriProcessedFile = "";

            #region getting_result

            _writer.WriteLine(operationToApply);
            uriProcessedFile = _reader.ReadLine();

            _fileToUpload.Seek(0, SeekOrigin.Begin); //fundamental!

            _processedFileStream = DataSource.GetOutput(uriProcessedFile);

            DataSource.DeleteFile(fileMetadata.Uri);

            #endregion

            var md5 = Hash.ComputeMD5(_fileToUpload);
            var actualMd5Stream = new MemoryStream(Shared.StringToByteArray(md5));

            Assert.IsTrue(Shared.StreamsAreEqual(_processedFileStream, actualMd5Stream),
                          "Md5 computed for the file locally is different from that computed on the cloud");

            DataSource.DeleteOutput(uriProcessedFile);

            if (!commonUser) return;
            DataSource.Logout();
            DataSource.Login(DefaultAdminEmail, DefaultAdminPwd);
            DataSource.DeleteUser(CommonUserEmail);
        }

        private void UploadAndInvertImage(string imageName, Stream imageContent, Stream invertedImageContent,
                                          UserType userType)
        {
            var processingToolInformations = new List<ProcessingToolInformation>();

            if (userType == UserType.CommonUser)
            {
                DataSource.AddUser(CommonUserEmail, CommonUserPwd, userType);
                DataSource.Logout();
                DataSource.Login(CommonUserEmail, CommonUserPwd);
            }

            DataSource.AddFile(imageName, imageContent);
            var imageMetadata = DataSource.GetFileMetadata().First(fm => fm.Name.Equals(imageName));

            var email = (userType == UserType.CommonUser) ? CommonUserEmail : DefaultAdminEmail;
            var pwd = (userType == UserType.CommonUser) ? CommonUserPwd : DefaultAdminPwd;
            _writer.WriteLine(email);
            _writer.WriteLine(pwd);

            _writer.WriteLine(imageMetadata.ContentType);
            _writer.WriteLine(imageMetadata.Uri);

            var answer = _reader.ReadLine();
            if (answer == null || answer.Equals("KO"))
                throw new Exception();

            /* useless but have to do this */
            var numberOfProcessingTools = Int32.Parse(_reader.ReadLine());

            for (var i = 0; i < numberOfProcessingTools; ++i)
            {
                var info = _reader.ReadLine();
                processingToolInformations.Add(ProcessingToolInformation.FromString(info));
            }

            _writer.WriteLine("Color inverter");

            var uriProcessedFile = _reader.ReadLine();
            _processedFileStream = DataSource.GetOutput(uriProcessedFile);
            DataSource.Logout();

            Assert.IsTrue(Shared.StreamsAreEqual(_processedFileStream, invertedImageContent));  
        }
    }
}