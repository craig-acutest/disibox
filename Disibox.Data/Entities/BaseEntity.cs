﻿//
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
using Microsoft.WindowsAzure.StorageClient;

namespace Disibox.Data.Entities
{
    public abstract class BaseEntity : TableServiceEntity
    {
        /// <summary>
        /// Constructor whose goal is to enforce assignment to basic entity fields
        /// (<see cref="TableServiceEntity.RowKey"/> and <see cref="TableServiceEntity.PartitionKey"/>).
        /// </summary>
        /// <param name="rowKey">The row key associated with this entity.</param>
        /// <param name="partitionKey">The partition key associated with this group of entities.</param>
        /// <exception cref="ArgumentNullException">One of the arguments is null.</exception>
        protected BaseEntity(string rowKey, string partitionKey)
        {
            // Requirements
            Require.NotNull(rowKey, "rowKey");
            Require.NotNull(partitionKey, "partitionKey");

            RowKey = rowKey;
            PartitionKey = partitionKey;
        }

        /// <summary>
        /// Seems to be required for serialization sake.
        /// </summary>
        /// <param name="partitionKey">The partition key associated with these entities.</param>
        /// <exception cref="ArgumentNullException">Partition key is null.</exception>
        [Obsolete]
        protected BaseEntity(string partitionKey)
        {
            // Requirements
            Require.NotNull(partitionKey, "partitionKey");

            RowKey = partitionKey;
            PartitionKey = partitionKey;
        }
    }
}