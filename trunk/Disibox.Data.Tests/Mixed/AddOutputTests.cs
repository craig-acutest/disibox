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

using System.IO;
using Disibox.Utils;
using NUnit.Framework;

namespace Disibox.Data.Tests.Mixed
{
    public class AddOutputTests : BaseMixedTests
    {
        private const string ToolName = "Pino Tool";
        private const string ContentType = "txt/plain";

        [SetUp]
        protected override void SetUp()
        {
            base.SetUp();
        }

        [TearDown]
        protected override void TearDown()
        {
            base.TearDown();
        }

        /*=============================================================================
            Valid calls
        =============================================================================*/

        [Test]
        public void AddOneOutput()
        {
            var uri = ServerDataSource.AddOutput(ToolName, ContentType, FileStreams[0]);

            ClientDataSource.Login(DefaultAdminEmail, DefaultAdminPwd);
            var output = ClientDataSource.GetOutput(uri);
            ClientDataSource.Logout();

            Assert.True(Shared.StreamsAreEqual(output, FileStreams[0]));
        }

        [Test]
        public void AddManyOutputs()
        {
            var uris = new string[FileStreams.Count];
            for (var i = 0; i < FileStreams.Count; ++i)
                uris[i] = ServerDataSource.AddOutput(ToolName, ContentType, FileStreams[i]);

            var outputs = new Stream[FileStreams.Count];
            ClientDataSource.Login(DefaultAdminEmail, DefaultAdminPwd);
            for (var i = 0; i < FileStreams.Count; ++i)
                outputs[i] = ClientDataSource.GetOutput(uris[i]);
            ClientDataSource.Logout();

            for (var i = 0; i < FileStreams.Count; ++i)
                Assert.True(Shared.StreamsAreEqual(outputs[i], FileStreams[i]));
        }
    }
}
