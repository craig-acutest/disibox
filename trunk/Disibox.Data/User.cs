﻿using Microsoft.WindowsAzure.StorageClient;

namespace Disibox.Data
{
    internal sealed class User : TableServiceEntity
    {
        public const string UserPartitionKey = "users";

        /// <summary>
        /// In addition to the properties required by the data model, every entity in table 
        /// storage has two key properties: the PartitionKey and the RowKey. These properties 
        /// together form the table's primary key and uniquely identify each entity in the table. 
        /// </summary>
        /// <param name="id"></param>
        /// <param name="email"></param>
        /// <param name="pwd"></param>
        /// <param name="userType"></param>
        public User(string id, string email, string pwd, UserType userType)
        {
            PartitionKey = UserPartitionKey;
            RowKey = char.ToLower(userType.ToString()[0]) + id;
            Email = email;
            HashedPassword = Utils.EncryptPwd(pwd);
            Type = userType;
        }

        /// <summary>
        /// User email address.
        /// </summary>
        public string Email { get; private set; }

        /// <summary>
        /// Hashed user password.
        /// </summary>
        public string HashedPassword { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public UserType Type { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userEmail"></param>
        /// <param name="userPwd"></param>
        /// <returns></returns>
        public bool Matches(string userEmail, string userPwd)
        {
            return (Email == userEmail) && (HashedPassword == Utils.EncryptPwd(userPwd));
        }
    }
}
